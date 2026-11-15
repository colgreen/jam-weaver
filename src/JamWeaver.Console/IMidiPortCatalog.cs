namespace JamWeaver.ConsoleApp;

/// <summary>Discovers MIDI port names and opens ports by their current device index.</summary>
/// <remarks>
/// Lists are snapshots; indices can change when devices connect or disconnect.
/// The caller owns each successfully opened port and must transfer it to a selection owner or dispose it.
/// Opening an input does not start listening. Discovery and open failures are reported by throwing.
/// </remarks>
public interface IMidiPortCatalog
{
    IReadOnlyList<string> OutputNames();
    IReadOnlyList<string> InputNames();
    IMidiOutputPort OpenOutput(int index);
    IMidiInputPort OpenInput(int index);
}
