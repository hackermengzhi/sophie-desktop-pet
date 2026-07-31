using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;

namespace SophieDesktopPet;

internal sealed class MacUpdateInstaller
{
    private const string BundleName = "Sophie Desktop Pet.app";
    private const string ExecutableName = "SophieDesktopPet";

    private const string HelperScript = """
        #!/bin/zsh
        set -u

        pid="$1"
        current="$2"
        ready="$3"
        backup="${current}.sophie-update-backup"
        helper="$0"

        trap '/bin/rm -f -- "$helper"' EXIT

        [[ "$pid" == <-> ]] || exit 20
        [[ "$current" == /*/Sophie\ Desktop\ Pet.app ]] || exit 21
        [[ "$ready" == "$current".sophie-update-ready-* ]] || exit 22
        [[ -d "$current" && -d "$ready" ]] || exit 23
        [[ ! -e "$backup" ]] || exit 24

        while /bin/kill -0 "$pid" 2>/dev/null; do
            /bin/sleep 0.2
        done

        if ! /bin/mv -- "$current" "$backup"; then
            /usr/bin/open "$current"
            exit 30
        fi

        if ! /bin/mv -- "$ready" "$current"; then
            /bin/mv -- "$backup" "$current"
            /usr/bin/open "$current"
            exit 31
        fi

        if ! /usr/bin/open "$current"; then
            failed="${ready}.failed"
            /bin/mv -- "$current" "$failed"
            /bin/mv -- "$backup" "$current"
            /usr/bin/open "$current"
            exit 32
        fi

        /bin/rm -rf -- "$backup"
        exit 0
        """;

    public async Task PrepareAndLaunchAsync(
        PreparedUpdate update,
        CancellationToken cancellationToken)
    {
        var currentBundle = FindCurrentBundle();
        var currentParent = Directory.GetParent(currentBundle)?.FullName ??
            throw new InvalidOperationException("无法找到应用所在文件夹。");
        EnsureWritable(currentParent);

        var cacheRoot = GetCacheRoot();
        Directory.CreateDirectory(cacheRoot);
        var stagingRoot = Path.Combine(
            cacheRoot,
            "staging",
            $"{update.AssetId}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingRoot);
        string? readyBundle = null;
        var helperStarted = false;
        try
        {
            ValidateZipEntries(update.ZipPath, stagingRoot);
            await RunProcessAsync(
                "/usr/bin/ditto",
                ["-x", "-k", "--", update.ZipPath, stagingRoot],
                cancellationToken);

            var bundles = Directory.GetDirectories(
                stagingRoot,
                BundleName,
                SearchOption.TopDirectoryOnly);
            if (bundles.Length != 1)
            {
                throw new InvalidDataException(
                    $"更新包中的应用数量不正确：{bundles.Length}。");
            }
            await ValidateBundleAsync(bundles[0], cancellationToken);

            readyBundle =
                currentBundle + $".sophie-update-ready-{Guid.NewGuid():N}";
            var backupBundle = currentBundle + ".sophie-update-backup";
            if (Directory.Exists(readyBundle) ||
                File.Exists(readyBundle) ||
                Directory.Exists(backupBundle) ||
                File.Exists(backupBundle))
            {
                throw new IOException("更新临时文件已存在，无法安全安装。");
            }

            await RunProcessAsync(
                "/usr/bin/ditto",
                ["--", bundles[0], readyBundle],
                cancellationToken);
            await ValidateBundleAsync(readyBundle, cancellationToken);

            var helperPath = Path.Combine(
                cacheRoot,
                $"UpdateHelper-{Guid.NewGuid():N}.zsh");
            await File.WriteAllTextAsync(
                helperPath,
                HelperScript,
                cancellationToken);

            var startInfo = new ProcessStartInfo
            {
                FileName = "/bin/zsh",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(helperPath);
            startInfo.ArgumentList.Add(
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
            startInfo.ArgumentList.Add(currentBundle);
            startInfo.ArgumentList.Add(readyBundle);

            _ = Process.Start(startInfo) ??
                throw new InvalidOperationException("无法启动更新安装器。");
            helperStarted = true;
        }
        finally
        {
            TryDeleteDirectory(stagingRoot);
            if (!helperStarted && readyBundle is not null)
            {
                TryDeleteDirectory(readyBundle);
            }
        }
    }

    private static string FindCurrentBundle()
    {
        var processPath = Environment.ProcessPath ??
            throw new InvalidOperationException("无法确定当前程序路径。");
        var executable = new FileInfo(Path.GetFullPath(processPath));
        var directory = executable.Directory;
        while (directory is not null &&
               !directory.Name.EndsWith(
                   ".app",
                   StringComparison.Ordinal))
        {
            directory = directory.Parent;
        }

        if (directory is null ||
            !string.Equals(
                directory.Name,
                BundleName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "请从 Sophie Desktop Pet.app 启动后再安装更新。");
        }

        var expectedExecutable = Path.Combine(
            directory.FullName,
            "Contents",
            "MacOS",
            ExecutableName);
        if (!string.Equals(
                Path.GetFullPath(processPath),
                Path.GetFullPath(expectedExecutable),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("当前应用结构不符合预期。");
        }
        return directory.FullName;
    }

    private static void EnsureWritable(string parent)
    {
        var probe = Path.Combine(
            parent,
            $".sophie-update-probe-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(probe);
            Directory.Delete(probe);
        }
        catch (Exception error)
        {
            throw new IOException(
                "当前安装位置不可写，请把应用移到用户 Applications 文件夹。",
                error);
        }
    }

    private static void ValidateZipEntries(
        string zipPath,
        string stagingRoot)
    {
        var canonicalRoot =
            Path.GetFullPath(stagingRoot) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(zipPath);
        if (archive.Entries.Count == 0 || archive.Entries.Count > 10_000)
        {
            throw new InvalidDataException("更新包文件数量不正确。");
        }

        long totalLength = 0;
        foreach (var entry in archive.Entries)
        {
            totalLength = checked(totalLength + entry.Length);
            if (totalLength > 1024L * 1024 * 1024)
            {
                throw new InvalidDataException("更新包解压后过大。");
            }
            var name = entry.FullName;
            if (name.Contains('\0') || Path.IsPathRooted(name))
            {
                throw new InvalidDataException("更新包包含不安全路径。");
            }
            var parts = name.Split(
                ['/', '\\'],
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Any(part => part == ".."))
            {
                throw new InvalidDataException("更新包包含路径穿越。");
            }
            var destination = Path.GetFullPath(
                Path.Combine(stagingRoot, name));
            if (!destination.StartsWith(
                    canonicalRoot,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException("更新文件超出暂存目录。");
            }

            var unixType = (entry.ExternalAttributes >> 16) & 0xF000;
            if (unixType == 0xA000)
            {
                throw new InvalidDataException("更新包不能包含符号链接。");
            }
        }
    }

    private static async Task ValidateBundleAsync(
        string bundle,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "自动安装更新仅支持 macOS。");
        }

        var infoPlist = Path.Combine(bundle, "Contents", "Info.plist");
        var executable = Path.Combine(
            bundle, "Contents", "MacOS", ExecutableName);
        RequireRegularFile(infoPlist);
        RequireRegularFile(executable);

        var mode = File.GetUnixFileMode(executable);
        if ((mode & (
                UnixFileMode.UserExecute |
                UnixFileMode.GroupExecute |
                UnixFileMode.OtherExecute)) == 0)
        {
            throw new InvalidDataException("更新程序缺少可执行权限。");
        }

        var bundleExecutable = (
            await RunProcessAsync(
                "/usr/bin/plutil",
                [
                    "-extract",
                    "CFBundleExecutable",
                    "raw",
                    "-o",
                    "-",
                    infoPlist
                ],
                cancellationToken)).Trim();
        if (!string.Equals(
                bundleExecutable,
                ExecutableName,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("更新应用的可执行文件名不正确。");
        }

        await RunProcessAsync(
            "/usr/bin/codesign",
            ["--verify", "--deep", "--strict", bundle],
            cancellationToken);
    }

    private static void RequireRegularFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists ||
            (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException($"更新应用缺少文件：{path}。");
        }
    }

    private static async Task<string> RunProcessAsync(
        string executable,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException($"无法启动 {executable}。");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
            throw;
        }
        var standardOutput = await output;
        var standardError = await error;
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{Path.GetFileName(executable)} 执行失败：" +
                standardError.Trim());
        }
        return standardOutput;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Cache cleanup failure must not hide the installation result.
        }
    }

    private static string GetCacheRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Caches",
        "SophieDesktopPet");
}
