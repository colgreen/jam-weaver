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

Console.WriteLine("JamWeaver");
Console.WriteLine("Type 'setup' to choose MIDI devices or 'help' for the live controls.");

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
var mutator = new PatternMutator();
var phraseMutator = new PhrasePatternMutator();
var history = new CandidateHistory();
var generatorMode = GeneratorMode.Euclidean;
var phraseLength = PhraseLength.FourBars;
var phraseActivity = PhraseActivity.Medium;
var phraseRhythm = PhraseRhythm.Syncopated;
var phraseMovement = PhraseLevel.Medium;
var phraseVariation = PhraseLevel.Medium;
var phraseTurnaround = PhraseTurnaround.Subtle;
var grooveSelection = GrooveSelection.Auto;
var grooveSimilarity = GrooveSimilarity.Related;
var motifShape = MotifShape.Auto;
Pattern? comparisonPhrase = null;
Pattern? comparisonGroove = null;
var comparisonShowsGroove = true;
var seedSource = RandomDefaults.CreateRandomSource();
using var patternLibrary = new PatternLibrary(Path.Combine(Environment.CurrentDirectory, "patterns"));
var externalClock = new ExternalMidiClockInput();
DryWetMidiInput? input = null;
externalClock.MessageReceived += (_, message) => transport.Process(ClockSource.External, message);

PrintSetup(output, input, transport, internalClock, player);
try
{
    while (true)
    {
        PrintPrompt(session, player, transport, generatorMode);
        var line = Console.ReadLine();
        if (line is null) break;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) continue;

        try
        {
            switch (parts[0].ToLowerInvariant())
            {
                case "setup": PrintSetup(output, input, transport, internalClock, player); break;
                case "out":
                    if (parts.Length == 1) { PrintOutputs(); break; }
                    player.Mute();
                    output.ReplacePort(DryWetMidiPortCatalog.OpenOutput(I(parts, 1)));
                    Console.WriteLine($"Output: {output.PortName}");
                    Console.WriteLine("Next: new, then go.");
                    break;
                case "in":
                    if (parts.Length == 1) { PrintInputs(); break; }
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
                    SelectCandidate(session, history, Generate(generatorMode, generateSeed, session.Candidate,
                        phraseLength, phraseActivity, phraseRhythm, phraseMovement, phraseVariation, phraseTurnaround,
                        grooveSelection, grooveSimilarity, motifShape, melodicGenerator, euclidean2Generator, phraseGenerator,
                        grooveGenerator, motifGenerator));
                    Console.WriteLine($"Generated candidate with seed {generateSeed}.");
                    break;
                case "compare":
                    if (parts.Length > 1 || comparisonPhrase is null || comparisonGroove is null)
                    {
                        var compareSeed = parts.Length > 1 ? U(parts, 1) : seedSource.NextULong();
                        var current = session.Candidate;
                        var context = current?.TonalContext ?? DefaultTonalContext();
                        var role = current?.Role ?? MusicalRole.Bass;
                        if (role != MusicalRole.Bass) throw new InvalidOperationException("Groove comparison currently requires the bass role.");
                        var name = new PatternName($"Compare {compareSeed}");
                        comparisonPhrase = phraseGenerator.Generate(new PhraseGeneratorSettings(name, PhraseLength.FourBars,
                            context, role, phraseActivity, phraseRhythm, phraseMovement, phraseVariation, phraseTurnaround, compareSeed));
                        comparisonGroove = grooveGenerator.Generate(new GrooveGeneratorSettings(name, context, role,
                            grooveSelection, grooveSimilarity, phraseActivity, phraseMovement, phraseVariation, phraseTurnaround, compareSeed));
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
                        Console.WriteLine($"Generator: {generatorMode.ToString().ToLowerInvariant()}");
                        Console.WriteLine("Choices: euclidean, euclidean2, motif, phrase, groove");
                        Console.WriteLine($"Controls: {GeneratorControls(generatorMode)}");
                        Console.WriteLine("Set with: generator <choice>");
                        break;
                    }
                    generatorMode = S(parts, 1).ToLowerInvariant() switch { "euclidean" => GeneratorMode.Euclidean, "euclidean2" => GeneratorMode.Euclidean2, "motif" => GeneratorMode.Motif, "phrase" => GeneratorMode.Phrase, "groove" => GeneratorMode.Groove, _ => throw new ArgumentException("Generator must be euclidean, euclidean2, motif, phrase, or groove.") };
                    Console.WriteLine($"Generator: {generatorMode.ToString().ToLowerInvariant()}");
                    Console.WriteLine($"Controls: {GeneratorControls(generatorMode)}");
                    break;
                case "groove": grooveSelection = ParseGrooveSelection(S(parts, 1)); break;
                case "shape":
                    if (parts.Length == 1)
                    {
                        Console.WriteLine($"Motif shape: {MotifText(motifShape)}{(generatorMode == GeneratorMode.Motif ? string.Empty : $" (inactive while generator is {generatorMode.ToString().ToLowerInvariant()})")}");
                        Console.WriteLine("Choices: auto, pedal, root-fifth, walking, call-response, arch, pickup, riff");
                        Console.WriteLine("Set with: shape <choice>");
                        Console.WriteLine("Explain choices with: shape help");
                        break;
                    }
                    if (S(parts, 1).ToLowerInvariant() is "help" or "?")
                    {
                        Console.WriteLine(ShapeHelpText());
                        break;
                    }
                    motifShape = ParseMotifShape(S(parts, 1));
                    Console.WriteLine($"Motif shape: {MotifText(motifShape)}{(generatorMode == GeneratorMode.Motif ? string.Empty : $" (saved for motif; inactive while generator is {generatorMode.ToString().ToLowerInvariant()})")}");
                    break;
                case "similarity": grooveSimilarity = EnumValue<GrooveSimilarity>(parts, 1); break;
                case "length": phraseLength = I(parts, 1) switch { 1 => PhraseLength.OneBar, 2 => PhraseLength.TwoBars, 4 => PhraseLength.FourBars, _ => throw new ArgumentException("Length must be 1, 2, or 4 bars.") }; break;
                case "activity": phraseActivity = EnumValue<PhraseActivity>(parts, 1); break;
                case "rhythm": phraseRhythm = EnumValue<PhraseRhythm>(parts, 1); break;
                case "movement": phraseMovement = EnumValue<PhraseLevel>(parts, 1); break;
                case "variation": phraseVariation = EnumValue<PhraseLevel>(parts, 1); break;
                case "turnaround": phraseTurnaround = EnumValue<PhraseTurnaround>(parts, 1); break;
                case "settings": Console.WriteLine($"Generator={generatorMode.ToString().ToLowerInvariant()}, Shape={MotifText(motifShape)}, Length={(int)phraseLength}, Activity={phraseActivity.ToString().ToLowerInvariant()}, Rhythm={phraseRhythm.ToString().ToLowerInvariant()}, Groove={GrooveText(grooveSelection)}, Similarity={grooveSimilarity.ToString().ToLowerInvariant()}, Movement={phraseMovement.ToString().ToLowerInvariant()}, Variation={phraseVariation.ToString().ToLowerInvariant()}, Turnaround={phraseTurnaround.ToString().ToLowerInvariant()}"); break;
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
                    PrintPattern(session, player);
                    break;
                case "palette": SelectCandidate(session, history, PatternTransformations.TogglePalette(Candidate(session))); PrintPattern(session, player); break;
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
                    PrintPattern(session, player);
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
                case "pattern": PrintPattern(session, player); break;
                case "library":
                    PrintLibrary(await patternLibrary.ListAsync());
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
                        PrintLibrary(entries);
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
                case "status": Console.WriteLine($"Output={output.PortName ?? "none"}, Input={input?.Name ?? "none"}, Source={transport.Source.ToString().ToLowerInvariant()}, State={transport.State}, Bar={transport.Position.Bar + 1}, Beat={transport.Position.Beat + 1}, Pulse={transport.Position.PulseInBeat}, BPM={internalClock.Bpm:0.##}, Pattern={(player.IsEnabled ? "playing" : "muted")}, Error={player.Error?.Message ?? "none"}"); break;
                case "help": Help(parts.Length > 1 ? parts[1] : null); break;
                case "quit" or "exit": return;
                default: Console.WriteLine("Unknown command. Type 'help'."); break;
            }
        }
        catch (Exception ex) { Console.WriteLine($"Error: {ex.Message}"); }
    }
}
finally { input?.Dispose(); }

static void PrintDevices()
{
    PrintOutputs();
    PrintInputs();
}
static void PrintOutputs()
{
    Console.WriteLine("Outputs:");
    foreach (var item in DryWetMidiPortCatalog.OutputNames().Select((name, index) => (name, index))) Console.WriteLine($"  {item.index}: {item.name}");
    Console.WriteLine("Select one with: out <number>");
}
static void PrintInputs()
{
    Console.WriteLine("Inputs:");
    foreach (var item in DryWetMidiPortCatalog.InputNames().Select((name, index) => (name, index))) Console.WriteLine($"  {item.index}: {item.name}");
    Console.WriteLine("Select one with: in <number>");
}
static void PrintSetup(SafeMidiOutput output, DryWetMidiInput? input, TransportEngine transport,
    InternalMidiClock internalClock, PatternPlayer player)
{
    Console.WriteLine($"Output: {output.PortName ?? "not selected"}");
    Console.WriteLine($"Input:  {input?.Name ?? "not selected"}");
    Console.WriteLine($"Clock:  {transport.Source.ToString().ToLowerInvariant()}, {internalClock.Bpm:0.##} BPM");
    Console.WriteLine($"Channel: {player.Channel.Number}");
    Console.WriteLine();
    PrintDevices();
    Console.WriteLine("Clock: source internal|external; set internal tempo with bpm <20..300>.");
    Console.WriteLine("Routing: ch <1..16> (or channel <1..16>).");
    Console.WriteLine("Next: out <number>, ch <number>, source internal, then new and go.");
}

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
static Pattern Generate(GeneratorMode mode, ulong seed, Pattern? current, PhraseLength length,
    PhraseActivity activity, PhraseRhythm rhythm, PhraseLevel movement, PhraseLevel variation,
    PhraseTurnaround turnaround, GrooveSelection grooveSelection, GrooveSimilarity similarity,
    MotifShape motifShape, MelodicPatternGenerator euclidean, Euclidean2PatternGenerator euclidean2,
    MelodicPhraseGenerator phrase,
    MelodicGrooveGenerator groove, MusicalMotifGenerator motif)
{
    var context = current?.TonalContext ?? DefaultTonalContext();
    var role = current?.Role ?? MusicalRole.Bass;
    var name = new PatternName($"Jam {seed}");
    return mode switch
    {
        GeneratorMode.Phrase => phrase.Generate(new PhraseGeneratorSettings(name, length, context, role, activity, rhythm,
            movement, variation, turnaround, seed)),
        GeneratorMode.Groove => groove.Generate(new GrooveGeneratorSettings(name, context, role, grooveSelection,
            similarity, activity, movement, variation, turnaround, seed)),
        GeneratorMode.Motif => motif.Generate(new MotifGeneratorSettings(name, context, role, motifShape,
            activity, movement, variation, seed)),
        GeneratorMode.Euclidean => euclidean.Generate(new MelodicGeneratorSettings(name, 16, PatternTiming.SixteenthNotes,
            context, role, new NormalizedAmount(.4), new NormalizedAmount(.35),
            new NormalizedAmount(.65), new NormalizedAmount(.8), new NormalizedAmount(.15), seed)),
        GeneratorMode.Euclidean2 => euclidean2.Generate(new MelodicGeneratorSettings(name, 64, PatternTiming.SixteenthNotes,
            context, role, new NormalizedAmount(.4), new NormalizedAmount(.35),
            new NormalizedAmount(.65), new NormalizedAmount(.8), new NormalizedAmount(.15), seed)),
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };
}
static void PrintPattern(CandidateSession session, PatternPlayer player)
{
    var candidate = session.Candidate;
    if (candidate is null) { Console.WriteLine("No pattern. Use 'new'."); return; }
    var tonal = candidate.TonalContext is { } context
        ? $", Key={PitchClass(context.Root.Value)} {context.Palette.ToString().Replace("Pentatonic", " pentatonic").ToLowerInvariant()}, Role={candidate.Role!.Value.ToString().ToLowerInvariant()}"
        : string.Empty;
    var seed = candidate.Recipe is { } recipe ? recipe.Seed.ToString(CultureInfo.InvariantCulture) : "n/a";
    var audible = player.CurrentPattern?.Id == candidate.Id ? "audible" : player.PendingPattern?.Id == candidate.Id ? "pending" : "not selected";
    Console.WriteLine($"Candidate={candidate.Name} ({audible}), Accepted={(session.Accepted?.Id == candidate.Id ? "yes" : "no")}, Mode={candidate.Mode}, Seed={seed}, Channel={player.Channel.Number}, Output={(player.IsEnabled ? "play" : "mute")}{tonal}");
    PrintPatternGrid(candidate);
}
static void PrintPatternGrid(Pattern pattern)
{
    var structural = 0UL;
    var exact = pattern.Recipe?.Parameters.TryGetValue("structural-mask", out var mask) == true
        && mask.Kind == RecipeValueKind.Text
        && ulong.TryParse(mask.Text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out structural);
    for (var barStart = 0; barStart < pattern.Steps.Length; barStart += 16)
    {
        var symbols = Enumerable.Range(barStart, Math.Min(16, pattern.Steps.Length - barStart)).Select(index =>
        {
            var step = pattern.Steps[index];
            if (step.Notes.Length == 0) return '.';
            if (step.Probability.Value < 1) return 'g';
            if (index < 64 && (structural & (1UL << index)) != 0) return 'X';
            return !exact && index % 16 == 0 ? 'X' : 'x';
        }).ToArray();
        Console.WriteLine($"  {(barStart / 16) + 1}: {string.Join(' ', symbols)}{(exact ? string.Empty : "  (approx.)")}");
        if (pattern.Recipe?.GeneratorId == MelodicGrooveGenerator.GeneratorId
            && pattern.Recipe.Parameters.TryGetValue($"bar-{barStart / 16}-features", out var features))
            Console.WriteLine($"     {new[] { "A", "A'", "B", "T" }[barStart / 16]} {features.Text}");
    }
}
static void PrintLibrary(IReadOnlyList<PatternLibraryEntry> entries)
{
    if (entries.Count == 0) { Console.WriteLine("No saved patterns."); return; }
    for (var index = 0; index < entries.Count; index++)
    {
        var entry = entries[index];
        if (!entry.IsValid) { Console.WriteLine($"  {index + 1}. [invalid] {entry.FileName}: {entry.Error}"); continue; }
        var tonal = entry.TonalContext is { } context
            ? $", {PitchClass(context.Root.Value)} {context.Palette.ToString().Replace("Pentatonic", " pentatonic").ToLowerInvariant()}, {entry.Role!.Value.ToString().ToLowerInvariant()}"
            : string.Empty;
        Console.WriteLine($"  {index + 1}. {entry.Name} [{entry.Mode!.Value.ToString().ToLowerInvariant()}{tonal}, seed {entry.Seed?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}]");
    }
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
static string PitchClass(int value) => new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" }[value];
static TonalContext DefaultTonalContext() => new(new RootPitchClass(9), PitchPalette.MinorPentatonic);
static void PrintPrompt(CandidateSession session, PatternPlayer player, TransportEngine transport, GeneratorMode generator)
{
    var transportText = transport.State.ToString().ToLowerInvariant();
    var outputText = player.IsEnabled ? "playing" : "muted";
    var candidate = session.Candidate;
    var patternText = candidate is null
        ? "no pattern"
        : player.PendingPattern?.Id == candidate.Id
            ? "pending"
            : session.Accepted?.Id == candidate.Id ? "safe" : "candidate";
    Console.Write($"jam [{transportText}, {outputText}, {generator.ToString().ToLowerInvariant()}, {patternText}]> ");
}
static void Help(string? command)
{
    if (command is null)
    {
        Console.WriteLine("""
Live controls:
  setup                    show configuration and available MIDI ports
  out <number>             select a MIDI output
  in <number>              select an input for external clock
  source internal|external select the clock source
  bpm <20..300>            set the internal-clock tempo
  ch <1..16>               set the pattern's MIDI channel (`channel` also works)
  generator                show or select how `new` creates patterns
  shape                    show or select the motif-only shape
  go | stop                start or stop the performance
  new [seed]               create a fresh pattern
  vary [strength]          create a related pattern
  back | forward           browse recent patterns
  keep | revert            move or return to the safe point
  key up|down | palette    find the key by ear
  role bass|middle|high    change register
  pattern                  show the pattern and rhythm grid
  library | save [name] | load <name>
  panic | status | quit

Type 'help <command>' for details or 'help advanced' for every control.
""");
        return;
    }

    var text = command.ToLowerInvariant() switch
    {
        "help" => "help [command]\nWithout an argument, lists all commands. With a command, explains its syntax and options.",
        "advanced" => """
Device/clock: setup | out <index> | in <index> | source internal|external | bpm <20..300>
Transport: start | continue | stop | play | mute | channel <1..16>
Generation: generator euclidean|euclidean2|motif|phrase|groove | new [seed] | compare [seed]
Shape: shape <shape> | groove <family> | similarity close|related|contrast | length 1|2|4
Character: activity sparse|medium|busy | rhythm steady|syncopated|broken
           movement low|medium|high | variation low|medium|high
           turnaround none|subtle|strong | settings
Editing: vary [target] [strength] [seed <number>] | back | forward | keep | revert
         key up|down | palette | role bass|middle|high
Library: library | save [name] | load <name>|#<number>
Raw MIDI: note | on | off | cc | pc
Safety/status: panic | pattern | status | quit

Type 'help <command>' for detailed syntax.
""",
        "setup" => "setup\nShows current MIDI and clock configuration, lists available ports, and prints the short commands used to select them.",
        "go" => "go\nEnables pattern output and starts internal transport if needed. With external clock selected, waits for MIDI Start or Continue.",
        "new" => "new [seed]\nCreates and auditions a fresh pattern using current settings. Omit seed for fresh material; supply it to reproduce a pattern.",
        "vary" => "vary [target] [strength-0..1] [seed <number>]\nCreates a related candidate with a fresh seed by default. Targets: rhythm, notes, expression, turnaround, or all. Default strength: 0.3. Examples: vary; vary 0.7; vary rhythm 0.5; vary 0.7 seed 123.",
        "back" => "back\nSelects the previous pattern in the eight-item recent-pattern list. Does not change the safe point.",
        "forward" => "forward\nSelects the next pattern in recent history. Does not change the safe point.",
        "keep" => "keep\nMakes the audible candidate the safe point. Only the safe point can be saved.",
        "revert" => "revert\nReturns to the safe point without removing patterns from recent history.",
        "key" => "key up|down\nMoves the candidate's tonal root by one semitone for by-ear key matching. Creates a new audition candidate.",
        "out" => "out <index>\nOpens the numbered MIDI output shown by 'setup'. Mutes pattern playback while changing ports.",
        "in" => "in <index>\nOpens the numbered MIDI input shown by 'setup' for external clock and transport messages.",
        "source" => "source internal|external\nSelects the transport clock. External waits for MIDI Start/Continue and Clock; internal uses 'bpm' and 'start'.",
        "bpm" => "bpm <20..300>\nSets the internal-clock tempo. It has no effect while the external clock source is selected.",
        "start" => "start\nStarts internal clock and transport. With external source selected, waits for MIDI Start from the input device.",
        "continue" => "continue\nContinues internal transport without resetting its position. External mode waits for MIDI Continue.",
        "stop" => "stop\nStops transport and releases active pattern notes. External clock disappearing also stops playback.",
        "generator" => "generator euclidean|euclidean2|motif|phrase|groove\nSelects how 'new' creates candidates. Default: euclidean. Euclidean2 is an experimental four-bar development of Euclidean; motif, phrase, and groove are also experimental. Groove requires role bass.",
        "shape" => ShapeHelpText(),
        "length" => "length 1|2|4\nSets phrase-generator length in bars. Default: 4. Motif and groove always produce four bars.",
        "activity" => "activity sparse|medium|busy\nControls rhythmic note density. Default: medium. Takes effect on the next 'new'.",
        "rhythm" => "rhythm steady|syncopated|broken\nSelects the phrase generator's rhythmic character. Default: syncopated.",
        "groove" => "groove auto|foundation|offbeat|anticipation|long-short|sparse-answer|broken\nSelects a groove-template family. Default: auto. Used only by generator groove.",
        "similarity" => "similarity close|related|contrast\nControls how different later groove bars are from A. Default: related. Used only by generator groove.",
        "movement" => "movement low|medium|high\nControls melodic contour width. Default: medium. Low favors repeats and small moves.",
        "variation" => "variation low|medium|high\nControls phrase development. Default: medium. In motif mode, low repeats A exactly as A-prime.",
        "turnaround" => "turnaround none|subtle|strong\nControls the final phrase bar. Default: subtle. Used by phrase and groove generators.",
        "settings" => "settings\nPrints all current generator controls. Some controls apply only to particular generator modes.",
        "compare" => "compare [seed]\nPrepares matched phrase/groove bass candidates, or toggles an existing comparison. Changes remain next-bar quantized.",
        "palette" => "palette\nToggles the candidate between major and minor pentatonic. Creates a new audition candidate.",
        "role" => "role bass|middle|high\nChanges the candidate's register without changing its key. Subsequent 'new' patterns inherit the role. Groove generation supports bass only.",
        "channel" => "channel <1..16>\nSets the user-facing MIDI channel for pattern playback.",
        "ch" => "ch <1..16>\nSets the user-facing MIDI channel for pattern playback. Short form of 'channel'.",
        "play" => "play\nEnables pattern note output. A running internal or external transport is also required to hear notes.",
        "mute" => "mute\nDisables pattern note output and releases active notes without stopping the clock.",
        "pattern" => "pattern\nShows candidate, playback state, tonal context, and note grid: X anchor, x note, g ghost, . rest.",
        "library" => "library\nLists saved patterns and their load numbers.",
        "save" => "save [name]\nSaves the accepted pattern as JSON. An optional name renames it before saving.",
        "load" => "load [name|#number]\nWithout an argument, lists saved patterns. With a name or #number, loads that pattern as a candidate. Names may contain spaces. Use keep to make it the safe point.",
        "note" => "note <channel> <note> <velocity> [milliseconds]\nSends a one-shot MIDI note. Default duration: 250 ms. Values are MIDI 1.0 ranges.",
        "on" => "on <channel> <note> <velocity>\nSends MIDI Note On. Remember to use 'off', or 'panic' if a note becomes stuck.",
        "off" => "off <channel> <note> [velocity]\nSends MIDI Note Off. Default release velocity: 0.",
        "cc" => "cc <channel> <controller> <value>\nSends a MIDI Control Change; controller and value must be 0..127.",
        "pc" => "pc <channel> <program>\nSends a MIDI Program Change; program is a raw MIDI value from 0..127.",
        "panic" => "panic\nMutes pattern playback and sends note cleanup for active/stuck notes.",
        "status" => "status\nShows ports, clock source/state/position, tempo, playback enabled state, and the latest playback error.",
        "quit" or "exit" => "quit | exit\nStops the application and disposes MIDI ports, clocks, and active notes safely.",
        _ => $"No help is available for '{command}'. Type 'help' to list commands."
    };
    Console.WriteLine(text);
}

static GrooveSelection ParseGrooveSelection(string value) => value.ToLowerInvariant() switch
{
    "auto" => GrooveSelection.Auto, "foundation" => GrooveSelection.Foundation, "offbeat" => GrooveSelection.Offbeat,
    "anticipation" => GrooveSelection.Anticipation, "long-short" => GrooveSelection.LongShort,
    "sparse-answer" => GrooveSelection.SparseAnswer, "broken" => GrooveSelection.Broken,
    _ => throw new ArgumentException("Unknown groove category.")
};
static string GrooveText(GrooveSelection value) => value switch
{
    GrooveSelection.LongShort => "long-short", GrooveSelection.SparseAnswer => "sparse-answer", _ => value.ToString().ToLowerInvariant()
};
static MotifShape ParseMotifShape(string value) => value.ToLowerInvariant() switch
{
    "auto" => MotifShape.Auto, "pedal" => MotifShape.Pedal, "root-fifth" => MotifShape.RootFifth,
    "walking" => MotifShape.Walking, "call-response" => MotifShape.CallResponse, "arch" => MotifShape.Arch,
    "pickup" => MotifShape.Pickup, "riff" => MotifShape.Riff, _ => throw new ArgumentException("Unknown motif shape.")
};
static string MotifText(MotifShape value) => value switch
{
    MotifShape.RootFifth => "root-fifth", MotifShape.CallResponse => "call-response", _ => value.ToString().ToLowerInvariant()
};
static string ShapeHelpText() => """
shape <choice>
Selects the motif's contour and rhythm. It takes effect on the next `new` in motif mode.

  auto           choose one of the shapes deterministically from the seed
  pedal          repeat a central note as a steady anchor
  root-fifth     alternate around the root and fifth
  walking        move stepwise through nearby scale tones
  call-response  alternate a short call with a related answer
  arch           rise through the scale, then return
  pickup         emphasize notes that lead into the next bar or loop
  riff           use a compact, syncopated repeating figure
""";
static string GeneratorControls(GeneratorMode mode) => mode switch
{
    GeneratorMode.Motif => "shape, activity, movement, variation",
    GeneratorMode.Phrase => "length, activity, rhythm, movement, variation, turnaround",
    GeneratorMode.Groove => "groove, similarity, activity, movement, variation, turnaround (bass only)",
    GeneratorMode.Euclidean => "key, palette, and role; other settings are fixed",
    GeneratorMode.Euclidean2 => "key, palette, and role; experimental four-bar development is fixed",
    _ => throw new ArgumentOutOfRangeException(nameof(mode))
};
enum GeneratorMode { Euclidean, Euclidean2, Phrase, Groove, Motif }
