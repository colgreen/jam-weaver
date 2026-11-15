using JamWeaver.Core.Midi;
using JamWeaver.Core.Performance;
using JamWeaver.Core.Persistence;
using JamWeaver.Core.Transport;
using Redzen.Random;

namespace JamWeaver.Console.Tests;

public sealed class ConsoleShutdownTests
{
    [Theory]
    [InlineData("quit\n", false)]
    [InlineData("", false)]
    [InlineData("quit\n", true)]
    [InlineData("", true)]
    public async Task Quit_and_eof_stop_before_port_disposal_and_continue_cleanup_after_stop_failure(string ending, bool failStop)
    {
        var port = new RecordingOutputPort(failStop);
        var scheduler = new CancellationScheduler();
        var error = await Record.ExceptionAsync(() => RunSessionAsync(
            $"source internal\nstart\non 1 60 100\n{ending}", port, scheduler));

        if (failStop)
        {
            Assert.IsType<InvalidOperationException>(error);
            Assert.Contains("MIDI Stop", error.Message);
            Assert.Contains("shutdown-test", error.Message);
        }
        else Assert.Null(error);

        Assert.Equal(["Start", "Clock", "On", "Stop", "Off"], port.Messages.Take(5));
        Assert.Equal(16, port.Messages.Count(message => message == "CC"));
        Assert.Equal("Dispose", port.Messages.Last());
        Assert.DoesNotContain("Clock", port.Messages.SkipWhile(message => message != "Stop").Skip(1));
        Assert.True(port.IsDisposed);
        Assert.ThrowsAny<OperationCanceledException>(scheduler.Pulse!);
    }

    [Fact]
    public async Task Panic_still_sends_all_notes_off_after_player_mute_fails()
    {
        var port = new RecordingOutputPort(false) { NoteOffFailuresRemaining = 1 };
        var transcript = new StringWriter();

        await RunSessionAsync("source internal\nnew 123\ngo\npanic\nquit\n", port, new CancellationScheduler(), transcript);

        Assert.Equal(2, port.Messages.Count(message => message == "Off"));
        Assert.Equal(16, port.Messages.TakeWhile(message => message != "Stop").Count(message => message == "CC"));
        Assert.Contains("MIDI panic encountered cleanup failures", transcript.ToString());
        Assert.True(port.IsDisposed);
    }

    private static async Task RunSessionAsync(string commands, RecordingOutputPort port, CancellationScheduler scheduler,
        StringWriter? transcript = null)
    {
        using var output = new SafeMidiOutput();
        output.ReplacePort(port);
        var transport = new TransportEngine();
        using var player = new PatternPlayer(output, transport);
        await using var watchdog = new ExternalClockWatchdog(transport);
        await using var clock = new InternalMidiClock(output, transport, scheduler);
        var phrase = new StructuredPhraseGenerator();
        var groove = new GrooveVocabularyGenerator();
        using var library = new PatternLibrary(Path.Combine(Path.GetTempPath(), $"jam-weaver-{Guid.NewGuid():N}"));
        var application = new JamWeaverConsole(new StringReader(commands), new ConsoleDisplay(transcript ?? new StringWriter()),
            output, transport, clock, player, new CandidateSession(player),
            new CandidateGenerator(new EuclideanMotifGenerator(), new TurnaroundLoopGenerator(),
                new EuclideanPhraseGenerator(), phrase, groove, new MotifGenerator()),
            new GenerationControls(), new CandidateHistory(), library, phrase, groove,
            RandomDefaults.CreateRandomSource(123), new ExternalMidiClockInput(), new FakeMidiPortCatalog());

        await application.RunAsync();
    }

    private sealed class CancellationScheduler : IClockPulseScheduler
    {
        public Action? Pulse { get; private set; }
        public Task RunAsync(Func<double> bpm, Action pulse, CancellationToken cancellationToken)
        {
            Pulse = pulse;
            pulse();
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class RecordingOutputPort(bool failStop) : IMidiOutputPort
    {
        public string Name => "shutdown-test";
        public List<string> Messages { get; } = [];
        public bool IsDisposed { get; private set; }
        public int NoteOffFailuresRemaining { get; set; }
        public void SendNoteOn(MidiChannel channel, MidiValue note, MidiValue velocity) => Add("On");
        public void SendNoteOff(MidiChannel channel, MidiValue note, MidiValue velocity)
        {
            Add("Off");
            if (NoteOffFailuresRemaining > 0)
            {
                NoteOffFailuresRemaining--;
                throw new InvalidOperationException("note-off send failed");
            }
        }
        public void SendControlChange(MidiChannel channel, MidiValue controller, MidiValue value) => Add("CC");
        public void SendProgramChange(MidiChannel channel, MidiValue program) => Add("Program");
        public void SendClock() => Add("Clock");
        public void SendStart() => Add("Start");
        public void SendContinue() => Add("Continue");
        public void SendStop()
        {
            Add("Stop");
            if (failStop) throw new InvalidOperationException("stop send failed");
        }
        public void Dispose()
        {
            Add("Dispose");
            IsDisposed = true;
        }
        private void Add(string message)
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
            Messages.Add(message);
        }
    }
}
