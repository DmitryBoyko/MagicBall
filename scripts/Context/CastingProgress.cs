using CrystalBall.Ui;
using Godot;

namespace CrystalBall.Context;

/// <summary>
/// Пейсинг завуалированных этапов: медленно, по одной строке, со звёздочкой успеха.
/// </summary>
public sealed class CastingProgress
{
    /// <summary>Пауза между строками (~вдвое медленнее прежних 0.28 с, с запасом на чтение).</summary>
    public const double MinSecondsBetween = 0.75;

    /// <summary>Держим строку на экране после появления, чтобы успеть прочитать.</summary>
    public const double HoldAfterShow = 0.55;

    private readonly CastingLogSheet _log;
    private readonly SceneTree? _tree;
    private DateTime _lastShownUtc = DateTime.MinValue;

    public CastingProgress(CastingLogSheet log, SceneTree? tree = null)
    {
        _log = log;
        _tree = tree;
    }

    public Task ReportAsync(CastingStage stage, CancellationToken cancellationToken = default) =>
        ReportAsync(stage, inPrompt: true, cancellationToken);

    /// <param name="inPrompt">true — сигнал уйдёт в промпт (золотая ★), false — нет (голубая ★).</param>
    public async Task ReportAsync(
        CastingStage stage,
        bool inPrompt,
        CancellationToken cancellationToken = default)
    {
        await EnsurePaceAsync(cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        _log.AppendLine(CastingStageCatalog.Phrase(stage), inPrompt);
        _lastShownUtc = DateTime.UtcNow;
        await WaitSecondsAsync(HoldAfterShow, cancellationToken).ConfigureAwait(true);
    }

    private async Task EnsurePaceAsync(CancellationToken cancellationToken)
    {
        if (_lastShownUtc == DateTime.MinValue)
            return;

        var elapsed = (DateTime.UtcNow - _lastShownUtc).TotalSeconds;
        var remain = MinSecondsBetween - elapsed;
        if (remain > 0)
            await WaitSecondsAsync(remain, cancellationToken).ConfigureAwait(true);
    }

    private async Task WaitSecondsAsync(double seconds, CancellationToken cancellationToken)
    {
        if (seconds <= 0)
            return;

        if (_tree != null)
        {
            var end = Time.GetTicksMsec() + (ulong)(seconds * 1000.0);
            while (Time.GetTicksMsec() < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _tree.ToSignal(_tree, SceneTree.SignalName.ProcessFrame);
            }

            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken).ConfigureAwait(true);
    }
}
