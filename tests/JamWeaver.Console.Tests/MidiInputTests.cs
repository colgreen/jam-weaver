using JamWeaver.Core.Transport;

namespace JamWeaver.Console.Tests;

public sealed class MidiInputTests
{
    [Fact]
    public async Task Replacement_waits_for_an_in_flight_forward_before_closing_or_starting_ports()
    {
        var clock = new ExternalMidiClockInput();
        using var releaseMessage = new ManualResetEventSlim();
        var messageEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        clock.MessageReceived += (_, _) =>
        {
            messageEntered.TrySetResult();
            if (!releaseMessage.Wait(TimeSpan.FromSeconds(5)))
                throw new TimeoutException("The test did not release the in-flight input message.");
        };
        using var input = new MidiInput(clock);
        var operations = new List<string>();
        var previous = new FakeInputPort("previous", operations);
        var proposed = new FakeInputPort("proposed", operations);
        input.ReplacePort(previous);
        operations.Clear();
        var forwarding = Task.Run(() => previous.Send(RealtimeMessage.Clock), TestContext.Current.CancellationToken);
        Task? replacement = null;
        try
        {
            await messageEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var replacementEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            replacement = Task.Run(() =>
            {
                replacementEntered.TrySetResult();
                input.ReplacePort(proposed);
            }, TestContext.Current.CancellationToken);
            await replacementEntered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            await Task.WhenAny(replacement, Task.Delay(100, TestContext.Current.CancellationToken));
            Assert.False(replacement.IsCompleted);
            Assert.Equal("previous", input.Name);
            Assert.Empty(operations);
        }
        finally
        {
            releaseMessage.Set();
            await forwarding.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            if (replacement is not null)
                await replacement.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        Assert.Equal(["previous:Detach", "previous:Stop", "previous:Dispose", "proposed:Attach", "proposed:Start"], operations);
        Assert.Equal("proposed", input.Name);
    }

    [Fact]
    public void Replacement_closes_the_old_listener_before_starting_the_new_one_and_ignores_late_callbacks()
    {
        var clock = new ExternalMidiClockInput();
        var received = new List<RealtimeMessage>();
        clock.MessageReceived += (_, message) => received.Add(message);
        using var input = new MidiInput(clock);
        var operations = new List<string>();
        var first = new FakeInputPort("first", operations);
        var second = new FakeInputPort("second", operations);
        input.ReplacePort(first);
        first.Send(RealtimeMessage.Start);
        var lateCallback = first.CaptureReceiver();
        operations.Clear();

        input.ReplacePort(second);
        lateCallback(RealtimeMessage.Clock);
        second.Send(RealtimeMessage.Continue);

        Assert.Equal(["first:Detach", "first:Stop", "first:Dispose", "second:Attach", "second:Start"], operations);
        Assert.Equal([RealtimeMessage.Start, RealtimeMessage.Continue], received);
        Assert.True(first.IsDisposed);
        Assert.Equal("second", input.Name);
        Assert.True(second.IsListening);
    }

    [Theory]
    [InlineData("Attach")]
    [InlineData("Start")]
    public void Startup_failure_detaches_stops_and_disposes_the_proposed_port(string operation)
    {
        using var input = new MidiInput(new ExternalMidiClockInput());
        var operations = new List<string>();
        var port = new FakeInputPort("proposed", operations);
        var failure = new InvalidOperationException("startup failed");
        port.Failures[operation] = failure;

        var error = Assert.Throws<InvalidOperationException>(() => input.ReplacePort(port));

        Assert.Same(failure, error.InnerException);
        Assert.Contains("proposed", error.Message);
        Assert.Null(input.Name);
        Assert.Equal(["proposed:Detach", "proposed:Stop", "proposed:Dispose"], operations.TakeLast(3));
        Assert.True(port.IsDisposed);
        Assert.False(port.IsListening);
    }

    [Theory]
    [InlineData("Detach")]
    [InlineData("Stop")]
    [InlineData("Dispose")]
    public void Failure_closing_the_previous_input_clears_selection_and_disposes_the_unstarted_proposal(string failedOperation)
    {
        using var input = new MidiInput(new ExternalMidiClockInput());
        var operations = new List<string>();
        var previous = new FakeInputPort("previous", operations);
        var proposed = new FakeInputPort("proposed", operations);
        input.ReplacePort(previous);
        previous.Failures[failedOperation] = new InvalidOperationException("close failed");
        operations.Clear();

        var error = Assert.Throws<AggregateException>(() => input.ReplacePort(proposed));

        Assert.Contains("previous", error.Flatten().InnerExceptions.Single().Message);
        Assert.Equal(["previous:Detach", "previous:Stop", "previous:Dispose", "proposed:Dispose"], operations);
        Assert.Null(input.Name);
        Assert.True(previous.IsDisposed);
        Assert.True(proposed.IsDisposed);
        Assert.False(proposed.IsListening);
    }

    [Fact]
    public void Startup_and_cleanup_failures_are_all_preserved()
    {
        using var input = new MidiInput(new ExternalMidiClockInput());
        var port = new FakeInputPort("proposed", []);
        port.Failures["Start"] = new InvalidOperationException("start failed");
        port.Failures["Stop"] = new InvalidOperationException("stop failed");
        port.Failures["Dispose"] = new InvalidOperationException("dispose failed");

        var error = Assert.Throws<AggregateException>(() => input.ReplacePort(port));

        Assert.Equal(3, error.Flatten().InnerExceptions.Count);
        Assert.Contains(error.Flatten().InnerExceptions, exception => exception.Message.Contains("start listening"));
        Assert.Contains(error.Flatten().InnerExceptions, exception => exception.Message.Contains("stop listening"));
        Assert.Contains(error.Flatten().InnerExceptions, exception => exception.Message.Contains("dispose MIDI input"));
        Assert.Null(input.Name);
        Assert.True(port.IsDisposed);
    }

    [Fact]
    public void Previous_input_and_rejected_proposal_disposal_failures_are_all_preserved()
    {
        using var input = new MidiInput(new ExternalMidiClockInput());
        var previous = new FakeInputPort("previous", []);
        var proposed = new FakeInputPort("proposed", []);
        input.ReplacePort(previous);
        previous.Failures["Stop"] = new InvalidOperationException("stop failed");
        previous.Failures["Dispose"] = new InvalidOperationException("dispose failed");
        proposed.Failures["Dispose"] = new InvalidOperationException("proposed dispose failed");

        var error = Assert.Throws<AggregateException>(() => input.ReplacePort(proposed));

        Assert.Equal(3, error.Flatten().InnerExceptions.Count);
        Assert.Contains(error.Flatten().InnerExceptions, exception => exception.Message.Contains("dispose proposed MIDI input"));
        Assert.Null(input.Name);
        Assert.True(previous.IsDisposed);
        Assert.True(proposed.IsDisposed);
    }

    [Fact]
    public void Disposal_attempts_all_stages_and_is_idempotent_after_failures()
    {
        var clock = new ExternalMidiClockInput();
        var received = new List<RealtimeMessage>();
        clock.MessageReceived += (_, message) => received.Add(message);
        using var input = new MidiInput(clock);
        var operations = new List<string>();
        var port = new FakeInputPort("selected", operations);
        input.ReplacePort(port);
        var lateCallback = port.CaptureReceiver();
        foreach (var operation in new[] { "Detach", "Stop", "Dispose" })
            port.Failures[operation] = new InvalidOperationException($"{operation} failed");
        operations.Clear();

        var error = Assert.Throws<AggregateException>(input.Dispose);
        input.Dispose();
        lateCallback(RealtimeMessage.Start);

        Assert.Equal(3, error.Flatten().InnerExceptions.Count);
        Assert.Equal(["selected:Detach", "selected:Stop", "selected:Dispose"], operations);
        Assert.Null(input.Name);
        Assert.True(port.IsDisposed);
        Assert.Empty(received);
    }

    [Fact]
    public void A_disposed_owner_disposes_a_proposed_port()
    {
        using var input = new MidiInput(new ExternalMidiClockInput());
        input.Dispose();
        var port = new FakeInputPort("proposed", []);

        Assert.Throws<ObjectDisposedException>(() => input.ReplacePort(port));

        Assert.True(port.IsDisposed);
        Assert.False(port.IsListening);
    }

    [Fact]
    public void Name_failure_disposes_the_proposal_and_preserves_the_active_listener()
    {
        using var input = new MidiInput(new ExternalMidiClockInput());
        var operations = new List<string>();
        var selected = new FakeInputPort("selected", operations);
        input.ReplacePort(selected);
        operations.Clear();
        var proposed = new FakeInputPort("proposed", operations) { NameFailure = new InvalidOperationException("name unavailable") };

        Assert.Throws<InvalidOperationException>(() => input.ReplacePort(proposed));

        Assert.Equal(["proposed:Dispose"], operations);
        Assert.Equal("selected", input.Name);
        Assert.True(selected.IsListening);
        Assert.False(selected.IsDisposed);
        Assert.True(proposed.IsDisposed);
    }

    [Fact]
    public void Selecting_the_same_instance_does_not_restart_the_listener()
    {
        using var input = new MidiInput(new ExternalMidiClockInput());
        var operations = new List<string>();
        var port = new FakeInputPort("selected", operations);
        input.ReplacePort(port);
        operations.Clear();
        port.NameFailure = new InvalidOperationException("native name unavailable");

        input.ReplacePort(port);

        Assert.Empty(operations);
        Assert.Equal("selected", input.Name);
        Assert.True(port.IsListening);
    }

    private sealed class FakeInputPort(string name, List<string> operations) : IMidiInputPort
    {
        private EventHandler<RealtimeMessage>? _receiver;
        public string Name => NameFailure is { } error ? throw error : name;
        public Exception? NameFailure { get; set; }
        public Dictionary<string, Exception> Failures { get; } = [];
        public bool IsListening { get; private set; }
        public bool IsDisposed { get; private set; }
        public event EventHandler<RealtimeMessage>? MessageReceived
        {
            add { _receiver += value; Record("Attach"); }
            remove { _receiver -= value; Record("Detach"); }
        }
        public void StartListening()
        {
            IsListening = true;
            Record("Start");
        }
        public void StopListening()
        {
            Record("Stop");
            IsListening = false;
        }
        public void Dispose()
        {
            try { Record("Dispose"); }
            finally { IsDisposed = true; IsListening = false; }
        }
        public void Send(RealtimeMessage message) => _receiver?.Invoke(this, message);
        public Action<RealtimeMessage> CaptureReceiver()
        {
            var receiver = _receiver;
            return message => receiver?.Invoke(this, message);
        }
        private void Record(string operation)
        {
            operations.Add($"{name}:{operation}");
            if (Failures.TryGetValue(operation, out var error)) throw error;
        }
    }
}
