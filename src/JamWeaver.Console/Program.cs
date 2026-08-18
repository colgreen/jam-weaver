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

Console.WriteLine("MIDI clock and message prototype");
Console.WriteLine("Type 'help' for commands.");

using var output = new SafeMidiOutput();
var transport = new TransportEngine();
await using var internalClock = new InternalMidiClock(output, transport);
await using var watchdog = new ExternalClockWatchdog(transport);
using var player = new PatternPlayer(output, transport);
var session = new CandidateSession(player);
var melodicGenerator = new MelodicPatternGenerator();
var phraseGenerator = new MelodicPhraseGenerator();
var grooveGenerator = new MelodicGrooveGenerator();
var motifGenerator = new MusicalMotifGenerator();
var mutator = new PatternMutator();
var phraseMutator = new PhrasePatternMutator();
var history = new CandidateHistory();
var generatorMode = GeneratorMode.Motif;
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

PrintDevices();
try
{
    while (true)
    {
        Console.Write("midi> ");
        var line = Console.ReadLine();
        if (line is null) break;
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) continue;

        try
        {
            switch (parts[0].ToLowerInvariant())
            {
                case "devices": PrintDevices(); break;
                case "out":
                    player.Mute();
                    output.ReplacePort(DryWetMidiPortCatalog.OpenOutput(I(parts, 1)));
                    Console.WriteLine($"Output: {output.PortName}");
                    break;
                case "in":
                    input?.Dispose();
                    input = new DryWetMidiInput(DryWetMidiPortCatalog.OpenInput(I(parts, 1)), externalClock);
                    Console.WriteLine($"Input: {input.Name}");
                    break;
                case "source":
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
                case "bpm": internalClock.Bpm = D(parts, 1); Console.WriteLine($"Tempo: {internalClock.Bpm:0.##} BPM"); break;
                case "start": if (transport.Source == ClockSource.External) Console.WriteLine("Waiting for external MIDI Start."); else internalClock.Start(); break;
                case "continue": if (transport.Source == ClockSource.External) Console.WriteLine("Waiting for external MIDI Continue."); else internalClock.Continue(); break;
                case "stop": if (transport.Source == ClockSource.External) transport.Process(ClockSource.External, RealtimeMessage.Stop); else internalClock.Stop(); break;
                case "generate":
                    var generateSeed = parts.Length > 1 ? U(parts, 1) : seedSource.NextULong();
                    SelectCandidate(session, history, Generate(generatorMode, generateSeed, session.Candidate,
                        phraseLength, phraseActivity, phraseRhythm, phraseMovement, phraseVariation, phraseTurnaround,
                        grooveSelection, grooveSimilarity, motifShape, melodicGenerator, phraseGenerator, grooveGenerator, motifGenerator));
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
                case "mutate":
                    var parent = session.Candidate ?? throw new InvalidOperationException("Generate a candidate pattern first.");
                    if (parts.Length > 1 && TryMutationTarget(parts[1], out var mutationTarget))
                    {
                        var targetedSeed = parts.Length > 2 ? U(parts, 2) : seedSource.NextULong();
                        var targetedStrength = parts.Length > 3 ? D(parts, 3) : .3;
                        SelectCandidate(session, history, phraseMutator.Mutate(parent,
                            new PhraseMutationSettings(mutationTarget, new NormalizedAmount(targetedStrength), targetedSeed)));
                        Console.WriteLine($"Mutated {mutationTarget.ToString().ToLowerInvariant()} with seed {targetedSeed} at strength {targetedStrength:0.##}.");
                    }
                    else
                    {
                        var mutationSeed = parts.Length > 1 ? U(parts, 1) : seedSource.NextULong();
                        var strength = parts.Length > 2 ? D(parts, 2) : .3;
                        SelectCandidate(session, history, mutator.Mutate(parent, new MutationSettings(new NormalizedAmount(strength), mutationSeed)));
                        Console.WriteLine($"Legacy mutation with seed {mutationSeed} at strength {strength:0.##}.");
                    }
                    break;
                case "previous": session.SetCandidate(history.Previous()); Console.WriteLine($"Candidate history {history.Position}/{history.Count}."); break;
                case "next": session.SetCandidate(history.Next()); Console.WriteLine($"Candidate history {history.Position}/{history.Count}."); break;
                case "generator":
                    generatorMode = S(parts, 1).ToLowerInvariant() switch { "motif" => GeneratorMode.Motif, "phrase" => GeneratorMode.Phrase, "simple" => GeneratorMode.Simple, "groove" => GeneratorMode.Groove, _ => throw new ArgumentException("Generator must be motif, phrase, groove, or simple.") };
                    Console.WriteLine($"Generator: {generatorMode.ToString().ToLowerInvariant()}");
                    break;
                case "groove": grooveSelection = ParseGrooveSelection(S(parts, 1)); break;
                case "shape": motifShape = ParseMotifShape(S(parts, 1)); break;
                case "similarity": grooveSimilarity = EnumValue<GrooveSimilarity>(parts, 1); break;
                case "length": phraseLength = I(parts, 1) switch { 1 => PhraseLength.OneBar, 2 => PhraseLength.TwoBars, 4 => PhraseLength.FourBars, _ => throw new ArgumentException("Length must be 1, 2, or 4 bars.") }; break;
                case "activity": phraseActivity = EnumValue<PhraseActivity>(parts, 1); break;
                case "rhythm": phraseRhythm = EnumValue<PhraseRhythm>(parts, 1); break;
                case "movement": phraseMovement = EnumValue<PhraseLevel>(parts, 1); break;
                case "variation": phraseVariation = EnumValue<PhraseLevel>(parts, 1); break;
                case "turnaround": phraseTurnaround = EnumValue<PhraseTurnaround>(parts, 1); break;
                case "settings": Console.WriteLine($"Generator={generatorMode.ToString().ToLowerInvariant()}, Shape={MotifText(motifShape)}, Length={(int)phraseLength}, Activity={phraseActivity.ToString().ToLowerInvariant()}, Rhythm={phraseRhythm.ToString().ToLowerInvariant()}, Groove={GrooveText(grooveSelection)}, Similarity={grooveSimilarity.ToString().ToLowerInvariant()}, Movement={phraseMovement.ToString().ToLowerInvariant()}, Variation={phraseVariation.ToString().ToLowerInvariant()}, Turnaround={phraseTurnaround.ToString().ToLowerInvariant()}"); break;
                case "accept": session.Accept(); Console.WriteLine("Candidate accepted."); break;
                case "reject": session.Reject(); Console.WriteLine("Returning to accepted pattern."); break;
                case "undo": session.Undo(); Console.WriteLine("Restoring previous accepted pattern."); break;
                case "root":
                    var rootPattern = Candidate(session);
                    var direction = S(parts, 1).ToLowerInvariant() switch
                    {
                        "up" => 1,
                        "down" => -1,
                        _ => throw new ArgumentException("Root direction must be up or down.")
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
                    PrintPattern(session, player);
                    break;
                case "channel": player.Channel = C(parts, 1); Console.WriteLine($"Pattern channel: {player.Channel.Number}"); break;
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
                    Console.WriteLine($"Saved {saved.FileName} in {patternLibrary.RootDirectory}");
                    break;
                case "recall":
                    var entries = await patternLibrary.ListAsync();
                    var recallIndex = I(parts, 1) - 1;
                    if ((uint)recallIndex >= (uint)entries.Length) throw new ArgumentOutOfRangeException(nameof(recallIndex), "Recall number is not in the library menu.");
                    var recalled = await patternLibrary.LoadAsync(entries[recallIndex]);
                    SelectCandidate(session, history, recalled);
                    Console.WriteLine($"Recalled '{recalled.Name}' ({(player.CurrentPattern?.Id == recalled.Id ? "audible" : "pending for next bar")}).");
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
    Console.WriteLine("Outputs:");
    foreach (var item in DryWetMidiPortCatalog.OutputNames().Select((name, index) => (name, index))) Console.WriteLine($"  {item.index}: {item.name}");
    Console.WriteLine("Inputs:");
    foreach (var item in DryWetMidiPortCatalog.InputNames().Select((name, index) => (name, index))) Console.WriteLine($"  {item.index}: {item.name}");
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
static Pattern Candidate(CandidateSession session) =>
    session.Candidate ?? throw new InvalidOperationException("Generate a candidate pattern first.");
static void SelectCandidate(CandidateSession session, CandidateHistory history, Pattern pattern)
{
    history.Add(pattern);
    session.SetCandidate(pattern);
}
static Pattern Generate(GeneratorMode mode, ulong seed, Pattern? current, PhraseLength length,
    PhraseActivity activity, PhraseRhythm rhythm, PhraseLevel movement, PhraseLevel variation,
    PhraseTurnaround turnaround, GrooveSelection grooveSelection, GrooveSimilarity similarity,
    MotifShape motifShape, MelodicPatternGenerator simple, MelodicPhraseGenerator phrase,
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
        _ => simple.Generate(new MelodicGeneratorSettings(name, 16, PatternTiming.SixteenthNotes,
            context, role, new NormalizedAmount(.4), new NormalizedAmount(.35),
            new NormalizedAmount(.65), new NormalizedAmount(.8), new NormalizedAmount(.15), seed))
    };
}
static void PrintPattern(CandidateSession session, PatternPlayer player)
{
    var candidate = session.Candidate;
    if (candidate is null) { Console.WriteLine("No pattern. Use 'generate'."); return; }
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
static string PitchClass(int value) => new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" }[value];
static TonalContext DefaultTonalContext() => new(new RootPitchClass(9), PitchPalette.MinorPentatonic);
static void Help(string? command)
{
    if (command is null)
    {
        Console.WriteLine("""
devices | out <index> | in <index> | source internal|external | bpm <20..300>
start | continue | stop | note <ch> <note> <vel> [ms] | on <ch> <note> <vel>
off <ch> <note> [vel] | cc <ch> <controller> <value> | pc <ch> <program>
generator motif|phrase|groove|simple | length 1|2|4 | activity sparse|medium|busy
shape auto|pedal|root-fifth|walking|call-response|arch|pickup|riff
groove auto|foundation|offbeat|anticipation|long-short|sparse-answer|broken
similarity close|related|contrast
rhythm steady|syncopated|broken | movement low|medium|high
variation low|medium|high | turnaround none|subtle|strong | settings
generate [seed] | compare [seed] | previous | next
mutate rhythm|notes|expression|turnaround|all [seed] [strength-0..1]
mutate [seed] [strength-0..1] | accept | reject | undo
root up|down | palette | role bass|middle|high | channel <1..16>
play | mute | pattern | panic | status | quit
library | save [name] | recall <number>
help <command> for syntax, options, defaults, and behavior
""");
        return;
    }

    var text = command.ToLowerInvariant() switch
    {
        "help" => "help [command]\nWithout an argument, lists all commands. With a command, explains its syntax and options.",
        "devices" => "devices\nLists available MIDI output and input ports with the indexes used by 'out' and 'in'.",
        "out" => "out <index>\nOpens the numbered MIDI output shown by 'devices'. Mutes pattern playback while changing ports.",
        "in" => "in <index>\nOpens the numbered MIDI input shown by 'devices' for external clock and transport messages.",
        "source" => "source internal|external\nSelects the transport clock. External waits for MIDI Start/Continue and Clock; internal uses 'bpm' and 'start'.",
        "bpm" => "bpm <20..300>\nSets the internal-clock tempo. It has no effect while the external clock source is selected.",
        "start" => "start\nStarts internal clock and transport. With external source selected, waits for MIDI Start from the input device.",
        "continue" => "continue\nContinues internal transport without resetting its position. External mode waits for MIDI Continue.",
        "stop" => "stop\nStops transport and releases active pattern notes. External clock disappearing also stops playback.",
        "generator" => "generator motif|phrase|groove|simple\nSelects how 'generate' creates candidates. Default: motif. Motif and groove currently require role bass.",
        "shape" => "shape auto|pedal|root-fifth|walking|call-response|arch|pickup|riff\nSelects the motif archetype. Default: auto. Takes effect on the next 'generate'.",
        "length" => "length 1|2|4\nSets phrase-generator length in bars. Default: 4. Motif and groove always produce four bars.",
        "activity" => "activity sparse|medium|busy\nControls rhythmic note density. Default: medium. Takes effect on the next 'generate'.",
        "rhythm" => "rhythm steady|syncopated|broken\nSelects the phrase generator's rhythmic character. Default: syncopated.",
        "groove" => "groove auto|foundation|offbeat|anticipation|long-short|sparse-answer|broken\nSelects a groove-template family. Default: auto. Used only by generator groove.",
        "similarity" => "similarity close|related|contrast\nControls how different later groove bars are from A. Default: related. Used only by generator groove.",
        "movement" => "movement low|medium|high\nControls melodic contour width. Default: medium. Low favors repeats and small moves.",
        "variation" => "variation low|medium|high\nControls phrase development. Default: medium. In motif mode, low repeats A exactly as A-prime.",
        "turnaround" => "turnaround none|subtle|strong\nControls the final phrase bar. Default: subtle. Used by phrase and groove generators.",
        "settings" => "settings\nPrints all current generator controls. Some controls apply only to particular generator modes.",
        "generate" => "generate [seed]\nCreates and auditions a candidate using current settings. Omit seed for fresh material; supply it to reproduce a pattern.",
        "compare" => "compare [seed]\nPrepares matched phrase/groove bass candidates, or toggles an existing comparison. Changes remain next-bar quantized.",
        "previous" => "previous\nSelects the previous candidate in the eight-item audition history. Does not change the accepted pattern.",
        "next" => "next\nSelects the next candidate in audition history. Does not change the accepted pattern.",
        "mutate" => "mutate rhythm|notes|expression|turnaround|all [seed] [strength-0..1]\nCreates a targeted mutation. Legacy form: mutate [seed] [strength]. Default strength: 0.3.",
        "accept" => "accept\nMakes the current candidate the accepted pattern. This is the pattern restored by reject and eligible for save.",
        "reject" => "reject\nDiscards the current audition and returns to the last accepted pattern.",
        "undo" => "undo\nRestores the previously accepted pattern.",
        "root" => "root up|down\nMoves the candidate's tonal root by one semitone for by-ear key matching. Creates a new audition candidate.",
        "palette" => "palette\nToggles the candidate between major and minor pentatonic. Creates a new audition candidate.",
        "role" => "role bass|middle|high\nChanges the candidate's register. Motif and groove generation currently support bass only.",
        "channel" => "channel <1..16>\nSets the user-facing MIDI channel for pattern playback.",
        "play" => "play\nEnables pattern note output. A running internal or external transport is also required to hear notes.",
        "mute" => "mute\nDisables pattern note output and releases active notes without stopping the clock.",
        "pattern" => "pattern\nShows candidate, playback state, tonal context, and note grid: X anchor, x note, g ghost, . rest.",
        "library" => "library\nLists saved accepted patterns and their recall numbers.",
        "save" => "save [name]\nSaves the accepted pattern as JSON. An optional name renames it before saving.",
        "recall" => "recall <number>\nLoads a candidate using the number shown by 'library'. Use accept if you want it to become accepted.",
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
enum GeneratorMode { Simple, Phrase, Groove, Motif }
