namespace JamWeaver.Core.Sequencer;

public readonly record struct PatternId
{
    public PatternId(Guid value)
    {
        Value = value;
        Validate(nameof(value));
    }
    public Guid Value { get; }
    public static PatternId New() => new(Guid.NewGuid());
    internal void Validate(string parameterName)
    {
        if (Value == Guid.Empty) throw new ArgumentException("Pattern ID cannot be empty.", parameterName);
    }
}

public readonly record struct PatternName
{
    public PatternName(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        value = value.Trim();
        Value = value;
        Validate(nameof(value));
    }
    public string Value { get; }
    public override string ToString() => Value;
    internal void Validate(string parameterName)
    {
        if (string.IsNullOrWhiteSpace(Value) || Value.Length > 80)
            throw new ArgumentOutOfRangeException(parameterName, "Pattern name must contain 1-80 characters.");
    }
}

public readonly record struct PatternSchemaVersion
{
    public PatternSchemaVersion(int value)
    {
        Value = value;
        Validate(nameof(value));
    }
    public int Value { get; }
    public static PatternSchemaVersion Current => new(1);
    internal void Validate(string parameterName)
    {
        if (Value < 1) throw new ArgumentOutOfRangeException(parameterName, "Pattern schema version must be positive.");
    }
}
