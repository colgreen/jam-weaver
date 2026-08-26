namespace JamWeaver.Core.Generation.Phrase;

public enum PhraseLength { OneBar = 1, TwoBars = 2, FourBars = 4 }
public enum PhraseActivity { Sparse, Medium, Busy }
public enum PhraseRhythm { Steady, Syncopated, Broken }
public enum PhraseLevel { Low, Medium, High }
public enum PhraseTurnaround { None, Subtle, Strong }

public sealed record PhraseGeneratorSettings
{
    public PhraseGeneratorSettings(PatternName name, PhraseLength length, TonalContext tonalContext,
        MusicalRole role, PhraseActivity activity, PhraseRhythm rhythm, PhraseLevel movement,
        PhraseLevel variation, PhraseTurnaround turnaround, ulong seed)
    {
        if (!Enum.IsDefined(length)) throw new ArgumentOutOfRangeException(nameof(length));
        if (!Enum.IsDefined(activity)) throw new ArgumentOutOfRangeException(nameof(activity));
        if (!Enum.IsDefined(rhythm)) throw new ArgumentOutOfRangeException(nameof(rhythm));
        if (!Enum.IsDefined(movement)) throw new ArgumentOutOfRangeException(nameof(movement));
        if (!Enum.IsDefined(variation)) throw new ArgumentOutOfRangeException(nameof(variation));
        if (!Enum.IsDefined(turnaround)) throw new ArgumentOutOfRangeException(nameof(turnaround));
        (Name, Length, TonalContext, Role, Activity, Rhythm, Movement, Variation, Turnaround, Seed) =
            (name, length, tonalContext, role, activity, rhythm, movement, variation, turnaround, seed);
    }

    public PatternName Name { get; }
    public PhraseLength Length { get; }
    public TonalContext TonalContext { get; }
    public MusicalRole Role { get; }
    public PhraseActivity Activity { get; }
    public PhraseRhythm Rhythm { get; }
    public PhraseLevel Movement { get; }
    public PhraseLevel Variation { get; }
    public PhraseTurnaround Turnaround { get; }
    public ulong Seed { get; }
    public int BarCount => (int)Length;
    public int StepCount => BarCount * 16;
}
