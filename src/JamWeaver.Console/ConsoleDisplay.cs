using System.Globalization;
using JamWeaver.ConsoleApp.DryWetMidi;
using JamWeaver.Core.Generation;
using JamWeaver.Core.Generation.Groove;
using JamWeaver.Core.Generation.Motif;
using JamWeaver.Core.Midi;
using JamWeaver.Core.Performance;
using JamWeaver.Core.Persistence;
using JamWeaver.Core.Sequencer;
using JamWeaver.Core.Transport;

namespace JamWeaver.ConsoleApp;

/// <summary>Renders JamWeaver's terminal presentation without changing application state.</summary>
public sealed class ConsoleDisplay
{
    private readonly TextWriter writer;

    public ConsoleDisplay(TextWriter writer) => this.writer = writer ?? throw new ArgumentNullException(nameof(writer));

    public void WriteLine(string? text = null) => writer.WriteLine(text);
    public void Write(string text) => writer.Write(text);

    public void WriteStartup()
    {
        writer.WriteLine("JamWeaver");
        writer.WriteLine("Type 'setup' to choose MIDI devices or 'help' for the live controls.");
    }

    public void WriteOutputs()
    {
        writer.WriteLine("Outputs:");
        foreach (var item in DryWetMidiPortCatalog.OutputNames().Select((name, index) => (name, index)))
            writer.WriteLine($"  {item.index}: {item.name}");
        writer.WriteLine("Select one with: out <number>");
    }

    public void WriteInputs()
    {
        writer.WriteLine("Inputs:");
        foreach (var item in DryWetMidiPortCatalog.InputNames().Select((name, index) => (name, index)))
            writer.WriteLine($"  {item.index}: {item.name}");
        writer.WriteLine("Select one with: in <number>");
    }

    public void WriteSetup(SafeMidiOutput output, string? inputName, TransportEngine transport,
        InternalMidiClock internalClock, PatternPlayer player)
    {
        writer.WriteLine($"Output: {output.PortName ?? "not selected"}");
        writer.WriteLine($"Input:  {inputName ?? "not selected"}");
        writer.WriteLine($"Clock:  {transport.Source.ToString().ToLowerInvariant()}, {internalClock.Bpm:0.##} BPM");
        writer.WriteLine($"Channel: {player.Channel.Number}");
        writer.WriteLine();
        WriteOutputs();
        WriteInputs();
        writer.WriteLine("Clock: source internal|external; set internal tempo with bpm <20..300>.");
        writer.WriteLine("Routing: ch <1..16> (or channel <1..16>).");
        writer.WriteLine("Next: out <number>, ch <number>, source internal, then new and go.");
    }

    public void WritePattern(CandidateSession session, PatternPlayer player)
    {
        var candidate = session.Candidate;
        if (candidate is null) { writer.WriteLine("No pattern. Use 'new'."); return; }
        var tonal = candidate.TonalContext is { } context
            ? $", Key={PitchClass(context.Root.Value)} {PaletteText(context.Palette)}, Role={candidate.Role!.Value.ToString().ToLowerInvariant()}"
            : string.Empty;
        var seed = candidate.Recipe is { } recipe ? recipe.Seed.ToString(CultureInfo.InvariantCulture) : "n/a";
        var audible = player.CurrentPattern?.Id == candidate.Id ? "audible" : player.PendingPattern?.Id == candidate.Id ? "pending" : "not selected";
        writer.WriteLine($"Candidate={candidate.Name} ({audible}), Accepted={(session.Accepted?.Id == candidate.Id ? "yes" : "no")}, Mode={candidate.Mode}, Seed={seed}, Channel={player.Channel.Number}, Output={(player.IsEnabled ? "play" : "mute")}{tonal}");
        WritePatternGrid(candidate);
    }

    public void WritePatternGrid(Pattern pattern)
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
            writer.WriteLine($"  {(barStart / 16) + 1}: {string.Join(' ', symbols)}{(exact ? string.Empty : "  (approx.)")}");
            if (pattern.Recipe?.GeneratorId == MelodicGrooveGenerator.GeneratorId
                && pattern.Recipe.Parameters.TryGetValue($"bar-{barStart / 16}-features", out var features))
                writer.WriteLine($"     {new[] { "A", "A'", "B", "T" }[barStart / 16]} {features.Text}");
        }
    }

    public void WriteLibrary(IReadOnlyList<PatternLibraryEntry> entries)
    {
        if (entries.Count == 0) { writer.WriteLine("No saved patterns."); return; }
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (!entry.IsValid) { writer.WriteLine($"  {index + 1}. [invalid] {entry.FileName}: {entry.Error}"); continue; }
            var tonal = entry.TonalContext is { } context
                ? $", {PitchClass(context.Root.Value)} {PaletteText(context.Palette)}, {entry.Role!.Value.ToString().ToLowerInvariant()}"
                : string.Empty;
            writer.WriteLine($"  {index + 1}. {entry.Name} [{entry.Mode!.Value.ToString().ToLowerInvariant()}{tonal}, seed {entry.Seed?.ToString(CultureInfo.InvariantCulture) ?? "n/a"}]");
        }
    }

    public void WritePrompt(CandidateSession session, PatternPlayer player, TransportEngine transport, GeneratorMode generator)
    {
        var transportText = transport.State.ToString().ToLowerInvariant();
        var outputText = player.IsEnabled ? "playing" : "muted";
        var candidate = session.Candidate;
        var patternText = candidate is null ? "no pattern"
            : player.PendingPattern?.Id == candidate.Id ? "pending"
            : session.Accepted?.Id == candidate.Id ? "safe" : "candidate";
        writer.Write($"jam [{transportText}, {outputText}, {generator.ToString().ToLowerInvariant()}, {patternText}]> ");
    }

    public void WriteStatus(SafeMidiOutput output, string? inputName, TransportEngine transport,
        InternalMidiClock internalClock, PatternPlayer player) =>
        writer.WriteLine($"Output={output.PortName ?? "none"}, Input={inputName ?? "none"}, Source={transport.Source.ToString().ToLowerInvariant()}, State={transport.State}, Bar={transport.Position.Bar + 1}, Beat={transport.Position.Beat + 1}, Pulse={transport.Position.PulseInBeat}, BPM={internalClock.Bpm:0.##}, Pattern={(player.IsEnabled ? "playing" : "muted")}, Error={player.Error?.Message ?? "none"}");

    public void WriteHelp(string? command)
    {
        if (command is null)
        {
            writer.WriteLine("""
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

        writer.WriteLine(HelpText(command));
    }

    public static string GrooveText(GrooveSelection value) => value switch
    {
        GrooveSelection.LongShort => "long-short", GrooveSelection.SparseAnswer => "sparse-answer",
        _ => value.ToString().ToLowerInvariant()
    };

    public static string MotifText(MotifShape value) => value switch
    {
        MotifShape.RootFifth => "root-fifth", MotifShape.CallResponse => "call-response",
        _ => value.ToString().ToLowerInvariant()
    };

    public static string GeneratorControls(GeneratorMode mode) => mode switch
    {
        GeneratorMode.Motif => "shape, activity, movement, variation",
        GeneratorMode.Phrase => "length, activity, rhythm, movement, variation, turnaround",
        GeneratorMode.Groove => "groove, similarity, activity, movement, variation, turnaround (bass only)",
        GeneratorMode.Euclidean => "key, palette, and role; other settings are fixed",
        GeneratorMode.Euclidean2 => "key, palette, and role; experimental four-bar development is fixed",
        _ => throw new ArgumentOutOfRangeException(nameof(mode))
    };

    private static string HelpText(string command) => command.ToLowerInvariant() switch
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

    private static string ShapeHelpText() => """
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

    private static string PitchClass(int value) =>
        new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" }[value];

    private static string PaletteText(PitchPalette palette) =>
        palette.ToString().Replace("Pentatonic", " pentatonic").ToLowerInvariant();
}
