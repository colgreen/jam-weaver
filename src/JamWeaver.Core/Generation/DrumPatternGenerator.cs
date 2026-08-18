using JamWeaver.Core.Sequencer;
using Redzen.Random;

namespace JamWeaver.Core.Generation;

public sealed class DrumPatternGenerator
{
    public const string GeneratorId = "drum-euclidean-voices";
    public const int GeneratorVersion = 1;

    public Pattern Generate(DrumGeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var random = RandomDefaults.CreateRandomSource(settings.Seed);
        var stepNotes = Enumerable.Range(0, settings.StepCount).Select(_ => new List<PatternNote>()).ToArray();
        var recipeParameters = new List<KeyValuePair<string, RecipeValue>>
        {
            GenerationHelpers.P("steps", settings.StepCount), GenerationHelpers.P("pulses-per-step", settings.Timing.PulsesPerStep),
            GenerationHelpers.P("gate", settings.Gate.Value), GenerationHelpers.P("velocity-variation", settings.VelocityVariation.Value),
            GenerationHelpers.P("voice-count", settings.Voices.Length)
        };
        var usedRotations = new HashSet<int>();
        for (var voiceIndex = 0; voiceIndex < settings.Voices.Length; voiceIndex++)
        {
            var voice = settings.Voices[voiceIndex];
            var hits = 1 + (int)Math.Round((settings.StepCount - 1) * voice.Density.Value, MidpointRounding.AwayFromZero);
            var rotation = ChooseRotation(settings.StepCount, hits, random, usedRotations);
            usedRotations.Add(rotation);
            var rhythm = EuclideanRhythm.Create(settings.StepCount, hits, rotation);
            for (var step = 0; step < rhythm.Length; step++)
            {
                if (!rhythm[step]) continue;
                stepNotes[step].Add(new PatternNote(new DrumPitch(voice.Note),
                    GenerationHelpers.Velocity(random, 100, settings.VelocityVariation), GenerationHelpers.Gate(settings.Gate)));
            }
            var prefix = $"voice-{voiceIndex}";
            recipeParameters.Add(GenerationHelpers.P($"{prefix}-note", voice.Note.Value));
            recipeParameters.Add(GenerationHelpers.P($"{prefix}-density", voice.Density.Value));
            recipeParameters.Add(GenerationHelpers.P($"{prefix}-hits", hits));
            recipeParameters.Add(GenerationHelpers.P($"{prefix}-rotation", rotation));
        }

        var steps = stepNotes.Select(notes => new PatternStep(notes, TriggerProbability.Always)).ToArray();
        var recipe = new GeneratorRecipe(GeneratorId, GeneratorVersion, settings.Seed, null, recipeParameters);
        return new Pattern(PatternId.New(), settings.Name, PatternSchemaVersion.Current, PatternMode.Drums,
            settings.Timing, steps, null, null, recipe);
    }

    private static int ChooseRotation(int steps, int hits, IRandomSource random, HashSet<int> used)
    {
        var choices = Enumerable.Range(0, steps).Where(r => EuclideanRhythm.Create(steps, hits, r)[0]).ToArray();
        var unused = choices.Where(r => !used.Contains(r)).ToArray();
        var pool = unused.Length > 0 ? unused : choices;
        return pool[random.Next(pool.Length)];
    }
}
