using JamWeaver.Core.Midi;
using JamWeaver.Core.Sequencer;
using Redzen.Random;

namespace JamWeaver.Core.Generation.Phrase;

public sealed class MelodicPhraseGenerator
{
    public const string GeneratorId = "melodic-structured-phrase";
    public const int GeneratorVersion = 1;
    private const double Gate = .8;
    private const double VelocityVariation = .15;

    public Pattern Generate(PhraseGeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var random = RandomDefaults.CreateRandomSource(settings.Seed);
        var hitTarget = HitTarget(settings);
        var bars = new bool[settings.BarCount][];
        bars[0] = BuildRhythm(settings.Rhythm, hitTarget, random);
        if (settings.BarCount == 2)
            bars[1] = BuildTurnaround(bars[0], settings, random);
        else if (settings.BarCount == 4)
        {
            bars[1] = VaryRhythm(bars[0], VariationEdits(settings.Variation), settings.Rhythm, random);
            bars[2] = BuildContrast(bars[0], settings, hitTarget, random);
            bars[3] = BuildTurnaround(bars[1], settings, random);
        }

        var validPitches = PentatonicPitchResolver.ValidPitches(settings.TonalContext, settings.Role);
        var motif = BuildMotif(validPitches, settings, random);
        var structuralMask = 0UL;
        var ghostMask = 0UL;
        var steps = new PatternStep[settings.StepCount];
        for (var bar = 0; bar < bars.Length; bar++)
        {
            var hits = bars[bar].Select((hit, step) => (hit, step)).Where(value => value.hit).Select(value => value.step).ToArray();
            var ghostStep = ChooseGhostStep(bar, hits, settings, random);
            for (var step = 0; step < 16; step++)
            {
                var absoluteStep = (bar * 16) + step;
                if (!bars[bar][step]) { steps[absoluteStep] = PatternStep.Rest; continue; }
                var structural = step == 0 || (step == 8 && bars[0][8]);
                var ghost = step == ghostStep;
                if (structural) structuralMask |= 1UL << absoluteStep;
                if (ghost) ghostMask |= 1UL << absoluteStep;
                var hitIndex = Array.IndexOf(hits, step);
                var pitch = PhrasePitch(motif, validPitches, bar, hitIndex, hits.Length, settings, random);
                var expression = Expression(settings.Role, structural, ghost, step == 15, random);
                var note = new PatternNote(pitch, expression.Velocity, expression.Gate);
                steps[absoluteStep] = new PatternStep([note], new TriggerProbability(ghost ? expression.Probability : 1));
            }
        }

        var recipe = new GeneratorRecipe(GeneratorId, GeneratorVersion, settings.Seed, null,
        [
            GenerationHelpers.P("length", settings.Length.ToString()),
            GenerationHelpers.P("activity", settings.Activity.ToString()),
            GenerationHelpers.P("rhythm", settings.Rhythm.ToString()),
            GenerationHelpers.P("movement", settings.Movement.ToString()),
            GenerationHelpers.P("variation", settings.Variation.ToString()),
            GenerationHelpers.P("turnaround", settings.Turnaround.ToString()),
            GenerationHelpers.P("root", settings.TonalContext.Root.Value),
            GenerationHelpers.P("palette", settings.TonalContext.Palette.ToString()),
            GenerationHelpers.P("role", settings.Role.ToString()),
            GenerationHelpers.P("steps", settings.StepCount),
            GenerationHelpers.P("pulses-per-step", PatternTiming.SixteenthNotes.PulsesPerStep),
            GenerationHelpers.P("gate", Gate),
            GenerationHelpers.P("velocity-variation", VelocityVariation),
            GenerationHelpers.P("hits-per-a-bar", hitTarget),
            GenerationHelpers.P("motif-length", motif.Length),
            GenerationHelpers.P("bar-roles", BarRoles(settings.BarCount)),
            GenerationHelpers.P("structural-mask", structuralMask.ToString("X16")),
            GenerationHelpers.P("ghost-mask", ghostMask.ToString("X16"))
        ]);
        return new Pattern(PatternId.New(), settings.Name, PatternSchemaVersion.Current, PatternMode.Melodic,
            PatternTiming.SixteenthNotes, steps, settings.Role, settings.TonalContext, recipe);
    }

    private static int HitTarget(PhraseGeneratorSettings settings)
    {
        var amount = settings.Activity switch
        {
            PhraseActivity.Sparse => NormalizedAmount.Low,
            PhraseActivity.Medium => NormalizedAmount.Medium,
            PhraseActivity.Busy => NormalizedAmount.High,
            _ => throw new ArgumentOutOfRangeException()
        };
        return MusicalRoleProfile.For(settings.Role).HitCount(16, amount);
    }

    private static bool[] BuildRhythm(PhraseRhythm character, int hits, IRandomSource random)
    {
        var rhythm = new bool[16];
        rhythm[0] = true;
        foreach (var step in Enumerable.Range(1, 15)
                     .OrderByDescending(step => random.NextDouble() * RhythmWeight(character, step))
                     .Take(hits - 1)) rhythm[step] = true;
        return rhythm;
    }

    private static double RhythmWeight(PhraseRhythm character, int step) => character switch
    {
        PhraseRhythm.Steady => step % 4 == 0 ? 5 : step % 2 == 0 ? 3 : 1,
        PhraseRhythm.Syncopated => step % 2 == 1 ? 4 : step % 4 == 2 ? 3 : 1.5,
        PhraseRhythm.Broken => step is 3 or 6 or 10 or 13 or 15 ? 3 : 1.5,
        _ => throw new ArgumentOutOfRangeException(nameof(character))
    };

    private static bool[] VaryRhythm(bool[] source, int edits, PhraseRhythm character, IRandomSource random)
    {
        var result = (bool[])source.Clone();
        for (var edit = 0; edit < edits; edit++)
        {
            var movable = Enumerable.Range(1, 15).Where(step => result[step]).ToArray();
            var rests = Enumerable.Range(1, 15).Where(step => !result[step]).ToArray();
            if (movable.Length == 0 || rests.Length == 0) break;
            var from = movable[random.Next(movable.Length)];
            var target = rests.OrderByDescending(step => random.NextDouble() * RhythmWeight(character, step)).First();
            result[from] = false;
            result[target] = true;
        }
        return result;
    }

    private static bool[] BuildContrast(bool[] identity, PhraseGeneratorSettings settings, int hits, IRandomSource random)
    {
        var contrast = BuildRhythm(settings.Rhythm, hits, random);
        contrast[0] = true;
        if (identity[8]) contrast[8] = true;
        while (contrast.Count(value => value) > hits)
        {
            var removable = Enumerable.Range(1, 15).Where(step => step != 8 && contrast[step]).ToArray();
            contrast[removable[random.Next(removable.Length)]] = false;
        }
        return contrast;
    }

    private static bool[] BuildTurnaround(bool[] source, PhraseGeneratorSettings settings, IRandomSource random)
    {
        if (settings.Turnaround == PhraseTurnaround.None) return (bool[])source.Clone();
        var edits = settings.Turnaround == PhraseTurnaround.Subtle ? 1 : 3;
        var result = VaryRhythm(source, edits, settings.Rhythm, random);
        if (settings.Turnaround == PhraseTurnaround.Strong)
        {
            result[15] = true;
            var sounding = Enumerable.Range(1, 14).Where(step => result[step]).ToArray();
            if (result.Count(value => value) > source.Count(value => value) && sounding.Length > 0)
                result[sounding[random.Next(sounding.Length)]] = false;
        }
        return result;
    }

    private static MelodicPitch[] BuildMotif(IReadOnlyList<MelodicPitch> valid, PhraseGeneratorSettings settings, IRandomSource random)
    {
        var length = settings.Variation switch { PhraseLevel.Low => 3, PhraseLevel.Medium => 4, PhraseLevel.High => 6, _ => 4 };
        var preferred = settings.Role == MusicalRole.Bass ? valid.Where(pitch => pitch.ScaleDegree is 0 or 3).ToArray() : valid.ToArray();
        var current = preferred[random.Next(preferred.Length)];
        var motif = new MelodicPitch[length];
        motif[0] = current;
        var maximumMove = settings.Movement switch { PhraseLevel.Low => 1, PhraseLevel.Medium => 2, PhraseLevel.High => 3, _ => 2 };
        for (var index = 1; index < motif.Length; index++)
        {
            var currentIndex = Enumerable.Range(0, valid.Count).First(i => valid[i] == current);
            var delta = random.Next(-maximumMove, maximumMove + 1);
            current = valid[Math.Clamp(currentIndex + delta, 0, valid.Count - 1)];
            motif[index] = current;
        }
        return motif;
    }

    private static MelodicPitch PhrasePitch(MelodicPitch[] motif, IReadOnlyList<MelodicPitch> valid,
        int bar, int hitIndex, int hitCount, PhraseGeneratorSettings settings, IRandomSource random)
    {
        var motifIndex = hitIndex % motif.Length;
        var pitch = motif[motifIndex];
        var shouldVary = bar switch
        {
            1 => hitIndex == hitCount - 1,
            2 => hitIndex % 2 == 1,
            3 => hitIndex == hitCount - 1,
            _ => false
        };
        if (!shouldVary) return pitch;
        if (bar == 3 && hitIndex == hitCount - 1)
        {
            var stable = valid.Where(value => value.ScaleDegree is 0 or 3).ToArray();
            return stable.OrderBy(value => Math.Abs(value.OctaveOffset - pitch.OctaveOffset)).First();
        }
        var index = Enumerable.Range(0, valid.Count).First(i => valid[i] == pitch);
        var direction = random.Next(2) == 0 ? -1 : 1;
        return valid[Math.Clamp(index + direction, 0, valid.Count - 1)];
    }

    private static ExpressionValue Expression(MusicalRole role, bool structural, bool ghost, bool pickup, IRandomSource random)
    {
        var profile = MusicalRoleProfile.For(role);
        var baseVelocity = profile.BaseVelocity + (structural ? 8 : ghost ? -18 : pickup ? -10 : 0);
        var velocity = new MidiValue(Math.Clamp(baseVelocity + random.Next(-4, 5), 1, 127));
        var gate = new NoteGate(structural ? .85 : pickup || ghost ? .45 : .7);
        return new ExpressionValue(velocity, gate, ghost ? .45 : 1);
    }

    private static int ChooseGhostStep(int bar, int[] hits, PhraseGeneratorSettings settings, IRandomSource random)
    {
        if (bar == 0 || settings.Variation == PhraseLevel.Low) return -1;
        var eligible = hits.Where(step => step != 0 && step != 8).ToArray();
        return eligible.Length == 0 ? -1 : eligible[random.Next(eligible.Length)];
    }

    private static int VariationEdits(PhraseLevel level) => level switch { PhraseLevel.Low => 1, PhraseLevel.Medium => 2, PhraseLevel.High => 3, _ => 2 };
    private static string BarRoles(int bars) => bars switch { 1 => "A", 2 => "A,T", 4 => "A,A-prime,B,T", _ => throw new ArgumentOutOfRangeException(nameof(bars)) };
    private readonly record struct ExpressionValue(MidiValue Velocity, NoteGate Gate, double Probability);
}
