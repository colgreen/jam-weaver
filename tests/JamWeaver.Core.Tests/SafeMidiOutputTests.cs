
namespace JamWeaver.Core.Tests;

public sealed class SafeMidiOutputTests
{
    [Fact]
    public async Task Timed_note_always_sends_note_off_when_cancelled()
    {
        var port = new FakeMidiOutputPort();
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            output.SendNoteAsync(new MidiChannel(1), new MidiValue(60), new MidiValue(80), TimeSpan.FromSeconds(1), cancellation.Token));

        Assert.Equal("On:1:60:80", port.Messages[0]);
        Assert.Equal("Off:1:60:0", port.Messages[1]);
    }

    [Fact]
    public void Replacing_port_silences_and_disposes_previous_port()
    {
        var first = new FakeMidiOutputPort("first");
        var second = new FakeMidiOutputPort("second");
        using var output = new SafeMidiOutput();
        output.ReplacePort(first);
        output.NoteOn(new MidiChannel(2), new MidiValue(64), new MidiValue(90));

        output.ReplacePort(second);

        Assert.Contains("Off:2:64:0", first.Messages);
        Assert.Equal(16, first.Messages.Count(message => message.StartsWith("CC:")));
        Assert.True(first.IsDisposed);
    }

    [Fact]
    public void Panic_sends_explicit_note_off_and_all_notes_off()
    {
        var port = new FakeMidiOutputPort();
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        output.NoteOn(new MidiChannel(1), new MidiValue(60), new MidiValue(100));

        output.Panic();

        Assert.Contains("Off:1:60:0", port.Messages);
        Assert.Equal(16, port.Messages.Count(message => message.StartsWith("CC:")));
    }
}
