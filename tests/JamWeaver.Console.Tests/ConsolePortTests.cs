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
    [InlineData("start", true)]
    [InlineData("continue", true)]
    [InlineData("start", false)]
    [InlineData("continue", false)]
    public async Task A_failed_clock_is_stopped_on_its_original_output_before_replacement(string operation, bool selectExternal)
    {
        await using var session = new Session(clockScheduler: new FailingScheduler());
        var operations = new List<string>();
        session.Catalog.OutputFactory = index =>
        {
            operations.Add($"Open:{index}");
            return new OutputPort(index == 0 ? "first" : "second", operations);
        };

        var sourceCommand = selectExternal ? "source external\n" : string.Empty;
        await session.RunAsync(new FaultAwaitingReader(
            $"out 0\nsource internal\n{operation}\n{sourceCommand}out 1\nquit\n", session.Clock));
        await session.Clock.DisposeAsync();
        session.Output.Dispose();

        Assert.Equal(["Open:0", $"first:{(operation == "start" ? "Start" : "Continue")}", "first:Stop", "Open:1", "first:Dispose", "second:Dispose"],
            operations.Where(message => !message.EndsWith(":CC")));
        Assert.NotNull(session.Clock.Error);
        Assert.Contains("scheduler failed", session.Clock.Error.Message);
        Assert.DoesNotContain("Error:", session.Transcript);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_owed_stops_block_source_and_output_changes_until_retry_or_shutdown(bool retryCommands)
    {
        await using var session = new Session(clockScheduler: new FailingScheduler());
        var operations = new List<string>();
        var first = new OutputPort("first", operations) { StopFailuresRemaining = 2 };
        session.Catalog.OutputFactory = index =>
        {
            operations.Add($"Open:{index}");
            return index == 0 ? first : new OutputPort("second", operations);
        };

        await session.RunAsync(new FaultAwaitingReader(
            "out 0\nsource internal\nstart\nsource external\nout 1\nquit\n", session.Clock));

        Assert.Equal(ClockSource.Internal, session.Transport.Source);
        Assert.Equal(TransportState.Stopped, session.Transport.State);
        Assert.False(session.Clock.IsRunning);
        Assert.Equal("first", session.Output.PortName);
        Assert.Equal(["Open:0", "first:Start", "first:Stop", "first:Stop"], operations);
        Assert.Equal(2, session.Transcript.Split("Error:").Length - 1);
        Assert.Contains("first", session.Transcript);
        Assert.Contains("stop send failed", session.Transcript);

        if (retryCommands) await session.RunAsync("source external\nout 1\nquit\n");
        await session.Clock.DisposeAsync();
        session.Output.Dispose();

        Assert.Equal(3, operations.Count(message => message == "first:Stop"));
        Assert.DoesNotContain("second:Stop", operations);
        Assert.Equal(retryCommands ? "second:Dispose" : "first:Dispose", operations.Last());
        if (retryCommands)
            Assert.True(operations.LastIndexOf("first:Stop") < operations.IndexOf("Open:1"));
    }

    [Theory]
    [InlineData("source internal\nsource external\n")]
    [InlineData("source internal\n")]
    [InlineData("")]
    public async Task Source_and_output_changes_with_an_unused_clock_send_no_transport_messages(string sourceCommands)
    {
        await using var session = new Session();
        var operations = new List<string>();
        session.Catalog.OutputFactory = index => new OutputPort(index == 0 ? "first" : "second", operations);

        await session.RunAsync($"out 0\n{sourceCommands}out 1\nquit\n");
        await session.Clock.DisposeAsync();
        session.Output.Dispose();

        Assert.Equal(["first:Dispose", "second:Dispose"], operations.Where(message => !message.EndsWith(":CC")));
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

        public Session(StringWriter? writer = null, IClockPulseScheduler? clockScheduler = null)
        {
            this.writer = writer ?? new StringWriter();
            player = new PatternPlayer(Output, Transport);
            Clock = new InternalMidiClock(Output, Transport, clockScheduler ?? scheduler);
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
        public int StopFailuresRemaining { get; set; }
        public void SendNoteOn(MidiChannel channel, MidiValue note, MidiValue velocity) => operations.Add($"{name}:On");
        public void SendNoteOff(MidiChannel channel, MidiValue note, MidiValue velocity) => operations.Add($"{name}:Off");
        public void SendControlChange(MidiChannel channel, MidiValue controller, MidiValue value) => operations.Add($"{name}:CC");
        public void SendProgramChange(MidiChannel channel, MidiValue program) => operations.Add($"{name}:Program");
        public void SendClock() => operations.Add($"{name}:Clock");
        public void SendStart() => operations.Add($"{name}:Start");
        public void SendContinue() => operations.Add($"{name}:Continue");
        public void SendStop()
        {
            operations.Add($"{name}:Stop");
            if (StopFailuresRemaining > 0)
            {
                StopFailuresRemaining--;
                throw new InvalidOperationException($"{name}: stop send failed");
            }
        }
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

    private sealed class FailingScheduler : IClockPulseScheduler
    {
        public Task RunAsync(Func<double> bpm, Action pulse, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("scheduler failed");
    }

    private sealed class FaultAwaitingReader(string commands, InternalMidiClock clock) : StringReader(commands)
    {
        private string? previous;

        public override async Task<string?> ReadLineAsync()
        {
            if (previous is "start" or "continue")
                await WaitForFailureAsync().WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            previous = await base.ReadLineAsync();
            return previous;
        }

        private async Task WaitForFailureAsync()
        {
            while (clock.IsRunning) await Task.Delay(1, TestContext.Current.CancellationToken);
            Assert.NotNull(clock.Error);
        }
    }
}
