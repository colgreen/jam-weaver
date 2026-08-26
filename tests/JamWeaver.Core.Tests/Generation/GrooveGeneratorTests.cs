using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JamWeaver.Core.Generation.Groove;
using JamWeaver.Core.Generation.Phrase;
using JamWeaver.Core.Persistence;
using Redzen.Random;

namespace JamWeaver.Core.Tests.Generation;

public sealed class GrooveGeneratorTests
{
    [Fact]
    public void Vocabulary_has_two_valid_stable_templates_per_category()
    {
        Assert.Equal(1, GrooveVocabulary.Version);
        Assert.Equal(12, GrooveVocabulary.Templates.Count);
        Assert.Equal(12, GrooveVocabulary.Templates.Select(t => t.Id).Distinct().Count());
        foreach (var category in Enum.GetValues<GrooveCategory>())
            Assert.Equal(2, GrooveVocabulary.Templates.Count(t => t.Category == category));
        Assert.All(GrooveVocabulary.Templates, template =>
        {
            Assert.NotEqual(0, template.RequiredOnsets & 1);
            Assert.Equal(0, template.RequiredOnsets & template.OptionalOnsets);
            Assert.Equal(0, template.MovableOnsets & ~template.Onsets);
            Assert.InRange(RhythmMetrics.Measure(template.Onsets).HitCount, 3, 8);
        });
    }

    [Fact]
    public void Metric_fixtures_are_exact_and_versioned()
    {
        var empty = RhythmMetrics.Measure(0);
        var quarters = RhythmMetrics.Measure(0x1111);
        var offbeats = RhythmMetrics.Measure(0x4444, 0x1111);

        Assert.Equal(new RhythmFeatureVector(1, 0, 0, 0, 0, 16, 0, 0, 0, 0), empty);
        Assert.Equal(4, quarters.HitCount);
        Assert.Equal(14, quarters.DownbeatStrength);
        Assert.Equal(3, quarters.MaximumRestGap);
        Assert.True(offbeats.WeightedSyncopation > quarters.WeightedSyncopation);
        Assert.Equal(8, offbeats.HammingDistance);
    }

    [Theory]
    [InlineData(0x0001, 0x0002, 1)]
    [InlineData(0x0001, 0x8000, 1)]
    [InlineData(0x0003, 0x0005, 1)]
    [InlineData(0x0001, 0x0003, 4)]
    [InlineData(0x0000, 0x0003, 8)]
    public void Directed_movement_is_exact(int source, int target, int expected) =>
        Assert.Equal(expected, RhythmMetrics.DirectedMovement((ushort)source, (ushort)target));

    [Fact]
    public void Search_is_deterministic_bounded_and_preserves_required_anchors()
    {
        var template = GrooveVocabulary.Get("anticipation-1");
        var first = RhythmVariationSearch.Find(template.Onsets, template, GrooveSimilarity.Related,
            PhraseActivity.Medium, false, RandomDefaults.CreateRandomSource(123));
        var second = RhythmVariationSearch.Find(template.Onsets, template, GrooveSimilarity.Related,
            PhraseActivity.Medium, false, RandomDefaults.CreateRandomSource(123));

        Assert.Equal(first, second);
        Assert.Equal(template.RequiredOnsets, first.Mask & template.RequiredOnsets);
        Assert.InRange(first.Features.HitCount, 3, 8);
        Assert.InRange(first.Features.MaximumRestGap, 0, 8);
        Assert.InRange(first.Features.MaximumOnsetCluster, 1, 4);
    }

    [Theory]
    [InlineData(GrooveSelection.Foundation, 101UL, "04BAAA1DCEACC461988D5593")]
    [InlineData(GrooveSelection.Offbeat, 102UL, "B3B542839CE36151BF81E60F")]
    [InlineData(GrooveSelection.Anticipation, 103UL, "8A4DEF198137B9D2A5ABE82C")]
    [InlineData(GrooveSelection.LongShort, 104UL, "4C451FD076C30A3FDBEDB27A")]
    [InlineData(GrooveSelection.SparseAnswer, 105UL, "C768E1FA6C9BC45ED9046ACF")]
    [InlineData(GrooveSelection.Broken, 106UL, "847749967CA93E7E88470F9E")]
    public void Category_snapshot(GrooveSelection selection, ulong seed, string expected)
    {
        var pattern = Generate(seed, selection);
        Assert.Equal(expected, Hash(Signature(pattern))[..24]);
    }

    [Fact]
    public void Fixed_seed_matrix_preserves_bass_range_anchors_and_expression_contracts()
    {
        foreach (var selection in Enum.GetValues<GrooveSelection>())
        foreach (var activity in Enum.GetValues<PhraseActivity>())
        foreach (var seed in Enumerable.Range(1, 20).Select(i => (ulong)i))
        {
            var pattern = Generate(seed, selection, activity);
            Assert.Equal(64, pattern.Steps.Length);
            for (var bar = 0; bar < 4; bar++)
            {
                var barSteps = pattern.Steps.Skip(bar * 16).Take(16).ToArray();
                Assert.NotEmpty(barSteps[0].Notes);
                Assert.InRange(barSteps.Count(s => s.Notes.Length > 0), 3, 8);
            }
            Assert.All(pattern.Steps.SelectMany(s => s.Notes), note =>
                _ = PentatonicPitchResolver.Resolve((MelodicPitch)note.Pitch, pattern.TonalContext!.Value, MusicalRole.Bass));
            var structural = Mask(pattern, "structural-mask");
            for (var i = 0; i < 64; i++) if ((structural & (1UL << i)) != 0)
            {
                Assert.Equal(1, pattern.Steps[i].Probability.Value);
                Assert.True(pattern.Steps[i].Notes[0].Gate.Value >= .8);
            }
        }
    }

    [Fact]
    public void Recipe_reconstruction_and_json_round_trip_are_exact()
    {
        var generator = new MelodicGrooveGenerator();
        var first = Generate(999, GrooveSelection.Broken);
        var reconstructed = generator.Generate(GeneratorRecipeReconstruction.Groove(first.Name, first.Recipe!));
        Assert.Equal(Signature(first), Signature(reconstructed));
        Assert.Equal(first.Recipe!.Parameters, reconstructed.Recipe!.Parameters);

        var codec = new PatternJsonCodec();
        var decoded = codec.Decode(codec.Encode(first, new DateTimeOffset(2026, 8, 9, 0, 0, 0, TimeSpan.Zero))).Pattern;
        Assert.Equal(first.Recipe.Parameters, decoded.Recipe!.Parameters);
        Assert.Equal(Signature(first), Signature(decoded));
    }

    [Fact]
    public void Non_bass_role_is_rejected_clearly() => Assert.Throws<ArgumentException>(() =>
        Settings(1, GrooveSelection.Auto, PhraseActivity.Medium, MusicalRole.Middle));

    private static Pattern Generate(ulong seed, GrooveSelection selection, PhraseActivity activity = PhraseActivity.Medium) =>
        new MelodicGrooveGenerator().Generate(Settings(seed, selection, activity));
    private static GrooveGeneratorSettings Settings(ulong seed, GrooveSelection selection, PhraseActivity activity,
        MusicalRole role = MusicalRole.Bass) => new(new PatternName("Groove"),
        new TonalContext(new RootPitchClass(2), PitchPalette.MinorPentatonic), role, selection,
        GrooveSimilarity.Related, activity, PhraseLevel.Medium, PhraseLevel.Medium, PhraseTurnaround.Subtle, seed);
    private static ulong Mask(Pattern pattern, string key) => ulong.Parse(pattern.Recipe!.Parameters[key].Text!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    private static string Signature(Pattern pattern) => string.Join("|", pattern.Steps.Select(step => step.Notes.Length == 0 ? "-" :
        $"{((MelodicPitch)step.Notes[0].Pitch).ScaleDegree}:{((MelodicPitch)step.Notes[0].Pitch).OctaveOffset}:" +
        $"{step.Notes[0].Velocity.Value}:{step.Notes[0].Gate.Value.ToString("0.##", CultureInfo.InvariantCulture)}:{step.Probability.Value.ToString("0.##", CultureInfo.InvariantCulture)}"));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
