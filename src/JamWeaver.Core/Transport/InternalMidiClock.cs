namespace JamWeaver.Core.Transport;

/// <summary>Owns one internal clock run at a time and retains failures for the caller to inspect.</summary>
/// <remarks>
/// Cancellation can interrupt waiting for a lifecycle operation; once admitted, it joins its owned run.
/// Keep the borrowed output alive through disposal, which sends any Stop still owed by this clock.
/// </remarks>
public sealed class InternalMidiClock : IAsyncDisposable
{
    private readonly SafeMidiOutput _output;
    private readonly TransportEngine _engine;
    private readonly IClockPulseScheduler _scheduler;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private readonly object _errorSync = new();
    private CancellationTokenSource? _cancellation;
    private Task? _runTask;
    private Exception? _error;
    private bool _needsMidiStop;
    private bool _disposed;
    private double _bpm = 120;

    public InternalMidiClock(SafeMidiOutput output, TransportEngine engine, IClockPulseScheduler? scheduler = null)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _scheduler = scheduler ?? new StopwatchClockPulseScheduler();
    }

    public double Bpm
    {
        get => Volatile.Read(ref _bpm);
        set
        {
            if (!double.IsFinite(value) || value is < 20 or > 300)
                throw new ArgumentOutOfRangeException(nameof(value), "BPM must be finite and 20-300.");
            Volatile.Write(ref _bpm, value);
        }
    }
    public bool IsRunning => Volatile.Read(ref _runTask) is { IsCompleted: false };
    public Exception? Error => Volatile.Read(ref _error);

    public Task StartAsync(CancellationToken cancellationToken = default) =>
        BeginAsync(resetPosition: true, cancellationToken);

    public Task ContinueAsync(CancellationToken cancellationToken = default) =>
        BeginAsync(resetPosition: false, cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureInternalSource();
            await StopRunAsync().ConfigureAwait(false);
            try { SendStop(); }
            finally { _engine.Process(ClockSource.Internal, RealtimeMessage.Stop); }
        }
        finally { _lifecycle.Release(); }
    }

    private async Task BeginAsync(bool resetPosition, CancellationToken cancellationToken)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureInternalSource();
            if (!resetPosition && IsRunning) return;
            await StopRunAsync().ConfigureAwait(false);
            EnsureInternalSource();
            Volatile.Write(ref _error, null);
            try
            {
                if (resetPosition) _output.Start(); else _output.Continue();
                _needsMidiStop = true;
                _engine.Process(ClockSource.Internal, resetPosition ? RealtimeMessage.Start : RealtimeMessage.Continue);
            }
            catch (Exception ex)
            {
                RecordError(ex);
                StopLocalTransport();
                throw;
            }
            _cancellation = new CancellationTokenSource();
            Volatile.Write(ref _runTask, RunAsync(_cancellation.Token));
        }
        finally { _lifecycle.Release(); }
    }

    private void EnsureInternalSource()
    {
        if (_engine.Source != ClockSource.Internal)
            throw new InvalidOperationException("The transport clock source is not internal.");
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _scheduler.RunAsync(() => Bpm, () => EmitPulse(cancellationToken), cancellationToken)
                .ConfigureAwait(false);
            if (!cancellationToken.IsCancellationRequested)
                throw new InvalidOperationException("The clock scheduler ended without cancellation.");
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested
            && ex.CancellationToken == cancellationToken) { }
        catch (Exception ex)
        {
            RecordError(new InvalidOperationException(
                $"Internal MIDI clock failed on output '{_output.PortName ?? "not selected"}': {ex.Message}", ex));
        }
        finally { StopLocalTransport(); }
    }

    private void EmitPulse(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _output.Clock();
        _engine.Process(ClockSource.Internal, RealtimeMessage.Clock);
    }

    private async Task StopRunAsync()
    {
        if (_cancellation is null) return;
        try
        {
            try { _cancellation.Cancel(); }
            catch (Exception ex) { RecordError(new InvalidOperationException("Could not cancel the internal clock run.", ex)); }
            if (_runTask is not null) await _runTask.ConfigureAwait(false);
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            Volatile.Write(ref _runTask, null);
        }
    }

    private void StopLocalTransport()
    {
        try { _engine.Process(ClockSource.Internal, RealtimeMessage.Stop); }
        catch (Exception ex) { RecordError(new InvalidOperationException("Could not stop local transport after the internal clock run.", ex)); }
    }

    private void RecordError(Exception error)
    {
        lock (_errorSync)
        {
            var previous = Error;
            Volatile.Write(ref _error, previous is null ? error
                : new AggregateException("The internal clock encountered multiple failures.", previous, error));
        }
    }

    private void SendStop()
    {
        _output.Stop();
        _needsMidiStop = false;
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            List<Exception> errors = [];
            try { await StopRunAsync().ConfigureAwait(false); }
            catch (Exception ex)
            {
                errors.Add(new InvalidOperationException($"Could not join the internal clock run during shutdown: {ex.Message}", ex));
            }
            try
            {
                if (_needsMidiStop) SendStop();
            }
            catch (Exception ex)
            {
                errors.Add(new InvalidOperationException(
                    $"Could not send MIDI Stop during internal clock shutdown to output '{_output.PortName ?? "not selected"}': {ex.Message}", ex));
            }
            finally { StopLocalTransport(); }
            if (errors.Count > 0)
            {
                var failure = errors.Count == 1 ? errors[0]
                    : new AggregateException("Internal clock shutdown encountered multiple failures.", errors);
                RecordError(failure);
                throw failure;
            }
        }
        finally { _lifecycle.Release(); }
    }
}
