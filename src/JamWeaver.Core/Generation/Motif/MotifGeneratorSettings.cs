using JamWeaver.Core.Generation.Phrase;

namespace JamWeaver.Core.Generation.Motif;

public enum MotifShape { Auto, Pedal, RootFifth, Walking, CallResponse, Arch, Pickup, Riff }

public sealed record MotifGeneratorSettings
{
    public MotifGeneratorSettings(PatternName name, TonalContext tonalContext, MusicalRole role,
        MotifShape shape, PhraseActivity activity, PhraseLevel movement, PhraseLevel variation, ulong seed)
    {
        if (!Enum.IsDefined(role)) throw new ArgumentOutOfRangeException(nameof(role));
        if (!Enum.IsDefined(shape) || !Enum.IsDefined(activity) || !Enum.IsDefined(movement) || !Enum.IsDefined(variation))
            throw new ArgumentOutOfRangeException(nameof(shape));
        (Name, TonalContext, Role, Shape, Activity, Movement, Variation, Seed) =
            (name, tonalContext, role, shape, activity, movement, variation, seed);
    }

    public PatternName Name { get; }
    public TonalContext TonalContext { get; }
    public MusicalRole Role { get; }
    public MotifShape Shape { get; }
    public PhraseActivity Activity { get; }
    public PhraseLevel Movement { get; }
    public PhraseLevel Variation { get; }
    public ulong Seed { get; }
}
