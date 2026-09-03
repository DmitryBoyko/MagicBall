using CrystalBall.App;

namespace CrystalBall.Ai;

public readonly record struct QueueOutcome<T>(T Value, float WaitedSeconds, bool TimedOut, bool Overflow);

/// <summary>
/// FIFO с таймаутом AI_QUEUE_TIMEOUT (5 с). Переполнение — сразу fallback, без вызова ИИ.
/// </summary>
public sealed class AiQueue
{
    public const int MaxDepth = 2;

    private readonly float _timeoutSeconds;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _pending;

    public AiQueue(float timeoutSeconds = AppConfig.DefaultQueueTimeout)
    {
        _timeoutSeconds = timeoutSeconds;
    }

    public bool IsOverflowing => Volatile.Read(ref _pending) >= MaxDepth;

    public async Task<QueueOutcome<T>> SubmitAsync<T>(Func<bool, CancellationToken, Task<T>> job, CancellationToken cancellationToken)
    {
        var pending = Interlocked.Increment(ref _pending);
        if (pending > MaxDepth)
        {
            Interlocked.Decrement(ref _pending);
            var overflowValue = await job(true, cancellationToken).ConfigureAwait(false);
            return new QueueOutcome<T>(overflowValue, 0f, false, true);
        }

        var started = DateTime.UtcNow;
        try
        {
            var entered = await _gate.WaitAsync(TimeSpan.FromSeconds(_timeoutSeconds), cancellationToken).ConfigureAwait(false);
            var waited = (float)(DateTime.UtcNow - started).TotalSeconds;
            var skipAi = !entered || waited > _timeoutSeconds;
            if (!entered)
            {
                var timed = await job(true, cancellationToken).ConfigureAwait(false);
                return new QueueOutcome<T>(timed, waited, true, false);
            }

            try
            {
                var value = await job(skipAi, cancellationToken).ConfigureAwait(false);
                return new QueueOutcome<T>(value, waited, skipAi, false);
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _pending);
        }
    }
}
