namespace JamWeaver.Core.Generation.Groove;

public static class GrooveVocabulary
{
    public const int Version = 1;
    private static readonly GrooveAccent[] DefaultAccents =
        Enumerable.Range(0, 16).Select(i => i == 0 ? GrooveAccent.Strong : i % 4 == 0 ? GrooveAccent.Normal : GrooveAccent.Light).ToArray();

    public static IReadOnlyList<GrooveTemplate> Templates { get; } =
    [
        T("foundation-1", GrooveCategory.Foundation, "1000100010001000", "0010000000100000", 2, 1),
        T("foundation-2", GrooveCategory.Foundation, "1000001010000010", "0001000000010000", 2, 1),
        T("offbeat-1", GrooveCategory.Offbeat, "1000000010000000", "0010001000100010", 2, 1),
        T("offbeat-2", GrooveCategory.Offbeat, "1000000010000000", "0001000100010100", 1, 1),
        T("anticipation-1", GrooveCategory.Anticipation, "1000000010000000", "0001001000010010", 1, 2),
        T("anticipation-2", GrooveCategory.Anticipation, "1000000010000000", "0000011000000110", 1, 2),
        T("long-short-1", GrooveCategory.LongShort, "1000000010000000", "0000110000001100", 2, 1),
        T("long-short-2", GrooveCategory.LongShort, "1000000010000000", "0011000000110000", 2, 1),
        T("sparse-answer-1", GrooveCategory.SparseAnswer, "1000000010000000", "0001000000010010", 2, 2),
        T("sparse-answer-2", GrooveCategory.SparseAnswer, "1000000010000000", "0010000000100100", 2, 2),
        T("broken-1", GrooveCategory.Broken, "1000000010000000", "0001001000100100", 1, 2),
        T("broken-2", GrooveCategory.Broken, "1000000010000000", "0010010000010010", 1, 2)
    ];

    public static GrooveTemplate Get(string id) => Templates.SingleOrDefault(t => t.Id == id)
        ?? throw new ArgumentException($"Unknown groove template '{id}'.", nameof(id));

    private static GrooveTemplate T(string id, GrooveCategory category, string required, string optional,
        int entry, int turnaround)
    {
        var requiredMask = Mask(required); var optionalMask = Mask(optional);
        return new GrooveTemplate(id, category, requiredMask, optionalMask, optionalMask,
            DefaultAccents, entry, turnaround);
    }

    private static ushort Mask(string grid)
    {
        ushort result = 0;
        for (var i = 0; i < grid.Length; i++) if (grid[i] == '1') result |= (ushort)(1 << i);
        return result;
    }
}
