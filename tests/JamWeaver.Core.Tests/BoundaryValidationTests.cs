using JamWeaver.Core.Performance;
using JamWeaver.Core.Transport;

namespace JamWeaver.Core.Tests;

public sealed class BoundaryValidationTests
{
    [Theory]
    [InlineData("on")]
    [InlineData("off")]
    [InlineData("cc")]
    [InlineData("program")]
    public void Default_channels_are_rejected_before_any_message_is_sent(string operation)
    {
        var port = new FakeMidiOutputPort();
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);

        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            switch (operation)
            {
                case "on": output.NoteOn(default, default, new MidiValue(100)); break;
                case "off": output.NoteOff(default, default, default); break;
                case "cc": output.ControlChange(default, default, default); break;
                case "program": output.ProgramChange(default, default); break;
            }
        });

        Assert.Equal("channel", error.ParamName);
        Assert.Empty(port.Messages);
    }

    [Fact]
    public void Route_and_library_channel_conversion_reject_default_channels()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new MidiRoute("test-output", default));
        Assert.Throws<ArgumentOutOfRangeException>(() => default(MidiChannel).ZeroBased);
        Assert.Equal(0, new MidiChannel(1).ZeroBased);
    }

    [Fact]
    public void Invalid_player_channel_does_not_release_notes_or_change_routing()
    {
        var port = new FakeMidiOutputPort();
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        var engine = new TransportEngine();
        using var player = new PatternPlayer(output, engine);
        player.Queue(new Pattern(PatternId.New(), new PatternName("channel validation"), PatternSchemaVersion.Current,
            PatternMode.Drums, new PatternTiming(96),
            [new PatternStep([new PatternNote(new DrumPitch(new MidiValue(60)), new MidiValue(100), new NoteGate(1))],
                TriggerProbability.Always)], null, null));
        player.Play();
        engine.Process(ClockSource.External, RealtimeMessage.Start);
        engine.Process(ClockSource.External, RealtimeMessage.Clock);

        Assert.Throws<ArgumentOutOfRangeException>(() => player.Channel = default);

        Assert.Equal(1, player.Channel.Number);
        Assert.Equal(["On:1:60:100"], port.Messages);
    }

    [Fact]
    public void Default_timeline_timing_is_rejected_without_damaging_the_existing_configuration()
    {
        var engine = new TransportEngine();
        Assert.Throws<ArgumentOutOfRangeException>(() => new SequencerTimeline(engine, default, 16));
        using var timeline = new SequencerTimeline(engine, PatternTiming.SixteenthNotes, 16);
        var boundaries = new List<TimelineBoundary>();
        timeline.Boundary += boundaries.Add;

        Assert.Throws<ArgumentOutOfRangeException>(() => timeline.Configure(default, 16));
        engine.Process(ClockSource.External, RealtimeMessage.Start);
        engine.Process(ClockSource.External, RealtimeMessage.Clock);

        Assert.Equal(0, Assert.Single(boundaries).StepIndex);
    }

    [Theory]
    [InlineData("source-selection")]
    [InlineData("message-source")]
    [InlineData("message-kind")]
    public void Undefined_transport_enums_cannot_mutate_a_running_transport(string operation)
    {
        var engine = new TransportEngine();
        engine.Process(ClockSource.External, RealtimeMessage.Start);
        engine.Process(ClockSource.External, RealtimeMessage.Clock);
        var original = engine.Snapshot;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            switch (operation)
            {
                case "source-selection": engine.SelectSource((ClockSource)999); break;
                case "message-source": engine.Process((ClockSource)999, RealtimeMessage.Start); break;
                case "message-kind": engine.Process(ClockSource.External, (RealtimeMessage)999); break;
            }
        });

        Assert.Equal(original, engine.Snapshot);
    }

    [Fact]
    public void Undefined_source_is_rejected_at_construction_and_unknown_input_messages_are_not_forwarded()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TransportEngine((ClockSource)999));
        var input = new ExternalMidiClockInput();
        var forwarded = false;
        input.MessageReceived += (_, _) => forwarded = true;

        Assert.Throws<ArgumentOutOfRangeException>(() => input.Receive((RealtimeMessage)999));

        Assert.False(forwarded);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(0)]
    [InlineData(19.9)]
    [InlineData(300.1)]
    public async Task Invalid_tempos_are_rejected_without_changing_the_clock(double bpm)
    {
        using var output = new SafeMidiOutput();
        await using var clock = new InternalMidiClock(output, new TransportEngine(ClockSource.Internal));

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Bpm = bpm);

        Assert.Equal(120, clock.Bpm);
        Assert.False(clock.IsRunning);
        Assert.Null(clock.Error);
    }

    [Fact]
    public void Undefined_palette_from_a_record_copy_is_rejected_before_resolution_or_pattern_transformation()
    {
        var invalid = default(TonalContext) with { Palette = (PitchPalette)999 };

        Assert.Throws<ArgumentOutOfRangeException>(() => PentatonicPitchResolver.Resolve(new MelodicPitch(0), invalid, MusicalRole.Bass));
        Assert.Throws<ArgumentOutOfRangeException>(() => PentatonicPitchResolver.ValidPitches(invalid, MusicalRole.Bass));
        Assert.Throws<ArgumentOutOfRangeException>(() => PentatonicPitchResolver.ValidPitches(default, (MusicalRole)999));
        var pattern = new Pattern(PatternId.New(), new PatternName("transform"), PatternSchemaVersion.Current,
            PatternMode.Melodic, PatternTiming.SixteenthNotes, [PatternStep.Rest], MusicalRole.Bass, default(TonalContext));
        Assert.Throws<ArgumentOutOfRangeException>(() => pattern.WithTonalContext(invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => pattern.WithRole((MusicalRole)999));
        Assert.Throws<ArgumentOutOfRangeException>(() => pattern.Rename(default));
        Assert.Equal(default(TonalContext), pattern.TonalContext);
        Assert.Equal(MusicalRole.Bass, pattern.Role);
    }
}
