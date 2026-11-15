using JamWeaver.Core.Generation.Groove;
using JamWeaver.Core.Generation.Phrase;
using Redzen.Random;

namespace JamWeaver.Core.Tests.Generation;

public sealed class GenerationValidationTests
{
    [Theory]
    [InlineData("euclidean")]
    [InlineData("turnaround")]
    [InlineData("drums")]
    public void Undefined_variation_targets_are_rejected_by_generator_entry_points(string family)
    {
        var settings = new EuclideanGeneratorSettings(new PatternName("variation"), 16,
            PatternTiming.SixteenthNotes, default, MusicalRole.Bass, new NormalizedAmount(.4),
            default, default, default, default, 123);
        IPatternGenerator generator;
        Pattern parent;
        if (family == "drums")
        {
            var drums = new EuclideanDrumGenerator();
            generator = drums;
            parent = drums.Generate(new EuclideanDrumGeneratorSettings(new PatternName("drums"), 16,
                PatternTiming.SixteenthNotes, [new DrumVoiceSettings(default, default)], default, default, 123));
        }
        else
        {
            IPatternGenerator<EuclideanGeneratorSettings> melodic = family == "turnaround"
                ? new TurnaroundLoopGenerator() : new EuclideanMotifGenerator();
            generator = melodic;
            parent = melodic.Generate(settings);
        }
        var invalid = new GeneratorVariationSettings((PhraseMutationTarget)999, default, 0);

        Assert.Throws<ArgumentOutOfRangeException>(() => generator.Vary(parent, invalid));
        Assert.Equal(generator.Lineage, parent.Recipe!.Lineage);
    }

    [Fact]
    public void Undefined_groove_accents_are_rejected_when_building_a_template()
    {
        var accents = Enumerable.Repeat(GrooveAccent.Normal, 16).ToArray();
        accents[8] = (GrooveAccent)255;

        Assert.Throws<ArgumentOutOfRangeException>(() => new GrooveTemplate("invalid", GrooveCategory.Foundation,
            1, 0x0110, 0x0110, accents, 0, 0));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Invalid_rhythm_search_controls_are_rejected_without_consuming_random_values(bool invalidSimilarity)
    {
        var random = RandomDefaults.CreateRandomSource(123);
        var template = GrooveVocabulary.Templates[0];

        Assert.Throws<ArgumentOutOfRangeException>(() => RhythmVariationSearch.Find(template.Onsets, template,
            invalidSimilarity ? (GrooveSimilarity)999 : GrooveSimilarity.Close,
            invalidSimilarity ? PhraseActivity.Medium : (PhraseActivity)999, false, random));

        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), random.NextULong());
    }
}
