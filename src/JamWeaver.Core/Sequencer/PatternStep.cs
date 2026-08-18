using System.Collections.Immutable;
using JamWeaver.Core.Midi;

namespace JamWeaver.Core.Sequencer;

public readonly record struct TriggerProbability
{
    public TriggerProbability(double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
    public double Value { get; }
    public static TriggerProbability Always => new(1);
}

public readonly record struct NoteGate
{
    public NoteGate(double value)
    {
        if (!double.IsFinite(value) || value is <= 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
    public double Value { get; }
}

public sealed record PatternNote
{
    public PatternNote(PatternPitch pitch, MidiValue velocity, NoteGate gate)
    {
        ArgumentNullException.ThrowIfNull(pitch);
        if (velocity.Value == 0) throw new ArgumentOutOfRangeException(nameof(velocity), "Note velocity must be 1-127.");
        Pitch = pitch;
        Velocity = velocity;
        Gate = gate;
    }
    public PatternPitch Pitch { get; }
    public MidiValue Velocity { get; }
    public NoteGate Gate { get; }
}

public sealed class PatternStep
{
    public PatternStep(IEnumerable<PatternNote> notes, TriggerProbability probability)
    {
        ArgumentNullException.ThrowIfNull(notes);
        Notes = notes.ToImmutableArray();
        if (Notes.Any(note => note is null)) throw new ArgumentException("Notes cannot contain null.", nameof(notes));
        if (Notes.Select(note => note.Pitch).Distinct().Count() != Notes.Length)
            throw new ArgumentException("A step cannot contain duplicate pitches.", nameof(notes));
        Probability = probability;
    }
    public ImmutableArray<PatternNote> Notes { get; }
    public TriggerProbability Probability { get; }
    public static PatternStep Rest => new([], TriggerProbability.Always);
}
