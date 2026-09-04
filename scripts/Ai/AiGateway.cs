using CrystalBall.App;
using CrystalBall.Context;

namespace CrystalBall.Ai;

/// <summary>
/// Сначала настоящий прокси, затем локальный synthesize. Ключ Sber на сервере.
/// </summary>
public sealed class AiGateway : IDisposable
{
    private readonly AppConfig _config;
    private readonly OracleProxyClient _proxy;
    private readonly GigaChatClient _direct;
    private readonly SemanticCache _cache;
    private readonly MeaningBank _bank = new();

    public AiGateway(AppConfig config)
    {
        _config = config;
        _proxy = new OracleProxyClient(config);
        _direct = new GigaChatClient(config);
        _cache = new SemanticCache(config.SemanticMatchThreshold);
    }

    public async Task<OracleResult> InterpretAsync(
        OracleContext context,
        CastingProgress? casting = null,
        CancellationToken cancellationToken = default)
    {
        if (casting != null)
        {
            await casting.ReportAsync(CastingStage.Weave, inPrompt: true, cancellationToken).ConfigureAwait(true);
            await casting.ReportAsync(CastingStage.Await, inPrompt: true, cancellationToken).ConfigureAwait(true);
        }

        if (_config.UseProxy && _proxy.IsConfigured)
        {
            try
            {
                return await _proxy.InterpretAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return Fallback(context, $"прокси недоступен: {ex.Message}");
            }
        }

        if (_direct.IsConfigured)
        {
            try
            {
                var raw = await _direct.GenerateAsync(context, cancellationToken).ConfigureAwait(false);
                var extracted = SummaryExtractor.Extract(raw);
                _cache.Remember(Fingerprint(context), raw);
                return new OracleResult
                {
                    Interpretation = extracted.Interpretation,
                    Summary = extracted.Summary,
                    OsirisPresent = true,
                    Source = "gigachat",
                    AiModel = _direct.LastModel,
                    FallbackUsed = false,
                };
            }
            catch (Exception ex)
            {
                return Fallback(context, ex.Message);
            }
        }

        return Fallback(context, "прокси и GigaChat не настроены");
    }

    private OracleResult Fallback(OracleContext context, string reason)
    {
        var key = Fingerprint(context);
        if (_cache.TryFind(key, out var cached, out _))
        {
            cached.FallbackReason = $"{reason}; {cached.FallbackReason}";
            return cached;
        }

        var snap = context.DynamicSnapshot;
        var raw = _bank.Synthesize(
            context.DeterministicProfile.UserName,
            snap.WeatherState,
            snap.EntropyWordAnchor,
            snap.BallTintModifier,
            snap.GeoLocationType);
        var extracted = SummaryExtractor.Extract(raw);
        return new OracleResult
        {
            Interpretation = extracted.Interpretation,
            Summary = extracted.Summary,
            OsirisPresent = false,
            Source = "synthesized",
            FallbackUsed = true,
            FallbackReason = reason,
        };
    }

    private static string Fingerprint(OracleContext context)
    {
        var d = context.DeterministicProfile;
        var s = context.DynamicSnapshot;
        return string.Join('|',
            d.UserName, d.ZodiacSign, d.DestinyNumber,
            s.EntropyWordAnchor, s.PhotoMysticTag, s.TimeOfDay, s.BallMoodModifier,
            s.BallTintModifier, s.InquiryPulseAura,
            s.GeoLocationType ?? "", s.WeatherState ?? "", s.DeviceBatteryAura ?? "");
    }

    public void Dispose()
    {
        _proxy.Dispose();
        _direct.Dispose();
    }
}
