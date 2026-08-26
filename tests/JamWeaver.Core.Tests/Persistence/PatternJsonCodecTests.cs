using System.Text;
using JamWeaver.Core.Persistence;

namespace JamWeaver.Core.Tests.Persistence;

public sealed class PatternJsonCodecTests
{
    private readonly PatternJsonCodec _codec = new();
    private static readonly DateTimeOffset SavedUtc = new(2026, 8, 9, 12, 34, 56, TimeSpan.Zero);

    [Fact]
    public void Melodic_pattern_and_all_recipe_values_round_trip_exactly()
    {
        var pattern = MelodicPattern();

        var data = _codec.Encode(pattern, SavedUtc);
        var decoded = _codec.Decode(data);

        AssertPattern(pattern, decoded.Pattern);
        Assert.Equal(SavedUtc, decoded.SavedUtc);
        Assert.EndsWith("\n", Encoding.UTF8.GetString(data));
        Assert.Contains("\"seed\": \"18446744073709551615\"", Encoding.UTF8.GetString(data));
    }

    [Fact]
    public void Multi_voice_drums_and_empty_step_round_trip()
    {
        var sounding = new PatternStep([
            new PatternNote(new DrumPitch(new MidiValue(36)), new MidiValue(110), new NoteGate(.25)),
            new PatternNote(new DrumPitch(new MidiValue(42)), new MidiValue(80), new NoteGate(1))], new TriggerProbability(.75));
        var pattern = new Pattern(PatternId.New(), new PatternName("Drums"), PatternSchemaVersion.Current,
            PatternMode.Drums, new PatternTiming(12), [sounding, PatternStep.Rest], null, null);

        var decoded = _codec.Decode(_codec.Encode(pattern, SavedUtc));

        AssertPattern(pattern, decoded.Pattern);
    }

    [Fact]
    public void Unknown_properties_are_tolerated()
    {
        var json = Encoding.UTF8.GetString(_codec.Encode(MelodicPattern(), SavedUtc))
            .Replace("\"formatVersion\": 1,", "\"formatVersion\": 1,\n  \"futureRootValue\": true,");

        var decoded = _codec.Decode(Encoding.UTF8.GetBytes(json));

        Assert.Equal("Fixture", decoded.Pattern.Name.Value);
    }

    [Theory]
    [InlineData("\"formatVersion\": 1", "\"formatVersion\": 2", "unsupported format version")]
    [InlineData("\"schemaVersion\": 1", "\"schemaVersion\": 2", "unsupported pattern schema version")]
    [InlineData("\"mode\": \"melodic\"", "\"mode\": \"future\"", "unknown pattern mode")]
    [InlineData("\"kind\": \"melodic\"", "\"kind\": \"future\"", "unknown pitch kind")]
    [InlineData("\"gate\": 0.5", "\"gate\": 0", "invalid")]
    public void Invalid_or_unsupported_content_is_rejected(string oldValue, string newValue, string message)
    {
        var json = Encoding.UTF8.GetString(_codec.Encode(MelodicPattern(), SavedUtc)).Replace(oldValue, newValue);

        var error = Assert.Throws<PatternPersistenceException>(() => _codec.Decode(Encoding.UTF8.GetBytes(json)));

        Assert.Contains(message, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Duplicate_properties_are_rejected()
    {
        var json = Encoding.UTF8.GetString(_codec.Encode(MelodicPattern(), SavedUtc))
            .Replace("\"formatVersion\": 1,", "\"formatVersion\": 1, \"formatVersion\": 1,");

        var error = Assert.Throws<PatternPersistenceException>(() => _codec.Decode(Encoding.UTF8.GetBytes(json)));

        Assert.Contains("duplicate property 'formatVersion'", error.Message);
    }

    [Fact]
    public void Empty_malformed_and_oversized_documents_are_rejected()
    {
        Assert.Throws<PatternPersistenceException>(() => _codec.Decode([]));
        Assert.Throws<PatternPersistenceException>(() => _codec.Decode("{"u8));
        Assert.Throws<PatternPersistenceException>(() => _codec.Decode(new byte[PatternJsonCodec.MaximumDocumentBytes + 1]));
    }

    internal static Pattern MelodicPattern(PatternId? id = null, string name = "Fixture")
    {
        var recipe = new GeneratorRecipe("fixture", 2, ulong.MaxValue, new PatternId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        [
            new("boolean", RecipeValue.FromBoolean(true)),
            new("integer", RecipeValue.FromInteger(-3)),
            new("number", RecipeValue.FromNumber(.125)),
            new("text", RecipeValue.FromText("hello"))
        ]);
        var note = new PatternNote(new MelodicPitch(4, -1, 1), new MidiValue(99), new NoteGate(.5));
        return new Pattern(id ?? new PatternId(Guid.Parse("20000000-0000-0000-0000-000000000002")),
            new PatternName(name), PatternSchemaVersion.Current, PatternMode.Melodic, PatternTiming.SixteenthNotes,
            [new PatternStep([note], new TriggerProbability(.625)), PatternStep.Rest], MusicalRole.Middle,
            new TonalContext(new RootPitchClass(11), PitchPalette.MajorPentatonic), recipe);
    }

    private static void AssertPattern(Pattern expected, Pattern actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.Timing, actual.Timing);
        Assert.Equal(expected.Role, actual.Role);
        Assert.Equal(expected.TonalContext, actual.TonalContext);
        Assert.Equal(expected.Steps.Length, actual.Steps.Length);
        for (var index = 0; index < expected.Steps.Length; index++)
        {
            Assert.Equal(expected.Steps[index].Probability, actual.Steps[index].Probability);
            Assert.Equal(expected.Steps[index].Notes, actual.Steps[index].Notes);
        }
        Assert.Equal(expected.Recipe?.GeneratorId, actual.Recipe?.GeneratorId);
        Assert.Equal(expected.Recipe?.GeneratorVersion, actual.Recipe?.GeneratorVersion);
        Assert.Equal(expected.Recipe?.Seed, actual.Recipe?.Seed);
        Assert.Equal(expected.Recipe?.ParentPatternId, actual.Recipe?.ParentPatternId);
        Assert.Equal(expected.Recipe?.Parameters, actual.Recipe?.Parameters);
    }
}
