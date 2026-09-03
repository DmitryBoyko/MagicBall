using CrystalBall.Ui;
using Godot;

namespace CrystalBall.Context;

/// <summary>
/// Пейсинг завуалированных этапов: мин. пауза между строками, без блокировки реальной работы дольше этапа.
/// </summary>
public sealed class CastingProgress
{
    public const double MinSecondsBetween = 0.55;

    private readonly CastingLogSheet _log;
    private readonly SceneTree? _tree;
    private DateTime _lastShownUtc = DateTime.MinValue;

    public CastingProgress(CastingLogSheet log, SceneTree? tree = null)
    {
        _log = log;
        _tree = tree;
    }

    public async Task ReportAsync(CastingStage stage, CancellationToken cancellationToken = default)
    {
        await EnsurePaceAsync(cancellationToken).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
        _log.AppendLine(CastingStageCatalog.Phrase(stage));
        _lastShownUtc = DateTime.UtcNow;
    }

    private async Task EnsurePaceAsync(CancellationToken cancellationToken)
    {
        if (_lastShownUtc == DateTime.MinValue)
            return;

        var elapsed = (DateTime.UtcNow - _lastShownUtc).TotalSeconds;
        var remain = MinSecondsBetween - elapsed;
        if (remain <= 0)
            return;

        if (_tree != null)
        {
            var end = Time.GetTicksMsec() + (ulong)(remain * 1000.0);
            while (Time.GetTicksMsec() < end)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _tree.ToSignal(_tree, SceneTree.SignalName.ProcessFrame);
            }

            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(remain), cancellationToken).ConfigureAwait(true);
    }
}
