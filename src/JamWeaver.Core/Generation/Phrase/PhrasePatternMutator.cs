using Redzen.Random;

namespace JamWeaver.Core.Generation.Phrase;

public enum PhraseMutationTarget { Rhythm, Notes, Expression, Turnaround, All }
public readonly record struct PhraseMutationSettings(PhraseMutationTarget Target, NormalizedAmount Strength, ulong Seed);

public sealed class PhrasePatternMutator
{
    public const string GeneratorId = "structured-phrase-mutation";
    public const int GeneratorVersion = 1;

    public Pattern Mutate(Pattern parent, PhraseMutationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(parent);
        if (parent.Mode != PatternMode.Melodic) throw new InvalidOperationException("Structured phrase mutation requires a melodic pattern.");
        if (!Enum.IsDefined(settings.Target)) throw new ArgumentOutOfRangeException(nameof(settings));
        var random = RandomDefaults.CreateRandomSource(settings.Seed);
        var steps = parent.Steps.ToArray();
        var structuralMask = StructuralMask(parent);
        var budget = 1 + (int)Math.Round(Math.Max(1, steps.Length / 8 - 1) * settings.Strength.Value,
            MidpointRounding.AwayFromZero);
        var changes = 0;
        for (var edit = 0; edit < budget; edit++)
        {
            var target = settings.Target == PhraseMutationTarget.All
                ? (PhraseMutationTarget)(edit % 4) : settings.Target;
            if (target == PhraseMutationTarget.Turnaround)
                changes += MutateNoteOrExpression(parent, steps, random, structuralMask, true, edit % 2 == 0);
            else if (target == PhraseMutationTarget.Rhythm)
                changes += MutateRhythm(steps, random, structuralMask);
            else if (target == PhraseMutationTarget.Notes)
                changes += MutateNoteOrExpression(parent, steps, random, structuralMask, false, true);
            else if (target == PhraseMutationTarget.Expression)
                changes += MutateNoteOrExpression(parent, steps, random, structuralMask, false, false);
        }
        if (changes == 0) throw new InvalidOperationException("No valid structured phrase mutation was available.");

        var recipe = new GeneratorRecipe(GeneratorId, GeneratorVersion, settings.Seed, parent.Id,
        [
            GenerationHelpers.P("target", settings.Target.ToString()),
            GenerationHelpers.P("strength", settings.Strength.Value),
            GenerationHelpers.P("edit-count", changes),
            GenerationHelpers.P("structural-mask", structuralMask.ToString("X16"))
        ]);
        return new Pattern(PatternId.New(), parent.Name, parent.SchemaVersion, parent.Mode, parent.Timing,
            steps, parent.Role, parent.TonalContext, recipe);
    }

    private static int MutateRhythm(PatternStep[] steps, IRandomSource random, ulong structuralMask)
    {
        var bars = Math.Max(1, steps.Length / 16);
        var bar = random.Next(bars);
        var start = bar * 16;
        var sources = Enumerable.Range(start, Math.Min(16, steps.Length - start))
            .Where(index => steps[index].Notes.Length > 0 && !IsStructural(structuralMask, index)).ToArray();
        var rests = Enumerable.Range(start, Math.Min(16, steps.Length - start))
            .Where(index => steps[index].Notes.Length == 0).ToArray();
        if (sources.Length == 0 || rests.Length == 0) return 0;
        var source = sources[random.Next(sources.Length)];
        var destination = rests.OrderBy(index => Math.Abs(index - source)).First();
        steps[destination] = steps[source];
        steps[source] = PatternStep.Rest;
        return 1;
    }

    private static int MutateNoteOrExpression(Pattern parent, PatternStep[] steps, IRandomSource random,
        ulong structuralMask, bool finalBarOnly, bool notes)
    {
        var first = finalBarOnly ? Math.Max(0, steps.Length - 16) : 0;
        var candidates = Enumerable.Range(first, steps.Length - first)
            .Where(index => steps[index].Notes.Length > 0 && !IsStructural(structuralMask, index)).ToArray();
        if (candidates.Length == 0) return 0;
        var index = candidates[random.Next(candidates.Length)];
        var step = steps[index];
        if (notes)
        {
            var note = step.Notes[0];
            var pitch = (MelodicPitch)note.Pitch;
            var valid = PentatonicPitchResolver.ValidPitches(parent.TonalContext!.Value, parent.Role!.Value);
            var pitchIndex = Enumerable.Range(0, valid.Count).First(i => valid[i] == pitch);
            var direction = random.Next(2) == 0 ? -1 : 1;
            var next = Math.Clamp(pitchIndex + direction, 0, valid.Count - 1);
            if (next == pitchIndex) next = Math.Clamp(pitchIndex - direction, 0, valid.Count - 1);
            var changed = new PatternNote(valid[next], note.Velocity, note.Gate);
            steps[index] = new PatternStep([changed, .. step.Notes.Skip(1)], step.Probability);
        }
        else
        {
            var changed = step.Notes.Select(note => new PatternNote(note.Pitch,
                new MidiValue(note.Velocity.Value >= 123 ? note.Velocity.Value - 5 : note.Velocity.Value + 5),
                new NoteGate(note.Gate.Value >= .95 ? .85 : Math.Min(1, note.Gate.Value + .05)))).ToArray();
            steps[index] = new PatternStep(changed, step.Probability);
        }
        return 1;
    }

    private static ulong StructuralMask(Pattern pattern)
    {
        if (pattern.Recipe?.Parameters.TryGetValue("structural-mask", out var value) == true
            && value.Kind == RecipeValueKind.Text
            && ulong.TryParse(value.Text, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)) return parsed;
        var mask = 0UL;
        for (var step = 0; step < pattern.Steps.Length && step < 64; step += 16)
            if (pattern.Steps[step].Notes.Length > 0) mask |= 1UL << step;
        return mask;
    }

    private static bool IsStructural(ulong mask, int step) => step < 64 && (mask & (1UL << step)) != 0;
}
