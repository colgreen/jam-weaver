using JamWeaver.Core.Generation.Phrase;
using Redzen.Random;

namespace JamWeaver.Core.Generation.Motif;

/// <summary>Develops a short melodic motif over four related bars using a versioned shape grammar.</summary>
public sealed class MotifGenerator : IPatternGenerator<MotifGeneratorSettings>
{
    public const string GeneratorId = "melodic-musical-motif";
    public const int GeneratorVersion = 4;

    private const int StepsPerBar = 16;
    private const int BarCount = 4;
    private const int StepCount = StepsPerBar * BarCount;
    private const int ExplicitShapeCount = 7;
    private const int RhythmVariantCount = 4;
    private const ushort ReturnBodyMask = 0x0fff;
    private const ushort ReturnPickupMask = 0x6000;

    public GeneratorLineage Lineage => new(GeneratorId, GeneratorVersion);

    public Pattern Vary(Pattern parent, GeneratorVariationSettings settings) =>
        CurrentGeneratorVariation.Vary(parent, settings, Lineage);

    public Pattern Generate(MotifGeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var random = RandomDefaults.CreateRandomSource(settings.Seed);
        var resolvedShape = settings.Shape == MotifShape.Auto
            ? Enum.GetValues<MotifShape>().Where(value => value != MotifShape.Auto).ElementAt(random.Next(ExplicitShapeCount))
            : settings.Shape;
        var (sourceMask, rhythmVariant) = CreateRhythm(resolvedShape, settings.Activity, random);
        var barMasks = DevelopRhythm(sourceMask, resolvedShape, settings.Variation);
        var validPitches = PentatonicPitchResolver.ValidPitches(settings.TonalContext, settings.Role);
        var motif = CreateMotif(validPitches, resolvedShape, settings.Movement, random);
        var barMotifs = DevelopMotif(motif, validPitches, resolvedShape, settings.Variation, random);
        var steps = Enumerable.Repeat(PatternStep.Rest, StepCount).ToArray();
        ulong structuralMask = 0;
        for (var bar = 0; bar < BarCount; bar++)
        {
            var onsets = Onsets(barMasks[bar]);
            for (var hit = 0; hit < onsets.Length; hit++)
            {
                var step = onsets[hit];
                var absoluteStep = bar * StepsPerBar + step;
                var pitch = barMotifs[bar][hit % barMotifs[bar].Length];
                var isAnchor = step == 0 || hit == 0;
                if (isAnchor) structuralMask |= 1UL << absoluteStep;
                var isPhraseEnd = bar == BarCount - 1 && hit == onsets.Length - 1;
                var velocity = Velocity(settings.Role, isAnchor, bar, hit, motif.Length);
                var gate = .68;
                if (isAnchor) gate = .85;
                else if (isPhraseEnd) gate = .75;
                var note = new PatternNote(pitch, velocity, new NoteGate(gate));
                steps[absoluteStep] = new PatternStep([note], TriggerProbability.Always);
            }
        }
        var recipe = CreateRecipe(settings, resolvedShape, rhythmVariant, barMasks, motif.Length, structuralMask);
        return new Pattern(PatternId.New(), settings.Name, PatternSchemaVersion.Current, PatternMode.Melodic,
            PatternTiming.SixteenthNotes, steps, settings.Role, settings.TonalContext, recipe);
    }

    private static MidiValue Velocity(MusicalRole role, bool isAnchor, int bar, int hit, int motifLength)
    {
        var emphasis = -5;
        if (isAnchor) emphasis = 8;
        else if (hit % motifLength == 0) emphasis = 3;
        var contrastBoost = bar == 2 ? 2 : 0;
        return new MidiValue(Math.Clamp(MusicalRoleProfile.For(role).BaseVelocity + emphasis + contrastBoost, 1, 127));
    }

    private static GeneratorRecipe CreateRecipe(MotifGeneratorSettings settings, MotifShape resolvedShape,
        int rhythmVariant, ushort[] barMasks, int motifLength, ulong structuralMask) =>
        new(GeneratorId, GeneratorVersion, settings.Seed, null,
        [
            GenerationHelpers.P("shape", settings.Shape.ToString()),
            GenerationHelpers.P("resolved-shape", resolvedShape.ToString()),
            GenerationHelpers.P("rhythm-variant", rhythmVariant),
            GenerationHelpers.P("activity", settings.Activity.ToString()),
            GenerationHelpers.P("movement", settings.Movement.ToString()),
            GenerationHelpers.P("variation", settings.Variation.ToString()),
            GenerationHelpers.P("root", settings.TonalContext.Root.Value),
            GenerationHelpers.P("palette", settings.TonalContext.Palette.ToString()),
            GenerationHelpers.P("role", settings.Role.ToString()),
            GenerationHelpers.P("steps", StepCount),
            GenerationHelpers.P("pulses-per-step", PatternTiming.SixteenthNotes.PulsesPerStep),
            GenerationHelpers.P("bar-0-mask", barMasks[0].ToString("X4")),
            GenerationHelpers.P("bar-1-mask", barMasks[1].ToString("X4")),
            GenerationHelpers.P("bar-2-mask", barMasks[2].ToString("X4")),
            GenerationHelpers.P("bar-3-mask", barMasks[3].ToString("X4")),
            GenerationHelpers.P("motif-length", motifLength),
            GenerationHelpers.P("development", "A,varied-A,contrasting-B,return"),
            GenerationHelpers.P("structural-mask", structuralMask.ToString("X16")),
            GenerationHelpers.P("ghost-mask", "0000000000000000")
        ]);

    private static (ushort Mask, int Variant) CreateRhythm(MotifShape shape, PhraseActivity activity, IRandomSource random)
    {
        (string Sparse, string Medium, string Busy) grids = shape switch
        {
            MotifShape.Pedal => ("1000100010001000", "1000101010001010", "1010101010101010"),
            MotifShape.RootFifth => ("1000001010000000", "1000101010000010", "1010101010001010"),
            MotifShape.Walking => ("1000100010001000", "1000101010001010", "1010101010101010"),
            MotifShape.CallResponse => ("1000100000101000", "1000101000101010", "1010101000101010"),
            MotifShape.Arch => ("1000010010000100", "1001010010010100", "1011010010010110"),
            MotifShape.Pickup => ("1000100010000010", "1000101010000110", "1010101010100110"),
            _ => ("1001000010010000", "1001001010010010", "1011001010110010")
        };
        var grid = activity switch
        {
            PhraseActivity.Sparse => grids.Sparse,
            PhraseActivity.Medium => grids.Medium,
            _ => grids.Busy
        };
        var mask = ParseRhythmGrid(grid);
        var variant = random.Next(RhythmVariantCount);
        var onsets = Onsets(mask);
        switch (variant)
        {
            case 1:
                mask = MoveOnset(mask, Math.Min(1, onsets.Length - 1), 1);
                break;
            case 2:
                mask = MoveOnset(mask, onsets.Length - 1, -1);
                break;
            case 3:
                var middleOnset = onsets.Length / 2;
                var displacement = onsets[middleOnset] % 2 == 0 ? 1 : -1;
                mask = MoveOnset(mask, middleOnset, displacement);
                break;
        }
        return (mask, variant);
    }

    private static ushort[] DevelopRhythm(ushort sourceMask, MotifShape shape, PhraseLevel variation)
    {
        var aPrimeMask = sourceMask;
        var contrastMask = sourceMask;
        var returnMask = sourceMask;
        if (variation != PhraseLevel.Low)
        {
            aPrimeMask = MoveLastOnset(aPrimeMask, -1);
            if (shape is MotifShape.CallResponse or MotifShape.Arch)
                contrastMask = SwapBarHalves(sourceMask);
            else
            {
                var displacement = shape is MotifShape.Pedal or MotifShape.RootFifth ? 2 : -2;
                contrastMask = MoveOnset(sourceMask, Onsets(sourceMask).Length / 2, displacement);
            }
        }
        if (variation == PhraseLevel.High)
            contrastMask = SwapBarHalves(MoveOnset(contrastMask, Onsets(contrastMask).Length - 1, 1));
        if (shape == MotifShape.Pickup || variation != PhraseLevel.Low)
            returnMask = (ushort)((sourceMask & ReturnBodyMask) | ReturnPickupMask);
        return [sourceMask, aPrimeMask, contrastMask, returnMask];
    }

    private static MelodicPitch[] CreateMotif(IReadOnlyList<MelodicPitch> validPitches, MotifShape shape,
        PhraseLevel movement, IRandomSource random)
    {
        var rootIndices = Enumerable.Range(0, validPitches.Count)
            .Where(index => validPitches[index].ScaleDegree == 0).ToArray();
        var rootIndex = rootIndices[random.Next(rootIndices.Length)];
        int[] contour = shape switch
        {
            MotifShape.Pedal => [0, 0, 1],
            MotifShape.RootFifth => [0, 3, 0],
            MotifShape.Walking => [0, 1, 2, 1],
            MotifShape.CallResponse => [0, 1, 0, -1],
            MotifShape.Arch => [0, 1, 2, 1, 0],
            MotifShape.Pickup => [0, 0, -1, 0],
            _ => [0, 2, 0, 1]
        };
        var movementScale = movement switch
        {
            PhraseLevel.Low => .5,
            PhraseLevel.Medium => 1,
            _ => 1.5
        };
        var deltas = contour.Select(delta => (int)Math.Round(delta * movementScale)).ToArray();
        var forward = ResolveContour(validPitches, rootIndex, deltas);
        var reflected = ResolveContour(validPitches, rootIndex, deltas.Select(delta => -delta).ToArray());
        return DistinctCount(reflected) > DistinctCount(forward) ? reflected : forward;
    }

    private static MelodicPitch[] ResolveContour(IReadOnlyList<MelodicPitch> validPitches, int rootIndex, int[] deltas) =>
        deltas.Select(delta => validPitches[Math.Clamp(rootIndex + delta, 0, validPitches.Count - 1)]).ToArray();

    private static int DistinctCount(IEnumerable<MelodicPitch> pitches) => pitches.Distinct().Count();

    private static MelodicPitch[][] DevelopMotif(MelodicPitch[] motif, IReadOnlyList<MelodicPitch> validPitches,
        MotifShape shape, PhraseLevel variation, IRandomSource random)
    {
        var aPrimeMotif = motif.ToArray();
        var contrastMotif = motif.ToArray();
        var returnMotif = motif.ToArray();
        if (variation != PhraseLevel.Low)
        {
            aPrimeMotif[^1] = NeighborPitch(aPrimeMotif[^1], validPitches, random.Next(2) == 0 ? -1 : 1);
            contrastMotif = shape is MotifShape.Arch or MotifShape.CallResponse
                ? motif.Reverse().ToArray()
                : motif.Skip(1).Append(motif[0]).ToArray();
        }
        if (variation == PhraseLevel.High) contrastMotif[^1] = NeighborPitch(contrastMotif[^1], validPitches, 1);
        returnMotif[^1] = validPitches.Where(pitch => pitch.ScaleDegree is 0 or 3)
            .OrderBy(pitch => PitchDistance(pitch, motif[0], validPitches)).First();
        return [motif, aPrimeMotif, contrastMotif, returnMotif];
    }

    private static MelodicPitch NeighborPitch(MelodicPitch pitch, IReadOnlyList<MelodicPitch> validPitches, int direction)
    {
        var index = PitchIndex(pitch, validPitches);
        return validPitches[Math.Clamp(index + direction, 0, validPitches.Count - 1)];
    }

    private static int PitchIndex(MelodicPitch pitch, IReadOnlyList<MelodicPitch> validPitches) =>
        Enumerable.Range(0, validPitches.Count).First(index => validPitches[index] == pitch);

    private static int PitchDistance(MelodicPitch first, MelodicPitch second, IReadOnlyList<MelodicPitch> validPitches) =>
        Math.Abs(PitchIndex(first, validPitches) - PitchIndex(second, validPitches));

    private static ushort MoveLastOnset(ushort mask, int delta)
    {
        var last = Onsets(mask).Last();
        if (last == 0) return mask;
        var target = Math.Clamp(last + delta, 1, StepsPerBar - 1);
        if ((mask & (1 << target)) != 0) return mask;
        return (ushort)((mask & ~(1 << last)) | (1 << target));
    }

    private static ushort MoveOnset(ushort mask, int onsetIndex, int delta)
    {
        var onsets = Onsets(mask);
        var source = onsets[Math.Clamp(onsetIndex, 0, onsets.Length - 1)];
        if (source == 0) return mask;
        var target = Math.Clamp(source + delta, 1, StepsPerBar - 1);
        if ((mask & (1 << target)) != 0)
        {
            target = Math.Clamp(source - Math.Sign(delta), 1, StepsPerBar - 1);
            if ((mask & (1 << target)) != 0) return mask;
        }
        return (ushort)((mask & ~(1 << source)) | (1 << target));
    }

    private static ushort SwapBarHalves(ushort mask) => (ushort)((mask >> 8) | ((mask & 0xff) << 8));

    private static int[] Onsets(ushort mask) => Enumerable.Range(0, StepsPerBar)
        .Where(step => (mask & (1 << step)) != 0).ToArray();

    private static ushort ParseRhythmGrid(string grid)
    {
        ushort mask = 0;
        for (var step = 0; step < StepsPerBar; step++)
        {
            if (grid[step] == '1') mask |= (ushort)(1 << step);
        }
        return mask;
    }
}
