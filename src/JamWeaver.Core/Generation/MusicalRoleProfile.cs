namespace JamWeaver.Core.Generation;

public sealed record MusicalRoleProfile(int MinimumNote, int MaximumNote, int MinimumHitsPer16,
    int MaximumHitsPer16, int BaseVelocity)
{
    public static MusicalRoleProfile For(MusicalRole role) => role switch
    {
        MusicalRole.Bass => new(36, 52, 3, 8, 100),
        MusicalRole.Middle => new(48, 72, 4, 12, 88),
        MusicalRole.High => new(67, 88, 2, 8, 76),
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public int HitCount(int stepCount, NormalizedAmount density)
    {
        var min = Math.Max(1, (int)Math.Round(MinimumHitsPer16 * stepCount / 16.0, MidpointRounding.AwayFromZero));
        var max = Math.Clamp((int)Math.Round(MaximumHitsPer16 * stepCount / 16.0, MidpointRounding.AwayFromZero), min, stepCount);
        return min + (int)Math.Round((max - min) * density.Value, MidpointRounding.AwayFromZero);
    }
}
