using System.Globalization;
using JamWeaver.Core.Generation;
using JamWeaver.Core.Generation.Motif;
using JamWeaver.Core.Generation.Phrase;
using JamWeaver.Core.Persistence;
using JamWeaver.Core.Sequencer;

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

    [Fact]
    public void Non_bass_role_is_rejected() => Assert.Throws<ArgumentException>(() =>
        new MotifGeneratorSettings(new PatternName("bad"), Context, MusicalRole.Middle, MotifShape.Auto,
            PhraseActivity.Medium, PhraseLevel.Medium, PhraseLevel.Medium, 1));

    private static readonly TonalContext Context = new(new RootPitchClass(2), PitchPalette.MinorPentatonic);
    private static MotifGeneratorSettings Settings(ulong seed, MotifShape shape = MotifShape.Auto,
        PhraseLevel variation = PhraseLevel.Medium, PhraseActivity activity = PhraseActivity.Medium) =>
        new(new PatternName("Motif"), Context, MusicalRole.Bass, shape, activity, PhraseLevel.Medium, variation, seed);
    private static int Hits(Pattern pattern, int bar) => pattern.Steps.Skip(bar * 16).Take(16).Count(step => step.Notes.Length > 0);
    private static string Signature(Pattern pattern) => string.Join("|", pattern.Steps.Select(Step));
    private static string Step(PatternStep step) => step.Notes.Length == 0 ? "-" :
        $"{((MelodicPitch)step.Notes[0].Pitch).ScaleDegree}:{((MelodicPitch)step.Notes[0].Pitch).OctaveOffset}:" +
        $"{step.Notes[0].Velocity.Value}:{step.Notes[0].Gate.Value.ToString("0.##", CultureInfo.InvariantCulture)}";
}
