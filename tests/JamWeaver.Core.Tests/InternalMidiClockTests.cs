using System.Threading.Channels;
using JamWeaver.Core.Performance;
using JamWeaver.Core.Transport;

namespace JamWeaver.Core.Tests;

public sealed class InternalMidiClockTests
{
    [Fact]
    public async Task Restart_joins_the_previous_run_before_sending_start_and_a_late_failure_cannot_cancel_its_replacement()
    {
        await using var fixture = new ClockFixture();
        await fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        var previous = await fixture.Scheduler.NextRunAsync();

        var restart = fixture.Clock.StartAsync(TestContext.Current.CancellationToken);

        Assert.True(previous.Token.IsCancellationRequested);
        Assert.False(restart.IsCompleted);
        Assert.Equal(["Start"], fixture.Port.Messages);
        previous.Fail(new InvalidOperationException("late failure from previous run"));
        await restart.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var current = await fixture.Scheduler.NextRunAsync();
        Assert.Equal(["Start", "Start"], fixture.Port.Messages);
        Assert.False(current.Token.IsCancellationRequested);
        Assert.True(fixture.Clock.IsRunning);
        Assert.Null(fixture.Clock.Error);

        Assert.ThrowsAny<OperationCanceledException>(previous.Pulse);
        Assert.False(current.Token.IsCancellationRequested);
        Assert.Equal(["Start", "Start"], fixture.Port.Messages);
    }

    [Fact]
    public async Task Stop_joins_the_run_before_sending_stop()
    {
        await using var fixture = new ClockFixture();
        await fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        var run = await fixture.Scheduler.NextRunAsync();

        var stop = fixture.Clock.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(run.Token.IsCancellationRequested);
        Assert.False(stop.IsCompleted);
        Assert.Equal(["Start"], fixture.Port.Messages);
        run.Complete();
        await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal(["Start", "Stop"], fixture.Port.Messages);
        Assert.Equal(TransportState.Stopped, fixture.Engine.State);
        Assert.False(fixture.Clock.IsRunning);
        Assert.Null(fixture.Clock.Error);
    }

    [Fact]
    public async Task Continue_preserves_position_and_does_not_replace_a_running_scheduler()
    {
        await using var fixture = new ClockFixture();
        await fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        var first = await fixture.Scheduler.NextRunAsync();
        for (var pulse = 0; pulse < 7; pulse++) first.Pulse();
        await fixture.Clock.ContinueAsync(TestContext.Current.CancellationToken);
        Assert.False(first.Token.IsCancellationRequested);
        Assert.DoesNotContain("Continue", fixture.Port.Messages);

        var stop = fixture.Clock.StopAsync(TestContext.Current.CancellationToken);
        first.Complete();
        await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await fixture.Clock.ContinueAsync(TestContext.Current.CancellationToken);
        _ = await fixture.Scheduler.NextRunAsync();

        Assert.Equal(7UL, fixture.Engine.Position.Pulse);
        Assert.Equal("Continue", fixture.Port.Messages.Last());
        Assert.True(fixture.Clock.IsRunning);
    }

    [Fact]
    public async Task Concurrent_restart_and_stop_requests_keep_ownership_of_both_runs()
    {
        await using var fixture = new ClockFixture();
        await fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        var first = await fixture.Scheduler.NextRunAsync();
        var restart = fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        var stop = fixture.Clock.StopAsync(TestContext.Current.CancellationToken);
        Assert.False(restart.IsCompleted);
        Assert.False(stop.IsCompleted);

        first.Complete();
        await restart.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var second = await fixture.Scheduler.NextRunAsync();
        await second.WaitForCancellationAsync();
        Assert.False(stop.IsCompleted);
        Assert.Equal(["Start", "Start"], fixture.Port.Messages);
        second.Complete();
        await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(["Start", "Start", "Stop"], fixture.Port.Messages);
        Assert.False(fixture.Clock.IsRunning);
    }

    [Fact]
    public async Task Cancelling_a_waiting_request_does_not_cancel_the_running_operation()
    {
        await using var fixture = new ClockFixture();
        await fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        var first = await fixture.Scheduler.NextRunAsync();
        var restart = fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var waiting = fixture.Clock.StopAsync(cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        Assert.False(restart.IsCompleted);
        first.Complete();
        await restart.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        var second = await fixture.Scheduler.NextRunAsync();
        Assert.False(second.Token.IsCancellationRequested);
        Assert.True(fixture.Clock.IsRunning);
        Assert.DoesNotContain("Stop", fixture.Port.Messages);
    }

    [Fact]
    public async Task An_admitted_stop_finishes_joining_even_if_its_callers_token_is_cancelled()
    {
        await using var fixture = new ClockFixture();
        await fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        var run = await fixture.Scheduler.NextRunAsync();
        using var cancellation = new CancellationTokenSource();
        var stop = fixture.Clock.StopAsync(cancellation.Token);
        cancellation.Cancel();

        Assert.False(stop.IsCompleted);
        run.Complete();
        await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(fixture.Clock.IsRunning);
        Assert.Equal(TransportState.Stopped, fixture.Engine.State);
    }

    [Fact]
    public async Task A_background_failure_is_retained_and_releases_active_pattern_notes()
    {
        await using var fixture = new ClockFixture();
        using var player = new PatternPlayer(fixture.Output, fixture.Engine);
        player.Queue(new Pattern(PatternId.New(), new PatternName("clock failure"), PatternSchemaVersion.Current,
            PatternMode.Drums, new PatternTiming(96),
            [new PatternStep([new PatternNote(new DrumPitch(new MidiValue(60)), new MidiValue(100), new NoteGate(1))],
                TriggerProbability.Always)], null, null));
        player.Play();
        await fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        var run = await fixture.Scheduler.NextRunAsync();
        run.Pulse();
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Engine.StateChanged += state =>
        {
            if (state == TransportState.Stopped) stopped.TrySetResult();
        };
        var failure = new InvalidOperationException("scheduler failed");
        run.Fail(failure);

        await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal(TransportState.Stopped, fixture.Engine.State);
        Assert.Contains("clock-test", fixture.Clock.Error!.Message);
        Assert.Same(failure, fixture.Clock.Error.InnerException);
        Assert.Equal(["On:1:60:100", "Off:1:60:0"], fixture.Port.Messages
            .Where(message => message.StartsWith("On:") || message.StartsWith("Off:")));

        await fixture.Clock.StopAsync(TestContext.Current.CancellationToken);
        Assert.False(fixture.Clock.IsRunning);

        await fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        Assert.Null(fixture.Clock.Error);
        Assert.True(fixture.Clock.IsRunning);
    }

    [Theory]
    [InlineData("throw")]
    [InlineData("unexpected-return")]
    [InlineData("unrequested-cancellation")]
    public async Task Synchronous_scheduler_failures_are_visible_before_disposal(string behavior)
    {
        using var output = new SafeMidiOutput();
        output.ReplacePort(new FakeMidiOutputPort("clock-test"));
        var engine = new TransportEngine(ClockSource.Internal);
        await using var clock = new InternalMidiClock(output, engine, new FailingScheduler(behavior));

        await clock.StartAsync(TestContext.Current.CancellationToken);

        Assert.False(clock.IsRunning);
        Assert.Equal(TransportState.Stopped, engine.State);
        Assert.NotNull(clock.Error);
        Assert.Contains("clock-test", clock.Error.Message);
        await clock.DisposeAsync();
        Assert.NotNull(clock.Error);
    }

    [Fact]
    public async Task Dispose_joins_the_run_and_repeated_disposal_is_safe()
    {
        await using var fixture = new ClockFixture();
        await fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        var run = await fixture.Scheduler.NextRunAsync();
        var disposal = fixture.Clock.DisposeAsync().AsTask();

        Assert.True(run.Token.IsCancellationRequested);
        Assert.False(disposal.IsCompleted);
        Assert.Equal(0, fixture.Port.StopAttempts);
        run.Complete();
        await disposal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await fixture.Clock.DisposeAsync();

        Assert.False(fixture.Clock.IsRunning);
        Assert.Equal(TransportState.Stopped, fixture.Engine.State);
        Assert.Null(fixture.Clock.Error);
        Assert.Equal(1, fixture.Port.StopAttempts);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Clock.StartAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Clock.ContinueAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => fixture.Clock.StopAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_clock_send_failure_is_retained_with_port_context_and_does_not_escape_disposal()
    {
        var failure = new InvalidOperationException("clock send failed");
        using var output = new SafeMidiOutput();
        output.ReplacePort(new FakeMidiOutputPort("failed-clock-port") { ClockFailure = failure });
        var engine = new TransportEngine(ClockSource.Internal);
        await using var clock = new InternalMidiClock(output, engine, new OnePulseScheduler());

        await clock.StartAsync(TestContext.Current.CancellationToken);

        Assert.False(clock.IsRunning);
        Assert.Equal(TransportState.Stopped, engine.State);
        Assert.Contains("failed-clock-port", clock.Error!.Message);
        Assert.Same(failure, clock.Error.InnerException);
        await clock.DisposeAsync();
        Assert.Same(failure, clock.Error.InnerException);
    }

    [Fact]
    public async Task A_stop_send_failure_still_leaves_the_run_joined_and_local_transport_stopped()
    {
        await using var fixture = new ClockFixture();
        var failure = new InvalidOperationException("stop send failed");
        fixture.Port.StopFailure = failure;
        await fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        var run = await fixture.Scheduler.NextRunAsync();

        var stop = fixture.Clock.StopAsync(TestContext.Current.CancellationToken);
        run.Complete();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => stop);

        Assert.Same(failure, error);
        Assert.True(run.Token.IsCancellationRequested);
        Assert.False(fixture.Clock.IsRunning);
        Assert.Equal(TransportState.Stopped, fixture.Engine.State);
        fixture.Port.StopFailure = null;
    }

    [Fact]
    public async Task Stop_does_not_hide_an_unrelated_cancellation_failure()
    {
        await using var fixture = new ClockFixture();
        await fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        var run = await fixture.Scheduler.NextRunAsync();
        var failure = new OperationCanceledException("unrelated cancellation");

        var stop = fixture.Clock.StopAsync(TestContext.Current.CancellationToken);
        run.Fail(failure);
        await stop.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Same(failure, fixture.Clock.Error!.InnerException);
        Assert.False(fixture.Clock.IsRunning);
    }

    [Fact]
    public async Task Restart_does_not_send_start_if_source_changed_while_joining_the_previous_run()
    {
        await using var fixture = new ClockFixture();
        await fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        var run = await fixture.Scheduler.NextRunAsync();

        var restart = fixture.Clock.StartAsync(TestContext.Current.CancellationToken);
        fixture.Engine.SelectSource(ClockSource.External);
        run.Complete();
        await Assert.ThrowsAsync<InvalidOperationException>(() => restart);

        Assert.False(fixture.Clock.IsRunning);
        Assert.Equal(ClockSource.External, fixture.Engine.Source);
        Assert.Equal(TransportState.Stopped, fixture.Engine.State);
        Assert.Equal(["Start"], fixture.Port.Messages);
    }

    private sealed class ClockFixture : IAsyncDisposable
    {
        public FakeMidiOutputPort Port { get; } = new("clock-test");
        public SafeMidiOutput Output { get; } = new();
        public TransportEngine Engine { get; } = new(ClockSource.Internal);
        public ControlledScheduler Scheduler { get; } = new();
        public InternalMidiClock Clock { get; }

        public ClockFixture()
        {
            Output.ReplacePort(Port);
            Clock = new InternalMidiClock(Output, Engine, Scheduler);
        }

        public async ValueTask DisposeAsync()
        {
            var disposal = Clock.DisposeAsync();
            Scheduler.ReleaseAll();
            try { await disposal; }
            finally { Output.Dispose(); }
        }
    }

    private sealed class ControlledScheduler : IClockPulseScheduler
    {
        private readonly Channel<ClockRun> _runs = Channel.CreateUnbounded<ClockRun>();
        private readonly List<ClockRun> _allRuns = [];
        private readonly object _sync = new();
        private bool _releaseAll;

        public Task RunAsync(Func<double> bpm, Action pulse, CancellationToken cancellationToken)
        {
            var run = new ClockRun(pulse, cancellationToken);
            lock (_sync)
            {
                _allRuns.Add(run);
                _runs.Writer.TryWrite(run);
                if (_releaseAll) run.Complete();
            }
            return run.Completion.Task;
        }

        public async Task<ClockRun> NextRunAsync() => await _runs.Reader
            .ReadAsync(TestContext.Current.CancellationToken).AsTask()
            .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        public void ReleaseAll()
        {
            lock (_sync)
            {
                _releaseAll = true;
                foreach (var run in _allRuns) run.Complete();
            }
        }
    }

    private sealed class ClockRun(Action pulse, CancellationToken token)
    {
        public CancellationToken Token { get; } = token;
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Pulse() => pulse();
        public void Complete() => Completion.TrySetResult();
        public void Fail(Exception error) => Completion.TrySetException(error);

        public async Task WaitForCancellationAsync()
        {
            var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = Token.Register(() => cancelled.TrySetResult());
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }
    }

    private sealed class FailingScheduler(string behavior) : IClockPulseScheduler
    {
        public Task RunAsync(Func<double> bpm, Action pulse, CancellationToken cancellationToken) => behavior switch
        {
            "throw" => throw new InvalidOperationException("synchronous scheduler failure"),
            "unexpected-return" => Task.CompletedTask,
            "unrequested-cancellation" => Task.FromException(new OperationCanceledException("unrequested cancellation")),
            _ => throw new ArgumentOutOfRangeException(nameof(behavior))
        };
    }

    private sealed class OnePulseScheduler : IClockPulseScheduler
    {
        public Task RunAsync(Func<double> bpm, Action pulse, CancellationToken cancellationToken)
        {
            pulse();
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
