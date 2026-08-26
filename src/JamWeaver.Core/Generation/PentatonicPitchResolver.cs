namespace JamWeaver.Core.Generation;

public static class PentatonicPitchResolver
{
    private static readonly int[] Major = [0, 2, 4, 7, 9];
    private static readonly int[] Minor = [0, 3, 5, 7, 10];

    public static MidiValue Resolve(MelodicPitch pitch, TonalContext context, MusicalRole role)
    {
        var profile = MusicalRoleProfile.For(role);
        var anchor = Enumerable.Range(profile.MinimumNote, profile.MaximumNote - profile.MinimumNote + 1)
            .First(note => note % 12 == context.Root.Value);
        var intervals = context.Palette == PitchPalette.MajorPentatonic ? Major : Minor;
        var note = anchor + intervals[pitch.ScaleDegree] + (pitch.OctaveOffset * 12) + pitch.ChromaticOffset;
        if (note < profile.MinimumNote || note > profile.MaximumNote)
            throw new ArgumentOutOfRangeException(nameof(pitch), "Pitch resolves outside the musical role range.");
        return new MidiValue(note);
    }

    public static IReadOnlyList<MelodicPitch> ValidPitches(TonalContext context, MusicalRole role) =>
        (from octave in Enumerable.Range(-4, 9)
         from degree in Enumerable.Range(0, 5)
         let pitch = new MelodicPitch(degree, octave)
         where CanResolve(pitch, context, role)
         orderby Resolve(pitch, context, role).Value
         select pitch).ToArray();

    private static bool CanResolve(MelodicPitch pitch, TonalContext context, MusicalRole role)
    {
        try { _ = Resolve(pitch, context, role); return true; }
        catch (ArgumentOutOfRangeException) { return false; }
    }
}
