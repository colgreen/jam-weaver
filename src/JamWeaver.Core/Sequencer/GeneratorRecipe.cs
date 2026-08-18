using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace JamWeaver.Core.Sequencer;

public enum RecipeValueKind { Integer, Number, Boolean, Text }

public readonly record struct RecipeValue
{
    private RecipeValue(RecipeValueKind kind, long integer, double number, bool boolean, string? text) =>
        (Kind, Integer, Number, Boolean, Text) = (kind, integer, number, boolean, text);

    public RecipeValueKind Kind { get; }
    public long Integer { get; }
    public double Number { get; }
    public bool Boolean { get; }
    public string? Text { get; }
    public static RecipeValue FromInteger(long value) => new(RecipeValueKind.Integer, value, default, default, null);
    public static RecipeValue FromNumber(double value) => double.IsFinite(value)
        ? new(RecipeValueKind.Number, default, value, default, null)
        : throw new ArgumentOutOfRangeException(nameof(value));
    public static RecipeValue FromBoolean(bool value) => new(RecipeValueKind.Boolean, default, default, value, null);
    public static RecipeValue FromText(string value) => new(RecipeValueKind.Text, default, default, default, value ?? throw new ArgumentNullException(nameof(value)));
}

public sealed class GeneratorRecipe
{
    private static readonly Regex KeyPattern = new("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant);

    public GeneratorRecipe(string generatorId, int generatorVersion, ulong seed,
        PatternId? parentPatternId, IEnumerable<KeyValuePair<string, RecipeValue>> parameters)
    {
        ArgumentNullException.ThrowIfNull(generatorId);
        generatorId = generatorId.Trim();
        if (generatorId.Length == 0) throw new ArgumentException("Generator ID is required.", nameof(generatorId));
        if (generatorVersion < 1) throw new ArgumentOutOfRangeException(nameof(generatorVersion));
        ArgumentNullException.ThrowIfNull(parameters);

        var builder = ImmutableSortedDictionary.CreateBuilder<string, RecipeValue>(StringComparer.Ordinal);
        foreach (var pair in parameters)
        {
            var key = pair.Key?.Trim() ?? throw new ArgumentException("Parameter key cannot be null.", nameof(parameters));
            if (!KeyPattern.IsMatch(key)) throw new ArgumentException($"Invalid recipe parameter key '{key}'.", nameof(parameters));
            if (!builder.TryAdd(key, pair.Value)) throw new ArgumentException($"Duplicate recipe parameter '{key}'.", nameof(parameters));
        }
        GeneratorId = generatorId;
        GeneratorVersion = generatorVersion;
        Seed = seed;
        ParentPatternId = parentPatternId;
        Parameters = builder.ToImmutable();
    }

    public string GeneratorId { get; }
    public int GeneratorVersion { get; }
    public ulong Seed { get; }
    public PatternId? ParentPatternId { get; }
    public ImmutableSortedDictionary<string, RecipeValue> Parameters { get; }
}
