using Redzen.Random;

namespace JamWeaver.Core.Generation;

public sealed class Euclidean2PatternGenerator : IPatternGenerator<MelodicGeneratorSettings>
{
    public const string GeneratorId = "melodic-euclidean-2";
    public const int GeneratorVersion = 1;

    public Pattern Generate(MelodicGeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.StepCount != 64 || settings.Timing != PatternTiming.SixteenthNotes)
            throw new ArgumentException("Euclidean2 requires four bars of sixteenth-note steps.", nameof(settings));
        var random = RandomDefaults.CreateRandomSource(settings.Seed);
        var profile = MusicalRoleProfile.For(settings.Role);
        var hits = profile.HitCount(16, settings.Density);
        var rotation = EuclideanRhythm.ChooseDownbeatRotation(16, hits, random);
        var a = EuclideanRhythm.Create(16, hits, rotation);
        var aPrime = MoveLastOnset(a, random.Next(2) == 0 ? -1 : 1);
        var bHits = Math.Clamp(hits + (random.Next(2) == 0 ? -1 : 1), 2, 15);
        var bRotation = EuclideanRhythm.ChooseDownbeatRotation(16, bHits, random);
        var b = EuclideanRhythm.Create(16, bHits, bRotation);
        var masks = new[] { a, aPrime, b, a.ToArray() };

        var motifLength = Math.Clamp(4 - (int)Math.Round(settings.Repetition.Value * 2,
            MidpointRounding.AwayFromZero), 2, 4);
        var validPitches = PentatonicPitchResolver.ValidPitches(settings.TonalContext, settings.Role);
        var motif = BuildMotif(random, validPitches, motifLength, settings.Role, settings.Movement);
        var barMotifs = new[]
        {
            motif,
            motif.Skip(1).Append(motif[0]).ToArray(),
            motif.Reverse().ToArray(),
            motif
        };
        var barPitches = new MelodicPitch[4][];
        for (var bar = 0; bar < 3; bar++)
            barPitches[bar] = BuildBarPitches(random, masks[bar], barMotifs[bar], validPitches,
                settings.Repetition, settings.Movement);
        barPitches[3] = barPitches[0].ToArray();

        var steps = Enumerable.Repeat(PatternStep.Rest, 64).ToArray();
        ulong structuralMask = 0;
        for (var bar = 0; bar < 4; bar++)
        {
            var hitIndex = 0;
            for (var step = 0; step < 16; step++)
            {
                if (!masks[bar][step]) continue;
                var pitch = barPitches[bar][hitIndex];
                steps[bar * 16 + step] = new PatternStep([new PatternNote(pitch,
                    GenerationHelpers.Velocity(random, profile.BaseVelocity, settings.VelocityVariation),
                    GenerationHelpers.Gate(settings.Gate))], TriggerProbability.Always);
                if (hitIndex == 0) structuralMask |= 1UL << (bar * 16 + step);
                hitIndex++;
            }
        }

        var recipe = new GeneratorRecipe(GeneratorId, GeneratorVersion, settings.Seed, null,
        [
            GenerationHelpers.P("steps", 64), GenerationHelpers.P("pulses-per-step", PatternTiming.SixteenthNotes.PulsesPerStep),
            GenerationHelpers.P("root", settings.TonalContext.Root.Value), GenerationHelpers.P("palette", settings.TonalContext.Palette.ToString()),
            GenerationHelpers.P("role", settings.Role.ToString()), GenerationHelpers.P("density", settings.Density.Value),
            GenerationHelpers.P("movement", settings.Movement.Value), GenerationHelpers.P("repetition", settings.Repetition.Value),
            GenerationHelpers.P("gate", settings.Gate.Value), GenerationHelpers.P("velocity-variation", settings.VelocityVariation.Value),
            GenerationHelpers.P("hits-a", hits), GenerationHelpers.P("hits-b", bHits), GenerationHelpers.P("rotation-a", rotation),
            GenerationHelpers.P("rotation-b", bRotation), GenerationHelpers.P("motif-length", motifLength),
            GenerationHelpers.P("bar-0-mask", Mask(a)), GenerationHelpers.P("bar-1-mask", Mask(aPrime)),
            GenerationHelpers.P("bar-2-mask", Mask(b)), GenerationHelpers.P("bar-3-mask", Mask(a)),
            GenerationHelpers.P("bar-roles", "A,A-prime,B,return"),
            GenerationHelpers.P("structural-mask", structuralMask.ToString("X16")),
            GenerationHelpers.P("ghost-mask", "0000000000000000")
        ]);
        return new Pattern(PatternId.New(), settings.Name, PatternSchemaVersion.Current, PatternMode.Melodic,
            PatternTiming.SixteenthNotes, steps, settings.Role, settings.TonalContext, recipe);
    }

    private static MelodicPitch[] BuildMotif(IRandomSource random, IReadOnlyList<MelodicPitch> valid,
        int length, MusicalRole role, NormalizedAmount movement)
    {
        var preferred = role == MusicalRole.Bass
            ? valid.Take(Math.Max(1, (valid.Count * 2 + 2) / 3)).Where(p => p.ScaleDegree is 0 or 3).ToArray()
            : valid.ToArray();
        if (preferred.Length == 0) preferred = valid.Take(Math.Max(1, valid.Count / 2)).ToArray();
        var current = preferred[random.Next(preferred.Length)];
        var motif = new List<MelodicPitch>(length) { current };
        for (var index = 1; index < length; index++)
        {
            current = VaryPitch(random, current, valid, movement);
            motif.Add(current);
        }
        return motif.ToArray();
    }

    private static MelodicPitch VaryPitch(IRandomSource random, MelodicPitch current,
        IReadOnlyList<MelodicPitch> valid, NormalizedAmount movement)
    {
        var index = Enumerable.Range(0, valid.Count).First(candidate => valid[candidate] == current);
        var maximumMove = 1 + (int)Math.Round(movement.Value * 3, MidpointRounding.AwayFromZero);
        var first = Math.Max(0, index - maximumMove);
        var last = Math.Min(valid.Count - 1, index + maximumMove);
        var alternatives = Enumerable.Range(first, last - first + 1).Where(candidate => candidate != index).ToArray();
        return alternatives.Length == 0 ? current : valid[alternatives[random.Next(alternatives.Length)]];
    }

    private static MelodicPitch[] BuildBarPitches(IRandomSource random, IReadOnlyList<bool> mask,
        IReadOnlyList<MelodicPitch> motif, IReadOnlyList<MelodicPitch> valid,
        NormalizedAmount repetition, NormalizedAmount movement)
    {
        var pitches = new List<MelodicPitch>();
        var hitCount = mask.Count(value => value);
        for (var hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            var pitch = motif[hitIndex % motif.Count];
            if (hitIndex >= motif.Count && random.NextDouble() > repetition.Value)
                pitch = VaryPitch(random, pitch, valid, movement);
            pitches.Add(pitch);
        }
        return pitches.ToArray();
    }

    private static bool[] MoveLastOnset(IReadOnlyList<bool> source, int delta)
    {
        var result = source.ToArray();
        var onset = Enumerable.Range(1, result.Length - 1).Last(index => result[index]);
        var destination = Math.Clamp(onset + delta, 1, result.Length - 1);
        if (result[destination]) destination = Math.Clamp(onset - delta, 1, result.Length - 1);
        if (destination != onset && !result[destination]) { result[onset] = false; result[destination] = true; }
        return result;
    }

    private static string Mask(IReadOnlyList<bool> rhythm)
    {
        ushort mask = 0;
        for (var index = 0; index < rhythm.Count; index++) if (rhythm[index]) mask |= (ushort)(1 << index);
        return mask.ToString("X4");
    }
}
