using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Godot;

namespace CrystalBall.Context;

/// <summary>
/// Погода по координатам без ключа: Open-Meteo и met.no параллельно, первый успешный ответ.
/// Таймаут 3 секунды; кэш 30 минут. При сбое поле сбрасывается и не идёт в промпт.
/// </summary>
public static class WeatherService
{
    public const double MaxSeconds = 3;

    private const string UserAgent = "MagicalBall/1.0 (crystal-ball; weather)";
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(MaxSeconds);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);
    private static readonly System.Net.Http.HttpClient Http = CreateHttp();
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static string _phrase = "";
    private static DateTime _resolvedUtc = DateTime.MinValue;
    private static (double Lat, double Lon)? _cachedCoords;
    private static Task? _warmup;

    public static bool HasPhrase => !string.IsNullOrWhiteSpace(_phrase);

    public static string Phrase => _phrase ?? "";

    public static void Warmup()
    {
        if (_warmup == null || (_warmup.IsCompleted && !IsFresh()))
            _warmup = ResolveAsync();
    }

    public static async Task<string> ResolveAsync()
    {
        if (IsFresh())
            return Phrase;

        using var cts = new CancellationTokenSource(Budget);
        try
        {
            return await ResolveCoreAsync(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Reset();
            return "";
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[Weather] {ex.Message}");
            Reset();
            return "";
        }
    }

    private static bool IsFresh() =>
        HasPhrase && _resolvedUtc != DateTime.MinValue && DateTime.UtcNow - _resolvedUtc < CacheTtl;

    private static bool IsFreshFor((double Lat, double Lon) coords) =>
        IsFresh() && _cachedCoords != null && Nearby(_cachedCoords.Value, coords);

    private static bool Nearby((double Lat, double Lon) a, (double Lat, double Lon) b) =>
        Math.Abs(a.Lat - b.Lat) < 0.25 && Math.Abs(a.Lon - b.Lon) < 0.25;

    private static void Reset()
    {
        _phrase = "";
        _resolvedUtc = DateTime.MinValue;
        _cachedCoords = null;
    }

    private static async Task<string> ResolveCoreAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var coords = GeoLocationService.TryReadDeviceCoords();
            if (coords == null)
                coords = await LookupIpAsync(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            if (coords == null)
            {
                Reset();
                return "";
            }

            if (IsFreshFor(coords.Value))
                return Phrase;

            var phrase = await FetchFirstAsync(coords.Value.Lat, coords.Value.Lon, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(phrase))
            {
                Reset();
                return "";
            }

            _phrase = phrase.Trim();
            _cachedCoords = coords;
            _resolvedUtc = DateTime.UtcNow;
            return Phrase;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<string?> FetchFirstAsync(double lat, double lon, CancellationToken cancellationToken)
    {
        var pending = new List<Task<string?>>
        {
            FromOpenMeteoAsync(lat, lon, cancellationToken),
            FromMetNoAsync(lat, lon, cancellationToken),
        };

        while (pending.Count > 0)
        {
            var done = await Task.WhenAny(pending).ConfigureAwait(false);
            pending.Remove(done);
            try
            {
                var phrase = await done.ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(phrase))
                    return phrase;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[Weather] {ex.Message}");
            }
        }

        return null;
    }

    private static async Task<string?> FromOpenMeteoAsync(double lat, double lon, CancellationToken cancellationToken)
    {
        var inv = CultureInfo.InvariantCulture;
        var url =
            "https://api.open-meteo.com/v1/forecast" +
            $"?latitude={lat.ToString(inv)}&longitude={lon.ToString(inv)}" +
            "&current=weather_code,temperature_2m,precipitation,cloud_cover" +
            "&forecast_days=1&timezone=auto";
        var json = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
        if (json == null || !json.Value.TryGetProperty("current", out var current)
            || current.ValueKind != JsonValueKind.Object)
            return null;

        var code = ReadInt(current, "weather_code");
        var temp = ReadDouble(current, "temperature_2m");
        if (code == null && temp == null)
            return null;
        return FormatWmo(code ?? 0, temp);
    }

    private static async Task<string?> FromMetNoAsync(double lat, double lon, CancellationToken cancellationToken)
    {
        var inv = CultureInfo.InvariantCulture;
        var url =
            "https://api.met.no/weatherapi/locationforecast/2.0/compact" +
            $"?lat={lat.ToString(inv)}&lon={lon.ToString(inv)}";
        var json = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
        if (json == null)
            return null;
        if (!json.Value.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
            return null;
        if (!props.TryGetProperty("timeseries", out var series) || series.ValueKind != JsonValueKind.Array
            || series.GetArrayLength() == 0)
            return null;
        var first = series[0];
        if (!first.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            return null;

        double? temp = null;
        if (data.TryGetProperty("instant", out var instant) && instant.ValueKind == JsonValueKind.Object
            && instant.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Object)
            temp = ReadDouble(details, "air_temperature");

        string? symbol = null;
        if (data.TryGetProperty("next_1_hours", out var next) && next.ValueKind == JsonValueKind.Object
            && next.TryGetProperty("summary", out var summary) && summary.ValueKind == JsonValueKind.Object
            && summary.TryGetProperty("symbol_code", out var codeEl))
            symbol = codeEl.GetString();

        var code = SymbolToWmo(symbol);
        if (code == null && temp == null)
            return null;
        return FormatWmo(code ?? 0, temp);
    }

    private static async Task<(double Lat, double Lon)?> LookupIpAsync(CancellationToken cancellationToken)
    {
        var json = await GetJsonAsync(
            "http://ip-api.com/json/?fields=status,lat,lon",
            cancellationToken).ConfigureAwait(false);
        if (json == null)
            return null;
        if (!json.Value.TryGetProperty("status", out var status) || status.GetString() != "success")
            return null;
        var lat = json.Value.TryGetProperty("lat", out var latEl) ? latEl.GetDouble() : 0;
        var lon = json.Value.TryGetProperty("lon", out var lonEl) ? lonEl.GetDouble() : 0;
        if (Math.Abs(lat) < 0.0001 && Math.Abs(lon) < 0.0001)
            return null;
        return (lat, lon);
    }

    private static string FormatWmo(int code, double? temp)
    {
        var sky = code switch
        {
            0 when temp >= 28 => "Ясное знойное небо",
            0 when temp <= -12 => "Ясное морозное небо",
            0 => "Ясное чистое небо",
            1 => "Почти ясное небо, лёгкая дымка",
            2 => "Переменная облачность",
            3 => "Плотные тучи, серое небо",
            45 or 48 => "Густой туман",
            51 or 53 => "Морось, мокрый воздух",
            55 => "Густая морось",
            56 or 57 => "Ледяная морось",
            61 => "Мелкий дождь",
            63 => "Плотные тучи, затяжной дождь",
            65 => "Проливной дождь",
            66 or 67 => "Ледяной дождь",
            71 => "Лёгкий снег",
            73 => "Снегопад",
            75 or 77 => "Метель, плотный снег",
            80 => "Краткий ливень",
            81 or 82 => "Сильные ливни",
            85 => "Снежные заряды",
            86 => "Сильный снег с зарядами",
            95 => "Гроза",
            96 or 99 => "Гроза с градом",
            _ => "Небо закрыто, погода неясна",
        };
        if (temp == null)
            return sky;
        var rounded = (int)Math.Round(temp.Value, MidpointRounding.AwayFromZero);
        var sign = rounded > 0 ? "+" : "";
        return $"{sky} ({sign}{rounded} °C)";
    }

    private static int? SymbolToWmo(string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return null;
        var key = symbol.Split('_')[0];
        return key switch
        {
            "clearsky" => 0,
            "fair" => 1,
            "partlycloudy" => 2,
            "cloudy" => 3,
            "fog" => 45,
            "lightrain" or "lightrainshowers" => 61,
            "rain" or "rainshowers" => 63,
            "heavyrain" or "heavyrainshowers" => 65,
            "lightsnow" or "lightsnowshowers" => 71,
            "snow" or "snowshowers" => 73,
            "heavysnow" or "heavysnowshowers" => 75,
            "sleet" or "lightsleet" or "heavysleet" => 66,
            "thunderstorm" => 95,
            _ when key.Contains("thunder", StringComparison.Ordinal) => 95,
            _ when key.Contains("snow", StringComparison.Ordinal) => 73,
            _ when key.Contains("rain", StringComparison.Ordinal) => 63,
            _ when key.Contains("fog", StringComparison.Ordinal) => 45,
            _ => 2,
        };
    }

    private static int? ReadInt(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el))
            return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
            return n;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out var d))
            return (int)Math.Round(d);
        return null;
    }

    private static double? ReadDouble(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Number)
            return null;
        return el.GetDouble();
    }

    private static async Task<JsonElement?> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            using var resp = await Http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
                return null;
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.Clone();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[Weather] HTTP {ex.Message}");
            return null;
        }
    }

    private static System.Net.Http.HttpClient CreateHttp()
    {
        var handler = new HttpClientHandler();
        if (OperatingSystem.IsAndroid())
            handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;
        var http = new System.Net.Http.HttpClient(handler) { Timeout = Budget };
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }
}
