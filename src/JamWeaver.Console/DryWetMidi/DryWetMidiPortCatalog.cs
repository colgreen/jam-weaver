using Melanchall.DryWetMidi.Multimedia;

namespace JamWeaver.ConsoleApp.DryWetMidi;

internal sealed class DryWetMidiPortCatalog : IMidiPortCatalog
{
    public IReadOnlyList<string> OutputNames() => OutputDevice.GetAll().Select(ReadAndDispose).ToArray();
    public IReadOnlyList<string> InputNames() => InputDevice.GetAll().Select(ReadAndDispose).ToArray();
    public IMidiOutputPort OpenOutput(int index) => new DryWetMidiOutput(OutputDevice.GetByIndex(index));
    public IMidiInputPort OpenInput(int index) => new DryWetMidiInput(InputDevice.GetByIndex(index));
    private static string ReadAndDispose(OutputDevice device) { using (device) return device.Name; }
    private static string ReadAndDispose(InputDevice device) { using (device) return device.Name; }
}
