namespace JamWeaver.Core.Sequencer;

public readonly record struct PatternTiming
{
    public const int MidiClockPulsesPerQuarterNote = 24;
    public const int InitialBeatsPerBar = 4;

    public PatternTiming(int pulsesPerStep)
    {
        var pulsesPerBar = MidiClockPulsesPerQuarterNote * InitialBeatsPerBar;
        if (pulsesPerStep is < 1 or > 96 || pulsesPerBar % pulsesPerStep != 0)
            throw new ArgumentOutOfRangeException(nameof(pulsesPerStep), "Pulses per step must be a positive divisor of 96.");
        PulsesPerStep = pulsesPerStep;
    }

    public int PulsesPerQuarterNote => MidiClockPulsesPerQuarterNote;
    public int BeatsPerBar => InitialBeatsPerBar;
    public int PulsesPerStep { get; }
    public static PatternTiming SixteenthNotes => new(6);
}
