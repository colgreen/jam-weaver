using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace JamWeaver.Core.Tests.Generation;

public sealed class TurnaroundLoopVariationTests
{
    [Theory]
    [InlineData(MusicalRole.Bass, PitchPalette.MinorPentatonic, 123UL, "F4403DF206159714028AF76C916B5127EF7A0B43C109A81700D6F87680372F87")]
    [InlineData(MusicalRole.Middle, PitchPalette.MajorPentatonic, 456UL, "3CBA47AA13635EAEAEECE9CC66C0E3293E28957CB8126DBF8D454A7A3D092B4D")]
    [InlineData(MusicalRole.High, PitchPalette.MinorPentatonic, 789UL, "6C7D1D7CA73B7DC3B8A7B8C0484A5B5E0816E6841539E5A75327330EDE7CC46E")]
    public void Supported_generation_and_variation_preserve_versioned_output(
        MusicalRole role, PitchPalette palette, ulong seed, string expectedHash)
    {
        var generator = new TurnaroundLoopGenerator();
        var source = Settings(seed);
        var settings = new EuclideanGeneratorSettings(source.Name, source.StepCount, source.Timing,
            new TonalContext(source.TonalContext.Root, palette), role, source.Density, source.Movement,
            source.Repetition, source.Gate, source.VelocityVariation, seed);
        var generated = generator.Generate(settings);
        var varied = generator.Vary(generated,
            new GeneratorVariationSettings(VariationTarget.All, new NormalizedAmount(.8), 456));

        var signature = string.Join("|", new[] { generated, varied }.Select(pattern =>
            string.Join(";", pattern.Steps.Select(step =>
                step.Probability.Value.ToString("R", CultureInfo.InvariantCulture) + ":" +
                string.Join(",", step.Notes.Select(note =>
                {
                    var pitch = (MelodicPitch)note.Pitch;
                    return FormattableString.Invariant(
                        $"{pitch.ScaleDegree}/{pitch.OctaveOffset}/{pitch.ChromaticOffset}/{note.Velocity.Value}/{note.Gate.Value:R}");
                }))))));
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))));
        AssertForm(generated);
        AssertForm(varied);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(12)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(96)]
    public void Generation_and_variation_reject_timing_outside_the_four_bar_sixteenth_note_form(int pulsesPerStep)
    {
        var generator = new TurnaroundLoopGenerator();
        var source = Settings(123);
        var timing = new PatternTiming(pulsesPerStep);
        var settings = new EuclideanGeneratorSettings(source.Name, source.StepCount, timing, source.TonalContext,
            source.Role, source.Density, source.Movement, source.Repetition, source.Gate, source.VelocityVariation, source.Seed);

        var generationError = Assert.Throws<ArgumentException>(() => generator.Generate(settings));

        Assert.Equal("settings", generationError.ParamName);
        Assert.Contains("sixteenth", generationError.Message);
        var supported = generator.Generate(source);
        // Retimed material also models incompatible snapshots saved before entry-point validation.
        var incompatible = new Pattern(supported.Id, supported.Name, supported.SchemaVersion, supported.Mode,
            timing, supported.Steps, supported.Role, supported.TonalContext, supported.Recipe);
        var variationError = Assert.Throws<InvalidOperationException>(() => generator.Vary(incompatible,
            new GeneratorVariationSettings(null, new NormalizedAmount(.3), 456)));
        Assert.Contains("four-bar melodic sixteenth-note", variationError.Message);
    }

    [Theory]
    [InlineData(VariationTarget.Rhythm)]
    [InlineData(VariationTarget.Notes)]
    [InlineData(VariationTarget.Expression)]
    [InlineData(VariationTarget.Turnaround)]
    [InlineData(VariationTarget.All)]
    public void Targeted_variation_is_deterministic_and_preserves_turnaround_loop_form(VariationTarget target)
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

        Assert.True(targets.Count(target => target == VariationTarget.Turnaround.ToString()) >
            targets.Count(target => target == VariationTarget.Notes.ToString()));
        Assert.Contains(VariationTarget.Turnaround.ToString(), targets);
        Assert.Contains(VariationTarget.Notes.ToString(), targets);
        Assert.All(variations, AssertForm);
    }

    [Fact]
    public void Explicit_turnaround_changes_only_the_final_gesture()
    {
        var generator = new TurnaroundLoopGenerator();
        var parent = generator.Generate(Settings(123));

        var varied = generator.Vary(parent,
            new GeneratorVariationSettings(VariationTarget.Turnaround, new NormalizedAmount(.3), 456));

        Assert.Equal(parent.Steps.Take(56).Select(StepSignature), varied.Steps.Take(56).Select(StepSignature));
        Assert.NotEqual(Signature(parent), Signature(varied));
    }

    [Fact]
    public void Repeated_variation_retains_form_and_lineage()
    {
        var generator = new TurnaroundLoopGenerator();
        var parent = generator.Generate(Settings(123));
        var rhythm = generator.Vary(parent,
            new GeneratorVariationSettings(VariationTarget.Rhythm, new NormalizedAmount(.8), 456));

        var notes = generator.Vary(rhythm,
            new GeneratorVariationSettings(VariationTarget.Notes, new NormalizedAmount(.8), 789));

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
            new GeneratorVariationSettings(VariationTarget.Turnaround,
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
            new GeneratorVariationSettings(VariationTarget.Notes, new NormalizedAmount(.3), 456));
        var high = generator.Vary(parent,
            new GeneratorVariationSettings(VariationTarget.Notes, new NormalizedAmount(.8), 456));

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
        Assert.Equal(PatternTiming.SixteenthNotes, pattern.Timing);
        Assert.Equal(4 * 96, pattern.Steps.Length * pattern.Timing.PulsesPerStep);
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
