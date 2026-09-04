using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace CrystalBall.App;

public sealed class AppConfig
{
    public const string ResourcePath = "res://config/api.json";
    public const float DefaultQueueTimeout = 5f;
    public const float SemanticThreshold = 0.82f;
    public const float VortexBurstSeconds = 0.08f;
    public const float VortexRampSeconds = 0.22f;
    public const float VortexFadeSeconds = 0.95f;
    public const float DefaultVortexSeconds = 4f;
    public const int VortexParticleCount = 1200;
    public const int TensorLength = 150528;
    public const int ImageSize = 224;
    public const int DefaultPhotoLookback = 2;
    public const int MinPhotoLookback = 1;
    public const int MaxPhotoLookback = 10;

    [JsonPropertyName("gigachat_oauth_url")]
    public string GigaChatOauthUrl { get; set; } = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";

    [JsonPropertyName("gigachat_chat_url")]
    public string GigaChatChatUrl { get; set; } = "https://api.giga.chat/v1/chat/completions";

    [JsonPropertyName("gigachat_chat_url_v2")]
    public string GigaChatChatUrlV2 { get; set; } = "https://api.giga.chat/v2/chat/completions";

    [JsonPropertyName("gigachat_credentials")]
    public string GigaChatCredentials { get; set; } = string.Empty;

    [JsonPropertyName("gigachat_client_id")]
    public string GigaChatClientId { get; set; } = "019e869b-0a33-7ee6-a49d-f2c9f3a449e7";

    [JsonPropertyName("gigachat_scope")]
    public string GigaChatScope { get; set; } = "GIGACHAT_API_PERS";

    [JsonPropertyName("gigachat_models")]
    public string[] GigaChatModels { get; set; } =
    [
        "GigaChat-2",
        "GigaChat-2-Pro",
        "GigaChat-2-Max",
        "GigaChat-3-Ultra",
    ];

    [JsonPropertyName("gigachat_verify_ssl")]
    public bool GigaChatVerifySsl { get; set; } = true;

    [JsonPropertyName("gigachat_ca_bundle")]
    public string GigaChatCaBundle { get; set; } = "res://config/certs/russian_trusted_root_ca_pem.crt";

    [JsonPropertyName("ai_queue_timeout")]
    public float AiQueueTimeout { get; set; } = DefaultQueueTimeout;

    [JsonPropertyName("ai_http_timeout")]
    public float AiHttpTimeout { get; set; } = 20f;

    [JsonPropertyName("semantic_threshold")]
    public float SemanticMatchThreshold { get; set; } = SemanticThreshold;

    [JsonPropertyName("other_apps_url")]
    public string OtherAppsUrl { get; set; } = "https://www.rustore.ru/catalog/developer/9cf9ks";

    [JsonPropertyName("proxy_base_url")]
    public string ProxyBaseUrl { get; set; } = "http://127.0.0.1:18437";

    [JsonPropertyName("android_base_url")]
    public string AndroidBaseUrl { get; set; } = "http://147.45.173.26:18437";

    [JsonPropertyName("vortex_seconds")]
    public float VortexSeconds { get; set; } = DefaultVortexSeconds;

    [JsonPropertyName("photo_lookback_count")]
    public int PhotoLookbackCount { get; set; } = DefaultPhotoLookback;

    [JsonPropertyName("use_proxy")]
    public bool UseProxy { get; set; } = true;

    public static AppConfig Current { get; private set; } = new();

    public static AppConfig Load()
    {
        var loaded = TryReadJson(ResourcePath) ?? TryReadJson("res://config/secrets.json") ?? new AppConfig();
        if (string.IsNullOrWhiteSpace(loaded.GigaChatCredentials))
        {
            var env = System.Environment.GetEnvironmentVariable("GIGACHAT_CREDENTIALS");
            if (!string.IsNullOrWhiteSpace(env))
                loaded.GigaChatCredentials = env.Trim();
        }

        Current = loaded;
        if (Current.VortexSeconds < 0.5f)
            Current.VortexSeconds = DefaultVortexSeconds;
        Current.PhotoLookbackCount = Math.Clamp(
            Current.PhotoLookbackCount, MinPhotoLookback, MaxPhotoLookback);
        return Current;
    }

    private static AppConfig? TryReadJson(string path)
    {
        if (!FileAccess.FileExists(path))
            return null;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
            return null;

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return JsonSerializer.Deserialize<AppConfig>(file.GetAsText(), options);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[AppConfig] Не удалось прочитать {path}: {ex.Message}");
            return null;
        }
    }
}
