using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace CrystalBall.Context;

/// <summary>
/// Локальная статистика тапов по календарным суткам. Сырые метки времени не уходят на сервер —
/// в промпт попадает только поэтический <c>inquiry_pulse_aura</c>.
/// </summary>
public static class InquiryPulseStore
{
    public const string Path = "user://oracle_pulse.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public static string RecordAndDescribe(DateTime nowLocal, int? windowDays = null)
    {
        var file = Load();
        var days = InquiryPulseMeter.ClampWindowDays(windowDays ?? file.WindowDays);
        file.WindowDays = days;
        Rotate(file, nowLocal, days);

        var stamps = Collect(file);
        stamps.Add(nowLocal);
        var reading = InquiryPulseMeter.Evaluate(stamps, nowLocal, days);

        Append(file, nowLocal);
        Save(file);
        return reading.Aura;
    }

    public static InquiryPulseFile Load()
    {
        if (!FileAccess.FileExists(Path))
            return new InquiryPulseFile();

        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Read);
        if (file == null)
            return new InquiryPulseFile();

        try
        {
            var loaded = JsonSerializer.Deserialize<InquiryPulseFile>(file.GetAsText(), JsonOptions)
                         ?? new InquiryPulseFile();
            loaded.Days ??= new Dictionary<string, List<string>>();
            loaded.WindowDays = InquiryPulseMeter.ClampWindowDays(loaded.WindowDays);
            return loaded;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[InquiryPulseStore] {ex.Message}");
            return new InquiryPulseFile();
        }
    }

    public static void Save(InquiryPulseFile data)
    {
        using var file = FileAccess.Open(Path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PushError("[InquiryPulseStore] Не удалось записать oracle_pulse.json");
            return;
        }

        file.StoreString(JsonSerializer.Serialize(data, JsonOptions));
    }

    internal static void Rotate(InquiryPulseFile file, DateTime nowLocal, int windowDays)
    {
        var start = InquiryPulseMeter.WindowStart(nowLocal, windowDays);
        var keep = new Dictionary<string, List<string>>();
        foreach (var pair in file.Days)
        {
            if (!DateTime.TryParseExact(pair.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var day))
                continue;
            if (day.Date < start)
                continue;
            var stamps = pair.Value?
                .Where(value => DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out _))
                .ToList() ?? [];
            if (stamps.Count == 0)
                continue;
            keep[pair.Key] = stamps;
        }

        file.Days = keep;
    }

    private static List<DateTime> Collect(InquiryPulseFile file)
    {
        var stamps = new List<DateTime>();
        foreach (var pair in file.Days)
        {
            if (pair.Value == null)
                continue;
            foreach (var raw in pair.Value)
            {
                if (DateTime.TryParse(raw, null, DateTimeStyles.RoundtripKind, out var stamp))
                    stamps.Add(stamp.Kind == DateTimeKind.Utc ? stamp.ToLocalTime() : stamp);
            }
        }

        return stamps;
    }

    private static void Append(InquiryPulseFile file, DateTime nowLocal)
    {
        var key = nowLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (!file.Days.TryGetValue(key, out var list) || list == null)
        {
            list = [];
            file.Days[key] = list;
        }

        list.Add(nowLocal.ToString("o"));
    }
}

public sealed class InquiryPulseFile
{
    [JsonPropertyName("window_days")]
    public int WindowDays { get; set; } = InquiryPulseMeter.DefaultWindowDays;

    [JsonPropertyName("days")]
    public Dictionary<string, List<string>> Days { get; set; } = new();
}
