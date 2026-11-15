namespace JamWeaver.Core.Tests;

public sealed class MidiOutputOwnershipTests
{
    [Theory]
    [InlineData("Off:1:60")]
    [InlineData("Off:1:64")]
    [InlineData("Off:2:67")]
    [InlineData("CC:1")]
    [InlineData("CC:8")]
    [InlineData("CC:16")]
    public void Panic_attempts_every_note_and_channel_after_any_send_failure(string failedOperation)
    {
        using var fixture = new OutputFixture();
        fixture.StartNotes();
        fixture.Port.Failure = operation => operation == failedOperation ? new InvalidOperationException("send failed") : null;

        var error = Assert.Throws<AggregateException>(fixture.Output.Panic);

        Assert.Equal(3, fixture.Port.Attempts.Count(operation => operation.StartsWith("Off:")));
        Assert.Equal(16, fixture.Port.Attempts.Count(operation => operation.StartsWith("CC:")));
        Assert.Single(error.InnerExceptions);
        Assert.Contains("selected-output", error.InnerExceptions[0].Message);
        Assert.Contains(failedOperation.StartsWith("Off:") ? "Note Off" : "CC 123", error.InnerExceptions[0].Message);
    }

    [Fact]
    public void Panic_retries_failed_note_offs_without_repeating_successful_ones()
    {
        using var fixture = new OutputFixture();
        fixture.StartNotes();
        fixture.Port.Failure = operation => operation == "Off:1:60" ? new InvalidOperationException("send failed") : null;
        Assert.Throws<AggregateException>(fixture.Output.Panic);
        fixture.Port.Failure = null;
        fixture.Port.Attempts.Clear();

        fixture.Output.Panic();

        Assert.Equal(["Off:1:60"], fixture.Port.Attempts.Where(operation => operation.StartsWith("Off:")));
        Assert.Equal(16, fixture.Port.Attempts.Count(operation => operation.StartsWith("CC:")));
    }

    [Fact]
    public void Failed_cleanup_keeps_the_previous_output_and_disposes_the_proposal()
    {
        using var fixture = new OutputFixture();
        fixture.StartNotes();
        fixture.Port.Failure = operation => operation == "Off:1:60" ? new InvalidOperationException("send failed") : null;
        var proposed = new FaultingPort("proposed-output");

        Assert.Throws<AggregateException>(() => fixture.Output.ReplacePort(proposed));

        Assert.Equal("selected-output", fixture.Output.PortName);
        Assert.Equal(0, fixture.Port.DisposeAttempts);
        Assert.Equal(1, proposed.DisposeAttempts);
        Assert.True(proposed.IsDisposed);
        Assert.Equal(16, fixture.Port.Attempts.Count(operation => operation.StartsWith("CC:")));
        fixture.Port.Failure = null;
        fixture.Output.NoteOn(new MidiChannel(1), new MidiValue(72), new MidiValue(100));
        Assert.Contains("On:1:72", fixture.Port.Attempts);
    }

    [Fact]
    public void Failed_previous_port_disposal_clears_selection_and_disposes_the_proposal()
    {
        using var fixture = new OutputFixture();
        var failure = new InvalidOperationException("old dispose failed");
        fixture.Port.Failure = operation => operation == "Dispose" ? failure : null;
        var proposed = new FaultingPort("proposed-output");

        var error = Assert.Throws<InvalidOperationException>(() => fixture.Output.ReplacePort(proposed));

        Assert.Same(failure, error.InnerException);
        Assert.Contains("selected-output", error.Message);
        Assert.Null(fixture.Output.PortName);
        Assert.Equal(1, fixture.Port.DisposeAttempts);
        Assert.Equal(1, proposed.DisposeAttempts);
        Assert.Throws<InvalidOperationException>(() => fixture.Output.Clock());
        fixture.Output.ReplacePort(new FaultingPort("recovery-output"));
        Assert.Equal("recovery-output", fixture.Output.PortName);
    }

    [Fact]
    public void Replacement_preserves_both_primary_and_rejected_port_disposal_failures()
    {
        using var fixture = new OutputFixture();
        var original = new InvalidOperationException("old dispose failed");
        var rejected = new InvalidOperationException("proposed dispose failed");
        fixture.Port.Failure = operation => operation == "Dispose" ? original : null;
        var proposed = new FaultingPort("proposed-output") { Failure = operation => operation == "Dispose" ? rejected : null };

        var error = Assert.Throws<AggregateException>(() => fixture.Output.ReplacePort(proposed));

        Assert.Equal(2, error.Flatten().InnerExceptions.Count);
        Assert.Contains(error.Flatten().InnerExceptions, exception => ReferenceEquals(exception.InnerException, original));
        Assert.Contains(error.Flatten().InnerExceptions, exception => ReferenceEquals(exception.InnerException, rejected));
        Assert.Null(fixture.Output.PortName);
        Assert.Equal(1, proposed.DisposeAttempts);
    }

    [Fact]
    public void Dispose_attempts_all_cleanup_and_disposal_and_preserves_each_failure()
    {
        using var fixture = new OutputFixture();
        fixture.StartNotes();
        fixture.Port.Failure = operation => operation is "Off:1:60" or "CC:8" or "Dispose"
            ? new InvalidOperationException($"failure at {operation}") : null;

        var error = Assert.Throws<AggregateException>(fixture.Output.Dispose);

        Assert.Equal(3, error.Flatten().InnerExceptions.Count);
        Assert.Equal(3, fixture.Port.Attempts.Count(operation => operation.StartsWith("Off:")));
        Assert.Equal(16, fixture.Port.Attempts.Count(operation => operation.StartsWith("CC:")));
        Assert.Equal(1, fixture.Port.DisposeAttempts);
        Assert.Null(fixture.Output.PortName);
        Assert.Throws<ObjectDisposedException>(() => fixture.Output.Clock());
        var attempts = fixture.Port.Attempts.Count;
        fixture.Output.Dispose();
        Assert.Equal(attempts, fixture.Port.Attempts.Count);
    }

    [Fact]
    public void A_disposed_owner_disposes_a_rejected_proposal()
    {
        using var fixture = new OutputFixture();
        fixture.Output.Dispose();
        var proposed = new FaultingPort("proposed-output");

        Assert.Throws<ObjectDisposedException>(() => fixture.Output.ReplacePort(proposed));

        Assert.Equal(1, proposed.DisposeAttempts);
        Assert.True(proposed.IsDisposed);
    }

    [Fact]
    public void Selecting_the_same_instance_leaves_its_notes_and_ownership_unchanged()
    {
        using var fixture = new OutputFixture();
        fixture.StartNotes();
        fixture.Port.Attempts.Clear();

        fixture.Output.ReplacePort(fixture.Port);

        Assert.Empty(fixture.Port.Attempts);
        Assert.Equal("selected-output", fixture.Output.PortName);
        Assert.False(fixture.Port.IsDisposed);
    }

    [Fact]
    public void Name_failure_disposes_the_proposal_without_touching_the_previous_output()
    {
        using var fixture = new OutputFixture();
        var proposed = new FaultingPort("proposed-output") { NameFailure = new InvalidOperationException("name unavailable") };

        Assert.Throws<InvalidOperationException>(() => fixture.Output.ReplacePort(proposed));

        Assert.Equal("selected-output", fixture.Output.PortName);
        Assert.Empty(fixture.Port.Attempts);
        Assert.Equal(1, proposed.DisposeAttempts);
    }

    [Fact]
    public void Cleanup_uses_the_cached_port_name_even_if_native_name_access_later_fails()
    {
        using var fixture = new OutputFixture();
        fixture.StartNotes();
        fixture.Port.NameFailure = new InvalidOperationException("native name unavailable");

        fixture.Output.Panic();

        Assert.Equal("selected-output", fixture.Output.PortName);
        Assert.Equal(16, fixture.Port.Attempts.Count(operation => operation.StartsWith("CC:")));
    }

    private sealed class OutputFixture : IDisposable
    {
        public SafeMidiOutput Output { get; } = new();
        public FaultingPort Port { get; } = new("selected-output");
        public OutputFixture() => Output.ReplacePort(Port);
        public void StartNotes()
        {
            Output.NoteOn(new MidiChannel(1), new MidiValue(60), new MidiValue(100));
            Output.NoteOn(new MidiChannel(1), new MidiValue(64), new MidiValue(100));
            Output.NoteOn(new MidiChannel(2), new MidiValue(67), new MidiValue(100));
        }
        public void Dispose()
        {
            Port.Failure = null;
            Output.Dispose();
        }
    }

    private sealed class FaultingPort(string name) : IMidiOutputPort
    {
        public string Name => NameFailure is { } failure ? throw failure : name;
        public Exception? NameFailure { get; set; }
        public Func<string, Exception?>? Failure { get; set; }
        public List<string> Attempts { get; } = [];
        public int DisposeAttempts { get; private set; }
        public bool IsDisposed { get; private set; }
        public void SendNoteOn(MidiChannel channel, MidiValue note, MidiValue velocity) => Record($"On:{channel.Number}:{note.Value}");
        public void SendNoteOff(MidiChannel channel, MidiValue note, MidiValue velocity) => Record($"Off:{channel.Number}:{note.Value}");
        public void SendControlChange(MidiChannel channel, MidiValue controller, MidiValue value) => Record($"CC:{channel.Number}");
        public void SendProgramChange(MidiChannel channel, MidiValue program) => Record("Program");
        public void SendClock() => Record("Clock");
        public void SendStart() => Record("Start");
        public void SendContinue() => Record("Continue");
        public void SendStop() => Record("Stop");
        public void Dispose()
        {
            DisposeAttempts++;
            try { Record("Dispose"); }
            finally { IsDisposed = true; }
        }
        private void Record(string operation)
        {
            Attempts.Add(operation);
            if (Failure?.Invoke(operation) is { } error) throw error;
        }
    }
}
