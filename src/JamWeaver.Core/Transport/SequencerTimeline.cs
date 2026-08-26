namespace JamWeaver.Core.Transport;

public readonly record struct TimelineBoundary(TransportPosition Position, bool IsBeatBoundary,
    bool IsBarBoundary, int? StepIndex);

public sealed class SequencerTimeline : IDisposable
{
    private readonly TransportEngine _engine;
    private Configuration _configuration;

    public SequencerTimeline(TransportEngine engine, PatternTiming timing, int stepCount)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _configuration = Validate(timing, stepCount);
        _engine.PulseAccepted += OnPulse;
    }

    public event Action<TimelineBoundary>? Boundary;

    public void Configure(PatternTiming timing, int stepCount) =>
        Interlocked.Exchange(ref _configuration, Validate(timing, stepCount));

    private void OnPulse(TransportPosition position)
    {
        var configuration = Volatile.Read(ref _configuration);
        int? step = position.Pulse % (ulong)configuration.Timing.PulsesPerStep == 0
            ? (int)((position.Pulse / (ulong)configuration.Timing.PulsesPerStep) % (ulong)configuration.StepCount)
            : null;
        Boundary?.Invoke(new TimelineBoundary(position, position.IsBeatBoundary, position.IsBarBoundary, step));
    }

    private static Configuration Validate(PatternTiming timing, int stepCount)
    {
        if (stepCount is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(stepCount));
        return new Configuration(timing, stepCount);
    }

    public void Dispose() => _engine.PulseAccepted -= OnPulse;
    private sealed record Configuration(PatternTiming Timing, int StepCount);
}
