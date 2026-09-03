using System.Text.Json.Serialization;

namespace CrystalBall.Ai;

public sealed class OracleResult
{
    [JsonPropertyName("interpretation")]
    public string Interpretation { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("osiris_present")]
    public bool OsirisPresent { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "synthesized";

    [JsonPropertyName("ai_model")]
    public string? AiModel { get; set; }

    [JsonPropertyName("fallback_used")]
    public bool FallbackUsed { get; set; }

    [JsonPropertyName("fallback_reason")]
    public string? FallbackReason { get; set; }

    [JsonPropertyName("similarity")]
    public float Similarity { get; set; }

    public bool HasLlmAnswer =>
        !FallbackUsed
        && OsirisPresent
        && string.Equals(Source, "gigachat", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(Interpretation);
}
