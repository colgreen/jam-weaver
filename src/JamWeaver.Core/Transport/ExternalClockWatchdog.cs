namespace JamWeaver.Core.Transport;

public sealed class ExternalClockWatchdog : IAsyncDisposable
{
    private readonly TransportEngine _engine;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _task;

    public ExternalClockWatchdog(TransportEngine engine, TimeSpan? timeout = null, TimeSpan? pollInterval = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _timeout = timeout ?? TimeSpan.FromMilliseconds(500);
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(50);
        if (_timeout < TimeSpan.FromMilliseconds(100) || _timeout > TimeSpan.FromSeconds(5)) throw new ArgumentOutOfRangeException(nameof(timeout));
        if (_pollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollInterval));
        _task = RunAsync(_cancellation.Token);
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
                _engine.CheckExternalClockLoss(_timeout);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        _cancellation.Cancel();
        await _task.ConfigureAwait(false);
        _cancellation.Dispose();
    }
}
