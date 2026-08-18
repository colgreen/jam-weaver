using System.Globalization;
using JamWeaver.Core.Generation.Phrase;
using JamWeaver.Core.Midi;
using JamWeaver.Core.Sequencer;
using Redzen.Random;

namespace JamWeaver.Core.Generation.Groove;

public sealed class MelodicGrooveGenerator
{
    public const string GeneratorId = "melodic-groove-vocabulary";
    public const int GeneratorVersion = 1;

    public Pattern Generate(GrooveGeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var random = RandomDefaults.CreateRandomSource(settings.Seed);
        var templates = settings.Groove == GrooveSelection.Auto ? GrooveVocabulary.Templates
            : GrooveVocabulary.Templates.Where(t => t.Category == Category(settings.Groove)).ToArray();
        var template = templates[random.Next(templates.Count)];
        var a = AdjustActivity(template.Onsets, template, settings.Activity, random);
        var aPrime = RhythmVariationSearch.Find(a, template, GrooveSimilarity.Close, settings.Activity, false, random);
        var b = RhythmVariationSearch.Find(a, template, settings.Similarity, settings.Activity, false, random);
        var turnaroundSimilarity = settings.Turnaround switch
        {
            PhraseTurnaround.None => GrooveSimilarity.Close,
            PhraseTurnaround.Subtle => GrooveSimilarity.Related,
            _ => GrooveSimilarity.Contrast
        };
        var turn = settings.Turnaround == PhraseTurnaround.None
            ? new RhythmVariationResult(aPrime.Mask, RhythmMetrics.Measure(aPrime.Mask, a), "none")
            : RhythmVariationSearch.Find(aPrime.Mask, template, turnaroundSimilarity, settings.Activity, true, random);
        var masks = new[] { a, aPrime.Mask, b.Mask, turn.Mask };
        var valid = PentatonicPitchResolver.ValidPitches(settings.TonalContext, settings.Role);
        var motif = BuildMotif(valid, settings, random, Steps(a).Length);
        var steps = new PatternStep[64]; ulong structuralMask = 0; ulong ghostMask = 0;
        for (var bar = 0; bar < 4; bar++)
        {
            var onsets = Steps(masks[bar]);
            for (var ordinal = 0; ordinal < onsets.Length; ordinal++)
            {
                var step = onsets[ordinal]; var absolute = bar * 16 + step;
                var sourceOrdinal = NearestSourceOrdinal(step, Steps(a));
                var pitch = TransformPitch(motif[sourceOrdinal % motif.Length], valid, bar, ordinal, onsets.Length, random);
                var accent = template.Accents[step];
                var structural = (template.RequiredOnsets & (1 << step)) != 0;
                var ghost = accent == GrooveAccent.Ghost || (!structural && settings.Variation == PhraseLevel.High && ordinal == onsets.Length - 2);
                if (structural) structuralMask |= 1UL << absolute;
                if (ghost) ghostMask |= 1UL << absolute;
                var velocity = Velocity(accent, structural, bar, step, settings.Role);
                var gate = Gate(accent, structural, bar, step);
                steps[absolute] = new PatternStep([new PatternNote(pitch, velocity, gate)], new TriggerProbability(ghost ? .45 : 1));
            }
            for (var step = 0; step < 16; step++) steps[bar * 16 + step] ??= PatternStep.Rest;
        }
        var recipe = Recipe(settings, template, masks, new[] { RhythmMetrics.Measure(a, a), aPrime.Features, b.Features, turn.Features },
            new[] { "none", aPrime.Relaxation, b.Relaxation, turn.Relaxation }, structuralMask, ghostMask);
        return new Pattern(PatternId.New(), settings.Name, PatternSchemaVersion.Current, PatternMode.Melodic,
            PatternTiming.SixteenthNotes, steps, settings.Role, settings.TonalContext, recipe);
    }

    private static ushort AdjustActivity(ushort mask, GrooveTemplate template, PhraseActivity activity, IRandomSource random)
    {
        var target = activity switch { PhraseActivity.Sparse => 4, PhraseActivity.Medium => 6, PhraseActivity.Busy => 8, _ => 6 };
        while (System.Numerics.BitOperations.PopCount(mask) < target)
        {
            var rests = Enumerable.Range(1, 15).Where(s => (mask & (1 << s)) == 0).ToArray();
            mask |= (ushort)(1 << rests[random.Next(rests.Length)]);
        }
        while (System.Numerics.BitOperations.PopCount(mask) > target)
        {
            var remove = Enumerable.Range(1, 15).Where(s => (mask & (1 << s)) != 0 && (template.RequiredOnsets & (1 << s)) == 0).ToArray();
            if (remove.Length == 0) break; mask &= (ushort)~(1 << remove[random.Next(remove.Length)]);
        }
        return mask;
    }

    private static MelodicPitch[] BuildMotif(IReadOnlyList<MelodicPitch> valid, GrooveGeneratorSettings settings, IRandomSource random, int count)
    {
        var motif = new MelodicPitch[count];
        var stable = valid.Where(p => p.ScaleDegree is 0 or 3).ToArray(); motif[0] = stable[random.Next(stable.Length)];
        var maxMove = settings.Movement switch { PhraseLevel.Low => 1, PhraseLevel.Medium => 2, _ => 3 };
        for (var i = 1; i < count; i++)
        {
            var index = Enumerable.Range(0, valid.Count).First(j => valid[j] == motif[i - 1]);
            motif[i] = valid[Math.Clamp(index + random.Next(-maxMove, maxMove + 1), 0, valid.Count - 1)];
        }
        return motif;
    }

    private static MelodicPitch TransformPitch(MelodicPitch pitch, IReadOnlyList<MelodicPitch> valid, int bar, int ordinal, int count, IRandomSource random)
    {
        if (bar == 0 || (bar == 1 && ordinal < count - 1)) return pitch;
        if (bar == 3 && ordinal == count - 1) return valid.Where(p => p.ScaleDegree is 0 or 3)
            .OrderBy(p => Math.Abs(p.OctaveOffset - pitch.OctaveOffset)).First();
        if (bar == 2 && ordinal % 2 == 1)
        {
            var index = Enumerable.Range(0, valid.Count).First(i => valid[i] == pitch);
            return valid[Math.Clamp(index + (random.Next(2) == 0 ? -1 : 1), 0, valid.Count - 1)];
        }
        return pitch;
    }

    private static MidiValue Velocity(GrooveAccent accent, bool structural, int bar, int step, MusicalRole role)
    {
        var offset = accent switch { GrooveAccent.Ghost => -24, GrooveAccent.Light => -10, GrooveAccent.Normal => 0, _ => 10 };
        if (structural) offset += 5; if (bar == 2 && step >= 8) offset += 4; if (bar == 3 && step < 8) offset -= 4;
        return new MidiValue(Math.Clamp(MusicalRoleProfile.For(role).BaseVelocity + offset, 1, 127));
    }
    private static NoteGate Gate(GrooveAccent accent, bool structural, int bar, int step) =>
        new(structural ? .85 : accent == GrooveAccent.Ghost ? .35 : bar == 2 && step >= 8 ? .55 : bar == 3 && step == 15 ? .45 : .68);
    private static int[] Steps(ushort mask) => Enumerable.Range(0, 16).Where(i => (mask & (1 << i)) != 0).ToArray();
    private static int NearestSourceOrdinal(int step, int[] source) => Enumerable.Range(0, source.Length)
        .OrderBy(i => Math.Min(Math.Abs(source[i] - step), 16 - Math.Abs(source[i] - step))).ThenBy(i => i).First();
    private static GrooveCategory Category(GrooveSelection selection) => selection switch
    {
        GrooveSelection.Foundation => GrooveCategory.Foundation, GrooveSelection.Offbeat => GrooveCategory.Offbeat,
        GrooveSelection.Anticipation => GrooveCategory.Anticipation, GrooveSelection.LongShort => GrooveCategory.LongShort,
        GrooveSelection.SparseAnswer => GrooveCategory.SparseAnswer, GrooveSelection.Broken => GrooveCategory.Broken,
        _ => throw new ArgumentOutOfRangeException(nameof(selection))
    };

    private static GeneratorRecipe Recipe(GrooveGeneratorSettings s, GrooveTemplate t, ushort[] masks,
        RhythmFeatureVector[] features, string[] relaxations, ulong structural, ulong ghost)
    {
        var values = new List<KeyValuePair<string, RecipeValue>>
        {
            GenerationHelpers.P("vocabulary-version", GrooveVocabulary.Version), GenerationHelpers.P("metric-version", RhythmMetrics.Version),
            GenerationHelpers.P("template", t.Id), GenerationHelpers.P("groove", s.Groove.ToString()), GenerationHelpers.P("similarity", s.Similarity.ToString()),
            GenerationHelpers.P("activity", s.Activity.ToString()), GenerationHelpers.P("movement", s.Movement.ToString()),
            GenerationHelpers.P("variation", s.Variation.ToString()), GenerationHelpers.P("turnaround", s.Turnaround.ToString()),
            GenerationHelpers.P("root", s.TonalContext.Root.Value), GenerationHelpers.P("palette", s.TonalContext.Palette.ToString()),
            GenerationHelpers.P("role", s.Role.ToString()), GenerationHelpers.P("steps", 64), GenerationHelpers.P("pulses-per-step", PatternTiming.SixteenthNotes.PulsesPerStep),
            GenerationHelpers.P("structural-mask", structural.ToString("X16")), GenerationHelpers.P("ghost-mask", ghost.ToString("X16"))
        };
        for (var i = 0; i < 4; i++)
        {
            values.Add(GenerationHelpers.P($"bar-{i}-mask", masks[i].ToString("X4")));
            values.Add(GenerationHelpers.P($"bar-{i}-features", FeatureText(features[i])));
            values.Add(GenerationHelpers.P($"bar-{i}-relaxation", relaxations[i]));
        }
        return new GeneratorRecipe(GeneratorId, GeneratorVersion, s.Seed, null, values);
    }
    private static string FeatureText(RhythmFeatureVector f) => string.Create(CultureInfo.InvariantCulture,
        $"hits={f.HitCount},sync={f.WeightedSyncopation},gap={f.MaximumRestGap},distance={f.HammingDistance},movement={f.MovementDistance}");
}
