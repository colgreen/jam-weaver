using JamWeaver.Core.Transport;

namespace JamWeaver.Core.Tests;

public sealed class TransportTests
{
    [Fact]
    public void Start_resets_position_and_continue_preserves_it()
    {
        var engine = new TransportEngine();
        engine.Process(ClockSource.External, RealtimeMessage.Start);
        for (var index = 0; index < 7; index++) engine.Process(ClockSource.External, RealtimeMessage.Clock);
        engine.Process(ClockSource.External, RealtimeMessage.Stop);

        engine.Process(ClockSource.External, RealtimeMessage.Continue);
        Assert.Equal(7UL, engine.Position.Pulse);

        engine.Process(ClockSource.External, RealtimeMessage.Start);
        Assert.Equal(0UL, engine.Position.Pulse);
        Assert.Equal(TransportState.Running, engine.State);
    }

    [Fact]
    public void Clock_pulses_are_ignored_unless_source_matches_and_transport_is_running()
    {
        var engine = new TransportEngine();
        var accepted = new List<TransportPosition>();
        engine.PulseAccepted += accepted.Add;

        Assert.False(engine.Process(ClockSource.External, RealtimeMessage.Clock));
        Assert.False(engine.Process(ClockSource.Internal, RealtimeMessage.Start));
        engine.Process(ClockSource.External, RealtimeMessage.Start);
        Assert.False(engine.Process(ClockSource.Internal, RealtimeMessage.Clock));
        Assert.True(engine.Process(ClockSource.External, RealtimeMessage.Clock));

        Assert.Equal([new TransportPosition(0)], accepted);
        Assert.Equal(1UL, engine.Position.Pulse);
    }

    [Fact]
    public void Selecting_a_source_stops_transport_and_preserves_position()
    {
        var engine = new TransportEngine();
        engine.Process(ClockSource.External, RealtimeMessage.Start);
        engine.Process(ClockSource.External, RealtimeMessage.Clock);

        engine.SelectSource(ClockSource.Internal);

        Assert.Equal(ClockSource.Internal, engine.Source);
        Assert.Equal(TransportState.Stopped, engine.State);
        Assert.Equal(1UL, engine.Position.Pulse);
    }

    [Fact]
    public void External_clock_loss_occurs_at_configured_timeout_and_continue_recovers()
    {
        var time = new ManualTimeProvider();
        var engine = new TransportEngine(ClockSource.External, time);
        engine.Process(ClockSource.External, RealtimeMessage.Start);
        time.Advance(TimeSpan.FromMilliseconds(499));
        Assert.False(engine.CheckExternalClockLoss(TimeSpan.FromMilliseconds(500)));

        time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.True(engine.CheckExternalClockLoss(TimeSpan.FromMilliseconds(500)));
        Assert.Equal(TransportState.ClockLost, engine.State);
        Assert.False(engine.Process(ClockSource.External, RealtimeMessage.Clock));

        engine.Process(ClockSource.External, RealtimeMessage.Continue);
        Assert.Equal(TransportState.Running, engine.State);
    }

    [Fact]
    public void Timeline_reports_sixteenth_note_beat_and_bar_boundaries()
    {
        var engine = new TransportEngine();
        using var timeline = new SequencerTimeline(engine, PatternTiming.SixteenthNotes, 16);
        var boundaries = new List<TimelineBoundary>();
        timeline.Boundary += boundaries.Add;
        engine.Process(ClockSource.External, RealtimeMessage.Start);
        for (var index = 0; index < 97; index++) engine.Process(ClockSource.External, RealtimeMessage.Clock);

        Assert.Equal([0, 6, 12, 18], boundaries.Take(19).Where(x => x.StepIndex.HasValue).Select(x => (int)x.Position.Pulse));
        Assert.Equal([0, 24, 48, 72, 96], boundaries.Where(x => x.IsBeatBoundary).Select(x => (int)x.Position.Pulse));
        Assert.Equal([0, 96], boundaries.Where(x => x.IsBarBoundary).Select(x => (int)x.Position.Pulse));
        Assert.Equal(0, boundaries[96].StepIndex);
    }

    [Fact]
    public void Timeline_wraps_pattern_independently_of_bar_length()
    {
        var engine = new TransportEngine();
        using var timeline = new SequencerTimeline(engine, PatternTiming.SixteenthNotes, 12);
        var steps = new List<int>();
        timeline.Boundary += boundary => { if (boundary.StepIndex is { } step) steps.Add(step); };
        engine.Process(ClockSource.External, RealtimeMessage.Start);
        for (var index = 0; index <= 72; index++) engine.Process(ClockSource.External, RealtimeMessage.Clock);

        Assert.Equal([0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 0], steps);
    }

    [Fact]
    public void Running_swap_uses_next_strictly_future_bar_and_latest_value_wins()
    {
        var swap = new BarQuantizedSwap<string>("original");
        swap.Queue("first", TransportState.Running, new TransportPosition(20));
        swap.Queue("latest", TransportState.Running, new TransportPosition(90));

        Assert.False(swap.TryActivate(new TransportPosition(95)));
        Assert.True(swap.TryActivate(new TransportPosition(96)));
        Assert.Equal("latest", swap.Current);
        Assert.Null(swap.Pending);
    }

    [Fact]
    public void Stopped_swap_is_immediate()
    {
        var swap = new BarQuantizedSwap<string>("original");
        swap.Queue("replacement", TransportState.Stopped, new TransportPosition(50));

        Assert.Equal("replacement", swap.Current);
        Assert.Null(swap.Pending);
    }

    [Fact]
    public async Task Internal_clock_orders_transport_and_emits_requested_pulses()
    {
        var port = new FakeMidiOutputPort();
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        var engine = new TransportEngine(ClockSource.Internal);
        var scheduler = new CountingScheduler(24);
        await using var clock = new InternalMidiClock(output, engine, scheduler);

        clock.Start();
        await scheduler.Completed.Task.WaitAsync(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        clock.Stop();

        Assert.Equal("Start", port.Messages[0]);
        Assert.Equal(24, port.Messages.Count(message => message == "Clock"));
        Assert.Equal(24UL, engine.Position.Pulse);
        Assert.Equal(TransportState.Stopped, engine.State);
    }

    [Fact]
    public async Task Internal_clock_rejects_start_when_external_source_is_selected()
    {
        using var output = new SafeMidiOutput();
        output.ReplacePort(new FakeMidiOutputPort());
        await using var clock = new InternalMidiClock(output, new TransportEngine(), new CountingScheduler(1));

        var error = Assert.Throws<InvalidOperationException>(clock.Start);
        Assert.Contains("not internal", error.Message);
    }

    [Fact]
    public void External_input_classifies_events_without_relaying_to_output()
    {
        var received = new List<RealtimeMessage>();
        var input = new ExternalMidiClockInput();
        input.MessageReceived += (_, message) => received.Add(message);

        input.Receive(RealtimeMessage.Start);
        input.Receive(RealtimeMessage.Clock);
        input.Receive(RealtimeMessage.Stop);

        Assert.Equal([RealtimeMessage.Start, RealtimeMessage.Clock, RealtimeMessage.Stop], received);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan duration) => _timestamp += duration.Ticks;
    }

    private sealed class CountingScheduler(int pulses) : IClockPulseScheduler
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task RunAsync(Func<double> bpm, Action pulse, CancellationToken cancellationToken)
        {
            for (var index = 0; index < pulses; index++) pulse();
            Completed.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
