
namespace JamWeaver.Core.Tests.Sequencer;

public sealed class PatternModelTests
{
    [Fact]
    public void Default_melodic_pattern_has_sixteenth_note_timing_and_sixteen_steps()
    {
        var pattern = MelodicPattern(Enumerable.Repeat(PatternStep.Rest, 16));
        Assert.Equal(16, pattern.Steps.Length);
        Assert.Equal(6, pattern.Timing.PulsesPerStep);
        Assert.Equal(24, pattern.Timing.PulsesPerQuarterNote);
    }

    [Fact]
    public void Pattern_defensively_copies_steps()
    {
        var steps = Enumerable.Repeat(PatternStep.Rest, 16).ToList();
        var pattern = MelodicPattern(steps);
        steps.Clear();
        Assert.Equal(16, pattern.Steps.Length);
    }

    [Fact]
    public void Mode_rejects_wrong_pitch_type()
    {
        var drumNote = new PatternNote(new DrumPitch(new MidiValue(36)), new MidiValue(100), new NoteGate(.5));
        var step = new PatternStep([drumNote], TriggerProbability.Always);
        Assert.Throws<ArgumentException>(() => MelodicPattern([step]));
    }

    [Fact]
    public void Step_rejects_duplicate_pitches()
    {
        var pitch = new MelodicPitch(0);
        var first = new PatternNote(pitch, new MidiValue(80), new NoteGate(.5));
        var second = new PatternNote(pitch, new MidiValue(90), new NoteGate(1));
        Assert.Throws<ArgumentException>(() => new PatternStep([first, second], TriggerProbability.Always));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Gate_rejects_invalid_values(double value) => Assert.Throws<ArgumentOutOfRangeException>(() => new NoteGate(value));

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Probability_rejects_invalid_values(double value) => Assert.Throws<ArgumentOutOfRangeException>(() => new TriggerProbability(value));

    [Fact]
    public void Musical_change_creates_snapshot_but_rename_preserves_identity()
    {
        var source = MelodicPattern([PatternStep.Rest]);
        var renamed = source.Rename(new PatternName("Renamed"));
        var changed = source.WithRole(MusicalRole.High);
        Assert.Equal(source.Id, renamed.Id);
        Assert.NotEqual(source.Id, changed.Id);
        Assert.Equal(MusicalRole.Bass, source.Role);
        Assert.Equal(MusicalRole.High, changed.Role);
    }

    [Fact]
    public void Drum_pattern_supports_multiple_literal_notes()
    {
        var notes = new[]
        {
            new PatternNote(new DrumPitch(new MidiValue(36)), new MidiValue(100), new NoteGate(.5)),
            new PatternNote(new DrumPitch(new MidiValue(38)), new MidiValue(90), new NoteGate(.5))
        };
        var pattern = new Pattern(PatternId.New(), new PatternName("Drums"), PatternSchemaVersion.Current,
            PatternMode.Drums, PatternTiming.SixteenthNotes, [new PatternStep(notes, TriggerProbability.Always)], null, null);
        Assert.Equal(2, pattern.Steps[0].Notes.Length);
    }

    private static Pattern MelodicPattern(IEnumerable<PatternStep> steps) =>
        new(PatternId.New(), new PatternName("Test"), PatternSchemaVersion.Current,
            PatternMode.Melodic, PatternTiming.SixteenthNotes, steps, MusicalRole.Bass,
            new TonalContext(new RootPitchClass(0), PitchPalette.MinorPentatonic));
}
