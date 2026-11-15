using JamWeaver.Core.Transport;

namespace JamWeaver.Core.Tests;

public sealed class InternalClockShutdownTests
{
    [Theory]
    [InlineData("start")]
    [InlineData("continue")]
    public async Task Disposal_sends_stop_before_output_disposal_and_no_later_pulse_can_be_sent(string operation)
    {
        var port = new FakeMidiOutputPort();
        var output = new SafeMidiOutput();
        var engine = new TransportEngine(ClockSource.Internal);
        var scheduler = new CancellationScheduler();
        var clock = new InternalMidiClock(output, engine, scheduler);
        using (output)
        {
            output.ReplacePort(port);
            await using (clock)
            {
                if (operation == "start") await clock.StartAsync(TestContext.Current.CancellationToken);
                else await clock.ContinueAsync(TestContext.Current.CancellationToken);
            }

            Assert.False(port.IsDisposed);
            Assert.Equal([operation == "start" ? "Start" : "Continue", "Clock", "Stop"], port.Messages);
            Assert.False(clock.IsRunning);
            Assert.Equal(TransportState.Stopped, engine.State);
            Assert.ThrowsAny<OperationCanceledException>(scheduler.Pulse!);
            Assert.Equal("Stop", port.Messages.Last());
            await clock.DisposeAsync();
            Assert.Equal(1, port.StopAttempts);
        }

        Assert.True(port.IsDisposed);
        Assert.DoesNotContain("Clock", port.Messages.SkipWhile(message => message != "Stop").Skip(1));
    }

    [Theory]
    [InlineData(ClockSource.Internal)]
    [InlineData(ClockSource.External)]
    public async Task An_unused_clock_sends_no_stop_including_when_following_external_transport(ClockSource source)
    {
        var port = new FakeMidiOutputPort();
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        var engine = new TransportEngine(source);
        if (source == ClockSource.External) engine.Process(ClockSource.External, RealtimeMessage.Start);
        await using var clock = new InternalMidiClock(output, engine);

        await clock.DisposeAsync();

        Assert.Empty(port.Messages);
        Assert.Equal(0, port.StopAttempts);
        Assert.Equal(source == ClockSource.External ? TransportState.Running : TransportState.Stopped, engine.State);
    }

    [Fact]
    public async Task Disposal_does_not_repeat_a_successful_explicit_stop()
    {
        var port = new FakeMidiOutputPort();
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        await using var clock = new InternalMidiClock(output, new TransportEngine(ClockSource.Internal), new CancellationScheduler());
        await clock.StartAsync(TestContext.Current.CancellationToken);

        await clock.StopAsync(TestContext.Current.CancellationToken);
        await clock.DisposeAsync();

        Assert.Equal(1, port.StopAttempts);
    }

    [Fact]
    public async Task A_background_failure_still_leaves_a_stop_due_at_disposal()
    {
        var port = new FakeMidiOutputPort { ClockFailure = new InvalidOperationException("clock failed") };
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        await using var clock = new InternalMidiClock(output, new TransportEngine(ClockSource.Internal), new CancellationScheduler());
        await clock.StartAsync(TestContext.Current.CancellationToken);
        var originalError = clock.Error;
        Assert.NotNull(originalError);
        Assert.False(clock.IsRunning);

        await clock.DisposeAsync();

        Assert.Equal(["Start", "Stop"], port.Messages);
        Assert.Same(originalError, clock.Error);
    }

    [Fact]
    public async Task Disposal_retries_a_failed_explicit_stop()
    {
        var port = new FakeMidiOutputPort();
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        await using var clock = new InternalMidiClock(output, new TransportEngine(ClockSource.Internal), new CancellationScheduler());
        await clock.StartAsync(TestContext.Current.CancellationToken);
        port.StopFailure = new InvalidOperationException("temporary stop failure");
        await Assert.ThrowsAsync<InvalidOperationException>(() => clock.StopAsync(TestContext.Current.CancellationToken));
        port.StopFailure = null;

        await clock.DisposeAsync();

        Assert.Equal(2, port.StopAttempts);
        Assert.Equal("Stop", port.Messages.Last());
    }

    [Fact]
    public async Task Stop_failure_during_scope_exit_still_allows_note_cleanup_and_port_disposal()
    {
        var failure = new InvalidOperationException("shutdown stop send failed");
        var port = new FakeMidiOutputPort("shutdown-port") { StopFailure = failure };
        var output = new SafeMidiOutput();
        var engine = new TransportEngine(ClockSource.Internal);
        var clock = new InternalMidiClock(output, engine, new CancellationScheduler());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using (output)
            {
                output.ReplacePort(port);
                await using (clock)
                {
                    await clock.StartAsync(TestContext.Current.CancellationToken);
                    output.NoteOn(new MidiChannel(1), new MidiValue(60), new MidiValue(100));
                }
            }
        });

        Assert.Same(failure, error.InnerException);
        Assert.Contains("shutdown-port", error.Message);
        Assert.Contains("MIDI Stop", error.Message);
        Assert.Same(error, clock.Error);
        Assert.False(clock.IsRunning);
        Assert.Equal(TransportState.Stopped, engine.State);
        Assert.Contains("Off:1:60:0", port.Messages);
        Assert.Equal(16, port.Messages.Count(message => message.StartsWith("CC:")));
        Assert.True(port.IsDisposed);
        await clock.DisposeAsync();
        Assert.Equal(1, port.StopAttempts);
    }

    [Fact]
    public async Task A_failed_start_send_does_not_leave_a_stop_due()
    {
        var port = new FakeMidiOutputPort { StartFailure = new InvalidOperationException("start send failed") };
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        await using var clock = new InternalMidiClock(output, new TransportEngine(ClockSource.Internal));
        await Assert.ThrowsAsync<InvalidOperationException>(() => clock.StartAsync(TestContext.Current.CancellationToken));

        await clock.DisposeAsync();

        Assert.Empty(port.Messages);
        Assert.Equal(0, port.StopAttempts);
    }

    [Fact]
    public async Task A_successful_start_send_followed_by_local_start_failure_still_leaves_a_stop_due()
    {
        var port = new FakeMidiOutputPort();
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        var engine = new TransportEngine(ClockSource.Internal);
        engine.StateChanged += state =>
        {
            if (state == TransportState.Running) throw new InvalidOperationException("local start failed");
        };
        await using var clock = new InternalMidiClock(output, engine);
        await Assert.ThrowsAsync<InvalidOperationException>(() => clock.StartAsync(TestContext.Current.CancellationToken));

        await clock.DisposeAsync();

        Assert.Equal(["Start", "Stop"], port.Messages);
        Assert.Equal(TransportState.Stopped, engine.State);
    }

    private sealed class CancellationScheduler : IClockPulseScheduler
    {
        public Action? Pulse { get; private set; }
        public Task RunAsync(Func<double> bpm, Action pulse, CancellationToken cancellationToken)
        {
            Pulse = pulse;
            pulse();
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
