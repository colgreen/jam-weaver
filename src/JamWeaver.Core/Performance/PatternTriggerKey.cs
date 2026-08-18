using JamWeaver.Core.Sequencer;

namespace JamWeaver.Core.Performance;

public static class PatternTriggerKey
{
    public static ulong Create(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var hash = 14695981039346656037UL;
        Mix(ref hash, (ulong)pattern.Mode);
        Mix(ref hash, (ulong)pattern.Timing.PulsesPerStep);
        Mix(ref hash, (ulong)(pattern.Role is { } role ? (int)role + 1 : 0));
        Mix(ref hash, (ulong)(pattern.TonalContext?.Root.Value ?? 0));
        Mix(ref hash, (ulong)(pattern.TonalContext is { } tonal ? (int)tonal.Palette + 1 : 0));
        foreach (var step in pattern.Steps)
        {
            Mix(ref hash, BitConverter.DoubleToUInt64Bits(step.Probability.Value));
            Mix(ref hash, (ulong)step.Notes.Length);
            foreach (var note in step.Notes)
            {
                switch (note.Pitch)
                {
                    case MelodicPitch melodic:
                        Mix(ref hash, 1);
                        Mix(ref hash, (ulong)melodic.ScaleDegree);
                        Mix(ref hash, unchecked((ulong)melodic.OctaveOffset));
                        Mix(ref hash, unchecked((ulong)melodic.ChromaticOffset));
                        break;
                    case DrumPitch drum:
                        Mix(ref hash, 2);
                        Mix(ref hash, (ulong)drum.NoteNumber.Value);
                        break;
                }
                Mix(ref hash, (ulong)note.Velocity.Value);
                Mix(ref hash, BitConverter.DoubleToUInt64Bits(note.Gate.Value));
            }
        }
        return Avalanche(hash);
    }

    internal static ulong Avalanche(ulong value)
    {
        value ^= value >> 30;
        value *= 0xbf58476d1ce4e5b9UL;
        value ^= value >> 27;
        value *= 0x94d049bb133111ebUL;
        return value ^ (value >> 31);
    }

    private static void Mix(ref ulong hash, ulong value)
    {
        hash ^= value;
        hash *= 1099511628211UL;
    }
}
