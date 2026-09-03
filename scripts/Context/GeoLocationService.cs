using System.Net.Http.Headers;
using System.Text.Json;
using Godot;

namespace CrystalBall.Context;

/// <summary>
/// GPS (Android) → reverse geocode как в LBSDetector: Nominatim, затем Photon.
/// Весь запрос ограничен 3 секундами; при таймауте параметр сбрасывается и не идёт в промпт.
/// </summary>
public static class GeoLocationService
{
    public const double MaxSeconds = 3;

    private const string FinePermission = "android.permission.ACCESS_FINE_LOCATION";
    private const string CoarsePermission = "android.permission.ACCESS_COARSE_LOCATION";
    private const string UserAgent = "MagicalBall/1.0 (crystal-ball; reverse-geocode)";
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(MaxSeconds);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    private static readonly System.Net.Http.HttpClient Http = CreateHttp();
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static string _settlement = "";
    private static DateTime _resolvedUtc = DateTime.MinValue;
    private static Task? _warmup;

    public static bool HasSettlement => !string.IsNullOrWhiteSpace(_settlement);

    public static string Settlement => _settlement ?? "";

    public static (double Lat, double Lon)? LastCoords { get; private set; }

    public static void Warmup()
    {
        RequestAndroidPermission();
        if (_warmup == null || (_warmup.IsCompleted && !IsFresh()))
            _warmup = ResolveAsync();
    }

    public static void RequestAndroidPermission()
    {
        if (OS.GetName() != "Android")
            return;
        OS.RequestPermission(FinePermission);
        OS.RequestPermission(CoarsePermission);
    }

    public static async Task<string> ResolveAsync()
    {
        if (IsFresh())
            return Settlement;

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
            GD.PushWarning($"[GeoLocation] {ex.Message}");
            Reset();
            return "";
        }
    }

    private static bool IsFresh() =>
        HasSettlement && _resolvedUtc != DateTime.MinValue && DateTime.UtcNow - _resolvedUtc < CacheTtl;

    private static void Reset()
    {
        _settlement = "";
        _resolvedUtc = DateTime.MinValue;
    }

    private static async Task<string> ResolveCoreAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsFresh())
                return Settlement;

            RequestAndroidPermission();
            var coords = ReadAndroidCoords();
            string? fromIpCity = null;
            if (coords == null)
            {
                var ip = await LookupIpAsync(cancellationToken).ConfigureAwait(false);
                if (ip != null)
                {
                    coords = (ip.Value.Lat, ip.Value.Lon);
                    fromIpCity = ip.Value.City;
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (coords != null)
            {
                LastCoords = coords;
                var named = await ReverseGeocodeAsync(coords.Value.Lat, coords.Value.Lon, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(named))
                {
                    Store(named);
                    return Settlement;
                }
            }

            if (!string.IsNullOrWhiteSpace(fromIpCity))
            {
                Store(fromIpCity);
                return Settlement;
            }

            Reset();
            return "";
        }
        finally
        {
            Gate.Release();
        }
    }

    private static void Store(string name)
    {
        _settlement = name.Trim();
        _resolvedUtc = HasSettlement ? DateTime.UtcNow : DateTime.MinValue;
    }

    public static (double Lat, double Lon)? TryReadDeviceCoords() =>
        ReadAndroidCoords() ?? LastCoords;

    private static (double Lat, double Lon)? ReadAndroidCoords()
    {
        if (OS.GetName() != "Android")
            return null;

        var script = GD.Load<GDScript>("res://scripts/Context/android_location.gd");
        if (script == null)
            return null;

        var instance = script.New().AsGodotObject();
        var raw = instance.Call("probe");
        if (raw.VariantType != Variant.Type.Dictionary)
            return null;

        var dict = raw.AsGodotDictionary();
        if (!dict.ContainsKey("ok") || !dict["ok"].AsBool())
            return null;

        var lat = dict["lat"].AsDouble();
        var lon = dict["lon"].AsDouble();
        if (Math.Abs(lat) < 0.0001 && Math.Abs(lon) < 0.0001)
            return null;
        return (lat, lon);
    }

    private static async Task<string?> ReverseGeocodeAsync(double lat, double lon, CancellationToken cancellationToken)
    {
        var named = await FromNominatimAsync(lat, lon, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(named))
            return named;
        cancellationToken.ThrowIfCancellationRequested();
        return await FromPhotonAsync(lat, lon, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string?> FromNominatimAsync(double lat, double lon, CancellationToken cancellationToken)
    {
        var url =
            "https://nominatim.openstreetmap.org/reverse" +
            $"?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            "&format=json&addressdetails=1&accept-language=ru&zoom=14";
        var json = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
        if (json == null)
            return null;
        if (!json.Value.TryGetProperty("address", out var address) || address.ValueKind != JsonValueKind.Object)
            return null;
        return FormatSettlement(
            First(address, "city", "town", "village", "hamlet", "municipality", "city_district", "suburb", "county"),
            First(address, "state", "region"));
    }

    private static async Task<string?> FromPhotonAsync(double lat, double lon, CancellationToken cancellationToken)
    {
        var url =
            "https://photon.komoot.io/reverse" +
            $"?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            "&lang=ru";
        var json = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
        if (json == null)
            return null;
        if (!json.Value.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array
            || features.GetArrayLength() == 0)
            return null;
        var first = features[0];
        if (!first.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
            return null;
        return FormatSettlement(
            First(props, "city", "town", "village", "municipality", "district", "county", "name"),
            First(props, "state", "region"));
    }

    private static async Task<(double Lat, double Lon, string City)?> LookupIpAsync(CancellationToken cancellationToken)
    {
        var json = await GetJsonAsync(
            "http://ip-api.com/json/?lang=ru&fields=status,city,regionName,lat,lon",
            cancellationToken).ConfigureAwait(false);
        if (json == null)
            return null;
        if (!json.Value.TryGetProperty("status", out var status) || status.GetString() != "success")
            return null;
        var lat = json.Value.TryGetProperty("lat", out var latEl) ? latEl.GetDouble() : 0;
        var lon = json.Value.TryGetProperty("lon", out var lonEl) ? lonEl.GetDouble() : 0;
        var city = First(json.Value, "city");
        var region = First(json.Value, "regionName");
        var label = FormatSettlement(city, region);
        if (string.IsNullOrWhiteSpace(label) && Math.Abs(lat) < 0.0001 && Math.Abs(lon) < 0.0001)
            return null;
        return (lat, lon, label ?? "");
    }

    private static string? FormatSettlement(string? place, string? region)
    {
        place = place?.Trim();
        region = region?.Trim();
        if (string.IsNullOrEmpty(place) && string.IsNullOrEmpty(region))
            return null;
        if (string.IsNullOrEmpty(place))
            return region;
        if (string.IsNullOrEmpty(region) || region == place)
            return place;
        return $"{place}, {region}";
    }

    private static string? First(JsonElement obj, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!obj.TryGetProperty(key, out var el))
                continue;
            var text = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                return text.Trim();
        }

        return null;
    }

    private static async Task<JsonElement?> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            req.Headers.TryAddWithoutValidation("Accept-Language", "ru");
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
            GD.PushWarning($"[GeoLocation] HTTP {ex.Message}");
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
