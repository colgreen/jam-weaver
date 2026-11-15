namespace JamWeaver.ConsoleApp;

/// <summary>Parses a command and its free-text argument from the same trimmed line before dispatch.</summary>
internal sealed class ConsoleCommand
{
    public string Name { get; }
    public string[] Parts { get; }
    public string ArgumentText { get; }

    private ConsoleCommand(string text, string[] parts)
    {
        Name = parts[0].ToLowerInvariant();
        Parts = parts;
        ArgumentText = text[parts[0].Length..].Trim();
    }

    public static ConsoleCommand? Parse(string line)
    {
        var text = line.Trim();
        if (text.Length == 0) return null;
        var parts = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var command = new ConsoleCommand(text, parts);
        command.ValidateArgumentCount();
        return command;
    }

    private void ValidateArgumentCount()
    {
        (int Min, int Max) bounds = Name switch
        {
            "setup" or "start" or "continue" or "stop" or "go" or "back" or "forward" or "settings"
                or "keep" or "revert" or "palette" or "play" or "mute" or "pattern" or "library"
                or "panic" or "status" or "quit" or "exit" => (0, 0),
            "out" or "in" or "source" or "bpm" or "new" or "compare" or "generator" or "shape"
                or "ch" or "channel" or "help" => (0, 1),
            "groove" or "similarity" or "length" or "activity" or "rhythm" or "movement"
                or "variation" or "turnaround" or "key" or "role" => (1, 1),
            "pc" => (2, 2),
            "on" or "cc" => (3, 3),
            "off" => (2, 3),
            "note" => (3, 4),
            // VariationArguments.Parse validates the optional target/strength/seed grammar before mutation.
            "vary" => (0, 4),
            // Save/load consume the entire argument text as a name or library query.
            _ => (0, int.MaxValue)
        };
        var count = Parts.Length - 1;
        if (count < bounds.Min)
            throw new ArgumentException($"Missing argument for '{Name}'. Type 'help {Name}' for usage.");
        if (count > bounds.Max)
            throw new ArgumentException($"Unexpected argument for '{Name}'. Type 'help {Name}' for usage.");
    }
}
