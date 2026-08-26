using JamWeaver.Core.Generation;

namespace JamWeaver.Core.Performance;

public static class PatternTransformations
{
    public static Pattern TransposeRoot(Pattern pattern, int semitones)
    {
        EnsureMelodic(pattern);
        var context = pattern.TonalContext!.Value;
        var root = ((context.Root.Value + semitones) % 12 + 12) % 12;
        return Transform(pattern, new TonalContext(new RootPitchClass(root), context.Palette), pattern.Role!.Value);
    }

    public static Pattern TogglePalette(Pattern pattern)
    {
        EnsureMelodic(pattern);
        var context = pattern.TonalContext!.Value;
        var palette = context.Palette == PitchPalette.MajorPentatonic
            ? PitchPalette.MinorPentatonic : PitchPalette.MajorPentatonic;
        return Transform(pattern, new TonalContext(context.Root, palette), pattern.Role!.Value);
    }

    public static Pattern ChangeRole(Pattern pattern, MusicalRole role)
    {
        EnsureMelodic(pattern);
        return Transform(pattern, pattern.TonalContext!.Value, role);
    }

    private static Pattern Transform(Pattern pattern, TonalContext context, MusicalRole role)
    {
        var steps = pattern.Steps.Select(step => new PatternStep(
            step.Notes.Select(note => new PatternNote(
                Fit((MelodicPitch)note.Pitch, context, role), note.Velocity, note.Gate)), step.Probability));
        return new Pattern(PatternId.New(), pattern.Name, pattern.SchemaVersion, PatternMode.Melodic,
            pattern.Timing, steps, role, context);
    }

    private static MelodicPitch Fit(MelodicPitch pitch, TonalContext context, MusicalRole role)
    {
        if (CanResolve(pitch, context, role)) return pitch;
        return Enumerable.Range(-4, 9)
            .OrderBy(offset => Math.Abs(offset - pitch.OctaveOffset))
            .Select(offset => new MelodicPitch(pitch.ScaleDegree, offset, pitch.ChromaticOffset))
            .First(candidate => CanResolve(candidate, context, role));
    }

    private static bool CanResolve(MelodicPitch pitch, TonalContext context, MusicalRole role)
    {
        try { _ = PentatonicPitchResolver.Resolve(pitch, context, role); return true; }
        catch (ArgumentOutOfRangeException) { return false; }
    }

    private static void EnsureMelodic(Pattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if (pattern.Mode != PatternMode.Melodic)
            throw new InvalidOperationException("Key and role controls apply only to melodic patterns.");
    }
}
