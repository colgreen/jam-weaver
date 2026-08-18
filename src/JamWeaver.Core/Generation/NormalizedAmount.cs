namespace JamWeaver.Core.Generation;

public readonly record struct NormalizedAmount
{
    public NormalizedAmount(double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
    public double Value { get; }
    public static NormalizedAmount Low => new(.25);
    public static NormalizedAmount Medium => new(.5);
    public static NormalizedAmount High => new(.75);
}
