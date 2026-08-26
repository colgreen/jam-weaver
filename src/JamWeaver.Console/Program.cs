using System.Globalization;
using JamWeaver.ConsoleApp.DryWetMidi;
using JamWeaver.Core.Generation;
using JamWeaver.Core.Generation.Phrase;
using JamWeaver.Core.Generation.Groove;
using JamWeaver.Core.Generation.Motif;
using JamWeaver.Core.Midi;
using JamWeaver.Core.Performance;
using JamWeaver.Core.Persistence;
using JamWeaver.Core.Sequencer;
using JamWeaver.Core.Transport;
using Redzen.Random;
using JamWeaver.ConsoleApp;

var display = new ConsoleDisplay(Console.Out);
display.WriteStartup();

using var output = new SafeMidiOutput();
var transport = new TransportEngine();
await using var internalClock = new InternalMidiClock(output, transport);
await using var watchdog = new ExternalClockWatchdog(transport);
using var player = new PatternPlayer(output, transport);
var session = new CandidateSession(player);
var melodicGenerator = new MelodicPatternGenerator();
var euclidean2Generator = new Euclidean2PatternGenerator();
var phraseGenerator = new MelodicPhraseGenerator();
var grooveGenerator = new MelodicGrooveGenerator();
var motifGenerator = new MusicalMotifGenerator();
var candidateGenerator = new CandidateGenerator(melodicGenerator, euclidean2Generator, phraseGenerator,
    grooveGenerator, motifGenerator);
var generationControls = new GenerationControls();
var mutator = new PatternMutator();
var phraseMutator = new PhrasePatternMutator();
var history = new CandidateHistory();
Pattern? comparisonPhrase = null;
Pattern? comparisonGroove = null;
var comparisonShowsGroove = true;
var seedSource = RandomDefaults.CreateRandomSource();
using var patternLibrary = new PatternLibrary(Path.Combine(Environment.CurrentDirectory, "patterns"));
var externalClock = new ExternalMidiClockInput();
DryWetMidiInput? input = null;
externalClock.MessageReceived += (_, message) => transport.Process(ClockSource.External, message);

display.WriteSetup(output, input?.Name, transport, internalClock, player);
try
{
    while (true)
    {
        display.WritePrompt(session, player, transport, generationControls.Mode);
        var line = Console.ReadLine();
        if (line is null) break;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) continue;

        try
        {
            switch (parts[0].ToLowerInvariant())
            {
                case "setup": display.WriteSetup(output, input?.Name, transport, internalClock, player); break;
                case "out":
                    if (parts.Length == 1) { display.WriteOutputs(); break; }
                    player.Mute();
                    output.ReplacePort(DryWetMidiPortCatalog.OpenOutput(I(parts, 1)));
                    Console.WriteLine($"Output: {output.PortName}");
                    Console.WriteLine("Next: new, then go.");
                    break;
                case "in":
                    if (parts.Length == 1) { display.WriteInputs(); break; }
                    input?.Dispose();
                    input = new DryWetMidiInput(DryWetMidiPortCatalog.OpenInput(I(parts, 1)), externalClock);
                    Console.WriteLine($"Input: {input.Name}");
                    break;
                case "source":
                    if (parts.Length == 1)
                    {
                        Console.WriteLine($"Clock source: {transport.Source.ToString().ToLowerInvariant()}");
                        Console.WriteLine("Select with: source internal|external");
                        break;
                    }
                    var source = S(parts, 1).ToLowerInvariant() switch
                    {
                        "external" => ClockSource.External,
                        "internal" => ClockSource.Internal,
                        _ => throw new ArgumentException("Source must be internal or external.")
                    };
                    if (transport.Source == ClockSource.Internal && internalClock.IsRunning) internalClock.Stop();
                    transport.SelectSource(source);
                    Console.WriteLine($"Clock source: {source.ToString().ToLowerInvariant()}");
                    break;
                case "bpm":
                    if (parts.Length == 1)
                    {
                        Console.WriteLine($"Tempo: {internalClock.Bpm:0.##} BPM");
                        Console.WriteLine("Set with: bpm <20..300>");
                        break;
                    }
                    internalClock.Bpm = D(parts, 1);
                    Console.WriteLine($"Tempo: {internalClock.Bpm:0.##} BPM");
                    break;
                case "start": if (transport.Source == ClockSource.External) Console.WriteLine("Waiting for external MIDI Start."); else internalClock.Start(); break;
                case "continue": if (transport.Source == ClockSource.External) Console.WriteLine("Waiting for external MIDI Continue."); else internalClock.Continue(); break;
                case "stop": if (transport.Source == ClockSource.External) transport.Process(ClockSource.External, RealtimeMessage.Stop); else internalClock.Stop(); break;
                case "go":
                    player.Play();
                    if (transport.Source == ClockSource.External)
                        Console.WriteLine("Pattern playback enabled; waiting for external MIDI Start or Continue.");
                    else if (!internalClock.IsRunning)
                        internalClock.Start();
                    else
                        Console.WriteLine("Pattern playback enabled; transport is already running.");
                    break;
                case "new":
                    var generateSeed = parts.Length > 1 ? U(parts, 1) : seedSource.NextULong();
                    SelectCandidate(session, history, candidateGenerator.Generate(generateSeed, session.Candidate,
                        generationControls));
                    Console.WriteLine($"Generated candidate with seed {generateSeed}.");
                    break;
                case "compare":
                    if (parts.Length > 1 || comparisonPhrase is null || comparisonGroove is null)
                    {
                        var compareSeed = parts.Length > 1 ? U(parts, 1) : seedSource.NextULong();
                        var current = session.Candidate;
                        var context = current?.TonalContext ?? CandidateGenerator.DefaultTonalContext();
                        var role = current?.Role ?? MusicalRole.Bass;
                        if (role != MusicalRole.Bass) throw new InvalidOperationException("Groove comparison currently requires the bass role.");
                        var name = new PatternName($"Compare {compareSeed}");
                        comparisonPhrase = phraseGenerator.Generate(new PhraseGeneratorSettings(name, PhraseLength.FourBars,
                            context, role, generationControls.Activity, generationControls.Rhythm,
                            generationControls.Movement, generationControls.Variation, generationControls.Turnaround, compareSeed));
                        comparisonGroove = grooveGenerator.Generate(new GrooveGeneratorSettings(name, context, role,
                            generationControls.Groove, generationControls.Similarity, generationControls.Activity,
                            generationControls.Movement, generationControls.Variation, generationControls.Turnaround, compareSeed));
                        history.Add(comparisonPhrase); history.Add(comparisonGroove); comparisonShowsGroove = false;
                        Console.WriteLine($"Prepared matched comparison with seed {compareSeed}.");
                    }
                    comparisonShowsGroove = !comparisonShowsGroove;
                    session.SetCandidate(comparisonShowsGroove ? comparisonGroove : comparisonPhrase);
                    Console.WriteLine($"Comparison: {(comparisonShowsGroove ? "groove" : "phrase")} (changes at next bar while running).");
                    break;
                case "vary":
                    var parent = session.Candidate ?? throw new InvalidOperationException("Create a candidate with 'new' first.");
                    var variationArgument = 1;
                    var mutationTarget = default(PhraseMutationTarget);
                    var hasTarget = parts.Length > variationArgument && TryMutationTarget(parts[variationArgument], out mutationTarget);
                    if (hasTarget) variationArgument++;
                    var (variationStrength, variationSeed) = VariationArguments(parts, variationArgument, seedSource);
                    if (hasTarget)
                    {
                        SelectCandidate(session, history, phraseMutator.Mutate(parent,
                            new PhraseMutationSettings(mutationTarget, new NormalizedAmount(variationStrength), variationSeed)));
                        Console.WriteLine($"Varied {mutationTarget.ToString().ToLowerInvariant()} with seed {variationSeed} at strength {variationStrength:0.##}.");
                    }
                    else
                    {
                        SelectCandidate(session, history, mutator.Mutate(parent,
                            new MutationSettings(new NormalizedAmount(variationStrength), variationSeed)));
                        Console.WriteLine($"Varied candidate with seed {variationSeed} at strength {variationStrength:0.##}.");
                    }
                    break;
                case "back":
                    session.SetCandidate(history.Previous()); Console.WriteLine($"Candidate history {history.Position}/{history.Count}."); break;
                case "forward":
                    session.SetCandidate(history.Next()); Console.WriteLine($"Candidate history {history.Position}/{history.Count}."); break;
                case "generator":
                    if (parts.Length == 1)
                    {
                        Console.WriteLine($"Generator: {generationControls.Mode.ToString().ToLowerInvariant()}");
                        Console.WriteLine("Choices: euclidean, euclidean2, motif, phrase, groove");
                        Console.WriteLine($"Controls: {ConsoleDisplay.GeneratorControls(generationControls.Mode)}");
                        Console.WriteLine("Set with: generator <choice>");
                        break;
                    }
                    generationControls.Mode = S(parts, 1).ToLowerInvariant() switch { "euclidean" => GeneratorMode.Euclidean, "euclidean2" => GeneratorMode.Euclidean2, "motif" => GeneratorMode.Motif, "phrase" => GeneratorMode.Phrase, "groove" => GeneratorMode.Groove, _ => throw new ArgumentException("Generator must be euclidean, euclidean2, motif, phrase, or groove.") };
                    Console.WriteLine($"Generator: {generationControls.Mode.ToString().ToLowerInvariant()}");
                    Console.WriteLine($"Controls: {ConsoleDisplay.GeneratorControls(generationControls.Mode)}");
                    break;
                case "groove": generationControls.Groove = ParseGrooveSelection(S(parts, 1)); break;
                case "shape":
                    if (parts.Length == 1)
                    {
                        Console.WriteLine($"Motif shape: {ConsoleDisplay.MotifText(generationControls.MotifShape)}{(generationControls.Mode == GeneratorMode.Motif ? string.Empty : $" (inactive while generator is {generationControls.Mode.ToString().ToLowerInvariant()})")}");
                        Console.WriteLine("Choices: auto, pedal, root-fifth, walking, call-response, arch, pickup, riff");
                        Console.WriteLine("Set with: shape <choice>");
                        Console.WriteLine("Explain choices with: shape help");
                        break;
                    }
                    if (S(parts, 1).ToLowerInvariant() is "help" or "?")
                    {
                        display.WriteHelp("shape");
                        break;
                    }
                    generationControls.MotifShape = ParseMotifShape(S(parts, 1));
                    Console.WriteLine($"Motif shape: {ConsoleDisplay.MotifText(generationControls.MotifShape)}{(generationControls.Mode == GeneratorMode.Motif ? string.Empty : $" (saved for motif; inactive while generator is {generationControls.Mode.ToString().ToLowerInvariant()})")}");
                    break;
                case "similarity": generationControls.Similarity = EnumValue<GrooveSimilarity>(parts, 1); break;
                case "length": generationControls.PhraseLength = I(parts, 1) switch { 1 => PhraseLength.OneBar, 2 => PhraseLength.TwoBars, 4 => PhraseLength.FourBars, _ => throw new ArgumentException("Length must be 1, 2, or 4 bars.") }; break;
                case "activity": generationControls.Activity = EnumValue<PhraseActivity>(parts, 1); break;
                case "rhythm": generationControls.Rhythm = EnumValue<PhraseRhythm>(parts, 1); break;
                case "movement": generationControls.Movement = EnumValue<PhraseLevel>(parts, 1); break;
                case "variation": generationControls.Variation = EnumValue<PhraseLevel>(parts, 1); break;
                case "turnaround": generationControls.Turnaround = EnumValue<PhraseTurnaround>(parts, 1); break;
                case "settings": Console.WriteLine($"Generator={generationControls.Mode.ToString().ToLowerInvariant()}, Shape={ConsoleDisplay.MotifText(generationControls.MotifShape)}, Length={(int)generationControls.PhraseLength}, Activity={generationControls.Activity.ToString().ToLowerInvariant()}, Rhythm={generationControls.Rhythm.ToString().ToLowerInvariant()}, Groove={ConsoleDisplay.GrooveText(generationControls.Groove)}, Similarity={generationControls.Similarity.ToString().ToLowerInvariant()}, Movement={generationControls.Movement.ToString().ToLowerInvariant()}, Variation={generationControls.Variation.ToString().ToLowerInvariant()}, Turnaround={generationControls.Turnaround.ToString().ToLowerInvariant()}"); break;
                case "keep":
                    session.Accept(); Console.WriteLine("Candidate kept as the safe point."); break;
                case "revert":
                    session.Reject(); Console.WriteLine("Returning to the safe point."); break;
                case "key":
                    var rootPattern = Candidate(session);
                    var direction = S(parts, 1).ToLowerInvariant() switch
                    {
                        "up" => 1,
                        "down" => -1,
                        _ => throw new ArgumentException("Key direction must be up or down.")
                    };
                    SelectCandidate(session, history, PatternTransformations.TransposeRoot(rootPattern, direction));
                    display.WritePattern(session, player);
                    break;
                case "palette": SelectCandidate(session, history, PatternTransformations.TogglePalette(Candidate(session))); display.WritePattern(session, player); break;
                case "role":
                    var selectedRole = S(parts, 1).ToLowerInvariant() switch
                    {
                        "bass" => MusicalRole.Bass,
                        "middle" or "mid" => MusicalRole.Middle,
                        "high" => MusicalRole.High,
                        _ => throw new ArgumentException("Role must be bass, middle, or high.")
                    };
                    SelectCandidate(session, history, PatternTransformations.ChangeRole(Candidate(session), selectedRole));
                    Console.WriteLine($"Register: {selectedRole.ToString().ToLowerInvariant()} (key unchanged).");
                    display.WritePattern(session, player);
                    break;
                case "ch":
                case "channel":
                    if (parts.Length == 1)
                    {
                        Console.WriteLine($"Pattern channel: {player.Channel.Number}");
                        Console.WriteLine("Set with: ch <1..16> (or channel <1..16>)");
                        break;
                    }
                    player.Channel = C(parts, 1);
                    Console.WriteLine($"Pattern channel: {player.Channel.Number}");
                    break;
                case "play": player.Play(); Console.WriteLine("Pattern playback enabled."); break;
                case "mute": player.Mute(); Console.WriteLine("Pattern playback muted."); break;
                case "pattern": display.WritePattern(session, player); break;
                case "library":
                    display.WriteLibrary(await patternLibrary.ListAsync());
                    Console.WriteLine($"Library: {patternLibrary.RootDirectory}");
                    break;
                case "save":
                    var accepted = session.Accepted ?? throw new InvalidOperationException("There is no accepted pattern to save.");
                    var requestedName = line[parts[0].Length..].Trim();
                    var patternToSave = requestedName.Length == 0 ? accepted : accepted.Rename(new PatternName(requestedName));
                    var saved = await patternLibrary.SaveAsync(patternToSave);
                    if (patternToSave.Name != accepted.Name) session.RenameAccepted(patternToSave);
                    Console.WriteLine($"Saved '{saved.Name}'. Load it with: load {saved.Name}");
                    break;
                case "load":
                    var entries = await patternLibrary.ListAsync();
                    var loadQuery = line[parts[0].Length..].Trim();
                    if (loadQuery.Length == 0)
                    {
                        display.WriteLibrary(entries);
                        if (entries.Length > 0) Console.WriteLine("Load with: load <name> or load #<number>");
                        break;
                    }
                    var loadEntry = ResolveLibraryEntry(entries, loadQuery);
                    var loaded = await patternLibrary.LoadAsync(loadEntry);
                    SelectCandidate(session, history, loaded);
                    Console.WriteLine($"Loaded '{loaded.Name}' as a candidate ({(player.CurrentPattern?.Id == loaded.Id ? "audible" : "pending for next bar")}).");
                    break;
                case "note": await output.SendNoteAsync(C(parts, 1), V(parts, 2), V(parts, 3), TimeSpan.FromMilliseconds(parts.Length > 4 ? I(parts, 4) : 250)); break;
                case "on": output.NoteOn(C(parts, 1), V(parts, 2), V(parts, 3)); break;
                case "off": output.NoteOff(C(parts, 1), V(parts, 2), new MidiValue(parts.Length > 3 ? I(parts, 3) : 0)); break;
                case "cc": output.ControlChange(C(parts, 1), V(parts, 2), V(parts, 3)); break;
                case "pc": output.ProgramChange(C(parts, 1), V(parts, 2)); break;
                case "panic": player.Mute(); output.Panic(); break;
                case "status": display.WriteStatus(output, input?.Name, transport, internalClock, player); break;
                case "help": display.WriteHelp(parts.Length > 1 ? parts[1] : null); break;
                case "quit" or "exit": return;
                default: Console.WriteLine("Unknown command. Type 'help'."); break;
            }
        }
        catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
    }
}
finally { input?.Dispose(); }

static string S(string[] p, int i) => i < p.Length ? p[i] : throw new ArgumentException("Missing argument.");
static int I(string[] p, int i) => int.Parse(S(p, i), CultureInfo.InvariantCulture);
static double D(string[] p, int i) => double.Parse(S(p, i), CultureInfo.InvariantCulture);
static ulong U(string[] p, int i) => ulong.Parse(S(p, i), CultureInfo.InvariantCulture);
static MidiChannel C(string[] p, int i) => new(I(p, i));
static MidiValue V(string[] p, int i) => new(I(p, i));
static T EnumValue<T>(string[] parts, int index) where T : struct, Enum =>
    Enum.TryParse<T>(S(parts, index), true, out var value) && Enum.IsDefined(value)
        ? value : throw new ArgumentException($"Invalid {typeof(T).Name} value.");
static bool TryMutationTarget(string value, out PhraseMutationTarget target) =>
    Enum.TryParse(value, true, out target) && Enum.IsDefined(target);
static (double Strength, ulong Seed) VariationArguments(string[] parts, int index, IRandomSource seedSource)
{
    var strength = .3;
    if (index < parts.Length && !parts[index].Equals("seed", StringComparison.OrdinalIgnoreCase))
        strength = D(parts, index++);
    ulong? seed = null;
    if (index < parts.Length)
    {
        if (!parts[index].Equals("seed", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Expected 'seed <number>' after variation strength.");
        seed = U(parts, ++index);
        index++;
    }
    if (index != parts.Length) throw new ArgumentException("Unexpected vary argument.");
    return (strength, seed ?? seedSource.NextULong());
}
static Pattern Candidate(CandidateSession session) =>
    session.Candidate ?? throw new InvalidOperationException("Create a candidate with 'new' first.");
static void SelectCandidate(CandidateSession session, CandidateHistory history, Pattern pattern)
{
    history.Add(pattern);
    session.SetCandidate(pattern);
}
static PatternLibraryEntry ResolveLibraryEntry(IReadOnlyList<PatternLibraryEntry> entries, string query)
{
    if (query.Length == 0) throw new ArgumentException("Choose a saved pattern with: load <name> or load #<number>.");
    if (query[0] == '#') return EntryAt(entries, ParseLibraryNumber(query[1..]));

    var matches = entries.Select((entry, index) => (entry, index))
        .Where(item => item.entry.IsValid && item.entry.Name?.Equals(query, StringComparison.OrdinalIgnoreCase) == true)
        .ToArray();
    if (matches.Length == 1) return matches[0].entry;
    if (matches.Length > 1)
    {
        var choices = string.Join(", ", matches.Select(item => $"#{item.index + 1}"));
        throw new ArgumentException($"More than one pattern is named '{query}'. Choose one from the library with: load {choices.Replace(", ", " or load ")}");
    }
    if (int.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        return EntryAt(entries, number);
    throw new ArgumentException($"No saved pattern is named '{query}'. Type 'library' to list saved patterns.");
}
static int ParseLibraryNumber(string value) => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
    ? number : throw new ArgumentException("Library selection must look like: load #2.");
static PatternLibraryEntry EntryAt(IReadOnlyList<PatternLibraryEntry> entries, int number)
{
    var index = number - 1;
    if ((uint)index >= (uint)entries.Count) throw new ArgumentOutOfRangeException(nameof(number), "Load number is not in the library list.");
    return entries[index];
}
static GrooveSelection ParseGrooveSelection(string value) => value.ToLowerInvariant() switch
{
    "auto" => GrooveSelection.Auto, "foundation" => GrooveSelection.Foundation, "offbeat" => GrooveSelection.Offbeat,
    "anticipation" => GrooveSelection.Anticipation, "long-short" => GrooveSelection.LongShort,
    "sparse-answer" => GrooveSelection.SparseAnswer, "broken" => GrooveSelection.Broken,
    _ => throw new ArgumentException("Unknown groove category.")
};
static MotifShape ParseMotifShape(string value) => value.ToLowerInvariant() switch
{
    "auto" => MotifShape.Auto, "pedal" => MotifShape.Pedal, "root-fifth" => MotifShape.RootFifth,
    "walking" => MotifShape.Walking, "call-response" => MotifShape.CallResponse, "arch" => MotifShape.Arch,
    "pickup" => MotifShape.Pickup, "riff" => MotifShape.Riff, _ => throw new ArgumentException("Unknown motif shape.")
};
