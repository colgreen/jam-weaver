using JamWeaver.Core.Sequencer;
using Redzen.Random;

namespace JamWeaver.Core.Generation;

public sealed class MelodicPatternGenerator : IPatternGenerator<MelodicGeneratorSettings>
{
    public const string GeneratorId = "melodic-euclidean-motif";
    public const int GeneratorVersion = 3;

    public Pattern Generate(MelodicGeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var random = RandomDefaults.CreateRandomSource(settings.Seed);
        var profile = MusicalRoleProfile.For(settings.Role);
        var hits = profile.HitCount(settings.StepCount, settings.Density);
        var rotation = EuclideanRhythm.ChooseDownbeatRotation(settings.StepCount, hits, random);
        var rhythm = EuclideanRhythm.Create(settings.StepCount, hits, rotation);
        var motifLength = Math.Clamp(4 - (int)Math.Round(settings.Repetition.Value * 2, MidpointRounding.AwayFromZero), 2, 4);
        var validPitches = PentatonicPitchResolver.ValidPitches(settings.TonalContext, settings.Role);
        var motif = BuildMotif(random, validPitches, motifLength, settings.Role, settings.Movement);
        var steps = new PatternStep[settings.StepCount];
        var hitIndex = 0;
        for (var i = 0; i < steps.Length; i++)
        {
            if (!rhythm[i]) { steps[i] = PatternStep.Rest; continue; }
            var pitch = motif[hitIndex % motif.Count];
            if (hitIndex >= motif.Count && random.NextDouble() > settings.Repetition.Value)
                pitch = VaryPitch(random, pitch, validPitches, settings.Movement);
            var note = new PatternNote(pitch,
                GenerationHelpers.Velocity(random, profile.BaseVelocity, settings.VelocityVariation),
                GenerationHelpers.Gate(settings.Gate));
            steps[i] = new PatternStep([note], TriggerProbability.Always);
            hitIndex++;
        }

        var recipe = new GeneratorRecipe(GeneratorId, GeneratorVersion, settings.Seed, null,
        [
            GenerationHelpers.P("steps", settings.StepCount), GenerationHelpers.P("pulses-per-step", settings.Timing.PulsesPerStep),
            GenerationHelpers.P("root", settings.TonalContext.Root.Value), GenerationHelpers.P("palette", settings.TonalContext.Palette.ToString()),
            GenerationHelpers.P("role", settings.Role.ToString()), GenerationHelpers.P("density", settings.Density.Value),
            GenerationHelpers.P("movement", settings.Movement.Value), GenerationHelpers.P("repetition", settings.Repetition.Value),
            GenerationHelpers.P("gate", settings.Gate.Value), GenerationHelpers.P("velocity-variation", settings.VelocityVariation.Value),
            GenerationHelpers.P("hits", hits), GenerationHelpers.P("rotation", rotation), GenerationHelpers.P("motif-length", motifLength)
        ]);
        return new Pattern(PatternId.New(), settings.Name, PatternSchemaVersion.Current, PatternMode.Melodic,
            settings.Timing, steps, settings.Role, settings.TonalContext, recipe);
    }

    private static IReadOnlyList<MelodicPitch> BuildMotif(IRandomSource random, IReadOnlyList<MelodicPitch> valid,
        int length, MusicalRole role, NormalizedAmount movement)
    {
        var preferred = role == MusicalRole.Bass
            ? valid.Take(Math.Max(1, (valid.Count * 2 + 2) / 3)).Where(p => p.ScaleDegree is 0 or 3).ToArray()
            : valid.ToArray();
        if (preferred.Length == 0) preferred = valid.Take(Math.Max(1, valid.Count / 2)).ToArray();
        var current = preferred[random.Next(preferred.Length)];
        var motif = new List<MelodicPitch>(length) { current };
        for (var i = 1; i < length; i++)
        {
            current = VaryPitch(random, current, valid, movement);
            motif.Add(current);
        }
        return motif;
    }

    private static MelodicPitch VaryPitch(IRandomSource random, MelodicPitch current,
        IReadOnlyList<MelodicPitch> valid, NormalizedAmount movement)
    {
        var index = Enumerable.Range(0, valid.Count).First(i => valid[i] == current);
        var maximumMove = 1 + (int)Math.Round(movement.Value * 3, MidpointRounding.AwayFromZero);
        var first = Math.Max(0, index - maximumMove);
        var last = Math.Min(valid.Count - 1, index + maximumMove);
        var alternatives = Enumerable.Range(first, last - first + 1)
            .Where(candidate => candidate != index).ToArray();
        return alternatives.Length == 0 ? current : valid[alternatives[random.Next(alternatives.Length)]];
    }
}
