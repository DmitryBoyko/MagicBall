using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CrystalBall.App;
using CrystalBall.Context;

namespace CrystalBall.Ai;

/// <summary>
/// HTTP-клиент настоящего шлюза. Ключ Sber на сервере, в APK его нет.
/// </summary>
public sealed class OracleProxyClient : IDisposable
{
    private readonly AppConfig _config;
    private readonly System.Net.Http.HttpClient _http;

    public OracleProxyClient(AppConfig config)
    {
        _config = config;
        _http = new System.Net.Http.HttpClient
        {
            Timeout = TimeSpan.FromSeconds(Math.Max(8, config.AiHttpTimeout + 5)),
        };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ResolveBaseUrl());

    public async Task<OracleResult> InterpretAsync(OracleContext context, CancellationToken cancellationToken)
    {
        var baseUrl = ResolveBaseUrl().TrimEnd('/');
        var url = $"{baseUrl}/api/v1/oracle";
        var json = JsonSerializer.Serialize(context);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"proxy http {(int)response.StatusCode}: {body[..Math.Min(body.Length, 180)]}");

        var parsed = JsonSerializer.Deserialize<OracleResult>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });
        if (parsed == null)
            throw new HttpRequestException("proxy returned empty body");

        var joined = string.IsNullOrWhiteSpace(parsed.Summary)
            ? parsed.Interpretation
            : $"{parsed.Interpretation}\n[[ИТОГ]] {parsed.Summary}";
        var extracted = SummaryExtractor.Extract(joined);
        parsed.Interpretation = extracted.Interpretation;
        parsed.Summary = extracted.Summary;
        return parsed;
    }

    private string ResolveBaseUrl()
    {
        if (OperatingSystem.IsAndroid() && !string.IsNullOrWhiteSpace(_config.AndroidBaseUrl))
            return _config.AndroidBaseUrl;
        return _config.ProxyBaseUrl;
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}
