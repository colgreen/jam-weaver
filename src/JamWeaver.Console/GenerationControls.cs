
namespace JamWeaver.ConsoleApp;

public enum GeneratorMode { Euclidean, Euclidean2, Phrase, Groove, Motif }

/// <summary>Holds the performer-selectable controls used to create candidates.</summary>
public sealed class GenerationControls
{
    public GeneratorMode Mode { get; set; } = GeneratorMode.Euclidean;
    public PhraseLength PhraseLength { get; set; } = PhraseLength.FourBars;
    public PhraseActivity Activity { get; set; } = PhraseActivity.Medium;
    public PhraseRhythm Rhythm { get; set; } = PhraseRhythm.Syncopated;
    public PhraseLevel Movement { get; set; } = PhraseLevel.Medium;
    public PhraseLevel Variation { get; set; } = PhraseLevel.Medium;
    public PhraseTurnaround Turnaround { get; set; } = PhraseTurnaround.Subtle;
    public GrooveSelection Groove { get; set; } = GrooveSelection.Auto;
    public GrooveSimilarity Similarity { get; set; } = GrooveSimilarity.Related;
    public MotifShape MotifShape { get; set; } = MotifShape.Auto;
}
