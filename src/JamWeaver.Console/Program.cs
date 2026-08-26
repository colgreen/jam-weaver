using JamWeaver.ConsoleApp;
using Redzen.Random;

var display = new ConsoleDisplay(Console.Out);
display.WriteStartup();

using var output = new SafeMidiOutput();
var transport = new TransportEngine();
await using var internalClock = new InternalMidiClock(output, transport);
await using var watchdog = new ExternalClockWatchdog(transport);
using var player = new PatternPlayer(output, transport);
var session = new CandidateSession(player);
var euclideanGenerator = new EuclideanMotifGenerator();
var euclideanPhraseGenerator = new EuclideanPhraseGenerator();
var phraseGenerator = new StructuredPhraseGenerator();
var grooveGenerator = new GrooveVocabularyGenerator();
var motifGenerator = new MotifGenerator();
var candidateGenerator = new CandidateGenerator(euclideanGenerator, euclideanPhraseGenerator, phraseGenerator,
    grooveGenerator, motifGenerator);
var generationControls = new GenerationControls();
var mutator = new PatternMutator();
var phraseMutator = new PhrasePatternMutator();
var history = new CandidateHistory();
using var patternLibrary = new PatternLibrary(Path.Combine(Environment.CurrentDirectory, "patterns"));
var seedSource = RandomDefaults.CreateRandomSource();
var externalClock = new ExternalMidiClockInput();

var application = new JamWeaverConsole(Console.In, display, output, transport, internalClock, player, session,
    candidateGenerator, generationControls, mutator, phraseMutator, history, patternLibrary, phraseGenerator,
    grooveGenerator, seedSource, externalClock);
await application.RunAsync();
