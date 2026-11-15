using JamWeaver.Core.Midi;
using JamWeaver.Core.Performance;
using JamWeaver.Core.Persistence;
using JamWeaver.Core.Transport;
using Redzen.Random;

namespace JamWeaver.Console.Tests;

public sealed class ConsolePortTests
{
    [Fact]
    public async Task Empty_discovery_keeps_help_available_without_opening_ports()
    {
        await using var session = new Session();

        await session.RunAsync("out\nin\nhelp\nquit\n");

        Assert.Contains("No MIDI outputs found.", session.Transcript);
        Assert.Contains("No MIDI inputs found.", session.Transcript);
        Assert.Contains("Live controls:", session.Transcript);
        Assert.DoesNotContain("Error:", session.Transcript);
        Assert.Null(session.Output.PortName);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Discovery_failure_is_contextual_and_does_not_block_other_lists_or_commands(bool failOutputs, bool failInputs)
    {
        await using var session = new Session();
        var outputAttempts = 0;
        var inputAttempts = 0;
        session.Catalog.OutputDiscovery = () =>
        {
            outputAttempts++;
            return failOutputs ? throw new InvalidOperationException("output discovery failed") : ["output device"];
        };
        session.Catalog.InputDiscovery = () =>
        {
            inputAttempts++;
            return failInputs ? throw new InvalidOperationException("input discovery failed") : ["input device"];
        };

        await session.RunAsync("setup\nout\nin\nbpm\nhelp\nquit\n");

        Assert.Equal(3, outputAttempts);
        Assert.Equal(3, inputAttempts);
        Assert.Contains(failOutputs ? "Could not discover MIDI outputs: output discovery failed" : "0: output device", session.Transcript);
        Assert.Contains(failInputs ? "Could not discover MIDI inputs: input discovery failed" : "0: input device", session.Transcript);
        Assert.DoesNotContain("No MIDI outputs found.", session.Transcript);
        Assert.DoesNotContain("No MIDI inputs found.", session.Transcript);
        Assert.Contains("Tempo: 120 BPM", session.Transcript);
        Assert.Contains("Live controls:", session.Transcript);
        AssertBridgeDetached(session);
    }

    [Fact]
    public async Task Setup_retries_discovery_after_startup_failure()
    {
        await using var session = new Session();
        var attempts = 0;
        session.Catalog.OutputDiscovery = () => ++attempts == 1
            ? throw new InvalidOperationException("temporary failure") : ["reconnected output"];

        await session.RunAsync("setup\nquit\n");

        Assert.Equal(2, attempts);
        Assert.Contains("Could not discover MIDI outputs: temporary failure", session.Transcript);
        Assert.Contains("0: reconnected output", session.Transcript);
    }

    [Fact]
    public async Task Output_replacement_stops_the_old_clock_before_opening_and_failed_open_retains_selection()
    {
        await using var session = new Session();
        var operations = new List<string>();
        var first = new OutputPort("first", operations);
        var second = new OutputPort("second", operations);
        session.Catalog.OutputFactory = index =>
        {
            operations.Add($"Open:{index}");
            return index switch
            {
                0 => first,
                1 => second,
                _ => throw new InvalidOperationException("output open failed")
            };
        };

        await session.RunAsync("out 0\nsource internal\nstart\nout 1\nout 2\nstatus\nhelp\nquit\n");

        Assert.Equal(["first:Stop", "Open:1", "first:Dispose"],
            operations.Where(operation => operation is "first:Stop" or "Open:1" or "first:Dispose"));
        Assert.Contains("first:Clock", operations);
        Assert.DoesNotContain("second:Start", operations);
        Assert.DoesNotContain("second:Clock", operations);
        Assert.Equal("second", session.Output.PortName);
        Assert.False(session.Clock.IsRunning);
        Assert.Equal(TransportState.Stopped, session.Transport.State);
        Assert.Contains("Error: output open failed", session.Transcript);
        Assert.Contains("Output=second", session.Transcript);
        Assert.Contains("Live controls:", session.Transcript);
    }

    [Theory]
    [InlineData("quit\n")]
    [InlineData("")]
    public async Task Input_open_failure_keeps_the_listener_and_replacement_closes_it_before_starting_another(string ending)
    {
        await using var session = new Session();
        var operations = new List<string>();
        var first = new InputPort("first", operations);
        var second = new InputPort("second", operations);
        session.Catalog.InputFactory = index => index switch
        {
            0 => first,
            1 => second,
            _ => throw new InvalidOperationException("input open failed")
        };

        await session.RunAsync($"in 0\nin 2\nstatus\nin 1\nstatus\n{ending}");

        Assert.Equal(["first:Start", "first:Stop", "first:Dispose", "second:Start", "second:Stop", "second:Dispose"], operations);
        Assert.Contains("Error: input open failed", session.Transcript);
        Assert.Contains("Input=first", session.Transcript);
        Assert.Contains("Input=second", session.Transcript);
        Assert.False(first.HasReceiver);
        Assert.False(second.HasReceiver);
        AssertBridgeDetached(session);
    }

    [Fact]
    public async Task Failed_input_start_is_cleaned_up_and_a_later_selection_can_recover()
    {
        await using var session = new Session();
        var operations = new List<string>();
        var broken = new InputPort("broken", operations) { FailStart = true };
        var working = new InputPort("working", operations);
        session.Catalog.InputFactory = index => index == 0 ? broken : working;

        await session.RunAsync("in 0\nstatus\nin 1\nstatus\nquit\n");

        Assert.Equal(["broken:Start", "broken:Stop", "broken:Dispose", "working:Start", "working:Stop", "working:Dispose"], operations);
        Assert.Contains("Could not start listening to MIDI input 'broken'", session.Transcript);
        Assert.Contains("Input=none", session.Transcript);
        Assert.Contains("Input=working", session.Transcript);
        Assert.False(broken.HasReceiver);
    }

    [Fact]
    public async Task Read_failure_closes_the_selected_input_and_detaches_external_clock()
    {
        await using var session = new Session();
        var operations = new List<string>();
        var input = new InputPort("input", operations);
        session.Catalog.InputFactory = _ => input;

        var error = await Assert.ThrowsAsync<IOException>(() => session.RunAsync(new FailingReader()));

        Assert.Equal("read failed", error.Message);
        Assert.Equal(["input:Start", "input:Stop", "input:Dispose"], operations);
        Assert.False(input.HasReceiver);
        AssertBridgeDetached(session);
    }

    [Fact]
    public async Task Startup_rendering_failure_still_detaches_external_clock()
    {
        await using var session = new Session(new FailingWriter());

        var error = await Assert.ThrowsAsync<IOException>(() => session.RunAsync("quit\n"));

        Assert.Equal("write failed", error.Message);
        AssertBridgeDetached(session);
    }

    private static void AssertBridgeDetached(Session session)
    {
        session.ExternalClock.Receive(RealtimeMessage.Start);
        Assert.Equal(TransportState.Stopped, session.Transport.State);
    }

    private sealed class Session : IAsyncDisposable
    {
        public FakeMidiPortCatalog Catalog { get; } = new();
        public SafeMidiOutput Output { get; } = new();
        public TransportEngine Transport { get; } = new();
        public ExternalMidiClockInput ExternalClock { get; } = new();
        public InternalMidiClock Clock { get; }
        public string Transcript => writer.ToString();
        private readonly StringWriter writer;
        private readonly PatternPlayer player;
        private readonly SinglePulseScheduler scheduler = new();
        private readonly PatternLibrary library = new(Path.Combine(Path.GetTempPath(), $"jam-weaver-{Guid.NewGuid():N}"));

        public Session(StringWriter? writer = null)
        {
            this.writer = writer ?? new StringWriter();
            player = new PatternPlayer(Output, Transport);
            Clock = new InternalMidiClock(Output, Transport, scheduler);
        }

        public Task RunAsync(string commands) => RunAsync(new PulseAwaitingReader(commands, scheduler.Pulsed.Task));

        public Task RunAsync(TextReader reader)
        {
            var phrase = new StructuredPhraseGenerator();
            var groove = new GrooveVocabularyGenerator();
            var application = new JamWeaverConsole(reader, new ConsoleDisplay(writer), Output, Transport,
                Clock, player, new CandidateSession(player),
                new CandidateGenerator(new EuclideanMotifGenerator(), new TurnaroundLoopGenerator(),
                    new EuclideanPhraseGenerator(), phrase, groove, new MotifGenerator()),
                new GenerationControls(), new CandidateHistory(), library, phrase, groove,
                RandomDefaults.CreateRandomSource(123), ExternalClock, Catalog);
            return application.RunAsync();
        }

        public async ValueTask DisposeAsync()
        {
            try { await Clock.DisposeAsync(); }
            finally
            {
                try { player.Dispose(); }
                finally
                {
                    try { Output.Dispose(); }
                    finally { library.Dispose(); }
                }
            }
        }
    }

    private sealed class SinglePulseScheduler : IClockPulseScheduler
    {
        public TaskCompletionSource Pulsed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task RunAsync(Func<double> bpm, Action pulse, CancellationToken cancellationToken)
        {
            pulse();
            Pulsed.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }

    private sealed class OutputPort(string name, List<string> operations) : IMidiOutputPort
    {
        public string Name => name;
        public void SendNoteOn(MidiChannel channel, MidiValue note, MidiValue velocity) => operations.Add($"{name}:On");
        public void SendNoteOff(MidiChannel channel, MidiValue note, MidiValue velocity) => operations.Add($"{name}:Off");
        public void SendControlChange(MidiChannel channel, MidiValue controller, MidiValue value) => operations.Add($"{name}:CC");
        public void SendProgramChange(MidiChannel channel, MidiValue program) => operations.Add($"{name}:Program");
        public void SendClock() => operations.Add($"{name}:Clock");
        public void SendStart() => operations.Add($"{name}:Start");
        public void SendContinue() => operations.Add($"{name}:Continue");
        public void SendStop() => operations.Add($"{name}:Stop");
        public void Dispose() => operations.Add($"{name}:Dispose");
    }

    private sealed class InputPort(string name, List<string> operations) : IMidiInputPort
    {
        public string Name => name;
        public bool FailStart { get; init; }
        public bool HasReceiver => MessageReceived is not null;
        public event EventHandler<RealtimeMessage>? MessageReceived;
        public void StartListening()
        {
            operations.Add($"{name}:Start");
            if (FailStart) throw new InvalidOperationException("listen failed");
        }
        public void StopListening() => operations.Add($"{name}:Stop");
        public void Dispose() => operations.Add($"{name}:Dispose");
    }

    private sealed class FailingReader : StringReader
    {
        public FailingReader() : base(string.Empty) { }
        private bool selected;
        public override Task<string?> ReadLineAsync()
        {
            if (selected) throw new IOException("read failed");
            selected = true;
            return Task.FromResult<string?>("in 0");
        }
    }

    private sealed class FailingWriter : StringWriter
    {
        public override void WriteLine(string? value) => throw new IOException("write failed");
    }
}
