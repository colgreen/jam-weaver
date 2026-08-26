namespace JamWeaver.Core.Sequencer;

public abstract record PatternPitch;

public sealed record MelodicPitch : PatternPitch
{
    public MelodicPitch(int scaleDegree, int octaveOffset = 0, int chromaticOffset = 0)
    {
        if (scaleDegree is < 0 or > 4) throw new ArgumentOutOfRangeException(nameof(scaleDegree));
        if (octaveOffset is < -4 or > 4) throw new ArgumentOutOfRangeException(nameof(octaveOffset));
        if (chromaticOffset is < -2 or > 2) throw new ArgumentOutOfRangeException(nameof(chromaticOffset));
        ScaleDegree = scaleDegree;
        OctaveOffset = octaveOffset;
        ChromaticOffset = chromaticOffset;
    }
    public int ScaleDegree { get; }
    public int OctaveOffset { get; }
    public int ChromaticOffset { get; }
}

public sealed record DrumPitch(MidiValue NoteNumber) : PatternPitch;
