using JamWeaver.Core.Generation.Phrase;
using Redzen.Random;

namespace JamWeaver.Core.Generation;

/// <summary>
/// Experimental four-bar A | A | A | A-turnaround form. The first three bars and
/// rhythm of the fourth are exact copies of the version-3 Euclidean baseline.
/// </summary>
public sealed class TurnaroundLoopGenerator : IPatternGenerator<EuclideanGeneratorSettings>
{
    public const string GeneratorId = "melodic-euclidean-turnaround";
    public const int GeneratorVersion = 1;
    public const string VariationId = "melodic-euclidean-turnaround-variation";
    public const int VariationVersion = 1;
    public GeneratorLineage Lineage => new(GeneratorId, GeneratorVersion);
    private const int StepsPerBar = 16;

    private readonly EuclideanMotifGenerator baseline = new();

    public Pattern Vary(Pattern parent, GeneratorVariationSettings settings)
    {
        ValidateVariationParent(parent);
        var random = RandomDefaults.CreateRandomSource(settings.Seed);
        var target = ResolveTarget(settings, random);
        var steps = parent.Steps.ToArray();
        var baseEditCount = settings.Strength.Value >= .67 ? 2 : 1;
        var edited = target switch
        {
            PhraseMutationTarget.Rhythm => VaryRhythm(parent, steps, random, baseEditCount),
            PhraseMutationTarget.Notes => VaryNotes(parent, steps, random, baseEditCount),
            PhraseMutationTarget.Expression => VaryExpression(steps, random, baseEditCount),
            PhraseMutationTarget.Turnaround => VaryTurnaround(parent, steps, random),
            _ => throw new ArgumentOutOfRangeException(nameof(settings))
        };

        var recipe = new GeneratorRecipe(VariationId, VariationVersion, settings.Seed, parent.Id,
        [
            GenerationHelpers.P("target", target.ToString()),
            GenerationHelpers.P("strength", settings.Strength.Value),
            GenerationHelpers.P("edit-count", edited.Length),
            GenerationHelpers.P("edited-steps", string.Join(",", edited))
        ], Lineage);
        return new Pattern(PatternId.New(), parent.Name, parent.SchemaVersion, parent.Mode, parent.Timing,
            steps, parent.Role, parent.TonalContext, recipe);
    }

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

    private static PhraseMutationTarget ResolveTarget(GeneratorVariationSettings settings, IRandomSource random)
    {
        if (settings.Target is { } explicitTarget && explicitTarget != PhraseMutationTarget.All)
            return explicitTarget;
        if (settings.Target is null && settings.Strength.Value < .67)
            return random.Next(4) < 3 ? PhraseMutationTarget.Turnaround : PhraseMutationTarget.Notes;
        return (PhraseMutationTarget)random.Next(4);
    }

    private static int[] VaryRhythm(Pattern parent, PatternStep[] steps, IRandomSource random, int editCount)
    {
        var edited = new List<int>();
        var used = new HashSet<int>();
        for (var edit = 0; edit < editCount; edit++)
        {
            var candidates = Enumerable.Range(1, StepsPerBar - 1)
                .Where(index => steps[index].Notes.Length > 0 && !used.Contains(index))
                .Select(index => new
                {
                    Source = index,
                    Rests = Enumerable.Range(index < 8 ? 0 : 8, 8)
                        .Where(destination => steps[destination].Notes.Length == 0 && !used.Contains(destination)).ToArray()
                })
                .Where(candidate => candidate.Rests.Length > 0).ToArray();
            if (candidates.Length == 0) break;
            var candidate = candidates[random.Next(candidates.Length)];
            var minimumDistance = candidate.Rests.Min(destination => Math.Abs(destination - candidate.Source));
            var nearest = candidate.Rests.Where(destination => Math.Abs(destination - candidate.Source) == minimumDistance).ToArray();
            var destination = nearest[random.Next(nearest.Length)];
            var moved = steps[candidate.Source];
            for (var bar = 0; bar < 4; bar++)
            {
                steps[bar * StepsPerBar + candidate.Source] = PatternStep.Rest;
                steps[bar * StepsPerBar + destination] = moved;
            }
            edited.Add(candidate.Source);
            edited.Add(destination);
            used.Add(candidate.Source);
            used.Add(destination);
        }
        if (edited.Count == 0) throw new InvalidOperationException("No valid turnaround-loop rhythm variation was available.");
        ApplyTurnaround(steps, parent.TonalContext!.Value, parent.Role!.Value);
        return edited.Distinct().Order().ToArray();
    }

    private static int[] VaryNotes(Pattern parent, PatternStep[] steps, IRandomSource random, int editCount)
    {
        var valid = PentatonicPitchResolver.ValidPitches(parent.TonalContext!.Value, parent.Role!.Value);
        var candidates = Enumerable.Range(0, StepsPerBar).Where(index => steps[index].Notes.Length > 0)
            .OrderBy(_ => random.NextUInt()).Take(editCount).ToArray();
        foreach (var index in candidates)
        {
            var pitch = (MelodicPitch)steps[index].Notes[0].Pitch;
            var pitchIndex = IndexOf(valid, pitch);
            var direction = random.Next(2) == 0 ? -1 : 1;
            var next = Math.Clamp(pitchIndex + direction, 0, valid.Count - 1);
            if (next == pitchIndex) next = Math.Clamp(pitchIndex - direction, 0, valid.Count - 1);
            for (var bar = 0; bar < 4; bar++) ReplacePitch(steps, bar * StepsPerBar + index, valid[next]);
        }
        ApplyTurnaround(steps, parent.TonalContext.Value, parent.Role.Value);
        return candidates;
    }

    private static int[] VaryExpression(PatternStep[] steps, IRandomSource random, int editCount)
    {
        var candidates = Enumerable.Range(0, StepsPerBar).Where(index => steps[index].Notes.Length > 0)
            .OrderBy(_ => random.NextUInt()).Take(editCount).ToArray();
        foreach (var index in candidates)
        {
            var velocityDelta = random.Next(2) == 0 ? -5 : 5;
            var gateDelta = random.Next(2) == 0 ? -.05 : .05;
            for (var bar = 0; bar < 4; bar++)
            {
                var stepIndex = bar * StepsPerBar + index;
                var step = steps[stepIndex];
                var notes = step.Notes.Select(note => new PatternNote(note.Pitch,
                    new MidiValue(Math.Clamp(note.Velocity.Value + velocityDelta, 1, 127)),
                    new NoteGate(Math.Clamp(note.Gate.Value + gateDelta, .05, 1)))).ToArray();
                steps[stepIndex] = new PatternStep(notes, step.Probability);
            }
        }
        return candidates;
    }

    private static int[] VaryTurnaround(Pattern parent, PatternStep[] steps, IRandomSource random)
    {
        var valid = PentatonicPitchResolver.ValidPitches(parent.TonalContext!.Value, parent.Role!.Value);
        var lateOnsets = Enumerable.Range(56, 8).Where(index => steps[index].Notes.Length > 0).ToArray();
        var approachStep = lateOnsets[^2];
        var finalStep = lateOnsets[^1];
        var currentApproach = (MelodicPitch)steps[approachStep].Notes[0].Pitch;
        var currentResolution = (MelodicPitch)steps[finalStep].Notes[0].Pitch;
        var gestures = Enumerable.Range(0, valid.Count)
            .Where(resolution => valid[resolution].ScaleDegree is 0 or 3)
            .SelectMany(resolution => new[] { resolution - 1, resolution + 1 }
                .Where(approach => approach >= 0 && approach < valid.Count)
                .Select(approach => (Approach: valid[approach], Resolution: valid[resolution])))
            .Where(gesture => gesture.Approach != gesture.Resolution
                && (gesture.Approach != currentApproach || gesture.Resolution != currentResolution))
            .Distinct().ToArray();
        if (gestures.Length == 0) throw new InvalidOperationException("No alternative turnaround gesture was available.");
        var gesture = gestures[random.Next(gestures.Length)];
        ReplacePitch(steps, approachStep, gesture.Approach);
        ReplacePitch(steps, finalStep, gesture.Resolution);
        return [approachStep, finalStep];
    }

    private void ValidateVariationParent(Pattern parent)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (parent.Recipe?.Lineage != Lineage)
            throw new InvalidOperationException("Turnaround Loop variation requires matching generator lineage.");
        if (parent.Mode != PatternMode.Melodic || parent.Steps.Length != 64
            || parent.Timing != PatternTiming.SixteenthNotes || parent.Role is null || parent.TonalContext is null)
            throw new InvalidOperationException("Turnaround Loop variation requires a four-bar melodic sixteenth-note pattern.");
        for (var index = 0; index < StepsPerBar; index++)
        {
            if (!SameStep(parent.Steps[index], parent.Steps[index + 16])
                || !SameStep(parent.Steps[index], parent.Steps[index + 32]))
                throw new InvalidOperationException("Turnaround Loop variation requires the first three bars to be exact A repetitions.");
            if ((parent.Steps[index].Notes.Length > 0) != (parent.Steps[index + 48].Notes.Length > 0))
                throw new InvalidOperationException("Turnaround Loop variation requires identical rhythm in all four bars.");
        }
        var lateOnsets = Enumerable.Range(56, 8).Where(index => parent.Steps[index].Notes.Length > 0).ToArray();
        if (lateOnsets.Length < 2)
            throw new InvalidOperationException("Turnaround Loop variation requires two late turnaround onsets.");
        var turnaroundSteps = lateOnsets.TakeLast(2).Select(index => index - 48).ToHashSet();
        for (var index = 0; index < StepsPerBar; index++)
        {
            var source = parent.Steps[index];
            var final = parent.Steps[index + 48];
            if (!turnaroundSteps.Contains(index) && !SameStep(source, final))
                throw new InvalidOperationException("Turnaround Loop variation permits pitch changes only in the final turnaround gesture.");
            if (turnaroundSteps.Contains(index)
                && (source.Probability != final.Probability
                    || !source.Notes.Select(note => (note.Velocity, note.Gate))
                        .SequenceEqual(final.Notes.Select(note => (note.Velocity, note.Gate)))))
                throw new InvalidOperationException("Turnaround Loop turnaround steps may change pitch only.");
        }
    }

    private static bool SameStep(PatternStep left, PatternStep right) =>
        left.Probability == right.Probability && left.Notes.SequenceEqual(right.Notes);

    private static int IndexOf(IReadOnlyList<MelodicPitch> pitches, MelodicPitch pitch) =>
        Enumerable.Range(0, pitches.Count).First(index => pitches[index] == pitch);

    private static void ReplacePitch(PatternStep[] steps, int step, MelodicPitch pitch)
    {
        var original = steps[step];
        var note = original.Notes[0];
        steps[step] = new PatternStep([new PatternNote(pitch, note.Velocity, note.Gate)], original.Probability);
    }
}
