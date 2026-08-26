using JamWeaver.Core.Generation;
using JamWeaver.Core.Generation.Groove;
using JamWeaver.Core.Generation.Motif;
using JamWeaver.Core.Generation.Phrase;
using JamWeaver.Core.Sequencer;

namespace JamWeaver.ConsoleApp;

/// <summary>Creates a candidate pattern from the selected console controls and current musical context.</summary>
public sealed class CandidateGenerator
{
    private readonly IPatternGenerator<MelodicGeneratorSettings> euclidean;
    private readonly IPatternGenerator<MelodicGeneratorSettings> euclidean2;
    private readonly IPatternGenerator<PhraseGeneratorSettings> phrase;
    private readonly IPatternGenerator<GrooveGeneratorSettings> groove;
    private readonly IPatternGenerator<MotifGeneratorSettings> motif;

    public CandidateGenerator(IPatternGenerator<MelodicGeneratorSettings> euclidean,
        IPatternGenerator<MelodicGeneratorSettings> euclidean2,
        IPatternGenerator<PhraseGeneratorSettings> phrase,
        IPatternGenerator<GrooveGeneratorSettings> groove,
        IPatternGenerator<MotifGeneratorSettings> motif)
    {
        this.euclidean = euclidean;
        this.euclidean2 = euclidean2;
        this.phrase = phrase;
        this.groove = groove;
        this.motif = motif;
    }

    public Pattern Generate(ulong seed, Pattern? current, GenerationControls controls)
    {
        ArgumentNullException.ThrowIfNull(controls);
        var context = current?.TonalContext ?? DefaultTonalContext();
        var role = current?.Role ?? MusicalRole.Bass;
        var name = new PatternName($"Jam {seed}");

        return controls.Mode switch
        {
            GeneratorMode.Phrase => phrase.Generate(new PhraseGeneratorSettings(name, controls.PhraseLength,
                context, role, controls.Activity, controls.Rhythm, controls.Movement, controls.Variation,
                controls.Turnaround, seed)),
            GeneratorMode.Groove => groove.Generate(new GrooveGeneratorSettings(name, context, role,
                controls.Groove, controls.Similarity, controls.Activity, controls.Movement, controls.Variation,
                controls.Turnaround, seed)),
            GeneratorMode.Motif => motif.Generate(new MotifGeneratorSettings(name, context, role,
                controls.MotifShape, controls.Activity, controls.Movement, controls.Variation, seed)),
            GeneratorMode.Euclidean => euclidean.Generate(MelodicSettings(name, 16, context, role, seed)),
            GeneratorMode.Euclidean2 => euclidean2.Generate(MelodicSettings(name, 64, context, role, seed)),
            _ => throw new ArgumentOutOfRangeException(nameof(controls), controls.Mode, "Unknown generator mode.")
        };
    }

    public static TonalContext DefaultTonalContext() =>
        new(new RootPitchClass(9), PitchPalette.MinorPentatonic);

    private static MelodicGeneratorSettings MelodicSettings(PatternName name, int stepCount,
        TonalContext context, MusicalRole role, ulong seed) =>
        new(name, stepCount, PatternTiming.SixteenthNotes, context, role, new NormalizedAmount(.4),
            new NormalizedAmount(.35), new NormalizedAmount(.65), new NormalizedAmount(.8),
            new NormalizedAmount(.15), seed);
}
