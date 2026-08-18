using JamWeaver.Core.Midi;
using JamWeaver.Core.Performance;
using JamWeaver.Core.Sequencer;
using JamWeaver.Core.Transport;

namespace JamWeaver.Core.Tests.Performance;

public sealed class PatternPlayerTests
{
    [Fact]
    public void Fractional_gate_sends_note_off_at_ceiling_pulse()
    {
        var fixture = new PlayerFixture(PatternWith([Note(60, .5)], new PatternTiming(6)));
        fixture.StartAndPulse(4);

        Assert.Equal(["On:1:60:100", "Off:1:60:0"], fixture.NoteMessages);
        fixture.Dispose();
    }

    [Fact]
    public void Full_gate_sends_note_off_before_retrigger()
    {
        var fixture = new PlayerFixture(PatternWith([Note(60, 1), Note(60, 1)], new PatternTiming(1)));
        fixture.StartAndPulse(2);

        Assert.Equal(["On:1:60:100", "Off:1:60:0", "On:1:60:100"], fixture.NoteMessages);
        fixture.Dispose();
    }

    [Fact]
    public void Simultaneous_notes_have_independent_gate_deadlines()
    {
        var step = new PatternStep([NoteValue(60, .5), NoteValue(64, 1)], TriggerProbability.Always);
        var fixture = new PlayerFixture(PatternWith([step, PatternStep.Rest], new PatternTiming(6)));
        fixture.StartAndPulse(7);

        Assert.Equal(["On:1:60:100", "On:1:64:100", "Off:1:60:0", "Off:1:64:0"], fixture.NoteMessages);
        fixture.Dispose();
    }

    [Fact]
    public void Melodic_pitch_is_resolved_through_context_and_role()
    {
        var step = new PatternStep([new PatternNote(new MelodicPitch(0), new MidiValue(90), new NoteGate(1))], TriggerProbability.Always);
        var pattern = new Pattern(PatternId.New(), new PatternName("melody"), PatternSchemaVersion.Current,
            PatternMode.Melodic, PatternTiming.SixteenthNotes, [step], MusicalRole.Middle,
            new TonalContext(new RootPitchClass(2), PitchPalette.MinorPentatonic));
        var fixture = new PlayerFixture(pattern);
        fixture.StartAndPulse(1);

        Assert.Equal("On:1:50:90", fixture.NoteMessages.Single());
        fixture.Dispose();
    }

    [Fact]
    public void Probability_decisions_are_deterministic_and_cover_both_outcomes()
    {
        var pattern = PatternWith([Note(60, 1, .5)], PatternTiming.SixteenthNotes);
        var first = Enumerable.Range(0, 100).Select(loop => StepTriggerDecision.ShouldTrigger(pattern, (ulong)loop, 0)).ToArray();
        var second = Enumerable.Range(0, 100).Select(loop => StepTriggerDecision.ShouldTrigger(pattern, (ulong)loop, 0)).ToArray();

        Assert.Equal(first, second);
        Assert.Contains(true, first);
        Assert.Contains(false, first);
    }

    [Fact]
    public void Latest_running_candidate_activates_at_next_strictly_future_bar()
    {
        var original = PatternWith([Note(60, .1)], new PatternTiming(96));
        var fixture = new PlayerFixture(original);
        fixture.StartAndPulse(1);
        fixture.Player.Queue(PatternWith([Note(62, .1)], new PatternTiming(96)));
        var latest = PatternWith([Note(64, .1)], new PatternTiming(96));
        fixture.Player.Queue(latest);
        fixture.Pulse(96);

        Assert.DoesNotContain("On:1:62:100", fixture.NoteMessages);
        Assert.Equal("On:1:64:100", fixture.NoteMessages.Last());
        Assert.Equal(latest.Id, fixture.Player.CurrentPattern!.Id);
        fixture.Dispose();
    }

    [Theory]
    [InlineData("mute")]
    [InlineData("stop")]
    [InlineData("channel")]
    [InlineData("dispose")]
    public void Active_notes_are_released_on_lifecycle_change(string operation)
    {
        var fixture = new PlayerFixture(PatternWith([Note(60, 1)], new PatternTiming(96)));
        fixture.StartAndPulse(1);

        switch (operation)
        {
            case "mute": fixture.Player.Mute(); break;
            case "stop": fixture.Engine.Process(ClockSource.External, RealtimeMessage.Stop); break;
            case "channel": fixture.Player.Channel = new MidiChannel(2); break;
            case "dispose": fixture.Player.Dispose(); break;
        }

        Assert.Equal(["On:1:60:100", "Off:1:60:0"], fixture.NoteMessages);
        fixture.Dispose();
    }

    [Fact]
    public void Playback_failure_is_contained_and_disables_player()
    {
        var port = new ThrowingMidiOutputPort();
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        var engine = new TransportEngine();
        using var player = new PatternPlayer(output, engine);
        player.Queue(PatternWith([Note(60, 1)], PatternTiming.SixteenthNotes));
        player.Play();

        engine.Process(ClockSource.External, RealtimeMessage.Start);
        var error = Record.Exception(() => engine.Process(ClockSource.External, RealtimeMessage.Clock));

        Assert.Null(error);
        Assert.False(player.IsEnabled);
        Assert.IsType<InvalidOperationException>(player.Error);
    }

    [Fact]
    public void Start_while_running_releases_notes_from_previous_position()
    {
        var fixture = new PlayerFixture(PatternWith([Note(60, 1)], new PatternTiming(96)));
        fixture.StartAndPulse(1);

        fixture.Engine.Process(ClockSource.External, RealtimeMessage.Start);
        fixture.Pulse(1);

        Assert.Equal(["On:1:60:100", "Off:1:60:0", "On:1:60:100"], fixture.NoteMessages);
        fixture.Dispose();
    }

    private static Pattern PatternWith(PatternStep[] steps, PatternTiming timing) =>
        new(PatternId.New(), new PatternName("test"), PatternSchemaVersion.Current, PatternMode.Drums,
            timing, steps, null, null);

    private static PatternStep Note(int note, double gate, double probability = 1) =>
        new([NoteValue(note, gate)], new TriggerProbability(probability));

    private static PatternNote NoteValue(int note, double gate) =>
        new(new DrumPitch(new MidiValue(note)), new MidiValue(100), new NoteGate(gate));

    private sealed class PlayerFixture : IDisposable
    {
        private readonly SafeMidiOutput _output = new();
        private readonly FakeMidiOutputPort _port = new();
        public PlayerFixture(Pattern pattern)
        {
            _output.ReplacePort(_port);
            Engine = new TransportEngine();
            Player = new PatternPlayer(_output, Engine);
            Player.Queue(pattern);
            Player.Play();
        }
        public TransportEngine Engine { get; }
        public PatternPlayer Player { get; }
        public IEnumerable<string> NoteMessages => _port.Messages.Where(message => message.StartsWith("On:") || message.StartsWith("Off:"));
        public void StartAndPulse(int count) { Engine.Process(ClockSource.External, RealtimeMessage.Start); Pulse(count); }
        public void Pulse(int count) { for (var index = 0; index < count; index++) Engine.Process(ClockSource.External, RealtimeMessage.Clock); }
        public void Dispose() { Player.Dispose(); _output.Dispose(); }
    }

    private sealed class ThrowingMidiOutputPort : IMidiOutputPort
    {
        public string Name => "throwing";
        public void SendNoteOn(MidiChannel channel, MidiValue note, MidiValue velocity) => throw new InvalidOperationException("send failed");
        public void SendNoteOff(MidiChannel channel, MidiValue note, MidiValue velocity) { }
        public void SendControlChange(MidiChannel channel, MidiValue controller, MidiValue value) { }
        public void SendProgramChange(MidiChannel channel, MidiValue program) { }
        public void SendClock() { }
        public void SendStart() { }
        public void SendContinue() { }
        public void SendStop() { }
        public void Dispose() { }
    }
}
