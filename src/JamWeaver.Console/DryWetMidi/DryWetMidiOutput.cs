using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using JamWeaver.Core.Midi;

namespace JamWeaver.ConsoleApp.DryWetMidi;

internal sealed class DryWetMidiOutput(OutputDevice device) : IMidiOutputPort
{
    public string Name => device.Name;
    public void SendNoteOn(MidiChannel c, MidiValue n, MidiValue v) => device.SendEvent(new NoteOnEvent(N(n), N(v)) { Channel = C(c) });
    public void SendNoteOff(MidiChannel c, MidiValue n, MidiValue v) => device.SendEvent(new NoteOffEvent(N(n), N(v)) { Channel = C(c) });
    public void SendControlChange(MidiChannel c, MidiValue n, MidiValue v) => device.SendEvent(new ControlChangeEvent(N(n), N(v)) { Channel = C(c) });
    public void SendProgramChange(MidiChannel c, MidiValue p) => device.SendEvent(new ProgramChangeEvent(N(p)) { Channel = C(c) });
    public void SendClock() => device.SendEvent(new TimingClockEvent());
    public void SendStart() => device.SendEvent(new StartEvent());
    public void SendContinue() => device.SendEvent(new ContinueEvent());
    public void SendStop() => device.SendEvent(new StopEvent());
    public void Dispose() => device.Dispose();
    private static FourBitNumber C(MidiChannel value) => (FourBitNumber)value.ZeroBased;
    private static SevenBitNumber N(MidiValue value) => (SevenBitNumber)value.Value;
}
