using System.Net.Http.Headers;
using System.Text.Json;
using CrystalBall.App;
using Godot;

namespace CrystalBall.Context;

/// <summary>
/// Только device GPS/network с accuracy ≤ порога → Nominatim, затем Photon.
/// Без IP-геолокации: нет валидных координат — в промпт ничего не уходит.
/// </summary>
public static class GeoLocationService
{
    public const double MaxSeconds = 3;
    /// <summary>Фон при старте: ждём первую точку дольше, чем на «Спросить».</summary>
    public static readonly TimeSpan StartupFixWait = TimeSpan.FromSeconds(20);
    /// <summary>Городской масштаб; сотни км (IP-like) отсекаем.</summary>
    public const double MaxAccuracyMeters = 15000;

    private const string UserAgent = "MagicalBall/1.0 (crystal-ball; reverse-geocode)";
    private const string CachePath = "user://geo_cache.json";
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(MaxSeconds);
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);
    private static readonly TimeSpan MissTtl = TimeSpan.FromSeconds(25);
    public static readonly TimeSpan AskJoinBudget = TimeSpan.Zero;

    private static readonly System.Net.Http.HttpClient Http = CreateHttp();
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static string _settlement = "";
    private static DateTime _resolvedUtc = DateTime.MinValue;
    private static DateTime _missUtc = DateTime.MinValue;
    private static Task<string>? _warmup;
    private static bool _diskLoaded;

    public static bool HasSettlement => !string.IsNullOrWhiteSpace(_settlement);

    public static string Settlement => _settlement ?? "";

    public static (double Lat, double Lon)? LastCoords { get; private set; }

    public static double? LastAccuracyMeters { get; private set; }

    public static void Warmup()
    {
        LoadDiskCache();
        if (IsFresh())
        {
            WeatherService.Warmup();
            return;
        }

        if (_warmup != null && !_warmup.IsCompleted)
            return;
        _warmup = ResolveStartupAsync();
    }

    /// <summary>«Спросить»: как домик в LBSDetector — читаем live-фикс потока, без ожидания GPS.</summary>
    public static Task<string> ResolveForAskAsync()
    {
        LoadDiskCache();
        GameRoot.Instance?.GetNodeOrNull(GameRoot.LocationHostName)?.Call("kick");
        var fix = ReadAndroidFix();
        if (fix != null)
        {
            LastCoords = (fix.Value.Lat, fix.Value.Lon);
            LastAccuracyMeters = fix.Value.AccuracyMeters;
            if (!HasSettlement)
                Store(PhraseFromCoords(fix.Value.Lat, fix.Value.Lon));
            Warmup();
            return Task.FromResult(Settlement);
        }

        Warmup();
        return Task.FromResult(HasSettlement ? Settlement : "");
    }

    private static async Task<string> ResolveStartupAsync()
    {
        using var gpsCts = new CancellationTokenSource(StartupFixWait);
        try
        {
            return await ResolveCoreAsync(gpsCts.Token, waitForFix: StartupFixWait).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return IsFresh() || HasSettlement ? Settlement : "";
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[GeoLocation] {ex.Message}");
            return IsFresh() || HasSettlement ? Settlement : "";
        }
    }

    public static async Task<string> ResolveAsync()
    {
        if (IsFresh())
            return Settlement;
        if (IsFreshMiss())
            return "";

        using var cts = new CancellationTokenSource(Budget);
        try
        {
            return await ResolveCoreAsync(cts.Token, waitForFix: Budget).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return IsFresh() ? Settlement : "";
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[GeoLocation] {ex.Message}");
            MarkMiss();
            return "";
        }
    }

    private static bool IsFresh() =>
        HasSettlement && LastCoords != null && _resolvedUtc != DateTime.MinValue
        && DateTime.UtcNow - _resolvedUtc < CacheTtl;

    private static bool IsFreshMiss() =>
        _missUtc != DateTime.MinValue && DateTime.UtcNow - _missUtc < MissTtl;

    private static void MarkMiss()
    {
        _missUtc = DateTime.UtcNow;
        ResetSettlementOnly();
    }

    private static void ResetSettlementOnly()
    {
        _settlement = "";
        _resolvedUtc = DateTime.MinValue;
        LastCoords = null;
        LastAccuracyMeters = null;
    }

    private static void Reset() => MarkMiss();

    private static async Task<string> ResolveCoreAsync(CancellationToken cancellationToken, TimeSpan waitForFix)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsFresh())
                return Settlement;

            var fix = await WaitForFixAsync(waitForFix, cancellationToken).ConfigureAwait(false);
            if (fix == null)
                return HasSettlement ? Settlement : "";

            LastCoords = (fix.Value.Lat, fix.Value.Lon);
            LastAccuracyMeters = fix.Value.AccuracyMeters;
            Store(PhraseFromCoords(fix.Value.Lat, fix.Value.Lon));
            WeatherService.Warmup();

            using var geoCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            geoCts.CancelAfter(Budget);
            var named = await ReverseGeocodeAsync(fix.Value.Lat, fix.Value.Lon, geoCts.Token)
                .ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(named))
                Store(named);

            return Settlement;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task<(double Lat, double Lon, double AccuracyMeters)?> WaitForFixAsync(
        TimeSpan wait, CancellationToken cancellationToken)
    {
        GameRoot.Instance?.GetNodeOrNull(GameRoot.LocationHostName)?.Call("kick");
        var fix = ReadAndroidFix();
        if (fix != null)
            return fix;

        var deadline = DateTime.UtcNow + wait;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(180, cancellationToken).ConfigureAwait(false);
            fix = ReadAndroidFix();
            if (fix != null)
                return fix;
            GameRoot.Instance?.GetNodeOrNull(GameRoot.LocationHostName)?.Call("kick");
        }

        return null;
    }

    private static void Store(string name)
    {
        _settlement = name.Trim();
        _resolvedUtc = HasSettlement ? DateTime.UtcNow : DateTime.MinValue;
        if (HasSettlement)
        {
            _missUtc = DateTime.MinValue;
            SaveDiskCache();
        }
        else
        {
            LastCoords = null;
            LastAccuracyMeters = null;
            _missUtc = DateTime.UtcNow;
        }
    }

    private sealed class GeoDiskCache
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
        public double Accuracy { get; set; }
        public string Settlement { get; set; } = "";
        public DateTime Utc { get; set; }
    }

    private static void LoadDiskCache()
    {
        if (_diskLoaded)
            return;
        _diskLoaded = true;
        try
        {
            if (!FileAccess.FileExists(CachePath))
                return;
            using var file = FileAccess.Open(CachePath, FileAccess.ModeFlags.Read);
            if (file == null)
                return;
            var parsed = JsonSerializer.Deserialize<GeoDiskCache>(file.GetAsText());
            if (parsed == null || string.IsNullOrWhiteSpace(parsed.Settlement))
                return;
            if (DateTime.UtcNow - parsed.Utc > CacheTtl)
                return;
            LastCoords = (parsed.Lat, parsed.Lon);
            LastAccuracyMeters = parsed.Accuracy;
            _settlement = parsed.Settlement.Trim();
            _resolvedUtc = parsed.Utc;
            _missUtc = DateTime.MinValue;
            GD.Print($"[GeoLocation] disk cache age={(DateTime.UtcNow - parsed.Utc).TotalMinutes:F0}m");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[GeoLocation] cache load: {ex.Message}");
        }
    }

    private static void SaveDiskCache()
    {
        if (!HasSettlement || LastCoords == null)
            return;
        try
        {
            var payload = JsonSerializer.Serialize(new GeoDiskCache
            {
                Lat = LastCoords.Value.Lat,
                Lon = LastCoords.Value.Lon,
                Accuracy = LastAccuracyMeters ?? 0,
                Settlement = Settlement,
                Utc = _resolvedUtc == DateTime.MinValue ? DateTime.UtcNow : _resolvedUtc,
            });
            using var file = FileAccess.Open(CachePath, FileAccess.ModeFlags.Write);
            file?.StoreString(payload);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[GeoLocation] cache save: {ex.Message}");
        }
    }

    /// <summary>Только свежий device-fix с допустимой точностью (без IP).</summary>
    public static (double Lat, double Lon)? TryReadDeviceCoords()
    {
        var fix = ReadAndroidFix();
        if (fix != null)
        {
            LastCoords = (fix.Value.Lat, fix.Value.Lon);
            LastAccuracyMeters = fix.Value.AccuracyMeters;
            return LastCoords;
        }

        // Кэш только от успешного device-фикса (не IP).
        if (LastCoords != null && LastAccuracyMeters is <= MaxAccuracyMeters)
            return LastCoords;
        return null;
    }

    private static string PhraseFromCoords(double lat, double lon)
    {
        var type = ComposeTerrain("местность", null, lat, lon);
        return string.IsNullOrWhiteSpace(type) ? "местность" : type;
    }

    private static (double Lat, double Lon, double AccuracyMeters)? ReadAndroidFix()
    {
        if (OS.GetName() != "Android")
            return null;

        Variant raw = default;
        var host = GameRoot.Instance?.GetNodeOrNull(GameRoot.LocationHostName);
        if (host != null)
            raw = host.Call("probe");
        else
        {
            var script = GD.Load<GDScript>(GameRoot.LocationScriptPath);
            if (script == null)
                return null;
            var instance = script.New().AsGodotObject();
            raw = instance.Call("probe");
        }

        if (raw.VariantType != Variant.Type.Dictionary)
            return null;

        var dict = raw.AsGodotDictionary();
        if (!dict.ContainsKey("ok") || !dict["ok"].AsBool())
            return null;

        var lat = dict["lat"].AsDouble();
        var lon = dict["lon"].AsDouble();
        if (Math.Abs(lat) < 0.0001 && Math.Abs(lon) < 0.0001)
            return null;

        var accuracy = dict.ContainsKey("accuracy") ? dict["accuracy"].AsDouble() : 50;
        if (accuracy <= 0)
            accuracy = 50;
        if (accuracy > MaxAccuracyMeters)
        {
            GD.Print($"[GeoLocation] fix rejected accuracy={accuracy:F0}m (max {MaxAccuracyMeters})");
            return null;
        }

        return (lat, lon, accuracy);
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
        var (place, kind) = ReadPlaceKind(address);
        var settlement = FormatSettlement(place, First(address, "state"));
        if (string.IsNullOrWhiteSpace(settlement))
            return null;
        return AppendTerrain(settlement, kind, First(address, "region"), lat, lon);
    }

    private static async Task<string?> FromPhotonAsync(double lat, double lon, CancellationToken cancellationToken)
    {
        var url =
            "https://photon.komoot.io/reverse" +
            $"?lat={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}" +
            $"&lon={lon.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        var json = await GetJsonAsync(url, cancellationToken).ConfigureAwait(false);
        if (json == null)
            return null;
        if (!json.Value.TryGetProperty("features", out var features) || features.ValueKind != JsonValueKind.Array
            || features.GetArrayLength() == 0)
            return null;
        var first = features[0];
        if (!first.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
            return null;
        var (place, kind) = ReadPlaceKind(props);
        if (place == null)
            place = First(props, "name");
        var settlement = FormatSettlement(place, First(props, "state", "region"));
        if (string.IsNullOrWhiteSpace(settlement))
            return null;
        return AppendTerrain(settlement, kind, First(props, "region", "state"), lat, lon);
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

    private static (string? Place, string Kind) ReadPlaceKind(JsonElement address)
    {
        if (Has(address, "city"))
            return (First(address, "city"), "город");
        if (Has(address, "town"))
            return (First(address, "town"), "небольшой город");
        if (Has(address, "village"))
            return (First(address, "village"), "село");
        if (Has(address, "hamlet"))
            return (First(address, "hamlet"), "деревня");
        if (Has(address, "municipality"))
            return (First(address, "municipality"), "посёлок");
        return (First(address, "city_district", "suburb", "district", "county"), "городская местность");
    }

    private static string AppendTerrain(string settlement, string kind, string? region, double lat, double lon)
    {
        var type = ComposeTerrain(kind, region, lat, lon);
        return string.IsNullOrWhiteSpace(type) ? settlement : $"{settlement} — {type}";
    }

    private static string ComposeTerrain(string kind, string? region, double lat, double lon)
    {
        var macro = MacroFromRegion(region) ?? MacroFromCoords(lat, lon);
        var coastal = NearCoast(lat, lon);
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(macro))
            parts.Add(macro);
        if (coastal)
            parts.Add("приморский");
        if (!string.IsNullOrWhiteSpace(kind))
            parts.Add(kind);
        return string.Join(" ", parts);
    }

    private static string? MacroFromRegion(string? region)
    {
        if (string.IsNullOrWhiteSpace(region))
            return null;
        if (region.Contains("Сибирск", StringComparison.OrdinalIgnoreCase))
            return "сибирский";
        if (region.Contains("Дальневосточ", StringComparison.OrdinalIgnoreCase))
            return "дальневосточный";
        if (region.Contains("Северо-Кавказ", StringComparison.OrdinalIgnoreCase))
            return "северокавказский";
        if (region.Contains("Северо-Запад", StringComparison.OrdinalIgnoreCase))
            return "северо-западный";
        if (region.Contains("Уральск", StringComparison.OrdinalIgnoreCase))
            return "уральский";
        if (region.Contains("Приволж", StringComparison.OrdinalIgnoreCase))
            return "поволжский";
        if (region.Contains("Южн", StringComparison.OrdinalIgnoreCase))
            return "южный";
        if (region.Contains("Центральн", StringComparison.OrdinalIgnoreCase))
            return "центральный";
        return null;
    }

    private static string? MacroFromCoords(double lat, double lon)
    {
        if (lat >= 66)
            return "заполярный";
        if (lon >= 120)
            return "дальневосточный";
        if (lon >= 65 && lon < 120 && lat is >= 48 and <= 76)
            return "сибирский";
        if (lon is >= 58 and < 65 && lat is >= 50 and <= 70)
            return "уральский";
        if (lat <= 47.5 && lon is >= 36 and <= 50)
            return "южный";
        if (lat is >= 58.8 and <= 62 && lon is >= 27 and <= 33)
            return "северо-западный";
        if (lat is >= 52 and <= 58 && lon is >= 30 and <= 42)
            return "центральный";
        return null;
    }

    /// <summary>Грубая маска российских морей: без отдельного coastline API.</summary>
    private static bool NearCoast(double lat, double lon) =>
        (lat is >= 41.0 and <= 47.4 && lon is >= 27.4 and <= 41.8) ||
        (lat is >= 36.5 and <= 47.2 && lon is >= 46.6 and <= 54.2) ||
        (lat is >= 53.9 and <= 66.2 && lon is >= 10.0 and <= 30.6) ||
        (lat is >= 63.7 and <= 67.6 && lon is >= 32.0 and <= 44.5) ||
        (lat is >= 68.0 and <= 71.2 && lon is >= 30.0 and <= 42.0) ||
        (lon >= 131 && lat is >= 42.0 and <= 71.0);

    private static bool Has(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var el) &&
        el.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(el.GetString());

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
