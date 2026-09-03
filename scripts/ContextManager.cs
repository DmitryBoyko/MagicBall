using System.Globalization;
using CrystalBall.Context;
using CrystalBall.Profile;
using Godot;
using Godot.Collections;

namespace CrystalBall.AI;

/// <summary>
/// Собирает детерминированный профиль, динамический слепок смартфона и модификаторы хаоса
/// строго в момент тапа «Спросить Оракула».
/// </summary>
public partial class ContextManager : Node
{
    public const string AnchorsPath = "res://data/semantic_anchors.json";

    private readonly System.Collections.Generic.List<(string Name, string Desc)> _anchors = [];
    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        LoadAnchors();
    }

    public async Task<OracleContext> AssembleAsync(UserProfile profile, PhotoAnalysis? photo = null)
    {
        var geoTask = GeoLocationService.ResolveAsync();
        var weatherTask = WeatherService.ResolveAsync();
        var context = Assemble(profile, photo);
        var geo = await geoTask;
        var weather = await weatherTask;
        context.DynamicSnapshot.GeoLocationType = string.IsNullOrWhiteSpace(geo) ? null : geo;
        context.DynamicSnapshot.WeatherState = string.IsNullOrWhiteSpace(weather) ? null : weather;
        return context;
    }

    public OracleContext Assemble(UserProfile profile, PhotoAnalysis? photo = null)
    {
        var now = DateTime.Now;
        var snapshot = GenerateDynamicSnapshot(now);
        if (photo != null)
        {
            snapshot.PhotoMysticTag = photo.MysticTag;
            snapshot.ImageNetRawTag = photo.RawTag;
            snapshot.PhotoColorPalette = photo.ColorPalette;
            snapshot.PhotoLuminanceVibe = photo.LuminanceVibe;
        }

        return new OracleContext
        {
            DeterministicProfile = new DeterministicProfile
            {
                UserName = profile.UserName,
                ZodiacSign = profile.ZodiacSign,
                AstrologicalElement = profile.AstrologicalElement,
                RulingPlanet = profile.RulingPlanet,
                DestinyNumber = profile.DestinyNumber,
                ChineseTotem = profile.ChineseTotem,
                AgeGroup = profile.AgeGroup,
            },
            DynamicSnapshot = snapshot,
        };
    }

    public Dictionary GenerateDynamicSnapshot()
    {
        var snap = GenerateDynamicSnapshot(DateTime.Now);
        return new Dictionary
        {
            { "exact_time_context", snap.ExactTimeContext },
            { "time_of_day", snap.TimeOfDay },
            { "current_season", snap.CurrentSeason },
            { "device_battery_aura", snap.DeviceBatteryAura },
            { "device_power_state", snap.DevicePowerState },
            { "inquiry_pulse_aura", snap.InquiryPulseAura },
            { "ball_mood_modifier", snap.BallMoodModifier },
            { "ball_tint_name", snap.BallTintName },
            { "ball_tint_meaning", snap.BallTintMeaning },
            { "ball_tint_modifier", snap.BallTintModifier },
            { "world_pressure_modifier", snap.WorldPressureModifier },
            { "entropy_word_anchor", snap.EntropyWordAnchor },
        };
    }

    public DynamicSnapshot GenerateDynamicSnapshot(DateTime now)
    {
        var culture = CultureInfo.GetCultureInfo("ru-RU");
        var weekday = culture.TextInfo.ToTitleCase(now.ToString("dddd", culture));
        var part = TimeOfDayCatalog.FromHour(now.Hour);
        var power = DevicePowerProbe.Read();
        var mood = (int)_rng.RandiRange(1, 3);
        var pressure = (int)_rng.RandiRange(1, 3);

        return new DynamicSnapshot
        {
            ExactTimeContext = $"{weekday}, {now:HH:mm}",
            TimeOfDay = TimeOfDayCatalog.Atmosphere(part),
            CurrentSeason = TimeOfDayCatalog.Season(now),
            GeoLocationType = GeoLocationService.HasSettlement ? GeoLocationService.Settlement : null,
            WeatherState = WeatherService.HasPhrase ? WeatherService.Phrase : null,
            DeviceBatteryAura = MapBatteryAura(power.Known ? power.Percent : -1),
            DevicePowerState = MapPowerState(power),
            InquiryPulseAura = InquiryPulseStore.RecordAndDescribe(now),
            BallMoodCode = mood,
            WorldPressureCode = pressure,
            BallMoodModifier = MapBallMood(mood),
            BallTintName = SessionBallTint.Name,
            BallTintMeaning = SessionBallTint.Meaning,
            BallTintModifier = SessionBallTint.Modifier,
            WorldPressureModifier = MapWorldPressure(pressure),
            EntropyWordAnchor = PickAnchor(),
        };
    }

    private void LoadAnchors()
    {
        _anchors.Clear();
        if (!FileAccess.FileExists(AnchorsPath))
        {
            GD.PrintErr($"[ContextManager] Файл {AnchorsPath} не найден.");
            return;
        }

        using var file = FileAccess.Open(AnchorsPath, FileAccess.ModeFlags.Read);
        if (file == null)
            return;

        var json = new Json();
        if (json.Parse(file.GetAsText()) != Error.Ok)
        {
            GD.PrintErr($"[ContextManager] Ошибка JSON: {json.GetErrorMessage()}");
            return;
        }

        if (json.Data.VariantType != Variant.Type.Dictionary)
            return;

        var root = (Dictionary)json.Data;
        if (!root.ContainsKey("anchors"))
            return;

        foreach (var item in (Godot.Collections.Array)root["anchors"])
        {
            if (item.VariantType != Variant.Type.Dictionary)
                continue;
            var row = (Dictionary)item;
            _anchors.Add((row["name"].AsString(), row["desc"].AsString()));
        }

        GD.Print($"[ContextManager] Загружено {_anchors.Count} слов-якорей.");
    }

    private string PickAnchor()
    {
        if (_anchors.Count == 0)
            return "Чистая Энтропия (Фокус развернут на свободный фатум)";

        var chosen = _anchors[(int)_rng.RandiRange(0, _anchors.Count - 1)];
        return $"{chosen.Name} ({chosen.Desc})";
    }

    private static string MapBatteryAura(int percent)
    {
        if (percent < 0)
            return "Аура батареи скрыта прибором (заряд неизвестен)";
        if (percent <= 40)
            return $"Критический / Истощенный (Заряд: {percent}%). " +
                   "Недосмотрел, утомлён, подавлен, озабочен, в спешке; дефицит внимания и ментального ресурса.";
        if (percent <= 80)
            return $"Стабильный / Рабочий (Заряд: {percent}%). " +
                   "Норма, баланс, повседневная рутина; контролирует ситуацию, ресурсы стабильны.";
        return $"Профицитный / Контролируемый (Заряд: {percent}%). " +
               "Организован, спокоен, уверен, предусмотрителен; готов к планированию.";
    }

    private static string MapPowerState(DevicePowerProbe.Reading reading)
    {
        if (!reading.Known)
            return "Статус питания скрыт прибором (Godot 4.7 не даёт OS.GetPowerState)";
        return reading.Charging
            ? "Получение подпитки и поддержки из внешних источников"
            : "Опора исключительно на собственные внутренние резервы";
    }

    private static string MapBallMood(int code) => code switch
    {
        1 => "Мягкая поддержка и духовное наставление",
        2 => "Строгое предостережение и холодный реализм",
        _ => "Запутанная, туманная загадка Судьбы",
    };

    private static string MapWorldPressure(int code) => code switch
    {
        1 => "Стагнация и тишина (Мир замер, уступив инициативу игроку)",
        2 => "Шум и суета (Социум агрессивно давит и пытается отвлечь)",
        _ => "Скрытая угроза и перелом (Внешняя среда нестабильна, требует защиты)",
    };
}
