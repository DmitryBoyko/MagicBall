using System.Text.Json.Serialization;

namespace CrystalBall.Context;

public sealed class DeterministicProfile
{
    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("zodiac_sign")]
    public string ZodiacSign { get; set; } = string.Empty;

    [JsonPropertyName("astrological_element")]
    public string AstrologicalElement { get; set; } = string.Empty;

    [JsonPropertyName("ruling_planet")]
    public string RulingPlanet { get; set; } = string.Empty;

    [JsonPropertyName("destiny_number")]
    public int DestinyNumber { get; set; }

    [JsonPropertyName("chinese_totem")]
    public string ChineseTotem { get; set; } = string.Empty;

    [JsonPropertyName("age_group")]
    public string AgeGroup { get; set; } = string.Empty;
}

public sealed class DynamicSnapshot
{
    [JsonPropertyName("exact_time_context")]
    public string ExactTimeContext { get; set; } = string.Empty;

    [JsonPropertyName("time_of_day")]
    public string TimeOfDay { get; set; } = string.Empty;

    [JsonPropertyName("current_season")]
    public string CurrentSeason { get; set; } = string.Empty;

    [JsonPropertyName("geo_location_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GeoLocationType { get; set; }

    [JsonPropertyName("weather_state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WeatherState { get; set; }

    [JsonPropertyName("device_battery_aura")]
    public string DeviceBatteryAura { get; set; } = string.Empty;

    [JsonPropertyName("device_power_state")]
    public string DevicePowerState { get; set; } = string.Empty;

    [JsonPropertyName("inquiry_pulse_aura")]
    public string InquiryPulseAura { get; set; } = string.Empty;

    [JsonPropertyName("photo_mystic_tag")]
    public string PhotoMysticTag { get; set; } = string.Empty;

    [JsonPropertyName("photo_color_palette")]
    public string PhotoColorPalette { get; set; } = string.Empty;

    [JsonPropertyName("photo_luminance_vibe")]
    public string PhotoLuminanceVibe { get; set; } = string.Empty;

    [JsonPropertyName("imagenet_raw_tag")]
    public string ImageNetRawTag { get; set; } = string.Empty;

    [JsonPropertyName("entropy_word_anchor")]
    public string EntropyWordAnchor { get; set; } = string.Empty;

    [JsonPropertyName("ball_mood_modifier")]
    public string BallMoodModifier { get; set; } = string.Empty;

    [JsonPropertyName("ball_tint_name")]
    public string BallTintName { get; set; } = string.Empty;

    [JsonPropertyName("ball_tint_meaning")]
    public string BallTintMeaning { get; set; } = string.Empty;

    [JsonPropertyName("ball_tint_modifier")]
    public string BallTintModifier { get; set; } = string.Empty;

    [JsonPropertyName("world_pressure_modifier")]
    public string WorldPressureModifier { get; set; } = string.Empty;

    [JsonPropertyName("ball_mood_code")]
    public int BallMoodCode { get; set; }

    [JsonPropertyName("world_pressure_code")]
    public int WorldPressureCode { get; set; }
}

public sealed class OracleContext
{
    [JsonPropertyName("deterministic_profile")]
    public DeterministicProfile DeterministicProfile { get; set; } = new();

    [JsonPropertyName("dynamic_snapshot")]
    public DynamicSnapshot DynamicSnapshot { get; set; } = new();
}

public sealed class PhotoAnalysis
{
    public string RawTag { get; set; } = string.Empty;
    public string MysticTag { get; set; } = string.Empty;
    public string ColorPalette { get; set; } = string.Empty;
    public string LuminanceVibe { get; set; } = string.Empty;
    /// <summary>true — брали кадры из галереи (этап PhotoScan в промпте).</summary>
    public bool FromGallery { get; set; }
}
