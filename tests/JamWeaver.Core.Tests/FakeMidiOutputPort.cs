
namespace JamWeaver.Core.Tests;

internal sealed class FakeMidiOutputPort(string name = "fake") : IMidiOutputPort
{
    public string Name { get; } = name;
    public List<string> Messages { get; } = [];
    public bool IsDisposed { get; private set; }
    public void SendNoteOn(MidiChannel c, MidiValue n, MidiValue v) => Messages.Add($"On:{c.Number}:{n.Value}:{v.Value}");
    public void SendNoteOff(MidiChannel c, MidiValue n, MidiValue v) => Messages.Add($"Off:{c.Number}:{n.Value}:{v.Value}");
    public void SendControlChange(MidiChannel c, MidiValue n, MidiValue v) => Messages.Add($"CC:{c.Number}:{n.Value}:{v.Value}");
    public void SendProgramChange(MidiChannel c, MidiValue p) => Messages.Add($"PC:{c.Number}:{p.Value}");
    public void SendClock() => Messages.Add("Clock");
    public void SendStart() => Messages.Add("Start");
    public void SendContinue() => Messages.Add("Continue");
    public void SendStop() => Messages.Add("Stop");
    public void Dispose() => IsDisposed = true;
}
