namespace JamWeaver.Core.Midi;

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
