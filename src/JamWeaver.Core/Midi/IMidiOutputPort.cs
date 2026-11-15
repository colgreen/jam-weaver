namespace JamWeaver.Core.Midi;

/// <summary>Sends validated MIDI messages through one output port.</summary>
/// <remarks>Passing a port to SafeMidiOutput.ReplacePort transfers disposal responsibility, even if replacement fails.</remarks>
public interface IMidiOutputPort : IDisposable
{
    string Name { get; }
    void SendNoteOn(MidiChannel channel, MidiValue note, MidiValue velocity);
    void SendNoteOff(MidiChannel channel, MidiValue note, MidiValue velocity);
    void SendControlChange(MidiChannel channel, MidiValue controller, MidiValue value);
    void SendProgramChange(MidiChannel channel, MidiValue program);
    void SendClock();
    void SendStart();
    void SendContinue();
    void SendStop();
}
