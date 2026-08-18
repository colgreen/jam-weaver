namespace JamWeaver.Core.Midi;

public readonly record struct MidiChannel
{
    public MidiChannel(int number)
    {
        if (number is < 1 or > 16)
            throw new ArgumentOutOfRangeException(nameof(number), "MIDI channel must be 1-16.");
        Number = number;
    }

    public int Number { get; }
    public int ZeroBased => Number - 1;
}
