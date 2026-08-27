using JamWeaver.Core.Generation.Phrase;

namespace JamWeaver.Core.Tests.Generation;

public sealed class TurnaroundLoopVariationTests
{
    [Theory]
    [InlineData(PhraseMutationTarget.Rhythm)]
    [InlineData(PhraseMutationTarget.Notes)]
    [InlineData(PhraseMutationTarget.Expression)]
    [InlineData(PhraseMutationTarget.Turnaround)]
    [InlineData(PhraseMutationTarget.All)]
    public void Targeted_variation_is_deterministic_and_preserves_turnaround_loop_form(PhraseMutationTarget target)
    {
        var generator = new TurnaroundLoopGenerator();
        var parent = generator.Generate(Settings(123));
        var settings = new GeneratorVariationSettings(target, new NormalizedAmount(.8), 456);

        var first = generator.Vary(parent, settings);
        var second = generator.Vary(parent, settings);

        Assert.Equal(Signature(first), Signature(second));
        Assert.NotEqual(Signature(parent), Signature(first));
        Assert.Equal(generator.Lineage, first.Recipe!.Lineage);
        Assert.Equal(parent.Id, first.Recipe.ParentPatternId);
        AssertForm(first);
    }

    [Fact]
    public void Low_strength_bare_variation_favors_but_does_not_force_the_turnaround()
    {
        var generator = new TurnaroundLoopGenerator();
        var parent = generator.Generate(Settings(123));

        var variations = Enumerable.Range(1, 32).Select(seed => generator.Vary(parent,
            new GeneratorVariationSettings(null, new NormalizedAmount(.3), (ulong)seed))).ToArray();
        var targets = variations.Select(pattern => pattern.Recipe!.Parameters["target"].Text).ToArray();

        Assert.True(targets.Count(target => target == PhraseMutationTarget.Turnaround.ToString()) >
            targets.Count(target => target == PhraseMutationTarget.Notes.ToString()));
        Assert.Contains(PhraseMutationTarget.Turnaround.ToString(), targets);
        Assert.Contains(PhraseMutationTarget.Notes.ToString(), targets);
        Assert.All(variations, AssertForm);
    }

    [Fact]
    public void Explicit_turnaround_changes_only_the_final_gesture()
    {
        var generator = new TurnaroundLoopGenerator();
        var parent = generator.Generate(Settings(123));

        var varied = generator.Vary(parent,
            new GeneratorVariationSettings(PhraseMutationTarget.Turnaround, new NormalizedAmount(.3), 456));

        Assert.Equal(parent.Steps.Take(56).Select(StepSignature), varied.Steps.Take(56).Select(StepSignature));
        Assert.NotEqual(Signature(parent), Signature(varied));
    }

    [Fact]
    public void Repeated_variation_retains_form_and_lineage()
    {
        var generator = new TurnaroundLoopGenerator();
        var parent = generator.Generate(Settings(123));
        var rhythm = generator.Vary(parent,
            new GeneratorVariationSettings(PhraseMutationTarget.Rhythm, new NormalizedAmount(.8), 456));

        var notes = generator.Vary(rhythm,
            new GeneratorVariationSettings(PhraseMutationTarget.Notes, new NormalizedAmount(.8), 789));

        Assert.Equal(generator.Lineage, notes.Recipe!.Lineage);
        Assert.Equal(rhythm.Id, notes.Recipe.ParentPatternId);
        AssertForm(notes);
    }

    [Fact]
    public void Representative_seeds_produce_different_valid_turnarounds()
    {
        var generator = new TurnaroundLoopGenerator();
        var parent = generator.Generate(Settings(123));

        var variations = Enumerable.Range(1, 12).Select(seed => generator.Vary(parent,
            new GeneratorVariationSettings(PhraseMutationTarget.Turnaround,
                new NormalizedAmount(.3), (ulong)seed))).ToArray();

        Assert.True(variations.Select(Signature).Distinct().Count() > 1);
        Assert.All(variations, AssertForm);
    }

    [Fact]
    public void Strength_increases_the_mirrored_A_edit_budget()
    {
        var generator = new TurnaroundLoopGenerator();
        var parent = generator.Generate(Settings(123));

        var low = generator.Vary(parent,
            new GeneratorVariationSettings(PhraseMutationTarget.Notes, new NormalizedAmount(.3), 456));
        var high = generator.Vary(parent,
            new GeneratorVariationSettings(PhraseMutationTarget.Notes, new NormalizedAmount(.8), 456));

        Assert.Equal(1, low.Recipe!.Parameters["edit-count"].Integer);
        Assert.Equal(2, high.Recipe!.Parameters["edit-count"].Integer);
    }

    [Fact]
    public void Generator_rejects_another_generators_material()
    {
        var parent = new EuclideanMotifGenerator().Generate(Settings(123));

        var error = Assert.Throws<InvalidOperationException>(() => new TurnaroundLoopGenerator().Vary(parent,
            new GeneratorVariationSettings(null, new NormalizedAmount(.3), 456)));

        Assert.Contains("lineage", error.Message);
    }

    private static void AssertForm(Pattern pattern)
    {
        Assert.Equal(64, pattern.Steps.Length);
        for (var index = 0; index < 16; index++)
        {
            Assert.Equal(StepSignature(pattern.Steps[index]), StepSignature(pattern.Steps[index + 16]));
            Assert.Equal(StepSignature(pattern.Steps[index]), StepSignature(pattern.Steps[index + 32]));
            Assert.Equal(pattern.Steps[index].Notes.Length > 0, pattern.Steps[index + 48].Notes.Length > 0);
        }

        var lateOnsets = Enumerable.Range(56, 8).Where(index => pattern.Steps[index].Notes.Length > 0).ToArray();
        Assert.True(lateOnsets.Length >= 2);
        var turnaround = lateOnsets.TakeLast(2).ToHashSet();
        for (var index = 48; index < 64; index++)
        {
            var source = pattern.Steps[index - 48];
            var final = pattern.Steps[index];
            if (!turnaround.Contains(index)) Assert.Equal(StepSignature(source), StepSignature(final));
            else
            {
                Assert.Equal(source.Probability, final.Probability);
                Assert.Equal(source.Notes.Select(note => (note.Velocity, note.Gate)),
                    final.Notes.Select(note => (note.Velocity, note.Gate)));
            }
        }
    }

    private static EuclideanGeneratorSettings Settings(ulong seed) => new(
        new PatternName("Turnaround Loop"), 16, PatternTiming.SixteenthNotes,
        new TonalContext(new RootPitchClass(2), PitchPalette.MinorPentatonic), MusicalRole.Bass,
        new NormalizedAmount(.5), new NormalizedAmount(.35), new NormalizedAmount(.75),
        new NormalizedAmount(.45), new NormalizedAmount(.25), seed);

    private static string Signature(Pattern pattern) => string.Join("|", pattern.Steps.Select(StepSignature));
    private static string StepSignature(PatternStep step) => step.Notes.Length == 0 ? "-" :
        $"{step.Probability.Value:R}:" + string.Join("+", step.Notes.Select(note =>
            $"{note.Pitch}:{note.Velocity.Value}:{note.Gate.Value:R}"));
}
