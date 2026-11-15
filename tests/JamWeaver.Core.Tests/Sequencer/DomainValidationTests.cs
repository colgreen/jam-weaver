using JamWeaver.Core.Generation.Groove;
using JamWeaver.Core.Generation.Motif;
using JamWeaver.Core.Generation.Phrase;
using JamWeaver.Core.Persistence;

namespace JamWeaver.Core.Tests.Sequencer;

public sealed class DomainValidationTests
{
    [Theory]
    [InlineData("id")]
    [InlineData("name")]
    [InlineData("schemaVersion")]
    [InlineData("timing")]
    [InlineData("mode")]
    [InlineData("role")]
    [InlineData("palette")]
    public void Pattern_rejects_invalid_default_values_and_undefined_enums(string field)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Pattern(
            field == "id" ? default : PatternId.New(),
            field == "name" ? default : new PatternName("valid"),
            field == "schemaVersion" ? default : PatternSchemaVersion.Current,
            field == "mode" ? (PatternMode)999 : PatternMode.Melodic,
            field == "timing" ? default : PatternTiming.SixteenthNotes,
            [PatternStep.Rest], field == "role" ? (MusicalRole)999 : MusicalRole.Bass,
            new TonalContext(default, field == "palette" ? (PitchPalette)999 : PitchPalette.MajorPentatonic)));
    }

    [Fact]
    public void A_default_gate_cannot_enter_a_pattern_note()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PatternNote(new DrumPitch(default), new MidiValue(100), default));
        Assert.Equal("gate", error.ParamName);
    }

    [Theory]
    [InlineData("parent")]
    [InlineData("lineage")]
    public void Recipe_rejects_default_identity_or_lineage_when_explicitly_supplied(string field) =>
        Assert.ThrowsAny<ArgumentException>(() => new GeneratorRecipe("test", 1, 0,
            field == "parent" ? default(PatternId) : null, [],
            field == "lineage" ? default(GeneratorLineage) : null));

    [Theory]
    [InlineData("euclidean", "name")]
    [InlineData("euclidean", "timing")]
    [InlineData("euclidean", "palette")]
    [InlineData("euclidean", "role")]
    [InlineData("drums", "name")]
    [InlineData("drums", "timing")]
    [InlineData("drums", "null-voice")]
    [InlineData("phrase", "name")]
    [InlineData("phrase", "palette")]
    [InlineData("phrase", "role")]
    [InlineData("groove", "name")]
    [InlineData("groove", "palette")]
    [InlineData("motif", "name")]
    [InlineData("motif", "palette")]
    public void Generator_settings_reject_invalid_values_before_generation(string family, string field) =>
        Assert.ThrowsAny<ArgumentException>(() => Settings(family, field));

    [Theory]
    [InlineData(PatternMode.Melodic)]
    [InlineData(PatternMode.Drums)]
    public void Valid_zero_defaults_round_trip_in_materialized_patterns(PatternMode mode)
    {
        var recipe = new GeneratorRecipe("unsupported-but-playable", 1, 0, null,
            [KeyValuePair.Create("zero", default(RecipeValue))]);
        var note = new PatternNote(mode == PatternMode.Melodic ? new MelodicPitch(0) : new DrumPitch(default),
            new MidiValue(1), new NoteGate(.01));
        var pattern = new Pattern(PatternId.New(), new PatternName("valid zeros"), PatternSchemaVersion.Current,
            mode, new PatternTiming(1), [new PatternStep([note], default)],
            mode == PatternMode.Melodic ? MusicalRole.Bass : null,
            mode == PatternMode.Melodic ? default(TonalContext) : null, recipe);
        var codec = new PatternJsonCodec();

        var encoded = codec.Encode(pattern, DateTimeOffset.UnixEpoch);
        var decoded = codec.Decode(encoded);

        Assert.Equal(encoded, codec.Encode(decoded.Pattern, decoded.SavedUtc));
        Assert.Equal(0, decoded.Pattern.Steps[0].Probability.Value);
        Assert.Equal(0, decoded.Pattern.Recipe!.Parameters["zero"].Integer);
    }

    [Fact]
    public void Encoding_rejects_a_schema_version_the_loader_cannot_read()
    {
        var pattern = new Pattern(PatternId.New(), new PatternName("future schema"), new PatternSchemaVersion(2),
            PatternMode.Drums, PatternTiming.SixteenthNotes, [PatternStep.Rest], null, null);

        var error = Assert.Throws<PatternPersistenceException>(() => new PatternJsonCodec().Encode(pattern, DateTimeOffset.UnixEpoch));

        Assert.Contains("unsupported pattern schema version", error.Message);
    }

    [Fact]
    public void Encoding_rejects_a_document_larger_than_the_read_limit()
    {
        var recipe = new GeneratorRecipe("large", 1, 0, null,
            [KeyValuePair.Create("text", RecipeValue.FromText(new string('x', PatternJsonCodec.MaximumDocumentBytes)))]);
        var pattern = new Pattern(PatternId.New(), new PatternName("large document"), PatternSchemaVersion.Current,
            PatternMode.Drums, PatternTiming.SixteenthNotes, [PatternStep.Rest], null, null, recipe);

        var error = Assert.Throws<PatternPersistenceException>(() => new PatternJsonCodec().Encode(pattern, DateTimeOffset.UnixEpoch));

        Assert.Contains("byte limit", error.Message);
    }

    private static object Settings(string family, string invalidField)
    {
        var name = invalidField == "name" ? default : new PatternName("settings");
        var timing = invalidField == "timing" ? default : PatternTiming.SixteenthNotes;
        var context = new TonalContext(default, invalidField == "palette" ? (PitchPalette)999 : PitchPalette.MajorPentatonic);
        var role = invalidField == "role" ? (MusicalRole)999 : MusicalRole.Bass;
        return family switch
        {
            "euclidean" => new EuclideanGeneratorSettings(name, 16, timing, context, role, default, default, default, default, default, 0),
            "drums" => new EuclideanDrumGeneratorSettings(name, 16, timing,
                invalidField == "null-voice" ? [null!] : [new DrumVoiceSettings(default, default)], default, default, 0),
            "phrase" => new PhraseGeneratorSettings(name, PhraseLength.OneBar, context, role, default, default, default, default, default, 0),
            "groove" => new GrooveGeneratorSettings(name, context, role, default, default, default, default, default, default, 0),
            "motif" => new MotifGeneratorSettings(name, context, role, default, default, default, default, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
    }
}
