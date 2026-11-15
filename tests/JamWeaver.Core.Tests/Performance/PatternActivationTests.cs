using JamWeaver.Core.Performance;
using JamWeaver.Core.Transport;

namespace JamWeaver.Core.Tests.Performance;

public sealed class PatternActivationTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 96)]
    [InlineData(95, 96)]
    [InlineData(96, 96)]
    [InlineData(97, 192)]
    [InlineData(192, 192)]
    [InlineData(193, 288)]
    public void A_running_candidate_uses_the_first_bar_boundary_whose_pulse_has_not_been_accepted(int clocksBeforeQueue, int activationPulse)
    {
        using var fixture = new PlayerFixture();
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Pulse(clocksBeforeQueue);
        var candidate = Pattern(64);

        fixture.Player.Queue(candidate);
        fixture.Pulse(activationPulse - clocksBeforeQueue);

        Assert.Equal(fixture.Original.Id, fixture.Player.CurrentPattern!.Id);
        Assert.Equal(candidate.Id, fixture.Player.PendingPattern!.Id);
        fixture.Pulse(1);
        Assert.Equal(candidate.Id, fixture.Player.CurrentPattern.Id);
        Assert.Null(fixture.Player.PendingPattern);
        Assert.Equal("On:1:64:100", fixture.NoteMessages.Last());
    }

    [Theory]
    [InlineData(64, 6, 76)]
    [InlineData(12, 6, 64)]
    [InlineData(8, 12, 60)]
    [InlineData(32, 3, 60)]
    public void Activation_preserves_absolute_phase_for_different_lengths_and_resolutions(int stepCount, int pulsesPerStep, int expectedNote)
    {
        using var fixture = new PlayerFixture();
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Pulse(1);
        fixture.Player.Queue(Pattern(60, stepCount, pulsesPerStep));

        fixture.Pulse(96);

        Assert.Equal(["Off:1:36:0", $"On:1:{expectedNote}:100"], fixture.NoteMessages.TakeLast(2));
    }

    [Fact]
    public async Task Queueing_after_a_boundary_was_accepted_does_not_apply_to_its_delayed_callback()
    {
        using var releaseBoundary = new ManualResetEventSlim();
        var boundaryEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = new PlayerFixture(engine => engine.PulseAccepted += position =>
        {
            if (position.Pulse != 96) return;
            boundaryEntered.TrySetResult();
            if (!releaseBoundary.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The test did not release the accepted boundary.");
        });
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Pulse(96);
        var boundary = Task.Run(() => fixture.Pulse(1), TestContext.Current.CancellationToken);
        var candidate = Pattern(64);
        try
        {
            await boundaryEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            fixture.Player.Queue(candidate);
        }
        finally
        {
            releaseBoundary.Set();
            await boundary.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.Equal(fixture.Original.Id, fixture.Player.CurrentPattern!.Id);
        Assert.Equal(candidate.Id, fixture.Player.PendingPattern!.Id);
        fixture.Pulse(96);
        Assert.Equal(candidate.Id, fixture.Player.CurrentPattern.Id);
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("clock-loss")]
    [InlineData("source")]
    public void Continue_preserves_a_pending_deadline_across_pauses_and_source_changes(string operation)
    {
        var time = new ManualTimeProvider();
        using var fixture = new PlayerFixture(timeProvider: time);
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Pulse(95);
        var candidate = Pattern(64);
        fixture.Player.Queue(candidate);

        switch (operation)
        {
            case "stop": fixture.Engine.Process(ClockSource.External, RealtimeMessage.Stop); break;
            case "clock-loss":
                time.Advance(TimeSpan.FromMilliseconds(500));
                Assert.True(fixture.Engine.CheckExternalClockLoss(TimeSpan.FromMilliseconds(500)));
                break;
            case "source": fixture.Engine.SelectSource(ClockSource.Internal); break;
        }

        Assert.Equal(candidate.Id, fixture.Player.PendingPattern!.Id);
        Assert.Equal(fixture.Original.Id, fixture.Player.CurrentPattern!.Id);
        fixture.Engine.Process(fixture.Engine.Source, RealtimeMessage.Continue);
        fixture.Pulse(1);
        Assert.Equal(fixture.Original.Id, fixture.Player.CurrentPattern.Id);
        fixture.Pulse(1);
        Assert.Equal(candidate.Id, fixture.Player.CurrentPattern.Id);
        Assert.Equal(1UL, fixture.Engine.Snapshot.StartCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_new_queue_while_stopped_or_clock_lost_replaces_pending_material_immediately(bool clockLost)
    {
        var time = new ManualTimeProvider();
        using var fixture = new PlayerFixture(timeProvider: time);
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Pulse(20);
        fixture.Player.Queue(Pattern(62));
        if (clockLost)
        {
            time.Advance(TimeSpan.FromMilliseconds(500));
            Assert.True(fixture.Engine.CheckExternalClockLoss(TimeSpan.FromMilliseconds(500)));
        }
        else fixture.Engine.Process(ClockSource.External, RealtimeMessage.Stop);
        var latest = Pattern(64);

        fixture.Player.Queue(latest);

        Assert.Equal(latest.Id, fixture.Player.CurrentPattern!.Id);
        Assert.Null(fixture.Player.PendingPattern);
        Assert.DoesNotContain("On:1:62:100", fixture.NoteMessages);
    }

    [Theory]
    [InlineData("running")]
    [InlineData("stop")]
    [InlineData("clock-loss")]
    [InlineData("source")]
    public void Start_activates_pending_material_on_its_first_pulse_from_the_beginning(string operation)
    {
        var time = new ManualTimeProvider();
        using var fixture = new PlayerFixture(timeProvider: time);
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Pulse(300);
        var candidate = Pattern(60, 64, 6);
        fixture.Player.Queue(candidate);
        switch (operation)
        {
            case "stop": fixture.Engine.Process(ClockSource.External, RealtimeMessage.Stop); break;
            case "clock-loss":
                time.Advance(TimeSpan.FromMilliseconds(500));
                Assert.True(fixture.Engine.CheckExternalClockLoss(TimeSpan.FromMilliseconds(500)));
                break;
            case "source": fixture.Engine.SelectSource(ClockSource.Internal); break;
        }

        fixture.Engine.Process(fixture.Engine.Source, RealtimeMessage.Start);

        Assert.Equal(fixture.Original.Id, fixture.Player.CurrentPattern!.Id);
        Assert.Equal(candidate.Id, fixture.Player.PendingPattern!.Id);
        Assert.DoesNotContain("On:1:60:100", fixture.NoteMessages);
        fixture.Pulse(1);
        Assert.Equal(candidate.Id, fixture.Player.CurrentPattern.Id);
        Assert.Null(fixture.Player.PendingPattern);
        Assert.Equal(["Off:1:36:0", "On:1:60:100"], fixture.NoteMessages.TakeLast(2));
        Assert.Equal(2UL, fixture.Engine.Snapshot.StartCount);
    }

    [Fact]
    public void Repeated_start_before_any_new_pulses_still_activates_only_the_latest_candidate()
    {
        using var fixture = new PlayerFixture();
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Pulse(300);
        fixture.Player.Queue(Pattern(62));
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        var latest = Pattern(64);
        fixture.Player.Queue(latest);
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);

        fixture.Pulse(1);

        Assert.Equal(latest.Id, fixture.Player.CurrentPattern!.Id);
        Assert.Equal("On:1:64:100", fixture.NoteMessages.Last());
        Assert.DoesNotContain("On:1:62:100", fixture.NoteMessages);
    }

    [Fact]
    public void Restart_activates_pending_material_even_when_note_output_is_muted()
    {
        using var fixture = new PlayerFixture();
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Pulse(300);
        var candidate = Pattern(64);
        fixture.Player.Queue(candidate);
        fixture.Player.Mute();

        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Pulse(1);

        Assert.Equal(candidate.Id, fixture.Player.CurrentPattern!.Id);
        Assert.Null(fixture.Player.PendingPattern);
        Assert.DoesNotContain("On:1:64:100", fixture.NoteMessages);
        Assert.False(fixture.Player.IsEnabled);
    }

    [Fact]
    public async Task A_new_candidate_queued_after_restart_pulse_zero_was_accepted_waits_until_the_next_bar()
    {
        using var releasePulse = new ManualResetEventSlim();
        var pulseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var fixture = new PlayerFixture(engine => engine.PulseAccepted += position =>
        {
            if (position.Pulse != 0 || engine.Snapshot.StartCount != 2) return;
            pulseEntered.TrySetResult();
            if (!releasePulse.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The test did not release the restarted first pulse.");
        });
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Pulse(300);
        fixture.Player.Queue(Pattern(62));
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        var firstPulse = Task.Run(() => fixture.Pulse(1), TestContext.Current.CancellationToken);
        var latest = Pattern(64);
        try
        {
            await pulseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            fixture.Player.Queue(latest);
        }
        finally
        {
            releasePulse.Set();
            await firstPulse.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.Equal(fixture.Original.Id, fixture.Player.CurrentPattern!.Id);
        Assert.Equal(latest.Id, fixture.Player.PendingPattern!.Id);
        fixture.Pulse(96);
        Assert.Equal(latest.Id, fixture.Player.CurrentPattern.Id);
        Assert.DoesNotContain("On:1:62:100", fixture.NoteMessages);
    }

    private static Pattern Pattern(int firstNote, int stepCount = 1, int pulsesPerStep = 96) =>
        new(PatternId.New(), new PatternName("activation"), PatternSchemaVersion.Current, PatternMode.Drums,
            new PatternTiming(pulsesPerStep), Enumerable.Range(0, stepCount).Select(index =>
                new PatternStep([new PatternNote(new DrumPitch(new MidiValue(firstNote + index)),
                    new MidiValue(100), new NoteGate(1))], TriggerProbability.Always)), null, null);

    private sealed class PlayerFixture : IDisposable
    {
        private readonly FakeMidiOutputPort _port = new();
        private readonly SafeMidiOutput _output = new();
        public TransportEngine Engine { get; }
        public Pattern Original { get; } = Pattern(36);
        public PatternPlayer Player { get; }
        public IEnumerable<string> NoteMessages => _port.Messages
            .Where(message => message.StartsWith("On:") || message.StartsWith("Off:"));

        public PlayerFixture(Action<TransportEngine>? beforePlayer = null, TimeProvider? timeProvider = null)
        {
            Engine = new TransportEngine(timeProvider: timeProvider);
            _output.ReplacePort(_port);
            beforePlayer?.Invoke(Engine);
            Player = new PatternPlayer(_output, Engine);
            Player.Queue(Original);
            Player.Play();
        }

        public void Pulse(int count)
        {
            for (var index = 0; index < count; index++) Engine.Process(Engine.Source, RealtimeMessage.Clock);
        }

        public void Dispose()
        {
            try { Player.Dispose(); }
            finally { _output.Dispose(); }
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => _timestamp;
        public void Advance(TimeSpan duration) => _timestamp += duration.Ticks;
    }
}
