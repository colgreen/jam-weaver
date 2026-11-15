using JamWeaver.Core.Transport;

namespace JamWeaver.Core.Tests;

public sealed class InternalClockResponsivenessTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Default_scheduler_yields_to_its_caller_during_sustained_overrun()
    {
        using var cancellation = new CancellationTokenSource();
        var fourPulses = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pulses = 0;
        // A separate caller lets cleanup cancel the reproduction even if RunAsync never returns its task.
        var invocation = Task.Factory.StartNew(() => new StopwatchClockPulseScheduler().RunAsync(() => 300, () =>
        {
            Thread.Sleep(15);
            if (Interlocked.Increment(ref pulses) == 4) fourPulses.TrySetResult();
        }, cancellation.Token), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        try
        {
            await fourPulses.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            _ = await invocation.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        }
        finally
        {
            cancellation.Cancel();
            var run = await invocation.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Default_clock_startup_and_stop_complete_while_output_remains_slow(bool resume)
    {
        var fourPulses = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pulses = 0;
        var slow = 1;
        var port = new FakeMidiOutputPort
        {
            ClockAction = () =>
            {
                if (Volatile.Read(ref slow) == 1) Thread.Sleep(15);
                if (Interlocked.Increment(ref pulses) == 4) fourPulses.TrySetResult();
            }
        };
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        var engine = new TransportEngine(ClockSource.Internal);
        await using var clock = new InternalMidiClock(output, engine) { Bpm = 300 };
        var startup = Task.Factory.StartNew(() => resume ? clock.ContinueAsync() : clock.StartAsync(),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        try
        {
            await fourPulses.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await startup.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await clock.StopAsync(TestContext.Current.CancellationToken).WaitAsync(Timeout, TestContext.Current.CancellationToken);

            Assert.False(clock.IsRunning);
            Assert.Null(clock.Error);
            Assert.Equal(TransportState.Stopped, engine.State);
            Assert.Equal(resume ? "Continue" : "Start", port.Messages[0]);
            Assert.Equal("Stop", port.Messages.Last());
            Assert.Equal((ulong)pulses, engine.Position.Pulse);
            Assert.Equal(pulses, port.Messages.Count(message => message == "Clock"));
        }
        finally
        {
            // Release the old inline-start implementation on assertion failure, without hanging disposal.
            Volatile.Write(ref slow, 0);
            await startup.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Inline_scheduler_cannot_hold_startup_and_disposal_joins_its_callbacks(bool resume)
    {
        using var scheduler = new InlineScheduler();
        var port = new FakeMidiOutputPort();
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        var engine = new TransportEngine(ClockSource.Internal);
        await using var clock = new InternalMidiClock(output, engine, scheduler);
        var startup = Task.Factory.StartNew(() => resume ? clock.ContinueAsync() : clock.StartAsync(),
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
        try
        {
            await scheduler.Entered.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            await startup.WaitAsync(Timeout, TestContext.Current.CancellationToken);
            var disposal = clock.DisposeAsync().AsTask();
            await scheduler.Cancelled.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            Assert.False(disposal.IsCompleted);
            Assert.Equal([resume ? "Continue" : "Start", "Clock"], port.Messages);
            scheduler.Release.Set();
            await disposal.WaitAsync(Timeout, TestContext.Current.CancellationToken);

            Assert.False(clock.IsRunning);
            Assert.Null(clock.Error);
            Assert.Equal(TransportState.Stopped, engine.State);
            Assert.ThrowsAny<OperationCanceledException>(scheduler.Pulse!);
            Assert.Equal([resume ? "Continue" : "Start", "Clock", "Stop"], port.Messages);
        }
        finally
        {
            scheduler.Release.Set();
            await startup.WaitAsync(Timeout, TestContext.Current.CancellationToken);
        }
    }

    private sealed class InlineScheduler : IClockPulseScheduler, IDisposable
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        public Action? Pulse { get; private set; }

        public Task RunAsync(Func<double> bpm, Action pulse, CancellationToken cancellationToken)
        {
            Pulse = pulse;
            pulse();
            using var registration = cancellationToken.Register(() => Cancelled.TrySetResult());
            Entered.TrySetResult();
            Release.Wait();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }

        public void Dispose() => Release.Dispose();
    }
}
