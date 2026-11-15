using System.Security.Cryptography;
using JamWeaver.Core.Persistence;

namespace JamWeaver.Core.Tests.Generation;

public sealed class VariationCompatibilityTests
{
    private static readonly PatternId ParentId = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    private static readonly PatternId VariationId = new(Guid.Parse("20000000-0000-0000-0000-000000000002"));
    private static readonly DateTimeOffset SavedUtc = new(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Target_names_and_numeric_values_remain_stable()
    {
        Assert.Equal(["Rhythm", "Notes", "Expression", "Turnaround", "All"], Enum.GetNames<VariationTarget>());
        Assert.Equal([0, 1, 2, 3, 4], Enum.GetValues<VariationTarget>().Select(target => (int)target));
    }

    [Theory]
    [InlineData(false, VariationTarget.Rhythm, "B141F28CC2864721870D149EA19C5FDA93BA5A6B621E916CE7AD287AA333089C")]
    [InlineData(false, VariationTarget.Notes, "63C7EBB0487B7DF876B5330254378ADC6405E5D8A636B89D01AC8BD9DBFEDC75")]
    [InlineData(false, VariationTarget.Expression, "F9152C0B5A4A2D2A10791F8300BEDB41DD5E08D04DD672D0DA12537EFD06F882")]
    [InlineData(false, VariationTarget.Turnaround, "A0CD40AE84B974480007000237F692FD3F9960AF9F2D08CAADA7C22FF29A787E")]
    [InlineData(false, VariationTarget.All, "6E7E118C17F626F7C77B49A159799410FB6661429E0A9139DE16D53286BF600A")]
    [InlineData(true, VariationTarget.Rhythm, "6DCBD370D6E16057F0734C52D84EE18256AEB83B159E25BA951B7ECA27274E2A")]
    [InlineData(true, VariationTarget.Notes, "D43A694C905A8E9A86E4D3FA752695F1FA5F5D1AC8B83DB471997B6A432E3CA7")]
    [InlineData(true, VariationTarget.Expression, "5D4AED4EE502721E4501D952D3DA076C404594B2115C53D86DE046C02F5848B1")]
    [InlineData(true, VariationTarget.Turnaround, "537936E4402AA91EB677A2A92B648BAA6E96EE2A390B6C5BA88DD9B74B658293")]
    [InlineData(true, VariationTarget.All, "88E57C1CFE5D5AB4DD424120CA7A7BA1AF2DF8D408F96588DC977C09FFACF2BD")]
    public void Targeted_variation_preserves_versioned_material_and_saved_recipe(
        bool turnaround, VariationTarget target, string expectedHash)
    {
        IPatternGenerator<EuclideanGeneratorSettings> generator = turnaround
            ? new TurnaroundLoopGenerator() : new EuclideanMotifGenerator();
        var settings = new EuclideanGeneratorSettings(new PatternName("Variation compatibility"), 16,
            PatternTiming.SixteenthNotes, new TonalContext(new RootPitchClass(2), PitchPalette.MinorPentatonic),
            MusicalRole.Bass, new NormalizedAmount(.5), new NormalizedAmount(.35), new NormalizedAmount(.75),
            new NormalizedAmount(.45), new NormalizedAmount(.25), 123);
        var parent = WithId(generator.Generate(settings), ParentId);
        var codec = new PatternJsonCodec();
        var original = codec.Encode(parent, SavedUtc);

        var varied = generator.Vary(parent, new GeneratorVariationSettings(target, new NormalizedAmount(.8), 456));

        // Only snapshot identities and save time are fixed; all material and recipe data remain authoritative.
        var data = codec.Encode(WithId(varied, VariationId), SavedUtc);
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(data)));
        Assert.Equal(original, codec.Encode(parent, SavedUtc));
        var decoded = codec.Decode(data);
        Assert.Equal(data, codec.Encode(decoded.Pattern, decoded.SavedUtc));
        Assert.Equal(ParentId, decoded.Pattern.Recipe!.ParentPatternId);
        Assert.Equal(generator.Lineage, decoded.Pattern.Recipe.Lineage);
        Assert.Equal(456UL, decoded.Pattern.Recipe.Seed);
        if (!turnaround || target != VariationTarget.All)
            Assert.Equal(target.ToString(), decoded.Pattern.Recipe.Parameters["target"].Text);
    }

    private static Pattern WithId(Pattern pattern, PatternId id) => new(id, pattern.Name, pattern.SchemaVersion,
        pattern.Mode, pattern.Timing, pattern.Steps, pattern.Role, pattern.TonalContext, pattern.Recipe);
}
