using System.Net.Http.Headers;
using System.Text.Json;
using Godot;

namespace CrystalBall.Context;

/// <summary>
/// GPS (Android) → reverse geocode как в LBSDetector: Nominatim, затем Photon.
/// </summary>
public static class GeoLocationService
{
    public const string Unavailable = "Местоположение недоступно";
    private const string FinePermission = "android.permission.ACCESS_FINE_LOCATION";
    private const string CoarsePermission = "android.permission.ACCESS_COARSE_LOCATION";
    private const string UserAgent = "MagicalBall/1.0 (crystal-ball; reverse-geocode)";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(4);

    private static readonly System.Net.Http.HttpClient Http = CreateHttp();
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static string _settlement = Unavailable;
    private static DateTime _resolvedUtc = DateTime.MinValue;
    private static Task? _warmup;

    public static string Settlement =>
        string.IsNullOrWhiteSpace(_settlement) ? Unavailable : _settlement;

    public static void Warmup()
    {
        RequestAndroidPermission();
        if (_warmup == null || (_warmup.IsCompleted && !IsFresh()))
            _warmup = ResolveAsync(TimeSpan.FromSeconds(8));
    }

    public static void RequestAndroidPermission()
    {
        if (OS.GetName() != "Android")
            return;
        OS.RequestPermission(FinePermission);
        OS.RequestPermission(CoarsePermission);
    }

    public static async Task<string> ResolveAsync(TimeSpan? timeout = null)
    {
        if (IsFresh())
            return Settlement;

        var limit = timeout ?? TimeSpan.FromSeconds(4.5);
        try
        {
            var waiter = ResolveCoreAsync();
            var done = await Task.WhenAny(waiter, Task.Delay(limit)).ConfigureAwait(false);
            if (done != waiter)
                return Settlement;
            return await waiter.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[GeoLocation] {ex.Message}");
            return Settlement;
        }
    }

    private static bool IsFresh() =>
        _resolvedUtc != DateTime.MinValue
        && DateTime.UtcNow - _resolvedUtc < CacheTtl
        && _settlement != Unavailable;

    private static async Task<string> ResolveCoreAsync()
    {
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsFresh())
                return Settlement;

            RequestAndroidPermission();
            var coords = ReadAndroidCoords();
            string? fromIpCity = null;
            if (coords == null)
            {
                var ip = await LookupIpAsync().ConfigureAwait(false);
                if (ip != null)
                {
                    coords = (ip.Value.Lat, ip.Value.Lon);
                    fromIpCity = ip.Value.City;
                }
            }

            if (coords != null)
            {
                var named = await ReverseGeocodeAsync(coords.Value.Lat, coords.Value.Lon).ConfigureAwait(false);
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

            if (_settlement == Unavailable)
                Store(Unavailable, touchCache: false);
            return Settlement;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static void Store(string name, bool touchCache = true)
    {
        _settlement = name.Trim();
        if (touchCache && _settlement != Unavailable)
            _resolvedUtc = DateTime.UtcNow;
    }

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

    private static async Task<string?> ReverseGeocodeAsync(double lat, double lon)
    {
        var named = await FromNominatimAsync(lat, lon).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(named))
            return named;
        return await FromPhotonAsync(lat, lon).ConfigureAwait(false);
    }

    private static async Task<string?> FromNominatimAsync(double lat, double lon)
    {
        var url =
            "https://nominatim.openstreetmap.org/reverse" +
            $"?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            "&format=json&addressdetails=1&accept-language=ru&zoom=14";
        var json = await GetJsonAsync(url).ConfigureAwait(false);
        if (json == null)
            return null;
        if (!json.Value.TryGetProperty("address", out var address) || address.ValueKind != JsonValueKind.Object)
            return null;
        return FormatSettlement(
            First(address, "city", "town", "village", "hamlet", "municipality", "city_district", "suburb", "county"),
            First(address, "state", "region"));
    }

    private static async Task<string?> FromPhotonAsync(double lat, double lon)
    {
        var url =
            "https://photon.komoot.io/reverse" +
            $"?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            "&lang=ru";
        var json = await GetJsonAsync(url).ConfigureAwait(false);
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

    private static async Task<(double Lat, double Lon, string City)?> LookupIpAsync()
    {
        var json = await GetJsonAsync("http://ip-api.com/json/?lang=ru&fields=status,city,regionName,lat,lon").ConfigureAwait(false);
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

    private static async Task<JsonElement?> GetJsonAsync(string url)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            req.Headers.TryAddWithoutValidation("Accept-Language", "ru");
            using var resp = await Http.SendAsync(req).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(body))
                return null;
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.Clone();
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
        var http = new System.Net.Http.HttpClient(handler) { Timeout = HttpTimeout };
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }
}
