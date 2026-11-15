using JamWeaver.Core.Generation.Phrase;

namespace JamWeaver.Core.Generation;

public readonly record struct GeneratorVariationSettings(
    PhraseMutationTarget? Target, NormalizedAmount Strength, ulong Seed)
{
    internal void Validate()
    {
        if (Target is { } target && !Enum.IsDefined(target)) throw new ArgumentOutOfRangeException(nameof(Target));
    }
}

internal static class CurrentGeneratorVariation
{
    public static Pattern Vary(Pattern parent, GeneratorVariationSettings settings, GeneratorLineage owner)
    {
        ArgumentNullException.ThrowIfNull(parent);
        settings.Validate();
        if (parent.Recipe?.Lineage != owner)
            throw new InvalidOperationException(
                $"Generator '{owner.GeneratorId}' version {owner.GeneratorVersion} cannot vary a pattern from another generator.");
        return settings.Target is { } target
            ? new PhrasePatternMutator().Mutate(parent,
                new PhraseMutationSettings(target, settings.Strength, settings.Seed))
            : new PatternMutator().Mutate(parent, new MutationSettings(settings.Strength, settings.Seed));
    }
}
