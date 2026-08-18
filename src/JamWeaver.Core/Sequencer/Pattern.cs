using System.Collections.Immutable;

namespace JamWeaver.Core.Sequencer;

public sealed class Pattern : IEquatable<Pattern>
{
    public Pattern(PatternId id, PatternName name, PatternSchemaVersion schemaVersion,
        PatternMode mode, PatternTiming timing, IEnumerable<PatternStep> steps,
        MusicalRole? role, TonalContext? tonalContext, GeneratorRecipe? recipe = null)
    {
        ArgumentNullException.ThrowIfNull(steps);
        var copiedSteps = steps.ToImmutableArray();
        if (copiedSteps.Length is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(steps), "Pattern must contain 1-256 steps.");
        if (copiedSteps.Any(step => step is null)) throw new ArgumentException("Steps cannot contain null.", nameof(steps));
        ValidateMode(mode, copiedSteps, role, tonalContext);
        Id = id;
        Name = name;
        SchemaVersion = schemaVersion;
        Mode = mode;
        Timing = timing;
        Steps = copiedSteps;
        Role = role;
        TonalContext = tonalContext;
        Recipe = recipe;
    }

    public PatternId Id { get; }
    public PatternName Name { get; }
    public PatternSchemaVersion SchemaVersion { get; }
    public PatternMode Mode { get; }
    public PatternTiming Timing { get; }
    public ImmutableArray<PatternStep> Steps { get; }
    public MusicalRole? Role { get; }
    public TonalContext? TonalContext { get; }
    public GeneratorRecipe? Recipe { get; }

    public Pattern Rename(PatternName name) => new(Id, name, SchemaVersion, Mode, Timing, Steps, Role, TonalContext, Recipe);
    public Pattern WithSteps(IEnumerable<PatternStep> steps) => NewSnapshot(Name, Mode, Timing, steps, Role, TonalContext);
    public Pattern WithRole(MusicalRole role) => Mode == PatternMode.Melodic
        ? NewSnapshot(Name, Mode, Timing, Steps, role, TonalContext)
        : throw new InvalidOperationException("Drum patterns do not have a musical role.");
    public Pattern WithTonalContext(TonalContext context) => Mode == PatternMode.Melodic
        ? NewSnapshot(Name, Mode, Timing, Steps, Role, context)
        : throw new InvalidOperationException("Drum patterns do not have tonal context.");

    private Pattern NewSnapshot(PatternName name, PatternMode mode, PatternTiming timing,
        IEnumerable<PatternStep> steps, MusicalRole? role, TonalContext? context) =>
        new(PatternId.New(), name, SchemaVersion, mode, timing, steps, role, context);

    private static void ValidateMode(PatternMode mode, ImmutableArray<PatternStep> steps,
        MusicalRole? role, TonalContext? context)
    {
        var pitches = steps.SelectMany(step => step.Notes).Select(note => note.Pitch);
        if (mode == PatternMode.Melodic)
        {
            if (role is null || context is null) throw new ArgumentException("Melodic patterns require role and tonal context.");
            if (pitches.Any(pitch => pitch is not MelodicPitch)) throw new ArgumentException("Melodic patterns can contain only melodic pitches.");
        }
        else
        {
            if (role is not null || context is not null) throw new ArgumentException("Drum patterns cannot have role or tonal context.");
            if (pitches.Any(pitch => pitch is not DrumPitch)) throw new ArgumentException("Drum patterns can contain only drum pitches.");
        }
    }

    public bool Equals(Pattern? other) => other is not null && Id == other.Id;
    public override bool Equals(object? obj) => obj is Pattern other && Equals(other);
    public override int GetHashCode() => Id.GetHashCode();
}
