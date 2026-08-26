using System.Globalization;
using JamWeaver.Core.Generation.Motif;
using JamWeaver.Core.Generation.Phrase;
using JamWeaver.Core.Persistence;

namespace JamWeaver.Core.Tests.Generation;

public sealed class MotifGeneratorTests
{
    [Theory]
    [InlineData(MotifShape.Pedal)]
    [InlineData(MotifShape.RootFifth)]
    [InlineData(MotifShape.Walking)]
    [InlineData(MotifShape.CallResponse)]
    [InlineData(MotifShape.Arch)]
    [InlineData(MotifShape.Pickup)]
    [InlineData(MotifShape.Riff)]
    public void Every_shape_is_deterministic_bounded_and_has_no_random_triggers(MotifShape shape)
    {
        var generator = new MusicalMotifGenerator();
        var first = generator.Generate(Settings(123, shape));
        var second = generator.Generate(Settings(123, shape));

        Assert.Equal(Signature(first), Signature(second));
        Assert.Equal(64, first.Steps.Length);
        Assert.All(first.Steps, step => Assert.Equal(1, step.Probability.Value));
        Assert.All(first.Steps.SelectMany(step => step.Notes), note =>
            _ = PentatonicPitchResolver.Resolve((MelodicPitch)note.Pitch, first.TonalContext!.Value, MusicalRole.Bass));
        for (var bar = 0; bar < 4; bar++)
            Assert.InRange(first.Steps.Skip(bar * 16).Take(16).Count(step => step.Notes.Length > 0), 3, 8);
        Assert.Equal("0000000000000000", first.Recipe!.Parameters["ghost-mask"].Text);
    }

    [Fact]
    public void Low_variation_repeats_A_exactly_as_A_prime()
    {
        var pattern = new MusicalMotifGenerator().Generate(Settings(456, MotifShape.RootFifth, PhraseLevel.Low));
        Assert.Equal(pattern.Steps.Take(16).Select(Step), pattern.Steps.Skip(16).Take(16).Select(Step));
    }

    [Fact]
    public void Medium_variation_changes_only_a_bounded_part_of_A_prime()
    {
        var pattern = new MusicalMotifGenerator().Generate(Settings(789, MotifShape.Walking));
        var differences = pattern.Steps.Take(16).Select(Step).Zip(pattern.Steps.Skip(16).Take(16).Select(Step))
            .Count(pair => pair.First != pair.Second);
        Assert.InRange(differences, 1, 3);
    }

    [Fact]
    public void Activity_has_clear_density_levels()
    {
        var sparse = new MusicalMotifGenerator().Generate(Settings(1, activity: PhraseActivity.Sparse));
        var medium = new MusicalMotifGenerator().Generate(Settings(1, activity: PhraseActivity.Medium));
        var busy = new MusicalMotifGenerator().Generate(Settings(1, activity: PhraseActivity.Busy));
        Assert.True(Hits(sparse, 0) < Hits(medium, 0));
        Assert.True(Hits(medium, 0) < Hits(busy, 0));
    }

    [Fact]
    public void Seeds_select_multiple_explainable_rhythm_variants_for_the_same_shape()
    {
        var patterns = Enumerable.Range(1, 32)
            .Select(seed => new MusicalMotifGenerator().Generate(Settings((ulong)seed, MotifShape.Riff)))
            .ToArray();

        Assert.True(patterns.Select(pattern => pattern.Recipe!.Parameters["rhythm-variant"].Integer).Distinct().Count() >= 3);
        Assert.True(patterns.Select(pattern => pattern.Recipe!.Parameters["bar-0-mask"].Text).Distinct().Count() >= 3);
    }

    [Fact]
    public void Recipe_reconstruction_and_json_round_trip_preserve_material()
    {
        var generator = new MusicalMotifGenerator();
        var first = generator.Generate(Settings(999, MotifShape.CallResponse));
        var reconstructed = generator.Generate(GeneratorRecipeReconstruction.Motif(first.Name, first.Recipe!));
        Assert.Equal(Signature(first), Signature(reconstructed));
        Assert.Equal(first.Recipe!.Parameters, reconstructed.Recipe!.Parameters);

        var codec = new PatternJsonCodec();
        var decoded = codec.Decode(codec.Encode(first, DateTimeOffset.UnixEpoch)).Pattern;
        Assert.Equal(Signature(first), Signature(decoded));
        Assert.Equal(first.Recipe.Parameters, decoded.Recipe!.Parameters);
    }

    [Theory]
    [InlineData(MusicalRole.Bass, 36, 52)]
    [InlineData(MusicalRole.Middle, 48, 72)]
    [InlineData(MusicalRole.High, 67, 88)]
    public void Every_role_is_deterministic_and_resolves_inside_its_register(
        MusicalRole role, int minimumNote, int maximumNote)
    {
        var settings = Settings(321, role: role);
        var generator = new MusicalMotifGenerator();
        var first = generator.Generate(settings);
        var second = generator.Generate(settings);

        Assert.Equal(role, first.Role);
        Assert.Equal(Signature(first), Signature(second));
        Assert.All(first.Steps.SelectMany(step => step.Notes), note =>
            Assert.InRange(PentatonicPitchResolver.Resolve(
                (MelodicPitch)note.Pitch, first.TonalContext!.Value, role).Value, minimumNote, maximumNote));
    }

    [Fact]
    public void Every_shape_uses_multiple_resolved_pitches_across_roots_and_roles()
    {
        foreach (var role in Enum.GetValues<MusicalRole>())
        foreach (var root in Enumerable.Range(0, 12))
        foreach (var shape in Enum.GetValues<MotifShape>().Where(value => value != MotifShape.Auto))
        {
            var settings = new MotifGeneratorSettings(new PatternName("Movement"),
                new TonalContext(new RootPitchClass(root), PitchPalette.MinorPentatonic), role, shape,
                PhraseActivity.Medium, PhraseLevel.Medium, PhraseLevel.Medium, 123);
            var pattern = new MusicalMotifGenerator().Generate(settings);
            var notes = pattern.Steps.SelectMany(step => step.Notes)
                .Select(note => PentatonicPitchResolver.Resolve((MelodicPitch)note.Pitch,
                    pattern.TonalContext!.Value, role).Value).Distinct();
            Assert.True(notes.Count() > 1, $"{role} {root} {shape} collapsed to one pitch.");
        }
    }

    private static readonly TonalContext Context = new(new RootPitchClass(2), PitchPalette.MinorPentatonic);
    private static MotifGeneratorSettings Settings(ulong seed, MotifShape shape = MotifShape.Auto,
        PhraseLevel variation = PhraseLevel.Medium, PhraseActivity activity = PhraseActivity.Medium,
        MusicalRole role = MusicalRole.Bass) =>
        new(new PatternName("Motif"), Context, role, shape, activity, PhraseLevel.Medium, variation, seed);
    private static int Hits(Pattern pattern, int bar) => pattern.Steps.Skip(bar * 16).Take(16).Count(step => step.Notes.Length > 0);
    private static string Signature(Pattern pattern) => string.Join("|", pattern.Steps.Select(Step));
    private static string Step(PatternStep step) => step.Notes.Length == 0 ? "-" :
        $"{((MelodicPitch)step.Notes[0].Pitch).ScaleDegree}:{((MelodicPitch)step.Notes[0].Pitch).OctaveOffset}:" +
        $"{step.Notes[0].Velocity.Value}:{step.Notes[0].Gate.Value.ToString("0.##", CultureInfo.InvariantCulture)}";
}
