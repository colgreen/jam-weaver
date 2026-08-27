namespace JamWeaver.Core.Generation;

/// <summary>
/// Experimental four-bar A | A | A | A-turnaround form. The first three bars and
/// rhythm of the fourth are exact copies of the version-3 Euclidean baseline.
/// </summary>
public sealed class ResearchEuclideanGenerator : IPatternGenerator<EuclideanGeneratorSettings>
{
    public const string GeneratorId = "experimental-euclidean-turnaround";
    public const int GeneratorVersion = 1;
    public GeneratorLineage Lineage => new(GeneratorId, GeneratorVersion);
    private const int StepsPerBar = 16;

    private readonly EuclideanMotifGenerator baseline = new();

    public Pattern Vary(Pattern parent, GeneratorVariationSettings settings) =>
        CurrentGeneratorVariation.Vary(parent, settings, Lineage);

    public Pattern Generate(EuclideanGeneratorSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.StepCount != StepsPerBar)
            throw new ArgumentException("The turnaround experiment requires a 16-step source bar.", nameof(settings));

        var source = baseline.Generate(settings);
        var steps = Enumerable.Range(0, 4).SelectMany(_ => source.Steps).ToArray();
        var changedSteps = ApplyTurnaround(steps, settings.TonalContext, settings.Role);

        var parameters = source.Recipe!.Parameters
            .Where(parameter => parameter.Key != "steps")
            .Append(GenerationHelpers.P("steps", 64))
            .Append(GenerationHelpers.P("source-generator", EuclideanMotifGenerator.GeneratorId))
            .Append(GenerationHelpers.P("source-version", EuclideanMotifGenerator.GeneratorVersion))
            .Append(GenerationHelpers.P("form", "a-a-a-a-turnaround"))
            .Append(GenerationHelpers.P("changed-steps", string.Join(",", changedSteps)));
        var recipe = new GeneratorRecipe(GeneratorId, GeneratorVersion, settings.Seed, null, parameters);

        return new Pattern(PatternId.New(), settings.Name, PatternSchemaVersion.Current, PatternMode.Melodic,
            settings.Timing, steps, settings.Role, settings.TonalContext, recipe);
    }

    private static int[] ApplyTurnaround(PatternStep[] steps, TonalContext context, MusicalRole role)
    {
        var valid = PentatonicPitchResolver.ValidPitches(context, role);
        var lateOnsets = Enumerable.Range(56, 8).Where(index => steps[index].Notes.Length > 0).ToArray();
        if (lateOnsets.Length < 2)
            throw new InvalidOperationException("The source bar needs two late onsets for an approach and resolution.");

        var finalStep = lateOnsets[^1];
        var approachStep = lateOnsets[^2];
        var finalPitch = (MelodicPitch)steps[finalStep].Notes[0].Pitch;
        var finalIndex = IndexOf(valid, finalPitch);
        var stableIndices = Enumerable.Range(0, valid.Count)
            .Where(index => valid[index].ScaleDegree is 0 or 3)
            .OrderBy(index => Math.Abs(index - finalIndex))
            .ToArray();
        var resolutionIndex = stableIndices.FirstOrDefault(index => index != finalIndex, finalIndex);
        if (finalPitch.ScaleDegree is not (0 or 3)) resolutionIndex = stableIndices[0];

        var originalApproach = (MelodicPitch)steps[approachStep].Notes[0].Pitch;
        var approachIndex = new[] { resolutionIndex - 1, resolutionIndex + 1 }
            .Concat(Enumerable.Range(0, valid.Count).OrderBy(index => Math.Abs(index - resolutionIndex)))
            .Where(index => index >= 0 && index < valid.Count && index != resolutionIndex && valid[index] != originalApproach)
            .Distinct()
            .First();

        ReplacePitch(steps, approachStep, valid[approachIndex]);
        ReplacePitch(steps, finalStep, valid[resolutionIndex]);
        return [approachStep, finalStep];
    }

    private static int IndexOf(IReadOnlyList<MelodicPitch> pitches, MelodicPitch pitch) =>
        Enumerable.Range(0, pitches.Count).First(index => pitches[index] == pitch);

    private static void ReplacePitch(PatternStep[] steps, int step, MelodicPitch pitch)
    {
        var original = steps[step];
        var note = original.Notes[0];
        steps[step] = new PatternStep([new PatternNote(pitch, note.Velocity, note.Gate)], original.Probability);
    }
}
