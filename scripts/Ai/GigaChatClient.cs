using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CrystalBall.App;
using CrystalBall.Context;
using Godot;

namespace CrystalBall.Ai;

public sealed class GigaChatException : Exception
{
    public GigaChatException(string message) : base(message)
    {
    }
}

/// <summary>
/// OAuth Basic → Bearer с кэшем, ротация GigaChat-2 → Pro → Max → Ultra.
/// </summary>
public sealed class GigaChatClient : IDisposable
{
    public const string RotationPath = "user://gigachat_rotation.json";
    public const string SystemPrompt =
        "Ты — беспристрастный, древний и мудрый дух Хрустального Шара. " +
        "Твоя единственная задача — составить одно глубокое, психологическое и метафорическое предсказание-совет " +
        "для пользователя на основе переданных энергетических и контекстных потоков.\n\n" +
        "СТРОГИЕ ПРАВИЛА ГЕНЕРАЦИИ:\n" +
        "1. ЯЗЫК: Пиши строго на русском языке.\n" +
        "2. ДЛИНА: Итоговый текст должен быть строго от 240 до 290 символов с учетом пробелов. " +
        "Превышение порога в 300 символов или генерация короткого ответа (менее 200 символов) является критической ошибкой.\n" +
        "3. ФОРМАТ: Текст должен состоять строго из одной емкой, законченной мысли, разбитой максимум на 2-3 коротких предложения. " +
        "Без приветствий, без вступлений, без подписей, без markdown, без списков и разделителей. Сразу выдавай текст гадания.\n" +
        "4. ТАБУ: Запрещено давать ответы в стиле да/нет, называть календарные даты, обещать богатство или смерть. " +
        "Тон серьёзный, кинематографичный, нуарный.\n" +
        "5. ЛОГИКА СВЯЗЕЙ: Незаметно вплети населённый пункт, погоду, заряд смартфона, палитру фото, слово-якорь и оттенок стекла шара " +
        "(ball_tint_modifier) в единую метафору. Оттенок задаёт эмоциональную модуляцию тона: не называй его в лоб " +
        "как краску и не говори «шар зелёный», а дай почувствовать его психологический смысл.\n" +
        "6. ФИНАЛ: После основного текста отдельной строкой напиши маркер [[ИТОГ]] и сразу за ним ключевую фразу " +
        "обычными словами. Саму фразу не бери в квадратные скобки, кавычки, звёздочки и любую другую разметку. " +
        "В тексте гадания и в фразе — только буквы, пробелы и обычные знаки препинания.";

    private readonly AppConfig _config;
    private readonly System.Net.Http.HttpClient _http;
    private string? _token;
    private DateTime _tokenExpiresUtc = DateTime.MinValue;
    public string? LastModel { get; private set; }

    public GigaChatClient(AppConfig config)
    {
        _config = config;
        var handler = new HttpClientHandler();
        if (!config.GigaChatVerifySsl || OperatingSystem.IsAndroid())
        {
            handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;
        }

        _http = new System.Net.Http.HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(5, config.AiHttpTimeout)),
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_config.GigaChatCredentials);

    public async Task<string> GenerateAsync(OracleContext context, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
            throw new GigaChatException("GIGACHAT_CREDENTIALS пуст: вставьте Authorization key из кабинета Sber.");

        var token = await GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        var models = BuildRotationQueue();
        var errors = new List<string>();
        var userJson = JsonSerializer.Serialize(context, new JsonSerializerOptions
        {
            WriteIndented = false,
        });

        foreach (var model in models)
        {
            try
            {
                var text = await ChatAsync(token, model, userJson, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(text))
                {
                    errors.Add($"{model}: пустой ответ");
                    continue;
                }

                LastModel = model;
                return text;
            }
            catch (GigaChatException ex)
            {
                errors.Add(ex.Message);
            }
        }

        throw new GigaChatException(errors.Count == 0 ? "Все модели ротации молчат." : string.Join("; ", errors));
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow < _tokenExpiresUtc.AddSeconds(-30))
            return _token;

        using var request = new HttpRequestMessage(HttpMethod.Post, _config.GigaChatOauthUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", _config.GigaChatCredentials.Trim());
        request.Headers.TryAddWithoutValidation("RqUID", Guid.NewGuid().ToString());
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["scope"] = _config.GigaChatScope,
        });

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new GigaChatException($"oauth failed: {(int)response.StatusCode} {body[..Math.Min(body.Length, 200)]}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("access_token", out var tokenEl))
            throw new GigaChatException("oauth returned no access_token");

        _token = tokenEl.GetString();
        if (string.IsNullOrEmpty(_token))
            throw new GigaChatException("oauth returned empty access_token");

        if (doc.RootElement.TryGetProperty("expires_at", out var expEl) && expEl.TryGetInt64(out var expires))
        {
            _tokenExpiresUtc = expires > 1_000_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(expires).UtcDateTime
                : DateTimeOffset.FromUnixTimeSeconds(expires).UtcDateTime;
        }
        else
        {
            _tokenExpiresUtc = DateTime.UtcNow.AddMinutes(25);
        }

        return _token;
    }

    private async Task<string> ChatAsync(string token, string model, string userJson, CancellationToken cancellationToken)
    {
        var payload = new
        {
            model,
            stream = false,
            repetition_penalty = 1,
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userJson },
            },
        };

        var json = JsonSerializer.Serialize(payload);
        var text = await PostChatAsync(_config.GigaChatChatUrl, token, json, cancellationToken).ConfigureAwait(false);
        if (text == null)
            text = await PostChatAsync(_config.GigaChatChatUrlV2, token, json, cancellationToken).ConfigureAwait(false);
        if (text == null)
            throw new GigaChatException($"{model}: сеть не ответила");
        return text;
    }

    private async Task<string?> PostChatAsync(string url, string token, string json, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var code = (int)response.StatusCode;
        if (code == 404)
            return null;
        if (code is 400 or 401 or 402 or 403 or 429 or 500 or 502 or 503 or 504)
            throw new GigaChatException($"{url} http {code}: {body[..Math.Min(body.Length, 200)]}");
        if (!response.IsSuccessStatusCode)
            throw new GigaChatException($"{url} http {code}");

        using var doc = JsonDocument.Parse(body);
        return ExtractContent(doc.RootElement);
    }

    private static string ExtractContent(JsonElement payload)
    {
        if (payload.TryGetProperty("choices", out var choices) &&
            choices.GetArrayLength() > 0 &&
            choices[0].TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var content))
        {
            return content.GetString()?.Trim() ?? string.Empty;
        }

        return string.Empty;
    }

    private List<string> BuildRotationQueue()
    {
        var models = _config.GigaChatModels.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
        if (models.Length == 0)
            models = ["GigaChat-2"];

        var index = LoadRotationIndex() % models.Length;
        var chosen = models[index];
        SaveRotationIndex((index + 1) % models.Length, chosen);

        var queue = new List<string> { chosen };
        for (var i = 1; i < models.Length; i++)
            queue.Add(models[(index + i) % models.Length]);
        return queue;
    }

    private static int LoadRotationIndex()
    {
        if (!FileAccess.FileExists(RotationPath))
            return 0;
        try
        {
            using var file = FileAccess.Open(RotationPath, FileAccess.ModeFlags.Read);
            if (file == null)
                return 0;
            using var doc = JsonDocument.Parse(file.GetAsText());
            return doc.RootElement.TryGetProperty("next", out var next) ? next.GetInt32() : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static void SaveRotationIndex(int next, string last)
    {
        using var file = FileAccess.Open(RotationPath, FileAccess.ModeFlags.Write);
        file?.StoreString(JsonSerializer.Serialize(new { next, last }));
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
