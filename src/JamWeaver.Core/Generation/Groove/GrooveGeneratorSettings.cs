using JamWeaver.Core.Generation.Phrase;

namespace JamWeaver.Core.Generation.Groove;

public enum GrooveSelection { Auto, Foundation, Offbeat, Anticipation, LongShort, SparseAnswer, Broken }
public enum GrooveSimilarity { Close, Related, Contrast }

public sealed record GrooveGeneratorSettings
{
    public GrooveGeneratorSettings(PatternName name, TonalContext tonalContext, MusicalRole role,
        GrooveSelection groove, GrooveSimilarity similarity, PhraseActivity activity,
        PhraseLevel movement, PhraseLevel variation, PhraseTurnaround turnaround, ulong seed)
    {
        if (role != MusicalRole.Bass) throw new ArgumentException("The groove generator currently supports the bass role only.", nameof(role));
        if (!Enum.IsDefined(groove) || !Enum.IsDefined(similarity) || !Enum.IsDefined(activity)
            || !Enum.IsDefined(movement) || !Enum.IsDefined(variation) || !Enum.IsDefined(turnaround))
            throw new ArgumentOutOfRangeException(nameof(groove));
        (Name, TonalContext, Role, Groove, Similarity, Activity, Movement, Variation, Turnaround, Seed) =
            (name, tonalContext, role, groove, similarity, activity, movement, variation, turnaround, seed);
    }

    public PatternName Name { get; }
    public TonalContext TonalContext { get; }
    public MusicalRole Role { get; }
    public GrooveSelection Groove { get; }
    public GrooveSimilarity Similarity { get; }
    public PhraseActivity Activity { get; }
    public PhraseLevel Movement { get; }
    public PhraseLevel Variation { get; }
    public PhraseTurnaround Turnaround { get; }
    public ulong Seed { get; }
}
