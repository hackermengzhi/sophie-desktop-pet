using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SophieDesktopPet;

internal sealed record LiveWeather(
    double TemperatureC,
    double FeelsLikeC,
    int WeatherCode,
    bool IsDay,
    string Location,
    WeatherMode Mode);

internal sealed class WeatherService : IDisposable
{
    private static readonly TimeSpan LocationLifetime = TimeSpan.FromHours(6);
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);

    private readonly HttpClient _httpClient = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };
    private readonly SemaphoreSlim _locationLock = new(1, 1);
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    private CachedLocation? _cachedLocation;
    private int _disposeState;

    public WeatherService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "SophieDesktopPet/1.1");
    }

    public async Task<LiveWeather> GetCurrentAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposeState) != 0, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(OperationTimeout);
        var operationToken = timeout.Token;

        var location = await GetLocationAsync(operationToken)
            .ConfigureAwait(false);
        var latitude = location.Latitude.ToString(
            "G17", CultureInfo.InvariantCulture);
        var longitude = location.Longitude.ToString(
            "G17", CultureInfo.InvariantCulture);
        var url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={Uri.EscapeDataString(latitude)}" +
            $"&longitude={Uri.EscapeDataString(longitude)}" +
            "&current=temperature_2m,apparent_temperature,weather_code," +
            "is_day&timezone=auto";

        using var response = await _httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            operationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<OpenMeteoResponse>(
                cancellationToken: operationToken)
            .ConfigureAwait(false);
        var current = payload?.Current ??
            throw new InvalidOperationException(
                "天气服务没有返回 current 数据。");

        var temperature = RequireFinite(
            current.Temperature, "temperature_2m");
        var feelsLike = RequireFinite(
            current.ApparentTemperature, "apparent_temperature");
        var code = current.WeatherCode ??
            throw new InvalidOperationException("天气服务没有返回 weather_code。");
        var isDay = current.IsDay ??
            throw new InvalidOperationException("天气服务没有返回 is_day。");

        if (isDay is not (0 or 1))
        {
            throw new InvalidOperationException($"无效昼夜值：{isDay}。");
        }

        var mode = MapWeatherCode(code);

        return new LiveWeather(
            temperature,
            feelsLike,
            code,
            isDay == 1,
            location.DisplayName,
            mode);
    }

    private async Task<CachedLocation> GetLocationAsync(
        CancellationToken cancellationToken)
    {
        var cached = _cachedLocation;
        if (IsFresh(cached))
        {
            return cached!;
        }

        await _locationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cached = _cachedLocation;
            if (IsFresh(cached))
            {
                return cached!;
            }

            using var response = await _httpClient.GetAsync(
                "https://ipwho.is/",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content
                .ReadFromJsonAsync<IpWhoResponse>(
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (payload?.Success != true)
            {
                throw new InvalidOperationException("无法自动定位当前位置。");
            }

            var latitude = RequireFinite(payload.Latitude, "latitude");
            var longitude = RequireFinite(payload.Longitude, "longitude");
            if (latitude is < -90 or > 90 ||
                longitude is < -180 or > 180)
            {
                throw new InvalidOperationException("定位坐标无效。");
            }

            cached = new CachedLocation(
                latitude,
                longitude,
                BuildLocation(payload.City, payload.Region, payload.Country),
                _timeProvider.GetTimestamp());
            _cachedLocation = cached;
            return cached;
        }
        finally
        {
            _locationLock.Release();
        }
    }

    private bool IsFresh(CachedLocation? location) =>
        location is not null &&
        _timeProvider.GetElapsedTime(location.ObtainedAt) < LocationLifetime;

    private static string BuildLocation(
        string? city,
        string? region,
        string? country)
    {
        var parts = new[] { city, region, country }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return parts.Length == 0 ? "当前位置" : string.Join(", ", parts);
    }

    private static double RequireFinite(double? value, string field)
    {
        if (value is not double result || !double.IsFinite(result))
        {
            throw new InvalidOperationException($"天气字段 {field} 缺失或无效。");
        }
        return result;
    }

    internal static WeatherMode MapWeatherCode(int code) => code switch
    {
        >= 71 and <= 77 => WeatherMode.Snow,
        >= 85 and <= 86 => WeatherMode.Snow,
        >= 51 and <= 67 => WeatherMode.Rain,
        >= 80 and <= 82 => WeatherMode.Rain,
        >= 95 and <= 99 => WeatherMode.Rain,
        0 or 1 or 2 or 3 or 45 or 48 => WeatherMode.Clear,
        _ => WeatherMode.Clear
    };

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }
        _httpClient.Dispose();
        _locationLock.Dispose();
    }

    private sealed record CachedLocation(
        double Latitude,
        double Longitude,
        string DisplayName,
        long ObtainedAt);

    private sealed class IpWhoResponse
    {
        [JsonPropertyName("success")]
        public bool? Success { get; init; }

        [JsonPropertyName("city")]
        public string? City { get; init; }

        [JsonPropertyName("region")]
        public string? Region { get; init; }

        [JsonPropertyName("country")]
        public string? Country { get; init; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; init; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; init; }
    }

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("current")]
        public OpenMeteoCurrent? Current { get; init; }
    }

    private sealed class OpenMeteoCurrent
    {
        [JsonPropertyName("temperature_2m")]
        public double? Temperature { get; init; }

        [JsonPropertyName("apparent_temperature")]
        public double? ApparentTemperature { get; init; }

        [JsonPropertyName("weather_code")]
        public int? WeatherCode { get; init; }

        [JsonPropertyName("is_day")]
        public int? IsDay { get; init; }

    }
}
