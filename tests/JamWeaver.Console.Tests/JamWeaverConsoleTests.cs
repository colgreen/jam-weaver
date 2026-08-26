using JamWeaver.ConsoleApp;
using JamWeaver.Core.Generation;
using JamWeaver.Core.Generation.Groove;
using JamWeaver.Core.Generation.Motif;
using JamWeaver.Core.Generation.Phrase;
using JamWeaver.Core.Midi;
using JamWeaver.Core.Performance;
using JamWeaver.Core.Persistence;
using JamWeaver.Core.Transport;
using Redzen.Random;

namespace JamWeaver.Console.Tests;

public sealed class JamWeaverConsoleTests
{
    [Fact]
    public async Task Command_failure_does_not_end_the_interactive_session()
    {
        using var output = new SafeMidiOutput();
        var transport = new TransportEngine();
        await using var internalClock = new InternalMidiClock(output, transport);
        using var player = new PatternPlayer(output, transport);
        var session = new CandidateSession(player);
        var phraseGenerator = new MelodicPhraseGenerator();
        var grooveGenerator = new MelodicGrooveGenerator();
        var candidateGenerator = new CandidateGenerator(new MelodicPatternGenerator(),
            new Euclidean2PatternGenerator(), phraseGenerator, grooveGenerator, new MusicalMotifGenerator());
        var writer = new StringWriter();
        using var library = new PatternLibrary(Path.Combine(Path.GetTempPath(), $"jam-weaver-{Guid.NewGuid():N}"));
        var application = new JamWeaverConsole(new StringReader("bpm invalid\nhelp\nquit\n"),
            new ConsoleDisplay(writer), output, transport, internalClock, player, session, candidateGenerator,
            new GenerationControls(), new PatternMutator(), new PhrasePatternMutator(), new CandidateHistory(),
            library, phraseGenerator, grooveGenerator, RandomDefaults.CreateRandomSource(123),
            new ExternalMidiClockInput());

        await application.RunAsync();

        Assert.Contains("Error:", writer.ToString());
        Assert.Contains("Live controls:", writer.ToString());
    }

    [Fact]
    public async Task Command_responses_use_the_injected_display_writer()
    {
        using var output = new SafeMidiOutput();
        var transport = new TransportEngine();
        await using var internalClock = new InternalMidiClock(output, transport);
        using var player = new PatternPlayer(output, transport);
        var session = new CandidateSession(player);
        var phraseGenerator = new MelodicPhraseGenerator();
        var grooveGenerator = new MelodicGrooveGenerator();
        var writer = new StringWriter();
        using var library = new PatternLibrary(Path.Combine(Path.GetTempPath(), $"jam-weaver-{Guid.NewGuid():N}"));
        var application = new JamWeaverConsole(new StringReader("bpm\nquit\n"), new ConsoleDisplay(writer), output,
            transport, internalClock, player, session,
            new CandidateGenerator(new MelodicPatternGenerator(), new Euclidean2PatternGenerator(), phraseGenerator,
                grooveGenerator, new MusicalMotifGenerator()),
            new GenerationControls(), new PatternMutator(), new PhrasePatternMutator(), new CandidateHistory(),
            library, phraseGenerator, grooveGenerator, RandomDefaults.CreateRandomSource(123),
            new ExternalMidiClockInput());

        await application.RunAsync();

        Assert.Contains("Tempo: 120 BPM", writer.ToString());
    }
}
