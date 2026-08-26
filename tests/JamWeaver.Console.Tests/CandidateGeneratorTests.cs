namespace JamWeaver.Console.Tests;

public sealed class CandidateGeneratorTests
{
    [Fact]
    public void Controls_have_the_existing_startup_defaults()
    {
        var controls = new GenerationControls();

        Assert.Equal(GeneratorMode.Euclidean, controls.Mode);
        Assert.Equal(PhraseLength.FourBars, controls.PhraseLength);
        Assert.Equal(PhraseActivity.Medium, controls.Activity);
        Assert.Equal(PhraseRhythm.Syncopated, controls.Rhythm);
        Assert.Equal(PhraseLevel.Medium, controls.Movement);
        Assert.Equal(PhraseLevel.Medium, controls.Variation);
        Assert.Equal(PhraseTurnaround.Subtle, controls.Turnaround);
        Assert.Equal(GrooveSelection.Auto, controls.Groove);
        Assert.Equal(GrooveSimilarity.Related, controls.Similarity);
        Assert.Equal(MotifShape.Auto, controls.MotifShape);
    }

    [Theory]
    [InlineData(GeneratorMode.Euclidean, MelodicPatternGenerator.GeneratorId, 16)]
    [InlineData(GeneratorMode.Euclidean2, Euclidean2PatternGenerator.GeneratorId, 64)]
    [InlineData(GeneratorMode.Phrase, MelodicPhraseGenerator.GeneratorId, 64)]
    [InlineData(GeneratorMode.Groove, MelodicGrooveGenerator.GeneratorId, 64)]
    [InlineData(GeneratorMode.Motif, MusicalMotifGenerator.GeneratorId, 64)]
    public void Mode_selects_the_expected_generator(GeneratorMode mode, string generatorId, int stepCount)
    {
        var controls = new GenerationControls { Mode = mode };
        var pattern = CreateGenerator().Generate(42, null, controls);

        Assert.Equal(generatorId, pattern.Recipe!.GeneratorId);
        Assert.Equal(stepCount, pattern.Steps.Length);
        Assert.Equal(42UL, pattern.Recipe.Seed);
    }

    [Fact]
    public void Phrase_controls_are_mapped_to_the_recipe()
    {
        var controls = new GenerationControls
        {
            Mode = GeneratorMode.Phrase,
            PhraseLength = PhraseLength.TwoBars,
            Activity = PhraseActivity.Busy,
            Rhythm = PhraseRhythm.Broken,
            Movement = PhraseLevel.High,
            Variation = PhraseLevel.Low,
            Turnaround = PhraseTurnaround.Strong
        };

        var pattern = CreateGenerator().Generate(99, null, controls);
        var settings = GeneratorRecipeReconstruction.Phrase(pattern.Name, pattern.Recipe!);

        Assert.Equal(controls.PhraseLength, settings.Length);
        Assert.Equal(controls.Activity, settings.Activity);
        Assert.Equal(controls.Rhythm, settings.Rhythm);
        Assert.Equal(controls.Movement, settings.Movement);
        Assert.Equal(controls.Variation, settings.Variation);
        Assert.Equal(controls.Turnaround, settings.Turnaround);
    }

    [Fact]
    public void Generation_inherits_tonal_context_and_role_from_the_current_candidate()
    {
        var context = new TonalContext(new RootPitchClass(4), PitchPalette.MajorPentatonic);
        var current = new MelodicPatternGenerator().Generate(new MelodicGeneratorSettings(
            new PatternName("Current"), 16, PatternTiming.SixteenthNotes, context, MusicalRole.High,
            new NormalizedAmount(.4), new NormalizedAmount(.35), new NormalizedAmount(.65),
            new NormalizedAmount(.8), new NormalizedAmount(.15), 1));
        var controls = new GenerationControls { Mode = GeneratorMode.Motif };

        var pattern = CreateGenerator().Generate(2, current, controls);

        Assert.Equal(context, pattern.TonalContext);
        Assert.Equal(MusicalRole.High, pattern.Role);
    }

    [Fact]
    public void Groove_rejects_an_inherited_non_bass_role()
    {
        var context = CandidateGenerator.DefaultTonalContext();
        var current = new MelodicPatternGenerator().Generate(new MelodicGeneratorSettings(
            new PatternName("Current"), 16, PatternTiming.SixteenthNotes, context, MusicalRole.Middle,
            new NormalizedAmount(.4), new NormalizedAmount(.35), new NormalizedAmount(.65),
            new NormalizedAmount(.8), new NormalizedAmount(.15), 1));
        var controls = new GenerationControls { Mode = GeneratorMode.Groove };

        var exception = Assert.Throws<ArgumentException>(() => CreateGenerator().Generate(2, current, controls));

        Assert.Contains("bass role only", exception.Message);
    }

    [Fact]
    public void Same_controls_context_and_seed_produce_equivalent_material()
    {
        var generator = CreateGenerator();
        var controls = new GenerationControls { Mode = GeneratorMode.Euclidean2 };

        var first = generator.Generate(123, null, controls);
        var second = generator.Generate(123, null, controls);

        Assert.Equal(Signature(first), Signature(second));
        Assert.Equal(first.Recipe!.GeneratorId, second.Recipe!.GeneratorId);
        Assert.Equal(first.Recipe.GeneratorVersion, second.Recipe.GeneratorVersion);
        Assert.Equal(first.Recipe.Seed, second.Recipe.Seed);
        Assert.True(first.Recipe.Parameters.OrderBy(pair => pair.Key)
            .SequenceEqual(second.Recipe.Parameters.OrderBy(pair => pair.Key)));
    }

    private static CandidateGenerator CreateGenerator() => new(
        new MelodicPatternGenerator(), new Euclidean2PatternGenerator(), new MelodicPhraseGenerator(),
        new MelodicGrooveGenerator(), new MusicalMotifGenerator());

    private static string Signature(Pattern pattern) => string.Join("|", pattern.Steps.Select(step =>
        string.Join("+", step.Notes.Select(note => $"{note.Pitch}:{note.Velocity.Value}:{note.Gate.Value:R}"))));
}
