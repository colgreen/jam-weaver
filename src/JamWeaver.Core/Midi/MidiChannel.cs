namespace JamWeaver.Core.Midi;

public readonly record struct MidiChannel
{
    public MidiChannel(int number)
    {
        Number = number;
        Validate(nameof(number));
    }

    public int Number { get; }
    public int ZeroBased
    {
        get { Validate(nameof(Number)); return Number - 1; }
    }

    internal void Validate(string parameterName)
    {
        if (Number is < 1 or > 16)
            throw new ArgumentOutOfRangeException(parameterName, "MIDI channel must be 1-16.");
    }
}
