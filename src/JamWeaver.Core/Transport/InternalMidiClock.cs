namespace JamWeaver.Core.Transport;

public sealed class InternalMidiClock : IAsyncDisposable
{
    private readonly SafeMidiOutput _output;
    private readonly TransportEngine _engine;
    private readonly IClockPulseScheduler _scheduler;
    private CancellationTokenSource? _cancellation;
    private Task? _runTask;
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
            if (value is < 20 or > 300) throw new ArgumentOutOfRangeException(nameof(value), "BPM must be 20-300.");
            Volatile.Write(ref _bpm, value);
        }
    }
    public bool IsRunning => _runTask is { IsCompleted: false };

    public void Start()
    {
        EnsureInternalSource();
        StopLoop();
        _output.Start();
        _engine.Process(ClockSource.Internal, RealtimeMessage.Start);
        StartLoop();
    }

    public void Continue()
    {
        EnsureInternalSource();
        if (IsRunning) return;
        _output.Continue();
        _engine.Process(ClockSource.Internal, RealtimeMessage.Continue);
        StartLoop();
    }

    public void Stop()
    {
        EnsureInternalSource();
        StopLoop();
        _output.Stop();
        _engine.Process(ClockSource.Internal, RealtimeMessage.Stop);
    }

    private void StartLoop()
    {
        _cancellation = new CancellationTokenSource();
        _runTask = _scheduler.RunAsync(() => Bpm, EmitPulse, _cancellation.Token);
    }

    private void EnsureInternalSource()
    {
        if (_engine.Source != ClockSource.Internal)
            throw new InvalidOperationException("The transport clock source is not internal.");
    }

    private void EmitPulse()
    {
        try
        {
            _output.Clock();
            _engine.Process(ClockSource.Internal, RealtimeMessage.Clock);
        }
        catch
        {
            StopLoop();
            _engine.Process(ClockSource.Internal, RealtimeMessage.Stop);
            throw;
        }
    }

    private void StopLoop()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    public async ValueTask DisposeAsync()
    {
        StopLoop();
        if (_runTask is not null)
        {
            try { await _runTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _runTask = null;
    }
}
