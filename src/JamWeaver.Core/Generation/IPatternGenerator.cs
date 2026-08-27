namespace JamWeaver.Core.Generation;

public interface IPatternGenerator
{
    GeneratorLineage Lineage { get; }
    Pattern Vary(Pattern parent, GeneratorVariationSettings settings);
}

public interface IPatternGenerator<in TSettings> : IPatternGenerator
{
    Pattern Generate(TSettings settings);
}
