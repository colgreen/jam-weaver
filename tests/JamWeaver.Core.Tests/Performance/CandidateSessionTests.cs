using JamWeaver.Core.Midi;
using JamWeaver.Core.Performance;
using JamWeaver.Core.Sequencer;
using JamWeaver.Core.Transport;

namespace JamWeaver.Core.Tests.Performance;

public sealed class CandidateSessionTests
{
    [Fact]
    public void First_candidate_establishes_baseline_and_later_candidate_can_be_accepted()
    {
        using var fixture = new SessionFixture();
        var first = Melodic(0);
        var second = Melodic(1);
        fixture.Session.SetCandidate(first);
        fixture.Session.SetCandidate(second);

        fixture.Session.Accept();

        Assert.Equal(second.Id, fixture.Session.Accepted!.Id);
        Assert.Equal(first.Id, fixture.Session.PreviousAccepted!.Id);
    }

    [Fact]
    public void Pending_candidate_cannot_be_accepted_before_audition()
    {
        using var fixture = new SessionFixture();
        fixture.Session.SetCandidate(Melodic(0));
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Clock);
        fixture.Session.SetCandidate(Melodic(1));

        Assert.Throws<InvalidOperationException>(fixture.Session.Accept);
    }

    [Fact]
    public void Candidate_can_be_accepted_after_pending_bar_activation()
    {
        using var fixture = new SessionFixture();
        fixture.Session.SetCandidate(Melodic(0));
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Clock);
        var candidate = Melodic(1);
        fixture.Session.SetCandidate(candidate);
        for (var index = 0; index < 96; index++) fixture.Engine.Process(ClockSource.External, RealtimeMessage.Clock);

        fixture.Session.Accept();

        Assert.Equal(candidate.Id, fixture.Session.Accepted!.Id);
    }

    [Fact]
    public void Reject_returns_to_accepted_and_undo_toggles_accepted_history()
    {
        using var fixture = new SessionFixture();
        var first = Melodic(0);
        var second = Melodic(1);
        fixture.Session.SetCandidate(first);
        fixture.Session.SetCandidate(second);
        fixture.Session.Accept();
        fixture.Session.SetCandidate(Melodic(2));
        fixture.Session.Reject();
        Assert.Equal(second.Id, fixture.Session.Candidate!.Id);

        fixture.Session.Undo();
        Assert.Equal(first.Id, fixture.Session.Accepted!.Id);
        fixture.Session.Undo();
        Assert.Equal(second.Id, fixture.Session.Accepted!.Id);
    }

    [Fact]
    public void Transformations_wrap_root_toggle_palette_and_change_role()
    {
        var original = Melodic(11);
        var wrapped = PatternTransformations.TransposeRoot(original, 1);
        var toggled = PatternTransformations.TogglePalette(wrapped);
        var high = PatternTransformations.ChangeRole(toggled, MusicalRole.High);

        Assert.Equal(0, wrapped.TonalContext!.Value.Root.Value);
        Assert.Equal(PitchPalette.MajorPentatonic, toggled.TonalContext!.Value.Palette);
        Assert.Equal(MusicalRole.High, high.Role);
        Assert.Null(high.Recipe);
        Assert.All(high.Steps.SelectMany(step => step.Notes), note =>
            Assert.InRange(JamWeaver.Core.Generation.PentatonicPitchResolver.Resolve(
                (MelodicPitch)note.Pitch, high.TonalContext!.Value, high.Role!.Value).Value, 0, 127));
    }

    [Fact]
    public void Melodic_transformations_reject_drum_patterns()
    {
        var drums = new Pattern(PatternId.New(), new PatternName("drums"), PatternSchemaVersion.Current,
            PatternMode.Drums, PatternTiming.SixteenthNotes,
            [new PatternStep([new PatternNote(new DrumPitch(new MidiValue(36)), new MidiValue(100), new NoteGate(.5))], TriggerProbability.Always)], null, null);

        Assert.Throws<InvalidOperationException>(() => PatternTransformations.TogglePalette(drums));
    }

    [Fact]
    public void Renaming_accepted_updates_matching_session_and_player_references()
    {
        using var fixture = new SessionFixture();
        var accepted = Melodic(0);
        fixture.Session.SetCandidate(accepted);
        var renamed = accepted.Rename(new PatternName("renamed"));

        fixture.Session.RenameAccepted(renamed);

        Assert.Equal("renamed", fixture.Session.Accepted!.Name.Value);
        Assert.Equal("renamed", fixture.Session.Candidate!.Name.Value);
        Assert.Equal("renamed", fixture.Player.CurrentPattern!.Name.Value);
    }

    [Fact]
    public void Renaming_accepted_does_not_disturb_a_different_candidate()
    {
        using var fixture = new SessionFixture();
        var accepted = Melodic(0);
        fixture.Session.SetCandidate(accepted);
        var candidate = Melodic(1);
        fixture.Session.SetCandidate(candidate);

        fixture.Session.RenameAccepted(accepted.Rename(new PatternName("renamed")));

        Assert.Equal("renamed", fixture.Session.Accepted!.Name.Value);
        Assert.Equal(candidate.Id, fixture.Session.Candidate!.Id);
        Assert.Equal(candidate.Id, fixture.Player.CurrentPattern!.Id);
    }

    private static Pattern Melodic(int root)
    {
        var step = new PatternStep([new PatternNote(new MelodicPitch(0), new MidiValue(100), new NoteGate(.8))], TriggerProbability.Always);
        return new Pattern(PatternId.New(), new PatternName("melody"), PatternSchemaVersion.Current,
            PatternMode.Melodic, PatternTiming.SixteenthNotes, [step], MusicalRole.Bass,
            new TonalContext(new RootPitchClass(root), PitchPalette.MinorPentatonic));
    }

    private sealed class SessionFixture : IDisposable
    {
        private readonly SafeMidiOutput _output = new();
        public SessionFixture()
        {
            _output.ReplacePort(new FakeMidiOutputPort());
            Engine = new TransportEngine();
            Player = new PatternPlayer(_output, Engine);
            Session = new CandidateSession(Player);
        }
        public TransportEngine Engine { get; }
        public PatternPlayer Player { get; }
        public CandidateSession Session { get; }
        public void Dispose() { Player.Dispose(); _output.Dispose(); }
    }
}
