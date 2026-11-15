using Redzen.Random;
using static JamWeaver.ConsoleApp.ConsoleArgumentParser;

namespace JamWeaver.ConsoleApp;

/// <summary>Runs JamWeaver's interactive command loop and coordinates command effects.</summary>
public sealed class JamWeaverConsole
{
    private readonly TextReader reader;
    private readonly ConsoleDisplay display;
    private readonly SafeMidiOutput output;
    private readonly TransportEngine transport;
    private readonly InternalMidiClock internalClock;
    private readonly PatternPlayer player;
    private readonly CandidateSession session;
    private readonly CandidateGenerator candidateGenerator;
    private readonly GenerationControls generationControls;
    private readonly CandidateHistory history;
    private readonly PatternLibrary patternLibrary;
    private readonly CandidateComparison comparison;
    private readonly IRandomSource seedSource;
    private readonly ExternalMidiClockInput externalClock;
    private readonly IMidiPortCatalog portCatalog;
    private readonly MidiInput input;

    public JamWeaverConsole(TextReader reader, ConsoleDisplay display, SafeMidiOutput output,
        TransportEngine transport, InternalMidiClock internalClock, PatternPlayer player,
        CandidateSession session, CandidateGenerator candidateGenerator, GenerationControls generationControls,
        CandidateHistory history,
        PatternLibrary patternLibrary, StructuredPhraseGenerator phraseGenerator,
        GrooveVocabularyGenerator grooveGenerator, IRandomSource seedSource, ExternalMidiClockInput externalClock,
        IMidiPortCatalog portCatalog)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.display = display ?? throw new ArgumentNullException(nameof(display));
        this.output = output ?? throw new ArgumentNullException(nameof(output));
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.internalClock = internalClock ?? throw new ArgumentNullException(nameof(internalClock));
        this.player = player ?? throw new ArgumentNullException(nameof(player));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.candidateGenerator = candidateGenerator ?? throw new ArgumentNullException(nameof(candidateGenerator));
        this.generationControls = generationControls ?? throw new ArgumentNullException(nameof(generationControls));
        this.history = history ?? throw new ArgumentNullException(nameof(history));
        this.patternLibrary = patternLibrary ?? throw new ArgumentNullException(nameof(patternLibrary));
        ArgumentNullException.ThrowIfNull(phraseGenerator);
        ArgumentNullException.ThrowIfNull(grooveGenerator);
        this.seedSource = seedSource ?? throw new ArgumentNullException(nameof(seedSource));
        this.externalClock = externalClock ?? throw new ArgumentNullException(nameof(externalClock));
        this.portCatalog = portCatalog ?? throw new ArgumentNullException(nameof(portCatalog));
        comparison = new CandidateComparison(phraseGenerator, grooveGenerator, this.seedSource,
            this.generationControls, this.session, this.history, this.display);
        input = new MidiInput(this.externalClock);
    }

    public async Task RunAsync()
    {
        externalClock.MessageReceived += ProcessExternalMessage;
        try
        {
            WriteSetup();
            while (true)
            {
                display.WritePrompt(session, player, transport, generationControls.Mode);
                var line = await reader.ReadLineAsync();
                if (line is null) break;
                try
                {
                    var command = ConsoleCommand.Parse(line);
                    if (command is null) continue;
                    var parts = command.Parts;
                    switch (command.Name)
                    {
                        case "setup": WriteSetup(); break;
                        case "out":
                            if (parts.Length == 1) WriteOutputs();
                            else await SelectOutputAsync(ParseInteger(parts, 1));
                            break;
                        case "in":
                            if (parts.Length == 1) WriteInputs();
                            else SelectInput(ParseInteger(parts, 1));
                            break;
                        case "source":
                            if (parts.Length == 1)
                            {
                                display.WriteLine($"Clock source: {transport.Source.ToString().ToLowerInvariant()}");
                                display.WriteLine("Select with: source internal|external");
                                break;
                            }
                            await SelectSourceAsync(Argument(parts, 1));
                            break;
                        case "bpm":
                            if (parts.Length == 1)
                            {
                                display.WriteLine($"Tempo: {internalClock.Bpm:0.##} BPM");
                                display.WriteLine("Set with: bpm <20..300>");
                                break;
                            }
                            internalClock.Bpm = ParseNumber(parts, 1);
                            display.WriteLine($"Tempo: {internalClock.Bpm:0.##} BPM");
                            break;
                        case "start": if (transport.Source == ClockSource.External) display.WriteLine("Waiting for external MIDI Start."); else await internalClock.StartAsync(); break;
                        case "continue": if (transport.Source == ClockSource.External) display.WriteLine("Waiting for external MIDI Continue."); else await internalClock.ContinueAsync(); break;
                        case "stop": if (transport.Source == ClockSource.External) transport.Process(ClockSource.External, RealtimeMessage.Stop); else await internalClock.StopAsync(); break;
                        case "go":
                            player.Play();
                            if (transport.Source == ClockSource.External)
                                display.WriteLine("Pattern playback enabled; waiting for external MIDI Start or Continue.");
                            else if (!internalClock.IsRunning)
                                await internalClock.StartAsync();
                            else
                                display.WriteLine("Pattern playback enabled; transport is already running.");
                            break;
                        case "new":
                            var generateSeed = parts.Length > 1 ? ParseSeed(parts, 1) : seedSource.NextULong();
                            SelectCandidate(session, history, candidateGenerator.Generate(generateSeed, session.Candidate,
                                generationControls));
                            display.WriteLine($"Generated candidate with seed {generateSeed}.");
                            break;
                        case "compare":
                            CompareCandidates(parts);
                            break;
                        case "vary":
                            VaryCandidate(parts);
                            break;
                        case "back":
                            PreviousCandidate();
                            break;
                        case "forward":
                            NextCandidate();
                            break;
                        case "generator":
                            if (parts.Length == 1)
                            {
                                display.WriteLine($"Generator: {ConsoleDisplay.GeneratorText(generationControls.Mode)}");
                                display.WriteLine(ConsoleDisplay.GeneratorDescription(generationControls.Mode));
                                display.WriteLine("Choices: euclidean, turnaround, motif, phrase, groove");
                                display.WriteLine($"Controls: {ConsoleDisplay.GeneratorControls(generationControls.Mode)}");
                                display.WriteLine("Set with: generator <choice>");
                                display.WriteLine("Explain choices with: generator help");
                                break;
                            }
                            if (Argument(parts, 1).ToLowerInvariant() is "help" or "?")
                            {
                                display.WriteHelp("generator");
                                break;
                            }
                            generationControls.Mode = ParseGeneratorMode(Argument(parts, 1));
                            display.WriteLine($"Generator: {ConsoleDisplay.GeneratorText(generationControls.Mode)}");
                            display.WriteLine(ConsoleDisplay.GeneratorDescription(generationControls.Mode));
                            display.WriteLine($"Controls: {ConsoleDisplay.GeneratorControls(generationControls.Mode)}");
                            break;
                        case "groove": generationControls.Groove = ParseGrooveSelection(Argument(parts, 1)); break;
                        case "shape":
                            if (parts.Length == 1)
                            {
                                display.WriteLine($"Motif shape: {ConsoleDisplay.MotifText(generationControls.MotifShape)}{(generationControls.Mode == GeneratorMode.Motif ? string.Empty : $" (inactive while generator is {ConsoleDisplay.GeneratorText(generationControls.Mode)})")}");
                                display.WriteLine("Choices: auto, pedal, root-fifth, walking, call-response, arch, pickup, riff");
                                display.WriteLine("Set with: shape <choice>");
                                display.WriteLine("Explain choices with: shape help");
                                break;
                            }
                            if (Argument(parts, 1).ToLowerInvariant() is "help" or "?")
                            {
                                display.WriteHelp("shape");
                                break;
                            }
                            generationControls.MotifShape = ParseMotifShape(Argument(parts, 1));
                            display.WriteLine($"Motif shape: {ConsoleDisplay.MotifText(generationControls.MotifShape)}{(generationControls.Mode == GeneratorMode.Motif ? string.Empty : $" (saved for motif; inactive while generator is {ConsoleDisplay.GeneratorText(generationControls.Mode)})")}");
                            break;
                        case "similarity": generationControls.Similarity = ParseEnumValue<GrooveSimilarity>(parts, 1); break;
                        case "length": generationControls.PhraseLength = ParsePhraseLength(parts, 1); break;
                        case "activity": generationControls.Activity = ParseEnumValue<PhraseActivity>(parts, 1); break;
                        case "rhythm": generationControls.Rhythm = ParseEnumValue<PhraseRhythm>(parts, 1); break;
                        case "movement": generationControls.Movement = ParseEnumValue<PhraseLevel>(parts, 1); break;
                        case "variation": generationControls.Variation = ParseEnumValue<PhraseLevel>(parts, 1); break;
                        case "turnaround": generationControls.Turnaround = ParseEnumValue<PhraseTurnaround>(parts, 1); break;
                        case "settings": display.WriteLine($"Generator={ConsoleDisplay.GeneratorText(generationControls.Mode)}, Shape={ConsoleDisplay.MotifText(generationControls.MotifShape)}, Length={(int)generationControls.PhraseLength}, Activity={generationControls.Activity.ToString().ToLowerInvariant()}, Rhythm={generationControls.Rhythm.ToString().ToLowerInvariant()}, Groove={ConsoleDisplay.GrooveText(generationControls.Groove)}, Similarity={generationControls.Similarity.ToString().ToLowerInvariant()}, Movement={generationControls.Movement.ToString().ToLowerInvariant()}, Variation={generationControls.Variation.ToString().ToLowerInvariant()}, Turnaround={generationControls.Turnaround.ToString().ToLowerInvariant()}"); break;
                        case "keep":
                            KeepCandidate();
                            break;
                        case "revert":
                            RevertCandidate();
                            break;
                        case "key":
                            var rootPattern = Candidate(session);
                            var direction = ParseKeyDirection(Argument(parts, 1));
                            SelectCandidate(session, history, PatternTransformations.TransposeRoot(rootPattern, direction));
                            display.WritePattern(session, player);
                            break;
                        case "palette": SelectCandidate(session, history, PatternTransformations.TogglePalette(Candidate(session))); display.WritePattern(session, player); break;
                        case "role":
                            var selectedRole = ParseRole(Argument(parts, 1));
                            SelectCandidate(session, history, PatternTransformations.ChangeRole(Candidate(session), selectedRole));
                            display.WriteLine($"Register: {selectedRole.ToString().ToLowerInvariant()} (key unchanged).");
                            display.WritePattern(session, player);
                            break;
                        case "ch":
                        case "channel":
                            if (parts.Length == 1)
                            {
                                display.WriteLine($"Pattern channel: {player.Channel.Number}");
                                display.WriteLine("Set with: ch <1..16> (or channel <1..16>)");
                                break;
                            }
                            player.Channel = ParseChannel(parts, 1);
                            display.WriteLine($"Pattern channel: {player.Channel.Number}");
                            break;
                        case "play": player.Play(); display.WriteLine("Pattern playback enabled."); break;
                        case "mute": player.Mute(); display.WriteLine("Pattern playback muted."); break;
                        case "pattern": display.WritePattern(session, player); break;
                        case "library":
                            display.WriteLibrary(await patternLibrary.ListAsync());
                            display.WriteLine($"Library: {patternLibrary.RootDirectory}");
                            break;
                        case "save":
                            await SaveAcceptedAsync(command.ArgumentText);
                            break;
                        case "load":
                            await LoadCandidateAsync(command.ArgumentText);
                            break;
                        case "note":
                            await output.SendNoteAsync(ParseChannel(parts, 1), ParseMidiValue(parts, 2),
                                ParseMidiValue(parts, 3), TimeSpan.FromMilliseconds(parts.Length > 4 ? ParseInteger(parts, 4) : 250));
                            break;
                        case "on":
                            output.NoteOn(ParseChannel(parts, 1), ParseMidiValue(parts, 2), ParseMidiValue(parts, 3));
                            break;
                        case "off":
                            output.NoteOff(ParseChannel(parts, 1), ParseMidiValue(parts, 2),
                                new MidiValue(parts.Length > 3 ? ParseInteger(parts, 3) : 0));
                            break;
                        case "cc":
                            output.ControlChange(ParseChannel(parts, 1), ParseMidiValue(parts, 2), ParseMidiValue(parts, 3));
                            break;
                        case "pc":
                            output.ProgramChange(ParseChannel(parts, 1), ParseMidiValue(parts, 2));
                            break;
                        case "panic": Panic(); break;
                        case "status": display.WriteStatus(output, input.Name, transport, internalClock, player); break;
                        case "help": display.WriteHelp(parts.Length > 1 ? parts[1] : null); break;
                        case "quit" or "exit": return;
                        default: display.WriteLine("Unknown command. Type 'help'."); break;
                    }
                }
                catch (Exception ex) { display.WriteLine($"Error: {ex.Message}"); }
            }
        }
        finally
        {
            externalClock.MessageReceived -= ProcessExternalMessage;
            input.Dispose();
        }
    }

    private void WriteSetup()
    {
        display.WriteConfiguration(output, input.Name, transport, internalClock, player);
        WriteOutputs();
        WriteInputs();
        display.WriteSetupGuidance();
    }

    private void WriteOutputs()
    {
        var names = DiscoverPortNames(portCatalog.OutputNames, "outputs");
        if (names is not null) display.WriteOutputs(names);
    }

    private void WriteInputs()
    {
        var names = DiscoverPortNames(portCatalog.InputNames, "inputs");
        if (names is not null) display.WriteInputs(names);
    }

    private IReadOnlyList<string>? DiscoverPortNames(Func<IReadOnlyList<string>> discover, string direction)
    {
        try { return discover(); }
        catch (Exception ex)
        {
            display.WriteLine($"Error: Could not discover MIDI {direction}: {ex.Message}");
            return null;
        }
    }

    private async Task SelectOutputAsync(int index)
    {
        player.Mute();
        await internalClock.StopOwnedTransportAsync();
        output.ReplacePort(portCatalog.OpenOutput(index));
        display.WriteLine($"Output: {output.PortName}");
        display.WriteLine("Next: new, then go.");
    }

    private async Task SelectSourceAsync(string value)
    {
        var source = ParseClockSource(value);
        await internalClock.StopOwnedTransportAsync();
        transport.SelectSource(source);
        display.WriteLine($"Clock source: {source.ToString().ToLowerInvariant()}");
    }

    private async Task SaveAcceptedAsync(string requestedName)
    {
        var accepted = session.Accepted ?? throw new InvalidOperationException("There is no accepted pattern to save.");
        var patternToSave = requestedName.Length == 0 ? accepted : accepted.Rename(new PatternName(requestedName));
        var saved = await patternLibrary.SaveAsync(patternToSave);
        if (patternToSave.Name != accepted.Name) session.RenameAccepted(patternToSave);
        display.WriteLine($"Saved '{saved.Name}'. Load it with: load {saved.Name}");
    }

    private async Task LoadCandidateAsync(string query)
    {
        var entries = await patternLibrary.ListAsync();
        if (query.Length == 0)
        {
            display.WriteLibrary(entries);
            if (entries.Length > 0) display.WriteLine("Load with: load <name> or load #<number>");
            return;
        }
        var entry = ResolveLibraryEntry(entries, query);
        var loaded = await patternLibrary.LoadAsync(entry);
        SelectCandidate(session, history, loaded);
        display.WriteLine($"Loaded '{loaded.Name}' as a candidate ({(player.CurrentPattern?.Id == loaded.Id ? "audible" : "pending for next bar")}).");
    }

    private void CompareCandidates(string[] parts)
    {
        comparison.Compare(parts.Length > 1 ? ParseSeed(parts, 1) : null);
    }

    private void PreviousCandidate()
    {
        session.SetCandidate(history.Previous());
        display.WriteLine($"Candidate history {history.Position}/{history.Count}.");
    }

    private void NextCandidate()
    {
        session.SetCandidate(history.Next());
        display.WriteLine($"Candidate history {history.Position}/{history.Count}.");
    }

    private void KeepCandidate()
    {
        session.Accept();
        display.WriteLine("Candidate kept as the safe point.");
    }

    private void RevertCandidate()
    {
        session.Reject();
        display.WriteLine("Returning to the safe point.");
    }

    private void VaryCandidate(string[] parts)
    {
        var parent = Candidate(session);
        var settings = VariationArguments.Parse(parts, seedSource);
        SelectCandidate(session, history, candidateGenerator.Vary(parent, settings));
        if (settings.Target is { } target)
            display.WriteLine($"Varied {target.ToString().ToLowerInvariant()} with seed {settings.Seed} at strength {settings.Strength.Value:0.##}.");
        else
            display.WriteLine($"Varied candidate with seed {settings.Seed} at strength {settings.Strength.Value:0.##}.");
    }

    private void SelectInput(int index)
    {
        input.ReplacePort(portCatalog.OpenInput(index));
        display.WriteLine($"Input: {input.Name}");
    }

    private void ProcessExternalMessage(object? sender, RealtimeMessage message) =>
        transport.Process(ClockSource.External, message);

    private void Panic()
    {
        List<Exception> errors = [];
        try { player.Mute(); }
        catch (Exception ex) { errors.Add(ex); }
        try { output.Panic(); }
        catch (Exception ex) { errors.Add(ex); }
        if (errors.Count > 0) throw new AggregateException("MIDI panic encountered cleanup failures.", errors);
    }

    private static Pattern Candidate(CandidateSession session) =>
        session.Candidate ?? throw new InvalidOperationException("Create a candidate with 'new' first.");
    private static void SelectCandidate(CandidateSession session, CandidateHistory history, Pattern pattern)
    {
        history.Add(pattern);
        session.SetCandidate(pattern);
    }
    private static PatternLibraryEntry ResolveLibraryEntry(IReadOnlyList<PatternLibraryEntry> entries, string query)
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
        if (TryParseLibraryNumber(query, out var number))
            return EntryAt(entries, number);
        throw new ArgumentException($"No saved pattern is named '{query}'. Type 'library' to list saved patterns.");
    }
    private static PatternLibraryEntry EntryAt(IReadOnlyList<PatternLibraryEntry> entries, int number)
    {
        var index = number - 1;
        if ((uint)index >= (uint)entries.Count) throw new ArgumentOutOfRangeException(nameof(number), "Load number is not in the library list.");
        return entries[index];
    }
}
