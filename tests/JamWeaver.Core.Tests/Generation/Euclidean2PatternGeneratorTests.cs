
namespace JamWeaver.Core.Tests.Generation;

public sealed class Euclidean2PatternGeneratorTests
{
    [Theory]
    [InlineData(MusicalRole.Bass)]
    [InlineData(MusicalRole.Middle)]
    [InlineData(MusicalRole.High)]
    public void Generates_reproducible_four_bar_patterns_in_the_selected_role(MusicalRole role)
    {
        var generator = new Euclidean2PatternGenerator();
        foreach (var seed in Enumerable.Range(1, 32).Select(value => (ulong)value))
        {
            var settings = Settings(seed, role);
            var first = generator.Generate(settings);
            var second = generator.Generate(settings);

            Assert.Equal(64, first.Steps.Length);
            Assert.Equal(Signature(first), Signature(second));
            Assert.True(first.Steps.SelectMany(step => step.Notes).Select(note => note.Pitch).Distinct().Count() > 1);
            Assert.All(first.Steps, step => Assert.Equal(TriggerProbability.Always, step.Probability));
            Assert.All(first.Steps.SelectMany(step => step.Notes), note => Assert.Contains(note.Pitch,
                PentatonicPitchResolver.ValidPitches(settings.TonalContext, role)));
        }
    }

    [Fact]
    public void Uses_bounded_A_Aprime_B_return_rhythm_development()
    {
        var pattern = new Euclidean2PatternGenerator().Generate(Settings(123, MusicalRole.Bass));
        var bars = Enumerable.Range(0, 4)
            .Select(bar => pattern.Steps.Skip(bar * 16).Take(16).Select(step => step.Notes.Length > 0).ToArray())
            .ToArray();

        Assert.Equal(bars[0], bars[3]);
        Assert.Equal(Pitches(pattern, 0), Pitches(pattern, 3));
        Assert.Equal(bars[0].Count(value => value), bars[1].Count(value => value));
        Assert.InRange(bars[0].Zip(bars[1]).Count(pair => pair.First != pair.Second), 0, 2);
        Assert.NotEqual(bars[0], bars[2]);
        Assert.InRange(Math.Abs(bars[0].Count(value => value) - bars[2].Count(value => value)), 0, 1);
    }

    [Fact]
    public void Rejects_non_four_bar_timing()
    {
        var source = Settings(1, MusicalRole.Bass);
        var settings = new MelodicGeneratorSettings(source.Name, 16, source.Timing, source.TonalContext,
            source.Role, source.Density, source.Movement, source.Repetition, source.Gate,
            source.VelocityVariation, source.Seed);

        Assert.Throws<ArgumentException>(() => new Euclidean2PatternGenerator().Generate(settings));
    }

    [Fact]
    public void Recipe_reconstructs_the_same_pattern()
    {
        var generator = new Euclidean2PatternGenerator();
        var first = generator.Generate(Settings(456, MusicalRole.Middle));
        var reconstructed = GeneratorRecipeReconstruction.Euclidean2(first.Name, first.Recipe!);

        Assert.Equal(Signature(first), Signature(generator.Generate(reconstructed)));
    }

    private static MelodicGeneratorSettings Settings(ulong seed, MusicalRole role) => new(
        new PatternName("Euclidean2"), 64, PatternTiming.SixteenthNotes,
        new TonalContext(new RootPitchClass(2), PitchPalette.MinorPentatonic), role,
        new NormalizedAmount(.4), new NormalizedAmount(.35), new NormalizedAmount(.65),
        new NormalizedAmount(.8), new NormalizedAmount(.15), seed);

    private static string Signature(Pattern pattern) => string.Join("|", pattern.Steps.Select(step =>
        step.Notes.Length == 0 ? "-" : string.Join("+", step.Notes.Select(note =>
            $"{note.Pitch}:{note.Velocity.Value}:{note.Gate.Value}:{step.Probability.Value}"))));

    private static MelodicPitch[] Pitches(Pattern pattern, int bar) => pattern.Steps
        .Skip(bar * 16).Take(16).SelectMany(step => step.Notes).Select(note => (MelodicPitch)note.Pitch).ToArray();
}
