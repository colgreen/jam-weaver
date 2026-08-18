namespace JamWeaver.Core.Midi;

public readonly record struct MidiValue
{
    public MidiValue(int value)
    {
        if (value is < 0 or > 127)
            throw new ArgumentOutOfRangeException(nameof(value), "MIDI value must be 0-127.");
        Value = value;
    }

    public int Value { get; }
}
