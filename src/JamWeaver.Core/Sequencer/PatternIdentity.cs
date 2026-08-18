namespace JamWeaver.Core.Sequencer;

public readonly record struct PatternId
{
    public PatternId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("Pattern ID cannot be empty.", nameof(value));
        Value = value;
    }
    public Guid Value { get; }
    public static PatternId New() => new(Guid.NewGuid());
}

public readonly record struct PatternName
{
    public PatternName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value = value.Trim();
        if (value.Length is < 1 or > 80) throw new ArgumentOutOfRangeException(nameof(value), "Pattern name must contain 1-80 characters.");
        Value = value;
    }
    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct PatternSchemaVersion
{
    public PatternSchemaVersion(int value)
    {
        if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
    public int Value { get; }
    public static PatternSchemaVersion Current => new(1);
}
