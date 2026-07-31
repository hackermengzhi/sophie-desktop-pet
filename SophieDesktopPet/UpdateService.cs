using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace SophieDesktopPet;

internal enum UpdateState
{
    Checking,
    Latest,
    Downloading,
    Ready,
    Installing,
    Error
}

internal sealed record PreparedUpdate(
    Version Version,
    string Tag,
    long AssetId,
    string ZipPath);

internal sealed record UpdateStatus(
    UpdateState State,
    string Text,
    PreparedUpdate? Prepared = null);

internal sealed class UpdateService : IAsyncDisposable
{
    private const string Owner = "hackermengzhi";
    private const string Repository = "sophie-desktop-pet";
    private const string AssetName =
        "Sophie-Desktop-Pet-macOS-arm64.zip";

    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(1);

    private readonly Version _currentVersion;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly MacUpdateInstaller _installer = new();
    private readonly CancellationTokenSource _lifetime = new();

    private UpdateStatus _status =
        new(UpdateState.Checking, "更新：准备检查…");
    private int _disposeState;
    private int _monitorStarted;

    public UpdateService(Version currentVersion)
    {
        _currentVersion = currentVersion;
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            BaseAddress = new Uri("https://api.github.com/"),
            Timeout = TimeSpan.FromMinutes(15)
        };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            $"SophieDesktopPet-Updater/{currentVersion}");
        _httpClient.DefaultRequestHeaders.Add(
            "X-GitHub-Api-Version", "2022-11-28");
    }

    public UpdateStatus Status => Volatile.Read(ref _status);

    public event EventHandler<UpdateStatus>? StatusChanged;

    public async Task RunMonitorAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _monitorStarted, 1, 0) != 0)
        {
            throw new InvalidOperationException("更新监控已经启动。");
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _lifetime.Token);
        var token = linked.Token;
        await CheckAsync(waitForOperation: true, token);
        try
        {
            using var timer = new PeriodicTimer(CheckInterval);
            while (await timer.WaitForNextTickAsync(token))
            {
                await CheckAsync(waitForOperation: true, token);
            }
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
        }
    }

    public Task CheckNowAsync(CancellationToken cancellationToken) =>
        CheckAsync(waitForOperation: false, cancellationToken);

    private async Task CheckAsync(
        bool waitForOperation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposeState) != 0, this);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _lifetime.Token);
        var token = linked.Token;
        var lockTaken = waitForOperation
            ? await WaitForOperationAsync(token)
            : await _operationLock.WaitAsync(0, token);
        if (!lockTaken)
        {
            return;
        }

        try
        {
            Publish(new UpdateStatus(UpdateState.Checking, "更新：正在检查…"));
            var release = await GetLatestReleaseAsync(token);
            var availableVersion = ParseTag(release.TagName);

            if (availableVersion <= _currentVersion)
            {
                Publish(new UpdateStatus(
                    UpdateState.Latest, "更新：已是最新版本"));
                return;
            }

            var matches = release.Assets
                .Where(asset =>
                    string.Equals(
                        asset.Name,
                        AssetName,
                        StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidDataException(
                    $"更新包数量不正确：{matches.Length}。");
            }

            var asset = matches[0];
            RequireSha256Digest(asset.Digest);
            var finalPath = GetDownloadPath(asset.Id);
            if (!await ValidateCachedDownloadAsync(
                    finalPath, asset, token))
            {
                Publish(new UpdateStatus(
                    UpdateState.Downloading,
                    $"更新：正在下载 v{availableVersion}…"));
                await DownloadAssetAsync(asset, finalPath, token);
            }

            var prepared = new PreparedUpdate(
                availableVersion,
                release.TagName,
                asset.Id,
                finalPath);
            Publish(new UpdateStatus(
                UpdateState.Ready,
                $"更新：安装 v{availableVersion} 并重启",
                prepared));
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
            Publish(new UpdateStatus(
                UpdateState.Error, "更新：检查失败，点此重试"));
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<bool> WaitForOperationAsync(CancellationToken token)
    {
        await _operationLock.WaitAsync(token);
        return true;
    }

    public async Task<bool> InstallReadyUpdateAsync(
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposeState) != 0, this);

        var prepared = Status.Prepared;
        if (prepared is null)
        {
            return false;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _lifetime.Token);
        var token = linked.Token;
        if (!await _operationLock.WaitAsync(0, token))
        {
            return false;
        }

        try
        {
            Publish(new UpdateStatus(
                UpdateState.Installing, "更新：正在准备安装…", prepared));
            await _installer.PrepareAndLaunchAsync(
                prepared, token);
            return true;
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception)
        {
            Publish(new UpdateStatus(
                UpdateState.Error, "更新：安装失败，点此重试", prepared));
            return false;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private async Task<GitHubRelease> GetLatestReleaseAsync(
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(
            HttpMethod.Get,
            $"repos/{Owner}/{Repository}/releases/latest",
            "application/vnd.github+json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubRelease>(
            cancellationToken: timeout.Token) ??
            throw new InvalidDataException("GitHub 没有返回版本信息。");
    }

    private async Task DownloadAssetAsync(
        GitHubAsset asset,
        string finalPath,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(finalPath) ??
            throw new InvalidOperationException("更新缓存路径无效。");
        Directory.CreateDirectory(directory);
        var partialPath = finalPath + ".partial";
        if (File.Exists(partialPath))
        {
            File.Delete(partialPath);
        }

        try
        {
            using var request = CreateRequest(
                HttpMethod.Get,
                asset.BrowserDownloadUrl,
                "application/octet-stream");
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var source =
                await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = new FileStream(
                partialPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 128,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1024 * 128];
            long total = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break;
                }
                await destination.WriteAsync(
                    buffer.AsMemory(0, read), cancellationToken);
                hasher.AppendData(buffer, 0, read);
                total += read;
            }
            await destination.FlushAsync(cancellationToken);

            if (total != asset.Size)
            {
                throw new InvalidDataException(
                    $"更新包大小不匹配：{total}/{asset.Size}。");
            }
            ValidateDigest(hasher.GetHashAndReset(), asset.Digest);
            File.Move(partialPath, finalPath, true);
        }
        catch
        {
            if (File.Exists(partialPath))
            {
                File.Delete(partialPath);
            }
            throw;
        }
    }

    private static async Task<bool> ValidateCachedDownloadAsync(
        string path,
        GitHubAsset asset,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        var info = new FileInfo(path);
        if (info.Length != asset.Size)
        {
            File.Delete(path);
            return false;
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 128,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        try
        {
            ValidateDigest(hash, asset.Digest);
            return true;
        }
        catch (InvalidDataException)
        {
            File.Delete(path);
            return false;
        }
    }

    private static void ValidateDigest(byte[] actualHash, string? digest)
    {
        RequireSha256Digest(digest);

        const string prefix = "sha256:";
        if (!digest!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("更新包摘要算法不受支持。");
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(digest[prefix.Length..]);
        }
        catch (FormatException error)
        {
            throw new InvalidDataException("更新包摘要格式无效。", error);
        }

        if (expected.Length != 32 ||
            !CryptographicOperations.FixedTimeEquals(actualHash, expected))
        {
            throw new InvalidDataException("更新包 SHA-256 校验失败。");
        }
    }

    private static void RequireSha256Digest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest))
        {
            throw new InvalidDataException(
                "更新包缺少 SHA-256 摘要。");
        }
        const string prefix = "sha256:";
        if (!digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            digest.Length != prefix.Length + 64)
        {
            throw new InvalidDataException("更新包摘要格式无效。");
        }
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string url,
        string accept)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
        return request;
    }

    private static string GetDownloadPath(long assetId)
    {
        var root = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.UserProfile),
            "Library",
            "Caches",
            "SophieDesktopPet",
            "downloads");
        return Path.Combine(
            root,
            assetId.ToString(CultureInfo.InvariantCulture) + ".zip");
    }

    private static Version ParseTag(string tag)
    {
        var value = tag.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }
        var suffix = value.IndexOfAny(['-', '+']);
        if (suffix >= 0)
        {
            value = value[..suffix];
        }
        if (!Version.TryParse(value, out var version))
        {
            throw new InvalidDataException($"无法识别版本号：{tag}。");
        }
        return version;
    }

    private void Publish(UpdateStatus status)
    {
        Volatile.Write(ref _status, status);
        StatusChanged?.Invoke(this, status);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        _lifetime.Cancel();
        await _operationLock.WaitAsync();
        _httpClient.Dispose();
        _lifetime.Dispose();
        _operationLock.Release();
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public required string TagName { get; init; }

        [JsonPropertyName("assets")]
        public required GitHubAsset[] Assets { get; init; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("name")]
        public required string Name { get; init; }

        [JsonPropertyName("size")]
        public long Size { get; init; }

        [JsonPropertyName("browser_download_url")]
        public required string BrowserDownloadUrl { get; init; }

        [JsonPropertyName("digest")]
        public string? Digest { get; init; }
    }
}
