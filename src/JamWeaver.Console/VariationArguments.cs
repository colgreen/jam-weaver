using Redzen.Random;
using static JamWeaver.ConsoleApp.ConsoleArgumentParser;

namespace JamWeaver.ConsoleApp;

/// <summary>Parses a tokenized vary command into settings for its optional target, strength, and named seed.</summary>
internal static class VariationArguments
{
    public static GeneratorVariationSettings Parse(string[] parts, IRandomSource seedSource)
    {
        var index = 1;
        VariationTarget? target = null;
        if (index < parts.Length
            && Enum.TryParse<VariationTarget>(parts[index], true, out var parsedTarget)
            && Enum.IsDefined(parsedTarget))
        {
            target = parsedTarget;
            index++;
        }

        var strength = .3;
        if (index < parts.Length && !parts[index].Equals("seed", StringComparison.OrdinalIgnoreCase))
            strength = ParseNumber(parts, index++);
        ulong? seed = null;
        if (index < parts.Length)
        {
            if (!parts[index].Equals("seed", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Expected 'seed <number>' after variation strength.");
            seed = ParseSeed(parts, ++index);
            index++;
        }
        if (index != parts.Length) throw new ArgumentException("Unexpected vary argument.");

        // Preserve seed consumption after grammar checks and before strength validation.
        var resolvedSeed = seed ?? seedSource.NextULong();
        return new GeneratorVariationSettings(target, new NormalizedAmount(strength), resolvedSeed);
    }
}
