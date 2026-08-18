using System.Numerics;

namespace JamWeaver.Core.Generation.Groove;

public enum GrooveCategory { Foundation, Offbeat, Anticipation, LongShort, SparseAnswer, Broken }
public enum GrooveAccent : byte { Ghost, Light, Normal, Strong }

public sealed record GrooveTemplate
{
    public GrooveTemplate(string id, GrooveCategory category, ushort requiredOnsets,
        ushort optionalOnsets, ushort movableOnsets, IReadOnlyList<GrooveAccent> accents,
        int entrySuitability, int turnaroundSuitability)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Template ID is required.", nameof(id));
        if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
        if ((requiredOnsets & optionalOnsets) != 0) throw new ArgumentException("Required and optional masks cannot overlap.");
        var sounding = (ushort)(requiredOnsets | optionalOnsets);
        if ((movableOnsets & ~sounding) != 0) throw new ArgumentException("Movable onsets must sound in the template.");
        if ((requiredOnsets & 1) == 0) throw new ArgumentException("Bass templates must require step zero.");
        if (accents is null || accents.Count != 16) throw new ArgumentException("Exactly 16 accents are required.", nameof(accents));
        if (entrySuitability is < 0 or > 2 || turnaroundSuitability is < 0 or > 2)
            throw new ArgumentOutOfRangeException(nameof(entrySuitability), "Suitability must be 0-2.");
        if (BitOperations.PopCount(sounding) is < 3 or > 8) throw new ArgumentException("Template must contain 3-8 onsets.");
        if (RhythmMetrics.Measure(sounding).MaximumRestGap > 8) throw new ArgumentException("Template rest gap is too long.");
        Id = id; Category = category; RequiredOnsets = requiredOnsets; OptionalOnsets = optionalOnsets;
        MovableOnsets = movableOnsets; Accents = accents.ToArray(); EntrySuitability = entrySuitability;
        TurnaroundSuitability = turnaroundSuitability;
    }

    public string Id { get; }
    public GrooveCategory Category { get; }
    public ushort RequiredOnsets { get; }
    public ushort OptionalOnsets { get; }
    public ushort MovableOnsets { get; }
    public IReadOnlyList<GrooveAccent> Accents { get; }
    public int EntrySuitability { get; }
    public int TurnaroundSuitability { get; }
    public ushort Onsets => (ushort)(RequiredOnsets | OptionalOnsets);
}
