using System.Text;
using System.Text.Json;
using JamWeaver.Core.Midi;
using JamWeaver.Core.Sequencer;

namespace JamWeaver.Core.Persistence;

public readonly record struct DecodedPattern(Pattern Pattern, DateTimeOffset SavedUtc);

public sealed class PatternJsonCodec
{
    public const int CurrentFormatVersion = 1;
    public const int MaximumDocumentBytes = 1024 * 1024;

    public byte[] Encode(Pattern pattern, DateTimeOffset savedUtc)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", CurrentFormatVersion);
            writer.WriteString("savedUtc", savedUtc.ToUniversalTime().ToString("O"));
            writer.WritePropertyName("pattern");
            WritePattern(writer, pattern);
            writer.WriteEndObject();
        }
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    public DecodedPattern Decode(ReadOnlySpan<byte> utf8Json, string context = "pattern JSON")
    {
        if (utf8Json.IsEmpty) throw new PatternPersistenceException($"{context} is empty.");
        if (utf8Json.Length > MaximumDocumentBytes)
            throw new PatternPersistenceException($"{context} exceeds the {MaximumDocumentBytes}-byte limit.");
        try
        {
            using var document = JsonDocument.Parse(utf8Json.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });
            EnsureNoDuplicateProperties(document.RootElement, "$", context);
            var root = RequireObject(document.RootElement, "$", context);
            var formatVersion = Required(root, "formatVersion", context).GetInt32();
            if (formatVersion != CurrentFormatVersion)
                throw new PatternPersistenceException($"{context} uses unsupported format version {formatVersion}; expected {CurrentFormatVersion}.");
            var savedUtcText = Required(root, "savedUtc", context).GetString()
                ?? throw new PatternPersistenceException($"{context} property 'savedUtc' cannot be null.");
            if (!DateTimeOffset.TryParseExact(savedUtcText, "O", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var savedUtc))
                throw new PatternPersistenceException($"{context} property 'savedUtc' is not a valid ISO-8601 timestamp.");
            return new DecodedPattern(ReadPattern(Required(root, "pattern", context), context), savedUtc.ToUniversalTime());
        }
        catch (PatternPersistenceException) { throw; }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or
                                   ArgumentException or OverflowException)
        {
            throw new PatternPersistenceException($"{context} is invalid: {ex.Message}", ex);
        }
    }

    private static void WritePattern(Utf8JsonWriter writer, Pattern pattern)
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", pattern.SchemaVersion.Value);
        writer.WriteString("id", pattern.Id.Value);
        writer.WriteString("name", pattern.Name.Value);
        writer.WriteString("mode", ModeName(pattern.Mode));
        writer.WriteNumber("pulsesPerStep", pattern.Timing.PulsesPerStep);
        writer.WritePropertyName("steps");
        writer.WriteStartArray();
        foreach (var step in pattern.Steps) WriteStep(writer, step);
        writer.WriteEndArray();
        if (pattern.Role is { } role) writer.WriteString("role", RoleName(role)); else writer.WriteNull("role");
        writer.WritePropertyName("tonalContext");
        if (pattern.TonalContext is { } tonal)
        {
            writer.WriteStartObject();
            writer.WriteNumber("root", tonal.Root.Value);
            writer.WriteString("palette", PaletteName(tonal.Palette));
            writer.WriteEndObject();
        }
        else writer.WriteNullValue();
        writer.WritePropertyName("recipe");
        if (pattern.Recipe is { } recipe) WriteRecipe(writer, recipe); else writer.WriteNullValue();
        writer.WriteEndObject();
    }

    private static void WriteStep(Utf8JsonWriter writer, PatternStep step)
    {
        writer.WriteStartObject();
        writer.WriteNumber("probability", step.Probability.Value);
        writer.WritePropertyName("notes");
        writer.WriteStartArray();
        foreach (var note in step.Notes)
        {
            writer.WriteStartObject();
            writer.WriteNumber("velocity", note.Velocity.Value);
            writer.WriteNumber("gate", note.Gate.Value);
            writer.WritePropertyName("pitch");
            writer.WriteStartObject();
            switch (note.Pitch)
            {
                case MelodicPitch melodic:
                    writer.WriteString("kind", "melodic");
                    writer.WriteNumber("scaleDegree", melodic.ScaleDegree);
                    writer.WriteNumber("octaveOffset", melodic.OctaveOffset);
                    writer.WriteNumber("chromaticOffset", melodic.ChromaticOffset);
                    break;
                case DrumPitch drum:
                    writer.WriteString("kind", "drum");
                    writer.WriteNumber("noteNumber", drum.NoteNumber.Value);
                    break;
                default: throw new InvalidOperationException("Unsupported pattern pitch type.");
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteRecipe(Utf8JsonWriter writer, GeneratorRecipe recipe)
    {
        writer.WriteStartObject();
        writer.WriteString("generatorId", recipe.GeneratorId);
        writer.WriteNumber("generatorVersion", recipe.GeneratorVersion);
        writer.WriteString("seed", recipe.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (recipe.ParentPatternId is { } parent) writer.WriteString("parentPatternId", parent.Value);
        else writer.WriteNull("parentPatternId");
        writer.WritePropertyName("parameters");
        writer.WriteStartObject();
        foreach (var (key, value) in recipe.Parameters)
        {
            writer.WritePropertyName(key);
            writer.WriteStartObject();
            writer.WriteString("kind", RecipeKindName(value.Kind));
            writer.WritePropertyName("value");
            switch (value.Kind)
            {
                case RecipeValueKind.Integer: writer.WriteNumberValue(value.Integer); break;
                case RecipeValueKind.Number: writer.WriteNumberValue(value.Number); break;
                case RecipeValueKind.Boolean: writer.WriteBooleanValue(value.Boolean); break;
                case RecipeValueKind.Text: writer.WriteStringValue(value.Text); break;
                default: throw new InvalidOperationException("Unsupported recipe value kind.");
            }
            writer.WriteEndObject();
        }
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static Pattern ReadPattern(JsonElement element, string context)
    {
        var value = RequireObject(element, "pattern", context);
        var schemaVersion = Required(value, "schemaVersion", context).GetInt32();
        if (schemaVersion != PatternSchemaVersion.Current.Value)
            throw new PatternPersistenceException($"{context} uses unsupported pattern schema version {schemaVersion}; expected {PatternSchemaVersion.Current.Value}.");
        var id = new PatternId(Required(value, "id", context).GetGuid());
        var name = new PatternName(RequiredString(value, "name", context));
        var mode = ReadMode(RequiredString(value, "mode", context), context);
        var timing = new PatternTiming(Required(value, "pulsesPerStep", context).GetInt32());
        var stepsElement = Required(value, "steps", context);
        if (stepsElement.ValueKind != JsonValueKind.Array) throw new PatternPersistenceException($"{context} property 'steps' must be an array.");
        var steps = stepsElement.EnumerateArray().Select(step => ReadStep(step, context)).ToArray();
        var roleElement = Required(value, "role", context);
        MusicalRole? role = roleElement.ValueKind == JsonValueKind.Null ? null : ReadRole(roleElement.GetString(), context);
        var tonalElement = Required(value, "tonalContext", context);
        TonalContext? tonal = tonalElement.ValueKind == JsonValueKind.Null ? null : ReadTonalContext(tonalElement, context);
        var recipeElement = Required(value, "recipe", context);
        var recipe = recipeElement.ValueKind == JsonValueKind.Null ? null : ReadRecipe(recipeElement, context);
        return new Pattern(id, name, new PatternSchemaVersion(schemaVersion), mode, timing, steps, role, tonal, recipe);
    }

    private static PatternStep ReadStep(JsonElement element, string context)
    {
        var value = RequireObject(element, "step", context);
        var probability = new TriggerProbability(Required(value, "probability", context).GetDouble());
        var notesElement = Required(value, "notes", context);
        if (notesElement.ValueKind != JsonValueKind.Array) throw new PatternPersistenceException($"{context} step property 'notes' must be an array.");
        var notes = notesElement.EnumerateArray().Select(note => ReadNote(note, context)).ToArray();
        return new PatternStep(notes, probability);
    }

    private static PatternNote ReadNote(JsonElement element, string context)
    {
        var value = RequireObject(element, "note", context);
        var velocity = new MidiValue(Required(value, "velocity", context).GetInt32());
        var gate = new NoteGate(Required(value, "gate", context).GetDouble());
        var pitch = ReadPitch(Required(value, "pitch", context), context);
        return new PatternNote(pitch, velocity, gate);
    }

    private static PatternPitch ReadPitch(JsonElement element, string context)
    {
        var value = RequireObject(element, "pitch", context);
        return RequiredString(value, "kind", context) switch
        {
            "melodic" => new MelodicPitch(Required(value, "scaleDegree", context).GetInt32(),
                Required(value, "octaveOffset", context).GetInt32(), Required(value, "chromaticOffset", context).GetInt32()),
            "drum" => new DrumPitch(new MidiValue(Required(value, "noteNumber", context).GetInt32())),
            var kind => throw new PatternPersistenceException($"{context} has unknown pitch kind '{kind}'.")
        };
    }

    private static TonalContext ReadTonalContext(JsonElement element, string context)
    {
        var value = RequireObject(element, "tonalContext", context);
        return new TonalContext(new RootPitchClass(Required(value, "root", context).GetInt32()),
            ReadPalette(RequiredString(value, "palette", context), context));
    }

    private static GeneratorRecipe ReadRecipe(JsonElement element, string context)
    {
        var value = RequireObject(element, "recipe", context);
        var seedText = RequiredString(value, "seed", context);
        if (!ulong.TryParse(seedText, System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var seed))
            throw new PatternPersistenceException($"{context} recipe seed is not an unsigned decimal integer.");
        var parentElement = Required(value, "parentPatternId", context);
        PatternId? parent = parentElement.ValueKind == JsonValueKind.Null ? null : new PatternId(parentElement.GetGuid());
        var parametersElement = RequireObject(Required(value, "parameters", context), "parameters", context);
        var parameters = parametersElement.EnumerateObject().Select(parameter =>
            new KeyValuePair<string, RecipeValue>(parameter.Name, ReadRecipeValue(parameter.Value, context))).ToArray();
        return new GeneratorRecipe(RequiredString(value, "generatorId", context),
            Required(value, "generatorVersion", context).GetInt32(), seed, parent, parameters);
    }

    private static RecipeValue ReadRecipeValue(JsonElement element, string context)
    {
        var value = RequireObject(element, "recipe parameter", context);
        var kind = RequiredString(value, "kind", context);
        var data = Required(value, "value", context);
        return kind switch
        {
            "integer" => RecipeValue.FromInteger(data.GetInt64()),
            "number" => RecipeValue.FromNumber(data.GetDouble()),
            "boolean" => RecipeValue.FromBoolean(data.GetBoolean()),
            "text" => RecipeValue.FromText(data.GetString() ?? throw new PatternPersistenceException($"{context} recipe text value cannot be null.")),
            _ => throw new PatternPersistenceException($"{context} has unknown recipe value kind '{kind}'.")
        };
    }

    private static JsonElement RequireObject(JsonElement element, string path, string context)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new PatternPersistenceException($"{context} {path} must be an object.");
        return element;
    }

    private static JsonElement Required(JsonElement parent, string name, string context) =>
        parent.TryGetProperty(name, out var value) ? value
            : throw new PatternPersistenceException($"{context} is missing required property '{name}'.");

    private static string RequiredString(JsonElement parent, string name, string context) =>
        Required(parent, name, context).GetString()
            ?? throw new PatternPersistenceException($"{context} property '{name}' cannot be null.");

    private static void EnsureNoDuplicateProperties(JsonElement element, string path, string context)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new PatternPersistenceException($"{context} contains duplicate property '{property.Name}' at {path}.");
                EnsureNoDuplicateProperties(property.Value, $"{path}.{property.Name}", context);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray()) EnsureNoDuplicateProperties(item, $"{path}[{index++}]", context);
        }
    }

    private static string ModeName(PatternMode value) => value switch { PatternMode.Melodic => "melodic", PatternMode.Drums => "drums", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static PatternMode ReadMode(string value, string context) => value switch { "melodic" => PatternMode.Melodic, "drums" => PatternMode.Drums, _ => throw new PatternPersistenceException($"{context} has unknown pattern mode '{value}'.") };
    private static string RoleName(MusicalRole value) => value switch { MusicalRole.Bass => "bass", MusicalRole.Middle => "middle", MusicalRole.High => "high", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static MusicalRole ReadRole(string? value, string context) => value switch { "bass" => MusicalRole.Bass, "middle" => MusicalRole.Middle, "high" => MusicalRole.High, _ => throw new PatternPersistenceException($"{context} has unknown musical role '{value}'.") };
    private static string PaletteName(PitchPalette value) => value switch { PitchPalette.MajorPentatonic => "majorPentatonic", PitchPalette.MinorPentatonic => "minorPentatonic", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
    private static PitchPalette ReadPalette(string value, string context) => value switch { "majorPentatonic" => PitchPalette.MajorPentatonic, "minorPentatonic" => PitchPalette.MinorPentatonic, _ => throw new PatternPersistenceException($"{context} has unknown pitch palette '{value}'.") };
    private static string RecipeKindName(RecipeValueKind value) => value switch { RecipeValueKind.Integer => "integer", RecipeValueKind.Number => "number", RecipeValueKind.Boolean => "boolean", RecipeValueKind.Text => "text", _ => throw new ArgumentOutOfRangeException(nameof(value)) };
}
