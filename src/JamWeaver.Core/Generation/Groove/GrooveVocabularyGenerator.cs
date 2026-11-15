using System.Globalization;
using JamWeaver.Core.Generation.Phrase;
using Redzen.Random;

namespace JamWeaver.Core.Generation.Groove;

/// <summary>Builds four-bar bass patterns from the versioned groove vocabulary and constrained rhythm variations.</summary>
public sealed class GrooveVocabularyGenerator : IPatternGenerator<GrooveGeneratorSettings>
{
    public const string GeneratorId = "melodic-groove-vocabulary";
    public const int GeneratorVersion = 1;

    private const int StepsPerBar = 16;
    private const int BarCount = 4;
    private const int StepCount = StepsPerBar * BarCount;
    private const int HalfBarSteps = StepsPerBar / 2;

    public GeneratorLineage Lineage => new(GeneratorId, GeneratorVersion);

    public Pattern Vary(Pattern parent, GeneratorVariationSettings settings) =>
        CurrentGeneratorVariation.Vary(parent, settings, Lineage);

    public Pattern Generate(GrooveGeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var random = RandomDefaults.CreateRandomSource(settings.Seed);
        var templates = settings.Groove == GrooveSelection.Auto
            ? GrooveVocabulary.Templates
            : GrooveVocabulary.Templates.Where(template => template.Category == ResolveCategory(settings.Groove)).ToArray();
        var template = templates[random.Next(templates.Count)];
        var sourceMask = AdjustActivity(template.Onsets, template, settings.Activity, random);
        var aPrime = RhythmVariationSearch.Find(sourceMask, template, GrooveSimilarity.Close, settings.Activity, false, random);
        var contrast = RhythmVariationSearch.Find(sourceMask, template, settings.Similarity, settings.Activity, false, random);
        var turnaroundSimilarity = settings.Turnaround switch
        {
            PhraseTurnaround.None => GrooveSimilarity.Close,
            PhraseTurnaround.Subtle => GrooveSimilarity.Related,
            _ => GrooveSimilarity.Contrast
        };
        var turnaround = settings.Turnaround == PhraseTurnaround.None
            ? new RhythmVariationResult(aPrime.Mask, RhythmMetrics.Measure(aPrime.Mask, sourceMask), "none")
            : RhythmVariationSearch.Find(aPrime.Mask, template, turnaroundSimilarity, settings.Activity, true, random);
        var barMasks = new[] { sourceMask, aPrime.Mask, contrast.Mask, turnaround.Mask };
        var validPitches = PentatonicPitchResolver.ValidPitches(settings.TonalContext, settings.Role);
        var sourceOnsets = Onsets(sourceMask);
        var motif = BuildMotif(validPitches, settings, random, sourceOnsets.Length);
        var steps = new PatternStep[StepCount];
        ulong structuralMask = 0;
        ulong ghostMask = 0;
        for (var bar = 0; bar < BarCount; bar++)
        {
            var onsets = Onsets(barMasks[bar]);
            for (var ordinal = 0; ordinal < onsets.Length; ordinal++)
            {
                var step = onsets[ordinal];
                var absoluteStep = bar * StepsPerBar + step;
                var sourceOrdinal = NearestSourceOrdinal(step, sourceOnsets);
                var pitch = TransformPitch(motif[sourceOrdinal % motif.Length], validPitches, bar, ordinal, onsets.Length, random);
                var accent = template.Accents[step];
                var isStructural = (template.RequiredOnsets & (1 << step)) != 0;
                var additionalGhost = !isStructural && settings.Variation == PhraseLevel.High && ordinal == onsets.Length - 2;
                var isGhost = accent == GrooveAccent.Ghost || additionalGhost;
                if (isStructural) structuralMask |= 1UL << absoluteStep;
                if (isGhost) ghostMask |= 1UL << absoluteStep;
                var velocity = Velocity(accent, isStructural, bar, step, settings.Role);
                var gate = Gate(accent, isStructural, bar, step);
                var note = new PatternNote(pitch, velocity, gate);
                steps[absoluteStep] = new PatternStep([note], new TriggerProbability(isGhost ? .45 : 1));
            }
            for (var step = 0; step < StepsPerBar; step++)
                steps[bar * StepsPerBar + step] ??= PatternStep.Rest;
        }
        var barFeatures = new[] { RhythmMetrics.Measure(sourceMask, sourceMask), aPrime.Features, contrast.Features, turnaround.Features };
        var relaxations = new[] { "none", aPrime.Relaxation, contrast.Relaxation, turnaround.Relaxation };
        var recipe = CreateRecipe(settings, template, barMasks, barFeatures, relaxations, structuralMask, ghostMask);
        return new Pattern(PatternId.New(), settings.Name, PatternSchemaVersion.Current, PatternMode.Melodic,
            PatternTiming.SixteenthNotes, steps, settings.Role, settings.TonalContext, recipe);
    }

    private static ushort AdjustActivity(ushort mask, GrooveTemplate template, PhraseActivity activity, IRandomSource random)
    {
        var targetHitCount = activity switch
        {
            PhraseActivity.Sparse => 4,
            PhraseActivity.Medium => 6,
            PhraseActivity.Busy => 8,
            _ => 6
        };
        while (System.Numerics.BitOperations.PopCount(mask) < targetHitCount)
        {
            var rests = Enumerable.Range(1, StepsPerBar - 1).Where(step => (mask & (1 << step)) == 0).ToArray();
            mask |= (ushort)(1 << rests[random.Next(rests.Length)]);
        }
        while (System.Numerics.BitOperations.PopCount(mask) > targetHitCount)
        {
            var removableOnsets = Enumerable.Range(1, StepsPerBar - 1)
                .Where(step => (mask & (1 << step)) != 0 && (template.RequiredOnsets & (1 << step)) == 0).ToArray();
            if (removableOnsets.Length == 0) break;
            mask &= (ushort)~(1 << removableOnsets[random.Next(removableOnsets.Length)]);
        }
        return mask;
    }

    private static MelodicPitch[] BuildMotif(IReadOnlyList<MelodicPitch> validPitches,
        GrooveGeneratorSettings settings, IRandomSource random, int motifLength)
    {
        var motif = new MelodicPitch[motifLength];
        var stablePitches = validPitches.Where(pitch => pitch.ScaleDegree is 0 or 3).ToArray();
        motif[0] = stablePitches[random.Next(stablePitches.Length)];
        var maximumMove = settings.Movement switch
        {
            PhraseLevel.Low => 1,
            PhraseLevel.Medium => 2,
            _ => 3
        };
        for (var ordinal = 1; ordinal < motifLength; ordinal++)
        {
            var currentIndex = PitchIndex(motif[ordinal - 1], validPitches);
            var movement = random.Next(-maximumMove, maximumMove + 1);
            var nextIndex = Math.Clamp(currentIndex + movement, 0, validPitches.Count - 1);
            motif[ordinal] = validPitches[nextIndex];
        }
        return motif;
    }

    private static MelodicPitch TransformPitch(MelodicPitch pitch, IReadOnlyList<MelodicPitch> validPitches,
        int bar, int ordinal, int noteCount, IRandomSource random)
    {
        if (bar == 0 || (bar == 1 && ordinal < noteCount - 1)) return pitch;
        if (bar == BarCount - 1 && ordinal == noteCount - 1)
            return validPitches.Where(candidate => candidate.ScaleDegree is 0 or 3)
                .OrderBy(candidate => Math.Abs(candidate.OctaveOffset - pitch.OctaveOffset)).First();
        if (bar == 2 && ordinal % 2 == 1)
        {
            var index = PitchIndex(pitch, validPitches);
            var direction = random.Next(2) == 0 ? -1 : 1;
            return validPitches[Math.Clamp(index + direction, 0, validPitches.Count - 1)];
        }
        return pitch;
    }

    private static int PitchIndex(MelodicPitch pitch, IReadOnlyList<MelodicPitch> validPitches) =>
        Enumerable.Range(0, validPitches.Count).First(index => validPitches[index] == pitch);

    private static MidiValue Velocity(GrooveAccent accent, bool isStructural, int bar, int step, MusicalRole role)
    {
        var offset = accent switch
        {
            GrooveAccent.Ghost => -24,
            GrooveAccent.Light => -10,
            GrooveAccent.Normal => 0,
            _ => 10
        };
        if (isStructural) offset += 5;
        if (bar == 2 && step >= HalfBarSteps) offset += 4;
        if (bar == BarCount - 1 && step < HalfBarSteps) offset -= 4;
        return new MidiValue(Math.Clamp(MusicalRoleProfile.For(role).BaseVelocity + offset, 1, 127));
    }

    private static NoteGate Gate(GrooveAccent accent, bool isStructural, int bar, int step)
    {
        if (isStructural) return new NoteGate(.85);
        if (accent == GrooveAccent.Ghost) return new NoteGate(.35);
        if (bar == 2 && step >= HalfBarSteps) return new NoteGate(.55);
        if (bar == BarCount - 1 && step == StepsPerBar - 1) return new NoteGate(.45);
        return new NoteGate(.68);
    }

    private static int[] Onsets(ushort mask) => Enumerable.Range(0, StepsPerBar)
        .Where(step => (mask & (1 << step)) != 0).ToArray();

    private static int NearestSourceOrdinal(int step, int[] sourceOnsets) => Enumerable.Range(0, sourceOnsets.Length)
        .OrderBy(ordinal =>
        {
            var distance = Math.Abs(sourceOnsets[ordinal] - step);
            return Math.Min(distance, StepsPerBar - distance);
        })
        .ThenBy(ordinal => ordinal).First();

    private static GrooveCategory ResolveCategory(GrooveSelection selection) => selection switch
    {
        GrooveSelection.Foundation => GrooveCategory.Foundation,
        GrooveSelection.Offbeat => GrooveCategory.Offbeat,
        GrooveSelection.Anticipation => GrooveCategory.Anticipation,
        GrooveSelection.LongShort => GrooveCategory.LongShort,
        GrooveSelection.SparseAnswer => GrooveCategory.SparseAnswer,
        GrooveSelection.Broken => GrooveCategory.Broken,
        _ => throw new ArgumentOutOfRangeException(nameof(selection))
    };

    private static GeneratorRecipe CreateRecipe(GrooveGeneratorSettings settings, GrooveTemplate template, ushort[] barMasks,
        RhythmFeatureVector[] barFeatures, string[] relaxations, ulong structuralMask, ulong ghostMask)
    {
        var values = new List<KeyValuePair<string, RecipeValue>>
        {
            GenerationHelpers.P("vocabulary-version", GrooveVocabulary.Version),
            GenerationHelpers.P("metric-version", RhythmMetrics.Version),
            GenerationHelpers.P("template", template.Id),
            GenerationHelpers.P("groove", settings.Groove.ToString()),
            GenerationHelpers.P("similarity", settings.Similarity.ToString()),
            GenerationHelpers.P("activity", settings.Activity.ToString()),
            GenerationHelpers.P("movement", settings.Movement.ToString()),
            GenerationHelpers.P("variation", settings.Variation.ToString()),
            GenerationHelpers.P("turnaround", settings.Turnaround.ToString()),
            GenerationHelpers.P("root", settings.TonalContext.Root.Value),
            GenerationHelpers.P("palette", settings.TonalContext.Palette.ToString()),
            GenerationHelpers.P("role", settings.Role.ToString()),
            GenerationHelpers.P("steps", StepCount),
            GenerationHelpers.P("pulses-per-step", PatternTiming.SixteenthNotes.PulsesPerStep),
            GenerationHelpers.P("structural-mask", structuralMask.ToString("X16")),
            GenerationHelpers.P("ghost-mask", ghostMask.ToString("X16"))
        };
        for (var bar = 0; bar < BarCount; bar++)
        {
            values.Add(GenerationHelpers.P($"bar-{bar}-mask", barMasks[bar].ToString("X4")));
            values.Add(GenerationHelpers.P($"bar-{bar}-features", FormatFeatures(barFeatures[bar])));
            values.Add(GenerationHelpers.P($"bar-{bar}-relaxation", relaxations[bar]));
        }
        return new GeneratorRecipe(GeneratorId, GeneratorVersion, settings.Seed, null, values);
    }

    private static string FormatFeatures(RhythmFeatureVector features) => string.Create(CultureInfo.InvariantCulture,
        $"hits={features.HitCount},sync={features.WeightedSyncopation},gap={features.MaximumRestGap},distance={features.HammingDistance},movement={features.MovementDistance}");
}
