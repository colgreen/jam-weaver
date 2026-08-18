using JamWeaver.Core.Sequencer;

namespace JamWeaver.Core.Persistence;

public sealed record PatternLibraryEntry(
    string FileName,
    bool IsValid,
    string? Error,
    PatternId? Id,
    string? Name,
    PatternMode? Mode,
    MusicalRole? Role,
    TonalContext? TonalContext,
    ulong? Seed,
    DateTimeOffset? SavedUtc);
