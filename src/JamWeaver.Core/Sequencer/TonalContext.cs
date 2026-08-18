namespace JamWeaver.Core.Sequencer;

public enum PatternMode { Melodic, Drums }
public enum MusicalRole { Bass, Middle, High }
public enum PitchPalette { MajorPentatonic, MinorPentatonic }

public readonly record struct RootPitchClass
{
    public RootPitchClass(int value)
    {
        if (value is < 0 or > 11) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
    public int Value { get; }
}

public readonly record struct TonalContext(RootPitchClass Root, PitchPalette Palette);
