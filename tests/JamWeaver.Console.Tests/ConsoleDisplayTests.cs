using JamWeaver.Core.Persistence;

namespace JamWeaver.Console.Tests;

public sealed class ConsoleDisplayTests
{
    [Fact]
    public void Pattern_grid_renders_four_bars_and_anchor_symbols()
    {
        var pattern = new MusicalMotifGenerator().Generate(new MotifGeneratorSettings(
            new PatternName("Grid"), CandidateGenerator.DefaultTonalContext(), MusicalRole.Bass,
            MotifShape.Pedal, PhraseActivity.Medium, PhraseLevel.Medium, PhraseLevel.Medium, 123));
        var writer = new StringWriter();

        new ConsoleDisplay(writer).WritePatternGrid(pattern);

        var lines = Lines(writer);
        Assert.Equal(4, lines.Length);
        Assert.All(lines, line => Assert.Contains("X", line));
        Assert.StartsWith("  1:", lines[0]);
        Assert.StartsWith("  4:", lines[3]);
    }

    [Fact]
    public void Library_keeps_duplicate_names_unambiguous_with_numbers()
    {
        var context = CandidateGenerator.DefaultTonalContext();
        PatternLibraryEntry[] entries =
        [
            new("first.json", true, null, PatternId.New(), "Loop", PatternMode.Melodic,
                MusicalRole.Bass, context, 10, DateTimeOffset.UnixEpoch),
            new("second.json", true, null, PatternId.New(), "Loop", PatternMode.Melodic,
                MusicalRole.Middle, context, 20, DateTimeOffset.UnixEpoch)
        ];
        var writer = new StringWriter();

        new ConsoleDisplay(writer).WriteLibrary(entries);

        var lines = Lines(writer);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("  1. Loop", lines[0]);
        Assert.StartsWith("  2. Loop", lines[1]);
        Assert.Contains("bass", lines[0]);
        Assert.Contains("middle", lines[1]);
    }

    [Fact]
    public void Invalid_library_entry_includes_file_and_error()
    {
        var writer = new StringWriter();
        var entry = new PatternLibraryEntry("broken.json", false, "Invalid recipe", null, null,
            null, null, null, null, null);

        new ConsoleDisplay(writer).WriteLibrary([entry]);

        Assert.Contains("[invalid] broken.json: Invalid recipe", writer.ToString());
    }

    [Theory]
    [InlineData(GeneratorMode.Euclidean, "other settings are fixed")]
    [InlineData(GeneratorMode.Motif, "shape, activity, movement, variation")]
    [InlineData(GeneratorMode.Groove, "bass only")]
    public void Generator_labels_describe_mode_specific_controls(GeneratorMode mode, string expected) =>
        Assert.Contains(expected, ConsoleDisplay.GeneratorControls(mode));

    [Fact]
    public void Shape_help_uses_command_spellings_for_compound_shapes()
    {
        var writer = new StringWriter();

        new ConsoleDisplay(writer).WriteHelp("shape");

        Assert.Contains("root-fifth", writer.ToString());
        Assert.Contains("call-response", writer.ToString());
    }

    private static string[] Lines(StringWriter writer) => writer.ToString()
        .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
