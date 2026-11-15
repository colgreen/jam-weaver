using JamWeaver.Core.Midi;

namespace JamWeaver.Console.Tests;

internal sealed class FakeMidiPortCatalog : IMidiPortCatalog
{
    public Func<IReadOnlyList<string>> OutputDiscovery { get; set; } = () => [];
    public Func<IReadOnlyList<string>> InputDiscovery { get; set; } = () => [];
    public Func<int, IMidiOutputPort> OutputFactory { get; set; } = index => throw new ArgumentOutOfRangeException(nameof(index));
    public Func<int, IMidiInputPort> InputFactory { get; set; } = index => throw new ArgumentOutOfRangeException(nameof(index));

    public IReadOnlyList<string> OutputNames() => OutputDiscovery();
    public IReadOnlyList<string> InputNames() => InputDiscovery();
    public IMidiOutputPort OpenOutput(int index) => OutputFactory(index);
    public IMidiInputPort OpenInput(int index) => InputFactory(index);
}
