using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JamWeaver.Core.Generation;
using JamWeaver.Core.Midi;
using JamWeaver.Core.Sequencer;

namespace JamWeaver.Core.Tests.Generation;

public sealed class GeneratorTests
{
    [Theory]
    [InlineData(MusicalRole.Bass, PitchPalette.MajorPentatonic, 101UL, "14B911486EB0BA3CAD0DB5B77011C0FD1AAD42AB1773D0C70359B6C5C2FC7230")]
    [InlineData(MusicalRole.Bass, PitchPalette.MinorPentatonic, 102UL, "CB09274EA934985A0CE4ADECA3E53C9535C0261A5A61937227AE0F3231F22340")]
    [InlineData(MusicalRole.Middle, PitchPalette.MajorPentatonic, 201UL, "6D6FE1906BBEBD4577186CAAF11819B5B9007A7D163626013DD4F930AFFBB645")]
    [InlineData(MusicalRole.Middle, PitchPalette.MinorPentatonic, 202UL, "14878E1192314E925AF781AC9FDC948063CABE6A555A97ED9D7A43A9F05A46A9")]
    [InlineData(MusicalRole.High, PitchPalette.MajorPentatonic, 301UL, "2DF4607AA03FC52A1BA3CC5A3AE275C94827087FD873C8F1DA1D8B03C28207FD")]
    [InlineData(MusicalRole.High, PitchPalette.MinorPentatonic, 302UL, "E38A317260483F0C031D6877174DA6A862FE3269833357721E87B0043373A7CD")]
    public void Role_and_palette_snapshot(MusicalRole role, PitchPalette palette, ulong seed, string expectedHash)
    {
        var settings = new MelodicGeneratorSettings(new PatternName("Snapshot"), 16, PatternTiming.SixteenthNotes,
            new TonalContext(new RootPitchClass(0), palette), role, new NormalizedAmount(.5),
            new NormalizedAmount(.5), new NormalizedAmount(.5), new NormalizedAmount(.5), new NormalizedAmount(.5), seed);
        Assert.Equal(expectedHash, Hash(Signature(new MelodicPatternGenerator().Generate(settings))));
    }

    [Fact]
    public void Melodic_generation_is_reproducible_and_starts_on_downbeat()
    {
        var generator = new MelodicPatternGenerator();
        var first = generator.Generate(MelodicSettings(123));
        var second = generator.Generate(MelodicSettings(123));
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(Signature(first), Signature(second));
        Assert.NotEmpty(first.Steps[0].Notes);
        Assert.All(first.Steps, step => Assert.Equal(1, step.Probability.Value));
        Assert.Equal("EB3A81757607D69AFB9FA684B45FE1DDCAD17CF6DAF794BFE62B5B0E7146B2BC", Hash(Signature(first)));
    }

    [Fact]
    public void Different_seed_changes_representative_melodic_pattern() =>
        Assert.NotEqual(Signature(new MelodicPatternGenerator().Generate(MelodicSettings(1))),
            Signature(new MelodicPatternGenerator().Generate(MelodicSettings(2))));

    [Fact]
    public void Drum_generation_supports_multiple_voices_and_is_reproducible()
    {
        var settings = new DrumGeneratorSettings(new PatternName("Drums"), 16, PatternTiming.SixteenthNotes,
        [
            new DrumVoiceSettings(new MidiValue(36), new NormalizedAmount(.4)),
            new DrumVoiceSettings(new MidiValue(38), new NormalizedAmount(.25)),
            new DrumVoiceSettings(new MidiValue(42), new NormalizedAmount(.6))
        ], new NormalizedAmount(.4), new NormalizedAmount(.25), 456);
        var generator = new DrumPatternGenerator();
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
        var parent = new MelodicPatternGenerator().Generate(MelodicSettings(123));
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
        var generator = new MelodicPatternGenerator();
        var generated = generator.Generate(MelodicSettings(123));
        var settings = GeneratorRecipeReconstruction.Melodic(generated.Name, generated.Recipe!);
        Assert.Equal(Signature(generated), Signature(generator.Generate(settings)));
    }

    [Fact]
    public void Recipe_reconstruction_rejects_unknown_parameter()
    {
        var generated = new MelodicPatternGenerator().Generate(MelodicSettings(123));
        var parameters = generated.Recipe!.Parameters.Add("unknown", RecipeValue.FromBoolean(true));
        var malformed = new GeneratorRecipe(generated.Recipe.GeneratorId, generated.Recipe.GeneratorVersion,
            generated.Recipe.Seed, null, parameters);
        Assert.Throws<ArgumentException>(() => GeneratorRecipeReconstruction.Melodic(generated.Name, malformed));
    }

    [Fact]
    public void Drum_recipe_reconstructs_equivalent_pattern()
    {
        var settings = new DrumGeneratorSettings(new PatternName("Drums"), 16, PatternTiming.SixteenthNotes,
            [new DrumVoiceSettings(new MidiValue(36), new NormalizedAmount(.4)), new DrumVoiceSettings(new MidiValue(42), new NormalizedAmount(.6))],
            new NormalizedAmount(.4), new NormalizedAmount(.2), 88);
        var generator = new DrumPatternGenerator();
        var generated = generator.Generate(settings);
        var reconstructed = GeneratorRecipeReconstruction.Drums(generated.Name, generated.Recipe!);
        Assert.Equal(Signature(generated), Signature(generator.Generate(reconstructed)));
    }

    [Fact]
    public void Mutation_is_repeatable_for_same_parent_settings_and_seed()
    {
        var parent = new MelodicPatternGenerator().Generate(MelodicSettings(123));
        var settings = new MutationSettings(new NormalizedAmount(.6), 777);
        var mutator = new PatternMutator();
        Assert.Equal(Signature(mutator.Mutate(parent, settings)), Signature(mutator.Mutate(parent, settings)));
    }

    private static MelodicGeneratorSettings MelodicSettings(ulong seed) => new(
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

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
