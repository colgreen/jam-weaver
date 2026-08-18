using JamWeaver.Core.Midi;

namespace JamWeaver.Core.Sequencer;

public sealed record MidiRoute
{
    public MidiRoute(string outputPortName, MidiChannel channel)
    {
        ArgumentNullException.ThrowIfNull(outputPortName);
        outputPortName = outputPortName.Trim();
        if (outputPortName.Length == 0) throw new ArgumentException("Output port name is required.", nameof(outputPortName));
        OutputPortName = outputPortName;
        Channel = channel;
    }
    public string OutputPortName { get; }
    public MidiChannel Channel { get; }
}
