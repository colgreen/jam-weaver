using JamWeaver.Core.Performance;
using JamWeaver.Core.Transport;

namespace JamWeaver.Core.Tests;

public sealed class TransportConcurrencyTests
{
    [Theory]
    [InlineData("stop")]
    [InlineData("source")]
    [InlineData("clock-loss")]
    public async Task Leaving_running_state_waits_for_an_in_flight_pulse_before_releasing_notes(string operation)
    {
        var port = new FakeMidiOutputPort();
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        var time = new ManualTimeProvider();
        var engine = new TransportEngine(ClockSource.External, time);
        using var releasePulse = new ManualResetEventSlim();
        var pulseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.PulseAccepted += _ =>
        {
            pulseEntered.TrySetResult();
            if (!releasePulse.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The test did not release the in-flight pulse.");
        };
        using var player = new PatternPlayer(output, engine);
        player.Queue(TestPattern());
        player.Play();
        engine.Process(ClockSource.External, RealtimeMessage.Start);
        var pulseTask = Task.Run(() => engine.Process(ClockSource.External, RealtimeMessage.Clock));
        Task? transitionTask = null;
        try
        {
            await pulseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            time.Advance(TimeSpan.FromMilliseconds(500));
            var transitionEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            transitionTask = Task.Run(() =>
            {
                transitionEntered.TrySetResult();
                switch (operation)
                {
                    case "stop": engine.Process(ClockSource.External, RealtimeMessage.Stop); break;
                    case "source": engine.SelectSource(ClockSource.Internal); break;
                    case "clock-loss": engine.CheckExternalClockLoss(TimeSpan.FromMilliseconds(500)); break;
                }
            }, TestContext.Current.CancellationToken);
            await transitionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            // Give the competing operation an opportunity to expose the original race.
            await Task.WhenAny(transitionTask, Task.Delay(100, TestContext.Current.CancellationToken));
        }
        finally
        {
            releasePulse.Set();
            await pulseTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (transitionTask is not null)
                await transitionTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.Equal(operation == "clock-loss" ? TransportState.ClockLost : TransportState.Stopped, engine.State);
        Assert.Equal(["On:1:60:100", "Off:1:60:0"],
            port.Messages.Where(message => message.StartsWith("On:") || message.StartsWith("Off:")));
        Assert.False(engine.Process(ClockSource.External, RealtimeMessage.Clock));
        Assert.Null(player.Error);
    }

    [Fact]
    public async Task State_queries_do_not_wait_for_a_blocked_subscriber()
    {
        var engine = new TransportEngine();
        using var releasePulse = new ManualResetEventSlim();
        var pulseEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.PulseAccepted += _ =>
        {
            pulseEntered.TrySetResult();
            if (!releasePulse.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The test did not release the in-flight pulse.");
        };
        engine.Process(ClockSource.External, RealtimeMessage.Start);
        var pulseTask = Task.Run(() => engine.Process(ClockSource.External, RealtimeMessage.Clock));
        try
        {
            await pulseEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var snapshot = await Task.Run(() => engine.Snapshot)
                .WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.Equal(ClockSource.External, snapshot.Source);
            Assert.Equal(TransportState.Running, snapshot.State);
            Assert.Equal(1UL, snapshot.Position.Pulse);
            Assert.Equal(1UL, snapshot.StartCount);
        }
        finally
        {
            releasePulse.Set();
            await pulseTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData("stop")]
    [InlineData("source")]
    [InlineData("clock-loss")]
    public void Subscribers_cannot_reorder_notifications_by_changing_transport_reentrantly(string operation)
    {
        var engine = new TransportEngine();
        Exception? error = null;
        engine.PulseAccepted += _ => error = Record.Exception(() =>
        {
            switch (operation)
            {
                case "stop": engine.Process(ClockSource.External, RealtimeMessage.Stop); break;
                case "source": engine.SelectSource(ClockSource.Internal); break;
                case "clock-loss": engine.CheckExternalClockLoss(TimeSpan.FromMilliseconds(500)); break;
            }
        });
        engine.Process(ClockSource.External, RealtimeMessage.Start);

        engine.Process(ClockSource.External, RealtimeMessage.Clock);

        Assert.IsType<InvalidOperationException>(error);
        Assert.Contains("notification callback", error.Message);
        Assert.Equal(TransportState.Running, engine.State);
        Assert.Equal(ClockSource.External, engine.Source);
        Assert.Equal(1UL, engine.Position.Pulse);
        engine.Process(ClockSource.External, RealtimeMessage.Stop);
        Assert.Equal(TransportState.Stopped, engine.State);
    }

    [Fact]
    public async Task Subscriber_failure_does_not_prevent_later_transport_changes()
    {
        var engine = new TransportEngine();
        void Fail(TransportPosition _) => throw new InvalidOperationException("subscriber failed");
        engine.PulseAccepted += Fail;
        engine.Process(ClockSource.External, RealtimeMessage.Start);
        Assert.Throws<InvalidOperationException>(() => engine.Process(ClockSource.External, RealtimeMessage.Clock));
        engine.PulseAccepted -= Fail;

        await Task.Run(() => engine.Process(ClockSource.External, RealtimeMessage.Stop))
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(TransportState.Stopped, engine.State);
    }

    private static Pattern TestPattern() => new(PatternId.New(), new PatternName("concurrent stop"),
        PatternSchemaVersion.Current, PatternMode.Drums, new PatternTiming(96),
        [new PatternStep([new PatternNote(new DrumPitch(new MidiValue(60)), new MidiValue(100),
            new NoteGate(1))], TriggerProbability.Always)], null, null);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref _timestamp);
        public void Advance(TimeSpan duration) => Interlocked.Add(ref _timestamp, duration.Ticks);
    }
}
