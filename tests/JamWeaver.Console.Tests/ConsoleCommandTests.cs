using JamWeaver.Core.Midi;
using JamWeaver.Core.Performance;
using JamWeaver.Core.Persistence;
using JamWeaver.Core.Transport;
using Redzen.Random;

namespace JamWeaver.Console.Tests;

public sealed class ConsoleCommandTests
{
    [Theory]
    [InlineData("  ")]
    [InlineData("\t ")]
    public async Task Indented_bare_save_and_load_preserve_the_accepted_name_and_list_the_library(string indent)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;

        await fixture.RunAsync($"{indent}save  \n{indent}load  \nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Same(accepted, fixture.Candidates.Accepted);
        Assert.Same(accepted, fixture.Candidates.Candidate);
        var entry = Assert.Single(await fixture.Library.ListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(accepted.Name.Value, entry.Name);
        Assert.Contains($"Saved '{accepted.Name}'.", fixture.Transcript);
        Assert.Contains("Load with: load <name> or load #<number>", fixture.Transcript);
    }

    [Theory]
    [InlineData("  save Review Indent", "  load Review Indent", "Review Indent")]
    [InlineData("\tSaVe\tReview  Indent  ", "\tLoAd\tReview  Indent  ", "Review  Indent")]
    [InlineData("  save Review Indent", "  load #1", "Review Indent")]
    [InlineData("  save Review Indent", "  load 1", "Review Indent")]
    public async Task Indented_save_and_load_round_trip_names_and_materialized_patterns(string save, string load, string name)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted!;

        await fixture.RunAsync($"{save}\nnew 456\n{load}\nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Equal(accepted.Id, fixture.Candidates.Accepted!.Id);
        Assert.Equal(name, fixture.Candidates.Accepted.Name.Value);
        Assert.Equal(accepted.Id, fixture.Candidates.Candidate!.Id);
        Assert.Equal(name, fixture.Candidates.Candidate.Name.Value);
        var loaded = fixture.Candidates.Candidate;
        Assert.Equal(accepted.Steps.Length, loaded.Steps.Length);
        for (var index = 0; index < accepted.Steps.Length; index++)
        {
            Assert.Equal(accepted.Steps[index].Probability, loaded.Steps[index].Probability);
            Assert.Equal(accepted.Steps[index].Notes, loaded.Steps[index].Notes);
        }
        Assert.Equal(accepted.Recipe!.Seed, fixture.Candidates.Candidate.Recipe!.Seed);
        Assert.Same(fixture.Candidates.Candidate, fixture.Player.CurrentPattern);
        Assert.Contains($"Loaded '{name}' as a candidate", fixture.Transcript);
        Assert.Equal(name, Assert.Single(await fixture.Library.ListAsync(TestContext.Current.CancellationToken)).Name);
    }

    public static TheoryData<string> ExtraArgumentCommands => new()
    {
        "setup extra", "out 0 extra", "in 0 extra", "source internal extra", "bpm 150 extra",
        "start extra", "continue extra", "stop extra", "go extra", "new 999 extra", "compare 999 extra",
        "vary all .5 seed 999 extra", "back extra", "forward extra", "generator motif extra", "groove offbeat extra",
        "shape riff extra", "similarity close extra", "length 1 extra", "activity busy extra", "rhythm steady extra",
        "movement high extra", "variation high extra", "turnaround strong extra", "settings extra", "keep extra",
        "revert extra", "key up extra", "palette extra", "role high extra", "ch 2 extra", "channel 2 extra",
        "play extra", "mute extra", "pattern extra", "library extra", "note 1 60 100 0 extra",
        "on 1 60 100 extra", "off 1 60 0 extra", "cc 1 7 80 extra", "pc 1 10 extra",
        "panic extra", "status extra", "help new extra", "quit extra", "exit extra"
    };

    [Theory]
    [MemberData(nameof(ExtraArgumentCommands))]
    public async Task Extra_arguments_are_rejected_before_command_effects_and_the_loop_continues(string command)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted;
        var controls = fixture.ControlSnapshot();
        var transport = fixture.Transport.Snapshot;

        await fixture.RunAsync($"{command}\nstatus\nquit\n");

        Assert.Contains("Error: Unexpected", fixture.Transcript);
        Assert.Contains("Output=command-test", fixture.Transcript);
        Assert.Same(accepted, fixture.Candidates.Accepted);
        Assert.Same(accepted, fixture.Candidates.Candidate);
        Assert.Same(accepted, fixture.Player.CurrentPattern);
        Assert.Equal(1, fixture.History.Count);
        Assert.Equal(controls, fixture.ControlSnapshot());
        Assert.Equal(transport, fixture.Transport.Snapshot);
        Assert.False(fixture.Player.IsEnabled);
        Assert.Equal(1, fixture.Player.Channel.Number);
        Assert.Equal(120, fixture.Clock.Bpm);
        Assert.Equal(0, fixture.PortOpenAttempts);
        Assert.Empty(fixture.Port.Messages);
        Assert.False(Directory.Exists(fixture.Library.RootDirectory));
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
    }

    [Theory]
    [InlineData("note 1 60")]
    [InlineData("on 1 60")]
    [InlineData("off 1")]
    [InlineData("cc 1 7")]
    [InlineData("pc 1")]
    [InlineData("key")]
    [InlineData("role")]
    public async Task Missing_arguments_are_reported_without_effects_and_the_loop_continues(string command)
    {
        await using var fixture = new Session();
        var accepted = fixture.Candidates.Accepted;

        await fixture.RunAsync($"{command}\nstatus\nquit\n");

        Assert.Contains("Error: Missing argument", fixture.Transcript);
        Assert.Contains("Output=command-test", fixture.Transcript);
        Assert.Same(accepted, fixture.Candidates.Candidate);
        Assert.Empty(fixture.Port.Messages);
    }

    [Fact]
    public async Task Whitespace_and_existing_optional_arguments_remain_usable()
    {
        await using var fixture = new Session();

        await fixture.RunAsync("  \n\t\n\tBPM\t150\nsource\nbpm\ngenerator\nshape\nch\nchannel\nhelp new\n"
            + "vary seed 1\nvary rhythm .5 seed 2\non\t1\t60\t100\noff 1 60\nnote 1 61 100 0\npc 1 10\nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Equal(150, fixture.Clock.Bpm);
        Assert.Equal(["On", "Off", "On", "Off", "Program"], fixture.Port.Messages);
        Assert.Equal(3, fixture.History.Count);
    }

    [Theory]
    [InlineData("generator help")]
    [InlineData("generator ?")]
    [InlineData("help generator")]
    public async Task Generator_help_explains_choices_without_changing_the_session(string command)
    {
        await using var fixture = new Session();
        var candidate = fixture.Candidates.Candidate;
        var controls = fixture.ControlSnapshot();

        await fixture.RunAsync($"{command}\nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Contains("1-bar loop", fixture.Transcript);
        Assert.Contains("bass only", fixture.Transcript);
        Assert.Contains("inactive", fixture.Transcript);
        Assert.DoesNotContain("euclidean-phrase", fixture.Transcript);
        Assert.DoesNotContain("euclidean2", fixture.Transcript);
        Assert.Equal(controls, fixture.ControlSnapshot());
        Assert.Same(candidate, fixture.Candidates.Candidate);
        Assert.Equal(1, fixture.History.Count);
        Assert.Equal(0, fixture.PortOpenAttempts);
        Assert.Empty(fixture.Port.Messages);
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
    }

    [Theory]
    [InlineData("euclidean-phrase")]
    [InlineData("EuClIdEaN2")]
    public async Task Retired_generator_names_are_rejected_without_changing_the_session(string choice)
    {
        await using var fixture = new Session();
        fixture.Controls.Mode = GeneratorMode.Motif;
        var candidate = fixture.Candidates.Candidate;
        var controls = fixture.ControlSnapshot();

        await fixture.RunAsync($"generator {choice}\nquit\n");

        Assert.Contains("Euclidean phrase is no longer selectable", fixture.Transcript);
        Assert.Contains("generator phrase", fixture.Transcript);
        Assert.Equal(controls, fixture.ControlSnapshot());
        Assert.Same(candidate, fixture.Candidates.Candidate);
        Assert.Same(candidate, fixture.Player.CurrentPattern);
        Assert.Equal(1, fixture.History.Count);
        Assert.Equal(0, fixture.PortOpenAttempts);
        Assert.Empty(fixture.Port.Messages);
        Assert.Equal(RandomDefaults.CreateRandomSource(123).NextULong(), fixture.Seeds.NextULong());
    }

    [Fact]
    public async Task Saved_euclidean_phrase_patterns_still_load_and_support_repeated_variation()
    {
        await using var fixture = new Session();
        var legacy = new EuclideanPhraseGenerator().Generate(new EuclideanGeneratorSettings(
            new PatternName("Legacy phrase"), 64, PatternTiming.SixteenthNotes,
            CandidateGenerator.DefaultTonalContext(), MusicalRole.Bass,
            new NormalizedAmount(.4), new NormalizedAmount(.35), new NormalizedAmount(.65),
            new NormalizedAmount(.8), new NormalizedAmount(.15), 123));
        await fixture.Library.SaveAsync(legacy, TestContext.Current.CancellationToken);

        await fixture.RunAsync("load #1\nkeep\nvary seed 456\nvary notes .3 seed 789\nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        var accepted = fixture.Candidates.Accepted!;
        Assert.Equal(legacy.Id, accepted.Id);
        Assert.Equal(legacy.Timing, accepted.Timing);
        Assert.Equal(legacy.Steps.Length, accepted.Steps.Length);
        for (var index = 0; index < legacy.Steps.Length; index++)
        {
            Assert.Equal(legacy.Steps[index].Notes, accepted.Steps[index].Notes);
            Assert.Equal(legacy.Steps[index].Probability, accepted.Steps[index].Probability);
        }
        Assert.Equal(4, fixture.History.Count);
        var varied = fixture.Candidates.Candidate!;
        Assert.NotEqual(legacy.Id, varied.Id);
        Assert.Equal(legacy.Recipe!.Lineage, varied.Recipe!.Lineage);
        Assert.Equal(789UL, varied.Recipe.Seed);
        Assert.Same(varied, fixture.Player.CurrentPattern);
        Assert.Empty(fixture.Port.Messages);
    }

    [Fact]
    public async Task Generator_listing_and_advanced_help_offer_only_current_choices()
    {
        await using var fixture = new Session();

        await fixture.RunAsync("generator\nhelp advanced\nquit\n");

        Assert.DoesNotContain("Error:", fixture.Transcript);
        Assert.Contains("Choices: euclidean, turnaround, motif, phrase, groove", fixture.Transcript);
        Assert.DoesNotContain("euclidean-phrase", fixture.Transcript);
        Assert.DoesNotContain("euclidean2", fixture.Transcript);
    }

    private sealed class Session : IAsyncDisposable
    {
        public SafeMidiOutput Output { get; } = new();
        public TransportEngine Transport { get; } = new();
        public InternalMidiClock Clock { get; }
        public PatternPlayer Player { get; }
        public CandidateSession Candidates { get; }
        public CandidateHistory History { get; } = new();
        public GenerationControls Controls { get; } = new();
        public PatternLibrary Library { get; } = new(Path.Combine(Path.GetTempPath(), $"jam-weaver-command-{Guid.NewGuid():N}"));
        public IRandomSource Seeds { get; } = RandomDefaults.CreateRandomSource(123);
        public RecordingPort Port { get; } = new();
        public int PortOpenAttempts { get; private set; }
        public string Transcript => writer.ToString();
        private readonly StringWriter writer = new();
        private readonly StructuredPhraseGenerator phrase = new();
        private readonly GrooveVocabularyGenerator groove = new();
        private readonly CandidateGenerator generator;

        public Session()
        {
            Output.ReplacePort(Port);
            Clock = new InternalMidiClock(Output, Transport);
            Player = new PatternPlayer(Output, Transport);
            Candidates = new CandidateSession(Player);
            generator = new CandidateGenerator(new EuclideanMotifGenerator(), new TurnaroundLoopGenerator(),
                new EuclideanPhraseGenerator(), phrase, groove, new MotifGenerator());
            var initial = generator.Generate(42, null, Controls);
            Candidates.SetCandidate(initial);
            History.Add(initial);
        }

        public object ControlSnapshot() => (Controls.Mode, Controls.PhraseLength, Controls.Activity, Controls.Rhythm,
            Controls.Movement, Controls.Variation, Controls.Turnaround, Controls.Groove, Controls.Similarity, Controls.MotifShape);

        public Task RunAsync(string commands)
        {
            var catalog = new FakeMidiPortCatalog
            {
                OutputFactory = _ => { PortOpenAttempts++; return new RecordingPort(); },
                InputFactory = _ => { PortOpenAttempts++; throw new InvalidOperationException("unexpected input open"); }
            };
            return new JamWeaverConsole(new StringReader(commands), new ConsoleDisplay(writer), Output, Transport, Clock,
                Player, Candidates, generator, Controls, History, Library, phrase, groove, Seeds,
                new ExternalMidiClockInput(), catalog).RunAsync();
        }

        public async ValueTask DisposeAsync()
        {
            try { await Clock.DisposeAsync(); }
            finally
            {
                Player.Dispose();
                Output.Dispose();
                Library.Dispose();
                if (Directory.Exists(Library.RootDirectory)) Directory.Delete(Library.RootDirectory, recursive: true);
            }
        }
    }

    private sealed class RecordingPort : IMidiOutputPort
    {
        public string Name => "command-test";
        public List<string> Messages { get; } = [];
        public void SendNoteOn(MidiChannel channel, MidiValue note, MidiValue velocity) => Messages.Add("On");
        public void SendNoteOff(MidiChannel channel, MidiValue note, MidiValue velocity) => Messages.Add("Off");
        public void SendControlChange(MidiChannel channel, MidiValue controller, MidiValue value) => Messages.Add("CC");
        public void SendProgramChange(MidiChannel channel, MidiValue program) => Messages.Add("Program");
        public void SendClock() => Messages.Add("Clock");
        public void SendStart() => Messages.Add("Start");
        public void SendContinue() => Messages.Add("Continue");
        public void SendStop() => Messages.Add("Stop");
        public void Dispose() { }
    }
}
