using System.Text.Json.Serialization;

namespace CrystalBall.Profile;

public sealed class UserProfile
{
    [JsonPropertyName("user_name")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("birth_date")]
    public string BirthDate { get; set; } = string.Empty;

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

    [JsonIgnore]
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(UserName) &&
        DateTime.TryParse(BirthDate, out _);
}
