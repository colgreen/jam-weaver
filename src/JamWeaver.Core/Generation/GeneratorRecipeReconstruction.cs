using JamWeaver.Core.Midi;
using JamWeaver.Core.Sequencer;
using JamWeaver.Core.Generation.Phrase;
using JamWeaver.Core.Generation.Groove;
using JamWeaver.Core.Generation.Motif;

namespace JamWeaver.Core.Generation;

public static class GeneratorRecipeReconstruction
{
    public static MutationSettings Mutation(GeneratorRecipe recipe)
    {
        RequireGenerator(recipe, PatternMutator.GeneratorId, PatternMutator.GeneratorVersion);
        RequireExactKeys(recipe, new HashSet<string>(["strength", "edit-count", "mode"], StringComparer.Ordinal));
        if (recipe.ParentPatternId is null) throw new ArgumentException("Mutation recipe requires a parent pattern ID.", nameof(recipe));
        _ = I(recipe, "edit-count");
        _ = E<PatternMode>(recipe, "mode");
        return new MutationSettings(N(recipe, "strength"), recipe.Seed);
    }

    public static MelodicGeneratorSettings Melodic(PatternName name, GeneratorRecipe recipe)
    {
        RequireGenerator(recipe, MelodicPatternGenerator.GeneratorId, MelodicPatternGenerator.GeneratorVersion);
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "steps", "pulses-per-step", "root", "palette", "role", "density", "movement", "repetition",
            "gate", "velocity-variation", "hits", "rotation", "motif-length"
        };
        RequireExactKeys(recipe, expected);
        return new MelodicGeneratorSettings(name, I(recipe, "steps"), new PatternTiming(I(recipe, "pulses-per-step")),
            new TonalContext(new RootPitchClass(I(recipe, "root")), E<PitchPalette>(recipe, "palette")),
            E<MusicalRole>(recipe, "role"), N(recipe, "density"), N(recipe, "movement"), N(recipe, "repetition"),
            N(recipe, "gate"), N(recipe, "velocity-variation"), recipe.Seed);
    }

    public static MelodicGeneratorSettings Euclidean2(PatternName name, GeneratorRecipe recipe)
    {
        RequireGenerator(recipe, Euclidean2PatternGenerator.GeneratorId, Euclidean2PatternGenerator.GeneratorVersion);
        RequireExactKeys(recipe, new HashSet<string>(
        [
            "steps", "pulses-per-step", "root", "palette", "role", "density", "movement", "repetition",
            "gate", "velocity-variation", "hits-a", "hits-b", "rotation-a", "rotation-b", "motif-length",
            "bar-0-mask", "bar-1-mask", "bar-2-mask", "bar-3-mask", "bar-roles", "structural-mask", "ghost-mask"
        ], StringComparer.Ordinal));
        if (I(recipe, "steps") != 64 || I(recipe, "pulses-per-step") != PatternTiming.SixteenthNotes.PulsesPerStep)
            throw new ArgumentException("Euclidean2 recipe timing is inconsistent.", nameof(recipe));
        if (I(recipe, "hits-a") is < 1 or > 15 || I(recipe, "hits-b") is < 1 or > 15
            || I(recipe, "rotation-a") is < 0 or > 15 || I(recipe, "rotation-b") is < 0 or > 15
            || I(recipe, "motif-length") is < 2 or > 4 || T(recipe, "bar-roles") != "A,A-prime,B,return")
            throw new ArgumentException("Euclidean2 recipe metadata is inconsistent.", nameof(recipe));
        for (var index = 0; index < 4; index++) _ = ParseHex(recipe, $"bar-{index}-mask", 4);
        _ = ParseHex(recipe, "structural-mask", 16); _ = ParseHex(recipe, "ghost-mask", 16);
        return new MelodicGeneratorSettings(name, 64, PatternTiming.SixteenthNotes,
            new TonalContext(new RootPitchClass(I(recipe, "root")), E<PitchPalette>(recipe, "palette")),
            E<MusicalRole>(recipe, "role"), N(recipe, "density"), N(recipe, "movement"), N(recipe, "repetition"),
            N(recipe, "gate"), N(recipe, "velocity-variation"), recipe.Seed);
    }

    public static DrumGeneratorSettings Drums(PatternName name, GeneratorRecipe recipe)
    {
        RequireGenerator(recipe, DrumPatternGenerator.GeneratorId, DrumPatternGenerator.GeneratorVersion);
        var voiceCount = I(recipe, "voice-count");
        if (voiceCount is < 1 or > 8) throw new ArgumentException("Recipe voice count must be 1-8.", nameof(recipe));
        var expected = new HashSet<string>(["steps", "pulses-per-step", "gate", "velocity-variation", "voice-count"], StringComparer.Ordinal);
        var voices = new List<DrumVoiceSettings>(voiceCount);
        for (var i = 0; i < voiceCount; i++)
        {
            var prefix = $"voice-{i}";
            expected.UnionWith([$"{prefix}-note", $"{prefix}-density", $"{prefix}-hits", $"{prefix}-rotation"]);
            voices.Add(new DrumVoiceSettings(new MidiValue(I(recipe, $"{prefix}-note")), N(recipe, $"{prefix}-density")));
        }
        RequireExactKeys(recipe, expected);
        return new DrumGeneratorSettings(name, I(recipe, "steps"), new PatternTiming(I(recipe, "pulses-per-step")),
            voices, N(recipe, "gate"), N(recipe, "velocity-variation"), recipe.Seed);
    }

    public static PhraseGeneratorSettings Phrase(PatternName name, GeneratorRecipe recipe)
    {
        RequireGenerator(recipe, MelodicPhraseGenerator.GeneratorId, MelodicPhraseGenerator.GeneratorVersion);
        RequireExactKeys(recipe, new HashSet<string>(
        [
            "length", "activity", "rhythm", "movement", "variation", "turnaround", "root", "palette",
            "role", "steps", "pulses-per-step", "gate", "velocity-variation", "hits-per-a-bar",
            "motif-length", "bar-roles", "structural-mask", "ghost-mask"
        ], StringComparer.Ordinal));
        var length = E<PhraseLength>(recipe, "length");
        if (I(recipe, "steps") != (int)length * 16 || I(recipe, "pulses-per-step") != PatternTiming.SixteenthNotes.PulsesPerStep)
            throw new ArgumentException("Phrase recipe length and timing are inconsistent.", nameof(recipe));
        _ = N(recipe, "gate");
        _ = N(recipe, "velocity-variation");
        _ = I(recipe, "hits-per-a-bar");
        _ = I(recipe, "motif-length");
        _ = T(recipe, "bar-roles");
        if (!ulong.TryParse(T(recipe, "structural-mask"), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out _)
            || !ulong.TryParse(T(recipe, "ghost-mask"), System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            throw new ArgumentException("Phrase recipe contains an invalid classification mask.", nameof(recipe));
        return new PhraseGeneratorSettings(name, length,
            new TonalContext(new RootPitchClass(I(recipe, "root")), E<PitchPalette>(recipe, "palette")),
            E<MusicalRole>(recipe, "role"), E<PhraseActivity>(recipe, "activity"),
            E<PhraseRhythm>(recipe, "rhythm"), E<PhraseLevel>(recipe, "movement"),
            E<PhraseLevel>(recipe, "variation"), E<PhraseTurnaround>(recipe, "turnaround"), recipe.Seed);
    }

    public static GrooveGeneratorSettings Groove(PatternName name, GeneratorRecipe recipe)
    {
        RequireGenerator(recipe, MelodicGrooveGenerator.GeneratorId, MelodicGrooveGenerator.GeneratorVersion);
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "vocabulary-version", "metric-version", "template", "groove", "similarity", "activity",
            "movement", "variation", "turnaround", "root", "palette", "role", "steps",
            "pulses-per-step", "structural-mask", "ghost-mask"
        };
        for (var i = 0; i < 4; i++) expected.UnionWith([$"bar-{i}-mask", $"bar-{i}-features", $"bar-{i}-relaxation"]);
        RequireExactKeys(recipe, expected);
        if (I(recipe, "vocabulary-version") != GrooveVocabulary.Version || I(recipe, "metric-version") != RhythmMetrics.Version)
            throw new NotSupportedException("Unsupported groove vocabulary or metric version.");
        if (I(recipe, "steps") != 64 || I(recipe, "pulses-per-step") != PatternTiming.SixteenthNotes.PulsesPerStep)
            throw new ArgumentException("Groove recipe timing is inconsistent.", nameof(recipe));
        _ = GrooveVocabulary.Get(T(recipe, "template"));
        foreach (var key in new[] { "structural-mask", "ghost-mask" }) _ = ParseHex(recipe, key, 16);
        for (var i = 0; i < 4; i++) { _ = ParseHex(recipe, $"bar-{i}-mask", 4); _ = T(recipe, $"bar-{i}-features"); _ = T(recipe, $"bar-{i}-relaxation"); }
        return new GrooveGeneratorSettings(name,
            new TonalContext(new RootPitchClass(I(recipe, "root")), E<PitchPalette>(recipe, "palette")),
            E<MusicalRole>(recipe, "role"), E<GrooveSelection>(recipe, "groove"), E<GrooveSimilarity>(recipe, "similarity"),
            E<PhraseActivity>(recipe, "activity"), E<PhraseLevel>(recipe, "movement"),
            E<PhraseLevel>(recipe, "variation"), E<PhraseTurnaround>(recipe, "turnaround"), recipe.Seed);
    }

    public static MotifGeneratorSettings Motif(PatternName name, GeneratorRecipe recipe)
    {
        RequireGenerator(recipe, MusicalMotifGenerator.GeneratorId, MusicalMotifGenerator.GeneratorVersion);
        var expected = new HashSet<string>(StringComparer.Ordinal)
        {
            "shape", "resolved-shape", "rhythm-variant", "activity", "movement", "variation", "root", "palette", "role",
            "steps", "pulses-per-step", "bar-0-mask", "bar-1-mask", "bar-2-mask", "bar-3-mask",
            "motif-length", "development", "structural-mask", "ghost-mask"
        };
        RequireExactKeys(recipe, expected);
        if (I(recipe, "steps") != 64 || I(recipe, "pulses-per-step") != PatternTiming.SixteenthNotes.PulsesPerStep)
            throw new ArgumentException("Motif recipe timing is inconsistent.", nameof(recipe));
        _ = E<MotifShape>(recipe, "resolved-shape");
        if (I(recipe, "rhythm-variant") is < 0 or > 3) throw new ArgumentException("Motif recipe rhythm variant is invalid.", nameof(recipe));
        _ = I(recipe, "motif-length"); _ = T(recipe, "development");
        for (var i = 0; i < 4; i++) _ = ParseHex(recipe, $"bar-{i}-mask", 4);
        _ = ParseHex(recipe, "structural-mask", 16); _ = ParseHex(recipe, "ghost-mask", 16);
        return new MotifGeneratorSettings(name,
            new TonalContext(new RootPitchClass(I(recipe, "root")), E<PitchPalette>(recipe, "palette")),
            E<MusicalRole>(recipe, "role"), E<MotifShape>(recipe, "shape"), E<PhraseActivity>(recipe, "activity"),
            E<PhraseLevel>(recipe, "movement"), E<PhraseLevel>(recipe, "variation"), recipe.Seed);
    }

    private static void RequireGenerator(GeneratorRecipe recipe, string id, int version)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (recipe.GeneratorId != id || recipe.GeneratorVersion != version)
            throw new NotSupportedException($"Unsupported generator recipe '{recipe.GeneratorId}' version {recipe.GeneratorVersion}.");
    }

    private static void RequireExactKeys(GeneratorRecipe recipe, HashSet<string> expected)
    {
        var actual = recipe.Parameters.Keys.ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expected))
            throw new ArgumentException("Recipe parameters are missing or unknown for this generator version.", nameof(recipe));
    }

    private static int I(GeneratorRecipe recipe, string key)
    {
        var value = Value(recipe, key, RecipeValueKind.Integer);
        return checked((int)value.Integer);
    }

    private static NormalizedAmount N(GeneratorRecipe recipe, string key) => new(Value(recipe, key, RecipeValueKind.Number).Number);

    private static string T(GeneratorRecipe recipe, string key) => Value(recipe, key, RecipeValueKind.Text).Text!;

    private static ulong ParseHex(GeneratorRecipe recipe, string key, int maximumDigits)
    {
        var text = T(recipe, key);
        if (text.Length > maximumDigits || !ulong.TryParse(text, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var result))
            throw new ArgumentException($"Recipe parameter '{key}' is not valid hexadecimal.", nameof(recipe));
        return result;
    }

    private static T E<T>(GeneratorRecipe recipe, string key) where T : struct, Enum
    {
        var text = Value(recipe, key, RecipeValueKind.Text).Text!;
        return Enum.TryParse<T>(text, false, out var value) && Enum.IsDefined(value)
            ? value : throw new ArgumentException($"Invalid {typeof(T).Name} value '{text}'.", nameof(recipe));
    }

    private static RecipeValue Value(GeneratorRecipe recipe, string key, RecipeValueKind kind)
    {
        if (!recipe.Parameters.TryGetValue(key, out var value)) throw new ArgumentException($"Missing recipe parameter '{key}'.", nameof(recipe));
        if (value.Kind != kind) throw new ArgumentException($"Recipe parameter '{key}' must be {kind}.", nameof(recipe));
        return value;
    }
}
