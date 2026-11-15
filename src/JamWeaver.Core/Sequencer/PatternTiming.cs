namespace JamWeaver.Core.Sequencer;

public readonly record struct PatternTiming
{
    public const int MidiClockPulsesPerQuarterNote = 24;
    public const int InitialBeatsPerBar = 4;

    public PatternTiming(int pulsesPerStep)
    {
        PulsesPerStep = pulsesPerStep;
        Validate(nameof(pulsesPerStep));
    }

    internal void Validate(string parameterName)
    {
        var pulsesPerBar = MidiClockPulsesPerQuarterNote * InitialBeatsPerBar;
        if (PulsesPerStep is < 1 or > 96 || pulsesPerBar % PulsesPerStep != 0)
            throw new ArgumentOutOfRangeException(parameterName, "Pulses per step must be a positive divisor of 96.");
    }

    public int PulsesPerQuarterNote => MidiClockPulsesPerQuarterNote;
    public int BeatsPerBar => InitialBeatsPerBar;
    public int PulsesPerStep { get; }
    public static PatternTiming SixteenthNotes => new(6);
}
