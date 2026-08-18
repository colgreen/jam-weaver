using Melanchall.DryWetMidi.Multimedia;

namespace JamWeaver.ConsoleApp.DryWetMidi;

internal static class DryWetMidiPortCatalog
{
    public static string[] OutputNames() => OutputDevice.GetAll().Select(ReadAndDispose).ToArray();
    public static string[] InputNames() => InputDevice.GetAll().Select(ReadAndDispose).ToArray();
    public static DryWetMidiOutput OpenOutput(int index) => new(OutputDevice.GetByIndex(index));
    public static InputDevice OpenInput(int index) => InputDevice.GetByIndex(index);
    private static string ReadAndDispose(OutputDevice device) { using (device) return device.Name; }
    private static string ReadAndDispose(InputDevice device) { using (device) return device.Name; }
}
