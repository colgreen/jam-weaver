using Redzen.Random;

namespace JamWeaver.Core.Generation;

public sealed class PatternMutator
{
    public const string GeneratorId = "controlled-mutation";
    public const int GeneratorVersion = 1;

    public Pattern Mutate(Pattern parent, MutationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(parent);
        var random = RandomDefaults.CreateRandomSource(settings.Seed);
        var steps = parent.Steps.ToArray();
        var maximum = Math.Max(1, steps.Length / 3);
        var editCount = 1 + (int)Math.Round((maximum - 1) * settings.Strength.Value, MidpointRounding.AwayFromZero);
        var indices = Enumerable.Range(0, steps.Length).OrderBy(_ => random.NextUInt()).Take(editCount).ToArray();
        foreach (var index in indices) steps[index] = MutateStep(parent, steps, index, random);

        var recipe = new GeneratorRecipe(GeneratorId, GeneratorVersion, settings.Seed, parent.Id,
        [
            GenerationHelpers.P("strength", settings.Strength.Value),
            GenerationHelpers.P("edit-count", editCount),
            GenerationHelpers.P("mode", parent.Mode.ToString())
        ]);
        return new Pattern(PatternId.New(), parent.Name, parent.SchemaVersion, parent.Mode, parent.Timing,
            steps, parent.Role, parent.TonalContext, recipe);
    }

    private static PatternStep MutateStep(Pattern parent, PatternStep[] steps, int index, IRandomSource random)
    {
        var step = steps[index];
        if (step.Notes.Length == 0)
        {
            var sources = steps.Where(candidate => candidate.Notes.Length > 0).ToArray();
            if (sources.Length == 0) throw new InvalidOperationException("A pattern with no notes cannot be mutated.");
            return new PatternStep(sources[random.Next(sources.Length)].Notes, TriggerProbability.Always);
        }

        var operation = random.Next(4);
        if (operation == 0 && parent.Mode == PatternMode.Melodic)
            return ChangeMelodicPitch(parent, step, random);
        if (operation == 1) return ChangeVelocity(step, random);
        if (operation == 2) return ChangeGate(step, random);

        var soundingSteps = steps.Count(candidate => candidate.Notes.Length > 0);
        return soundingSteps > 1 ? PatternStep.Rest : ChangeVelocity(step, random);
    }

    private static PatternStep ChangeMelodicPitch(Pattern parent, PatternStep step, IRandomSource random)
    {
        var note = step.Notes[0];
        var current = (MelodicPitch)note.Pitch;
        var valid = PentatonicPitchResolver.ValidPitches(parent.TonalContext!.Value, parent.Role!.Value);
        var index = Enumerable.Range(0, valid.Count).First(i => valid[i] == current);
        var direction = random.Next(2) == 0 ? -1 : 1;
        var nextIndex = Math.Clamp(index + direction, 0, valid.Count - 1);
        if (nextIndex == index) nextIndex = Math.Clamp(index - direction, 0, valid.Count - 1);
        var changed = new PatternNote(valid[nextIndex], note.Velocity, note.Gate);
        return new PatternStep([changed, .. step.Notes.Skip(1)], step.Probability);
    }

    private static PatternStep ChangeVelocity(PatternStep step, IRandomSource random)
    {
        var notes = step.Notes.Select(note => new PatternNote(note.Pitch,
            new MidiValue(Math.Clamp(note.Velocity.Value + (random.Next(2) == 0 ? -1 : 1), 1, 127)), note.Gate)).ToArray();
        if (notes.SequenceEqual(step.Notes))
            notes[0] = new PatternNote(notes[0].Pitch, new MidiValue(notes[0].Velocity.Value == 127 ? 126 : 2), notes[0].Gate);
        return new PatternStep(notes, step.Probability);
    }

    private static PatternStep ChangeGate(PatternStep step, IRandomSource random)
    {
        var notes = step.Notes.Select(note => new PatternNote(note.Pitch, note.Velocity,
            new NoteGate(Math.Clamp(note.Gate.Value + (random.Next(2) == 0 ? -.05 : .05), .05, 1)))).ToArray();
        if (notes.SequenceEqual(step.Notes))
            notes[0] = new PatternNote(notes[0].Pitch, notes[0].Velocity, new NoteGate(notes[0].Gate.Value == 1 ? .95 : 1));
        return new PatternStep(notes, step.Probability);
    }
}
