using JamWeaver.Core.Generation.Phrase;
using JamWeaver.Core.Midi;
using JamWeaver.Core.Sequencer;
using Redzen.Random;

namespace JamWeaver.Core.Generation.Motif;

public sealed class MusicalMotifGenerator
{
    public const string GeneratorId = "melodic-musical-motif";
    public const int GeneratorVersion = 1;

    public Pattern Generate(MotifGeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var random = RandomDefaults.CreateRandomSource(settings.Seed);
        var shape = settings.Shape == MotifShape.Auto
            ? Enum.GetValues<MotifShape>().Where(value => value != MotifShape.Auto).ElementAt(random.Next(7))
            : settings.Shape;
        var aMask = Rhythm(shape, settings.Activity);
        var masks = DevelopRhythm(aMask, shape, settings.Variation);
        var valid = PentatonicPitchResolver.ValidPitches(settings.TonalContext, settings.Role);
        var motif = Motif(valid, shape, settings.Movement, random);
        var barMotifs = DevelopMotif(motif, valid, shape, settings.Variation, random);
        var steps = Enumerable.Repeat(PatternStep.Rest, 64).ToArray();
        ulong structuralMask = 0;
        for (var bar = 0; bar < 4; bar++)
        {
            var onsets = Onsets(masks[bar]);
            for (var hit = 0; hit < onsets.Length; hit++)
            {
                var step = onsets[hit]; var absolute = bar * 16 + step;
                var pitch = barMotifs[bar][hit % barMotifs[bar].Length];
                var anchor = step == 0 || hit == 0;
                if (anchor) structuralMask |= 1UL << absolute;
                var phraseEnd = bar == 3 && hit == onsets.Length - 1;
                var velocity = new MidiValue(Math.Clamp(MusicalRoleProfile.For(settings.Role).BaseVelocity
                    + (anchor ? 8 : hit % motif.Length == 0 ? 3 : -5) + (bar == 2 ? 2 : 0), 1, 127));
                var gate = new NoteGate(anchor ? .85 : phraseEnd ? .75 : .68);
                steps[absolute] = new PatternStep([new PatternNote(pitch, velocity, gate)], TriggerProbability.Always);
            }
        }
        var recipe = new GeneratorRecipe(GeneratorId, GeneratorVersion, settings.Seed, null,
        [
            GenerationHelpers.P("shape", settings.Shape.ToString()), GenerationHelpers.P("resolved-shape", shape.ToString()),
            GenerationHelpers.P("activity", settings.Activity.ToString()), GenerationHelpers.P("movement", settings.Movement.ToString()),
            GenerationHelpers.P("variation", settings.Variation.ToString()), GenerationHelpers.P("root", settings.TonalContext.Root.Value),
            GenerationHelpers.P("palette", settings.TonalContext.Palette.ToString()), GenerationHelpers.P("role", settings.Role.ToString()),
            GenerationHelpers.P("steps", 64), GenerationHelpers.P("pulses-per-step", PatternTiming.SixteenthNotes.PulsesPerStep),
            GenerationHelpers.P("bar-0-mask", masks[0].ToString("X4")), GenerationHelpers.P("bar-1-mask", masks[1].ToString("X4")),
            GenerationHelpers.P("bar-2-mask", masks[2].ToString("X4")), GenerationHelpers.P("bar-3-mask", masks[3].ToString("X4")),
            GenerationHelpers.P("motif-length", motif.Length), GenerationHelpers.P("development", "A,A-prime,related-B,return"),
            GenerationHelpers.P("structural-mask", structuralMask.ToString("X16")), GenerationHelpers.P("ghost-mask", "0000000000000000")
        ]);
        return new Pattern(PatternId.New(), settings.Name, PatternSchemaVersion.Current, PatternMode.Melodic,
            PatternTiming.SixteenthNotes, steps, settings.Role, settings.TonalContext, recipe);
    }

    private static ushort Rhythm(MotifShape shape, PhraseActivity activity)
    {
        var grids = shape switch
        {
            MotifShape.Pedal => ("1000100010001000", "1000101010001010", "1010101010101010"),
            MotifShape.RootFifth => ("1000001010000000", "1000101010000010", "1010101010001010"),
            MotifShape.Walking => ("1000100010001000", "1000101010001010", "1010101010101010"),
            MotifShape.CallResponse => ("1000100000101000", "1000101000101010", "1010101000101010"),
            MotifShape.Arch => ("1000010010000100", "1001010010010100", "1011010010010110"),
            MotifShape.Pickup => ("1000100010000010", "1000101010000110", "1010101010100110"),
            _ => ("1001000010010000", "1001001010010010", "1011001010110010")
        };
        return Parse(activity switch { PhraseActivity.Sparse => grids.Item1, PhraseActivity.Medium => grids.Item2, _ => grids.Item3 });
    }

    private static ushort[] DevelopRhythm(ushort a, MotifShape shape, PhraseLevel variation)
    {
        var aPrime = a; var b = a; var turn = a;
        if (variation != PhraseLevel.Low)
        {
            aPrime = MoveLast(aPrime, -1);
            b = shape is MotifShape.CallResponse or MotifShape.Arch ? RotateHalf(a) : MoveLast(a, 1);
        }
        if (variation == PhraseLevel.High) b = RotateHalf(MoveLast(b, 1));
        if (shape == MotifShape.Pickup || variation != PhraseLevel.Low) turn = (ushort)((a & 0x0fff) | 0x6000);
        return [a, aPrime, b, turn];
    }

    private static MelodicPitch[] Motif(IReadOnlyList<MelodicPitch> valid, MotifShape shape, PhraseLevel movement, IRandomSource random)
    {
        var roots = Enumerable.Range(0, valid.Count).Where(i => valid[i].ScaleDegree == 0).ToArray();
        var rootIndex = roots[random.Next(roots.Length)];
        int[] contour = shape switch
        {
            MotifShape.Pedal => [0, 0, 1], MotifShape.RootFifth => [0, 3, 0], MotifShape.Walking => [0, 1, 2, 1],
            MotifShape.CallResponse => [0, 1, 0, -1], MotifShape.Arch => [0, 1, 2, 1, 0],
            MotifShape.Pickup => [0, 0, -1, 0], _ => [0, 2, 0, 1]
        };
        var scale = movement switch { PhraseLevel.Low => .5, PhraseLevel.Medium => 1, _ => 1.5 };
        return contour.Select(delta => valid[Math.Clamp(rootIndex + (int)Math.Round(delta * scale), 0, valid.Count - 1)]).ToArray();
    }

    private static MelodicPitch[][] DevelopMotif(MelodicPitch[] motif, IReadOnlyList<MelodicPitch> valid,
        MotifShape shape, PhraseLevel variation, IRandomSource random)
    {
        var aPrime = motif.ToArray(); var b = motif.ToArray(); var turn = motif.ToArray();
        if (variation != PhraseLevel.Low)
        {
            aPrime[^1] = Neighbor(aPrime[^1], valid, random.Next(2) == 0 ? -1 : 1);
            b = shape is MotifShape.Arch or MotifShape.CallResponse ? motif.Reverse().ToArray() : motif.Skip(1).Append(motif[0]).ToArray();
        }
        if (variation == PhraseLevel.High) b[^1] = Neighbor(b[^1], valid, 1);
        turn[^1] = valid.Where(p => p.ScaleDegree is 0 or 3).OrderBy(p => Distance(p, motif[0], valid)).First();
        return [motif, aPrime, b, turn];
    }

    private static MelodicPitch Neighbor(MelodicPitch pitch, IReadOnlyList<MelodicPitch> valid, int direction)
    {
        var index = Enumerable.Range(0, valid.Count).First(i => valid[i] == pitch);
        return valid[Math.Clamp(index + direction, 0, valid.Count - 1)];
    }
    private static int Distance(MelodicPitch a, MelodicPitch b, IReadOnlyList<MelodicPitch> valid) =>
        Math.Abs(Enumerable.Range(0, valid.Count).First(i => valid[i] == a) - Enumerable.Range(0, valid.Count).First(i => valid[i] == b));
    private static ushort MoveLast(ushort mask, int delta)
    {
        var last = Onsets(mask).Last(); if (last == 0) return mask;
        var target = Math.Clamp(last + delta, 1, 15); if ((mask & (1 << target)) != 0) return mask;
        return (ushort)((mask & ~(1 << last)) | (1 << target));
    }
    private static ushort RotateHalf(ushort mask) => (ushort)((mask >> 8) | ((mask & 0xff) << 8));
    private static int[] Onsets(ushort mask) => Enumerable.Range(0, 16).Where(i => (mask & (1 << i)) != 0).ToArray();
    private static ushort Parse(string grid) { ushort mask = 0; for (var i = 0; i < 16; i++) if (grid[i] == '1') mask |= (ushort)(1 << i); return mask; }
}
