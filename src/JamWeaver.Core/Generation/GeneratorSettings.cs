using System.Collections.Immutable;

namespace JamWeaver.Core.Generation;

public sealed record EuclideanGeneratorSettings
{
    public EuclideanGeneratorSettings(PatternName name, int stepCount, PatternTiming timing, TonalContext tonalContext,
        MusicalRole role, NormalizedAmount density, NormalizedAmount movement, NormalizedAmount repetition,
        NormalizedAmount gate, NormalizedAmount velocityVariation, ulong seed)
    {
        name.Validate(nameof(name));
        timing.Validate(nameof(timing));
        tonalContext.Validate(nameof(tonalContext));
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        if (stepCount is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(stepCount));
        (Name, StepCount, Timing, TonalContext, Role, Density, Movement, Repetition, Gate, VelocityVariation, Seed) =
            (name, stepCount, timing, tonalContext, role, density, movement, repetition, gate, velocityVariation, seed);
    }
    public PatternName Name { get; }
    public int StepCount { get; }
    public PatternTiming Timing { get; }
    public TonalContext TonalContext { get; }
    public MusicalRole Role { get; }
    public NormalizedAmount Density { get; }
    public NormalizedAmount Movement { get; }
    public NormalizedAmount Repetition { get; }
    public NormalizedAmount Gate { get; }
    public NormalizedAmount VelocityVariation { get; }
    public ulong Seed { get; }
}

public sealed record DrumVoiceSettings(MidiValue Note, NormalizedAmount Density);

public sealed class EuclideanDrumGeneratorSettings
{
    public EuclideanDrumGeneratorSettings(PatternName name, int stepCount, PatternTiming timing,
        IEnumerable<DrumVoiceSettings> voices, NormalizedAmount gate, NormalizedAmount velocityVariation, ulong seed)
    {
        name.Validate(nameof(name));
        timing.Validate(nameof(timing));
        if (stepCount is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(stepCount));
        ArgumentNullException.ThrowIfNull(voices);
        var copied = voices.ToImmutableArray();
        if (copied.Length is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(voices));
        if (copied.Any(voice => voice is null)) throw new ArgumentException("Drum voices cannot contain null.", nameof(voices));
        if (copied.Select(voice => voice.Note).Distinct().Count() != copied.Length)
            throw new ArgumentException("Drum voice notes must be distinct.", nameof(voices));
        (Name, StepCount, Timing, Voices, Gate, VelocityVariation, Seed) =
            (name, stepCount, timing, copied, gate, velocityVariation, seed);
    }
    public PatternName Name { get; }
    public int StepCount { get; }
    public PatternTiming Timing { get; }
    public ImmutableArray<DrumVoiceSettings> Voices { get; }
    public NormalizedAmount Gate { get; }
    public NormalizedAmount VelocityVariation { get; }
    public ulong Seed { get; }
}

public readonly record struct MutationSettings(NormalizedAmount Strength, ulong Seed);
