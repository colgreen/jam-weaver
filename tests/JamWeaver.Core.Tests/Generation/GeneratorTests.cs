using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JamWeaver.Core.Generation.Groove;
using JamWeaver.Core.Generation.Motif;
using JamWeaver.Core.Generation.Phrase;

namespace JamWeaver.Core.Tests.Generation;

public sealed class GeneratorTests
{
    [Fact]
    public void Generators_implement_their_typed_contracts()
    {
        Assert.IsAssignableFrom<IPatternGenerator<EuclideanGeneratorSettings>>(new EuclideanMotifGenerator());
        Assert.IsAssignableFrom<IPatternGenerator<EuclideanGeneratorSettings>>(new ResearchEuclideanGenerator());
        Assert.IsAssignableFrom<IPatternGenerator<EuclideanGeneratorSettings>>(new EuclideanPhraseGenerator());
        Assert.IsAssignableFrom<IPatternGenerator<EuclideanDrumGeneratorSettings>>(new EuclideanDrumGenerator());
        Assert.IsAssignableFrom<IPatternGenerator<PhraseGeneratorSettings>>(new StructuredPhraseGenerator());
        Assert.IsAssignableFrom<IPatternGenerator<GrooveGeneratorSettings>>(new GrooveVocabularyGenerator());
        Assert.IsAssignableFrom<IPatternGenerator<MotifGeneratorSettings>>(new MotifGenerator());
    }

    [Theory]
    [InlineData(MusicalRole.Bass, PitchPalette.MajorPentatonic, 101UL, "10483613F2D322E8DB66D196C52BAE821D81B220B12CAC7B45AF1EA5DE8856DC")]
    [InlineData(MusicalRole.Bass, PitchPalette.MinorPentatonic, 102UL, "36FABCD91D5FD697BBB1F44408FF54C61565D60BDDB97912363645068C42D08A")]
    [InlineData(MusicalRole.Middle, PitchPalette.MajorPentatonic, 201UL, "451934506D1107ACFE25A0F2EA8DC9E770FB82B7006656FD8A59BDF010D15D02")]
    [InlineData(MusicalRole.Middle, PitchPalette.MinorPentatonic, 202UL, "0287235B227C3CAFD64FB4714D89F5F6F8FB784B3DBC63B4621D7759A0F3A52C")]
    [InlineData(MusicalRole.High, PitchPalette.MajorPentatonic, 301UL, "EE1037715A229ADD2249C1A0EC5085E9DF611B1FC7BBA7AEEBA9452B65F49EDE")]
    [InlineData(MusicalRole.High, PitchPalette.MinorPentatonic, 302UL, "1BD8B6DFA1374A37E7AD64D8BB0DE151F29EA5CCA6D0C01F7739B25C76D5CF83")]
    public void Role_and_palette_snapshot(MusicalRole role, PitchPalette palette, ulong seed, string expectedHash)
    {
        var settings = new EuclideanGeneratorSettings(new PatternName("Snapshot"), 16, PatternTiming.SixteenthNotes,
            new TonalContext(new RootPitchClass(0), palette), role, new NormalizedAmount(.5),
            new NormalizedAmount(.5), new NormalizedAmount(.5), new NormalizedAmount(.5), new NormalizedAmount(.5), seed);
        Assert.Equal(expectedHash, Hash(Signature(new EuclideanMotifGenerator().Generate(settings))));
    }

    [Fact]
    public void Melodic_generation_is_reproducible_and_starts_on_downbeat()
    {
        var generator = new EuclideanMotifGenerator();
        var first = generator.Generate(EuclideanSettings(123));
        var second = generator.Generate(EuclideanSettings(123));
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(Signature(first), Signature(second));
        Assert.NotEmpty(first.Steps[0].Notes);
        Assert.All(first.Steps, step => Assert.Equal(1, step.Probability.Value));
        Assert.Equal("7CE827DA339067023E763E86339451D44D1CCC30882FCE06977F976ECF039B8D", Hash(Signature(first)));
    }

    [Fact]
    public void Different_seed_changes_representative_melodic_pattern() =>
        Assert.NotEqual(Signature(new EuclideanMotifGenerator().Generate(EuclideanSettings(1))),
            Signature(new EuclideanMotifGenerator().Generate(EuclideanSettings(2))));

    [Fact]
    public void Research_generator_repeats_three_bars_and_changes_only_two_late_pitches_in_bar_four()
    {
        var generator = new ResearchEuclideanGenerator();
        var first = generator.Generate(EuclideanSettings(123));
        var second = generator.Generate(EuclideanSettings(123));

        Assert.Equal(Signature(first), Signature(second));
        Assert.Equal(ResearchEuclideanGenerator.GeneratorId, first.Recipe!.GeneratorId);
        Assert.Equal("a-a-a-a-turnaround", first.Recipe.Parameters["form"].Text);
        Assert.Equal(64, first.Steps.Length);

        for (var step = 0; step < 16; step++)
        {
            Assert.Equal(StepSignature(first.Steps[step]), StepSignature(first.Steps[step + 16]));
            Assert.Equal(StepSignature(first.Steps[step]), StepSignature(first.Steps[step + 32]));
            if (step < 8) Assert.Equal(StepSignature(first.Steps[step]), StepSignature(first.Steps[step + 48]));
        }

        var changed = Enumerable.Range(56, 8)
            .Count(step => StepSignature(first.Steps[step - 48]) != StepSignature(first.Steps[step]));
        Assert.Equal(2, changed);
    }

    [Theory]
    [InlineData(MusicalRole.Bass)]
    [InlineData(MusicalRole.Middle)]
    [InlineData(MusicalRole.High)]
    public void Melodic_motif_does_not_collapse_to_one_pitch(MusicalRole role)
    {
        foreach (var seed in Enumerable.Range(1, 32).Select(value => (ulong)value))
        {
            var source = EuclideanSettings(seed);
            var settings = new EuclideanGeneratorSettings(source.Name, source.StepCount, source.Timing,
                source.TonalContext, role, source.Density, source.Movement, source.Repetition,
                source.Gate, source.VelocityVariation, source.Seed);
            var pattern = new EuclideanMotifGenerator().Generate(settings);
            Assert.True(pattern.Steps.SelectMany(step => step.Notes).Select(note => note.Pitch).Distinct().Count() > 1);
        }
    }

    [Fact]
    public void Drum_generation_supports_multiple_voices_and_is_reproducible()
    {
        var settings = new EuclideanDrumGeneratorSettings(new PatternName("Drums"), 16, PatternTiming.SixteenthNotes,
        [
            new DrumVoiceSettings(new MidiValue(36), new NormalizedAmount(.4)),
            new DrumVoiceSettings(new MidiValue(38), new NormalizedAmount(.25)),
            new DrumVoiceSettings(new MidiValue(42), new NormalizedAmount(.6))
        ], new NormalizedAmount(.4), new NormalizedAmount(.25), 456);
        var generator = new EuclideanDrumGenerator();
        var first = generator.Generate(settings);
        var second = generator.Generate(settings);
        Assert.Equal(Signature(first), Signature(second));
        Assert.True(first.Steps.Any(step => step.Notes.Length > 1));
        Assert.NotEmpty(first.Steps[0].Notes);
        Assert.Equal("6CA60D72CF8B31A4032BD16B3A6350F9E691EB8F2DD51D2E332BCE8617F89BE5", Hash(Signature(first)));
    }

    [Fact]
    public void Mutation_creates_changed_snapshot_with_bounded_edits_and_ancestry()
    {
        var parent = new EuclideanMotifGenerator().Generate(EuclideanSettings(123));
        var settings = new MutationSettings(new NormalizedAmount(.1), 999);
        var mutated = new PatternMutator().Mutate(parent, settings);
        Assert.NotEqual(parent.Id, mutated.Id);
        Assert.NotEqual(Signature(parent), Signature(mutated));
        Assert.Equal(parent.Id, mutated.Recipe!.ParentPatternId);
        Assert.Equal(PatternMutator.GeneratorId, mutated.Recipe.GeneratorId);
        Assert.Equal(parent.Steps.Length, mutated.Steps.Length);
        Assert.Equal(settings, GeneratorRecipeReconstruction.Mutation(mutated.Recipe));
    }

    [Fact]
    public void Melodic_recipe_reconstructs_equivalent_settings_and_pattern()
    {
        var generator = new EuclideanMotifGenerator();
        var generated = generator.Generate(EuclideanSettings(123));
        var settings = GeneratorRecipeReconstruction.Euclidean(generated.Name, generated.Recipe!);
        Assert.Equal(Signature(generated), Signature(generator.Generate(settings)));
    }

    [Fact]
    public void Recipe_reconstruction_rejects_unknown_parameter()
    {
        var generated = new EuclideanMotifGenerator().Generate(EuclideanSettings(123));
        var parameters = generated.Recipe!.Parameters.Add("unknown", RecipeValue.FromBoolean(true));
        var malformed = new GeneratorRecipe(generated.Recipe.GeneratorId, generated.Recipe.GeneratorVersion,
            generated.Recipe.Seed, null, parameters);
        Assert.Throws<ArgumentException>(() => GeneratorRecipeReconstruction.Euclidean(generated.Name, malformed));
    }

    [Fact]
    public void Drum_recipe_reconstructs_equivalent_pattern()
    {
        var settings = new EuclideanDrumGeneratorSettings(new PatternName("Drums"), 16, PatternTiming.SixteenthNotes,
            [new DrumVoiceSettings(new MidiValue(36), new NormalizedAmount(.4)), new DrumVoiceSettings(new MidiValue(42), new NormalizedAmount(.6))],
            new NormalizedAmount(.4), new NormalizedAmount(.2), 88);
        var generator = new EuclideanDrumGenerator();
        var generated = generator.Generate(settings);
        var reconstructed = GeneratorRecipeReconstruction.Drums(generated.Name, generated.Recipe!);
        Assert.Equal(Signature(generated), Signature(generator.Generate(reconstructed)));
    }

    [Fact]
    public void Mutation_is_repeatable_for_same_parent_settings_and_seed()
    {
        var parent = new EuclideanMotifGenerator().Generate(EuclideanSettings(123));
        var settings = new MutationSettings(new NormalizedAmount(.6), 777);
        var mutator = new PatternMutator();
        Assert.Equal(Signature(mutator.Mutate(parent, settings)), Signature(mutator.Mutate(parent, settings)));
    }

    private static EuclideanGeneratorSettings EuclideanSettings(ulong seed) => new(
        new PatternName("Bass"), 16, PatternTiming.SixteenthNotes,
        new TonalContext(new RootPitchClass(2), PitchPalette.MinorPentatonic), MusicalRole.Bass,
        new NormalizedAmount(.5), new NormalizedAmount(.35), new NormalizedAmount(.75),
        new NormalizedAmount(.45), new NormalizedAmount(.25), seed);

    private static string Signature(Pattern pattern) => string.Join("|", pattern.Steps.Select(step =>
        step.Notes.Length == 0 ? "-" : string.Join("+", step.Notes.Select(note =>
        {
            var pitch = note.Pitch switch
            {
                MelodicPitch melodic => $"m{melodic.ScaleDegree}:{melodic.OctaveOffset}:{melodic.ChromaticOffset}",
                DrumPitch drum => $"d{drum.NoteNumber.Value}",
                _ => throw new InvalidOperationException()
            };
            return $"{pitch},v{note.Velocity.Value},g{note.Gate.Value.ToString("0.###", CultureInfo.InvariantCulture)}";
        }))));

    private static string StepSignature(PatternStep step) => string.Join("+", step.Notes.Select(note =>
        $"{note.Pitch}:{note.Velocity.Value}:{note.Gate.Value:R}:{step.Probability.Value:R}"));

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
