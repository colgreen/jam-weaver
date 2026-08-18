using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using JamWeaver.Core.Generation;
using JamWeaver.Core.Generation.Phrase;
using JamWeaver.Core.Performance;
using JamWeaver.Core.Sequencer;

namespace JamWeaver.Core.Tests.Generation;

public sealed class PhraseGeneratorTests
{
    [Theory]
    [InlineData(MusicalRole.Bass, PhraseRhythm.Steady, 101UL, "97245F3E8C3887D13FEB5B5ABFF0545BDA181CC9F81660BE")]
    [InlineData(MusicalRole.Bass, PhraseRhythm.Syncopated, 102UL, "BDD113582694D7BD07E5FA68D92D7EC8881AF13D4F93BA8E")]
    [InlineData(MusicalRole.Bass, PhraseRhythm.Broken, 103UL, "CEB2963613E936496ED876D7D213EAD2EB43D9EB15B2B3A9")]
    [InlineData(MusicalRole.Middle, PhraseRhythm.Steady, 201UL, "F3C5E1BF1C410EB3E1A10FAD568FB5766955B87ED977BFAC")]
    [InlineData(MusicalRole.Middle, PhraseRhythm.Syncopated, 202UL, "26265C781400ECE309C15CEA3F5503FCA897EF605603D236")]
    [InlineData(MusicalRole.Middle, PhraseRhythm.Broken, 203UL, "1E8417A8E72C186D7E2E4BFC8545FF459ED69C575143838B")]
    [InlineData(MusicalRole.High, PhraseRhythm.Steady, 301UL, "647171608CD8E0AE2793CF5B0AD5202D16F907E345EF199E")]
    [InlineData(MusicalRole.High, PhraseRhythm.Syncopated, 302UL, "359EBE6CE28E7025E6C4A79B3195CA1067F77A05272631FC")]
    [InlineData(MusicalRole.High, PhraseRhythm.Broken, 303UL, "69E0D95B065B76E9B062E84713B3F4F1CE30DD2B12CD135C")]
    public void Role_and_rhythm_snapshot(MusicalRole role, PhraseRhythm rhythm, ulong seed, string expectedHash)
    {
        var pattern = new MelodicPhraseGenerator().Generate(Settings(seed, role, rhythm));
        Assert.Equal(expectedHash, Hash(Signature(pattern))[..48]);
    }

    [Theory]
    [InlineData(PhraseLength.OneBar, 16)]
    [InlineData(PhraseLength.TwoBars, 32)]
    [InlineData(PhraseLength.FourBars, 64)]
    public void Lengths_have_expected_steps_and_each_bar_sounds(PhraseLength length, int steps)
    {
        var pattern = new MelodicPhraseGenerator().Generate(Settings(42, length: length));

        Assert.Equal(steps, pattern.Steps.Length);
        for (var bar = 0; bar < (int)length; bar++)
            Assert.Contains(pattern.Steps.Skip(bar * 16).Take(16), step => step.Notes.Length > 0);
    }

    [Fact]
    public void Generation_and_recipe_reconstruction_are_reproducible()
    {
        var generator = new MelodicPhraseGenerator();
        var first = generator.Generate(Settings(123));
        var reconstructed = GeneratorRecipeReconstruction.Phrase(first.Name, first.Recipe!);
        var second = generator.Generate(reconstructed);

        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(Signature(first), Signature(second));
        Assert.Equal(first.Recipe!.Parameters, second.Recipe!.Parameters);
        Assert.Equal(PatternTriggerKey.Create(first), PatternTriggerKey.Create(second));
    }

    [Fact]
    public void Structural_hits_always_play_and_ghosts_are_sparse()
    {
        var pattern = new MelodicPhraseGenerator().Generate(Settings(456));
        var structural = Mask(pattern, "structural-mask");
        var ghost = Mask(pattern, "ghost-mask");

        Assert.NotEqual(0UL, structural);
        Assert.InRange(System.Numerics.BitOperations.PopCount(ghost), 1, 3);
        for (var step = 0; step < pattern.Steps.Length; step++)
        {
            if ((structural & (1UL << step)) != 0) Assert.Equal(1, pattern.Steps[step].Probability.Value);
            if ((ghost & (1UL << step)) != 0) Assert.InRange(pattern.Steps[step].Probability.Value, .2, .8);
        }
    }

    [Fact]
    public void Role_ranges_and_phrase_identity_hold_across_fixed_seed_matrix()
    {
        foreach (var role in Enum.GetValues<MusicalRole>())
        foreach (var palette in Enum.GetValues<PitchPalette>())
        foreach (var root in new[] { 0, 5, 11 })
        foreach (var seed in Enumerable.Range(1, 20).Select(value => (ulong)value))
        {
            var settings = new PhraseGeneratorSettings(new PatternName("matrix"), PhraseLength.FourBars,
                new TonalContext(new RootPitchClass(root), palette), role, PhraseActivity.Medium,
                PhraseRhythm.Syncopated, PhraseLevel.Medium, PhraseLevel.Medium, PhraseTurnaround.Subtle, seed);
            var pattern = new MelodicPhraseGenerator().Generate(settings);
            Assert.All(pattern.Steps.SelectMany(step => step.Notes), note =>
                _ = PentatonicPitchResolver.Resolve((MelodicPitch)note.Pitch, settings.TonalContext, role));
            var a = Sounding(pattern, 0);
            var aPrime = Sounding(pattern, 1);
            Assert.True(a.Intersect(aPrime).Count() >= Math.Max(1, a.Count / 2));
            Assert.Contains(0, a);
            Assert.Contains(0, aPrime);
        }
    }

    [Theory]
    [InlineData(PhraseMutationTarget.Rhythm)]
    [InlineData(PhraseMutationTarget.Notes)]
    [InlineData(PhraseMutationTarget.Expression)]
    [InlineData(PhraseMutationTarget.Turnaround)]
    public void Targeted_mutation_changes_content_and_preserves_structural_steps(PhraseMutationTarget target)
    {
        var parent = new MelodicPhraseGenerator().Generate(Settings(999));
        var mutated = new PhrasePatternMutator().Mutate(parent,
            new PhraseMutationSettings(target, NormalizedAmount.High, 1234));
        var structural = Mask(parent, "structural-mask");

        Assert.NotEqual(Signature(parent), Signature(mutated));
        Assert.Equal(parent.Id, mutated.Recipe!.ParentPatternId);
        for (var step = 0; step < parent.Steps.Length; step++)
            if ((structural & (1UL << step)) != 0) Assert.Equal(StepSignature(parent.Steps[step]), StepSignature(mutated.Steps[step]));
        if (target == PhraseMutationTarget.Notes || target == PhraseMutationTarget.Expression)
            Assert.Equal(parent.Steps.Select(step => step.Notes.Length > 0), mutated.Steps.Select(step => step.Notes.Length > 0));
        if (target == PhraseMutationTarget.Turnaround)
            Assert.Equal(parent.Steps.Take(parent.Steps.Length - 16).Select(StepSignature),
                mutated.Steps.Take(mutated.Steps.Length - 16).Select(StepSignature));
    }

    private static PhraseGeneratorSettings Settings(ulong seed, MusicalRole role = MusicalRole.Bass,
        PhraseRhythm rhythm = PhraseRhythm.Syncopated, PhraseLength length = PhraseLength.FourBars) =>
        new(new PatternName("Phrase"), length, new TonalContext(new RootPitchClass(2), PitchPalette.MinorPentatonic),
            role, PhraseActivity.Medium, rhythm, PhraseLevel.Medium, PhraseLevel.Medium, PhraseTurnaround.Subtle, seed);

    private static HashSet<int> Sounding(Pattern pattern, int bar) => pattern.Steps.Skip(bar * 16).Take(16)
        .Select((step, index) => (step, index)).Where(value => value.step.Notes.Length > 0).Select(value => value.index).ToHashSet();

    private static ulong Mask(Pattern pattern, string key) => ulong.Parse(pattern.Recipe!.Parameters[key].Text!, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    private static string Signature(Pattern pattern) => string.Join("|", pattern.Steps.Select(StepSignature));
    private static string StepSignature(PatternStep step) => step.Notes.Length == 0 ? "-" :
        $"p{step.Probability.Value.ToString("0.###", CultureInfo.InvariantCulture)}:" + string.Join("+", step.Notes.Select(note =>
        {
            var pitch = (MelodicPitch)note.Pitch;
            return $"{pitch.ScaleDegree}:{pitch.OctaveOffset}:{pitch.ChromaticOffset},v{note.Velocity.Value},g{note.Gate.Value.ToString("0.###", CultureInfo.InvariantCulture)}";
        }));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
