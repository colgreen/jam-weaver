using System.Globalization;

namespace JamWeaver.ConsoleApp;

/// <summary>Converts console tokens to typed values while domain types retain their validation rules.</summary>
internal static class ConsoleArgumentParser
{
    public static string Argument(string[] parts, int index) => index < parts.Length
        ? parts[index] : throw new ArgumentException("Missing argument.");

    public static int ParseInteger(string[] parts, int index) =>
        int.Parse(Argument(parts, index), CultureInfo.InvariantCulture);

    public static double ParseNumber(string[] parts, int index) =>
        double.Parse(Argument(parts, index), CultureInfo.InvariantCulture);

    public static ulong ParseSeed(string[] parts, int index) =>
        ulong.Parse(Argument(parts, index), CultureInfo.InvariantCulture);

    public static MidiChannel ParseChannel(string[] parts, int index) => new(ParseInteger(parts, index));

    public static MidiValue ParseMidiValue(string[] parts, int index) => new(ParseInteger(parts, index));

    public static T ParseEnumValue<T>(string[] parts, int index) where T : struct, Enum =>
        Enum.TryParse<T>(Argument(parts, index), true, out var value) && Enum.IsDefined(value)
            ? value : throw new ArgumentException($"Invalid {typeof(T).Name} value.");

    public static ClockSource ParseClockSource(string value) => value.ToLowerInvariant() switch
    {
        "external" => ClockSource.External,
        "internal" => ClockSource.Internal,
        _ => throw new ArgumentException("Source must be internal or external.")
    };

    public static GeneratorMode ParseGeneratorMode(string value) => value.ToLowerInvariant() switch
    {
        "euclidean" => GeneratorMode.Euclidean,
        "turnaround" => GeneratorMode.Turnaround,
        "motif" => GeneratorMode.Motif,
        "phrase" => GeneratorMode.Phrase,
        "groove" => GeneratorMode.Groove,
        "euclidean-phrase" or "euclidean2" => throw new ArgumentException(
            "Euclidean phrase is no longer selectable. Use 'generator phrase' for phrase generation or 'generator turnaround' for a repeated loop with an ending. Saved patterns remain playable."),
        _ => throw new ArgumentException("Generator must be euclidean, turnaround, motif, phrase, or groove.")
    };

    public static PhraseLength ParsePhraseLength(string[] parts, int index) => ParseInteger(parts, index) switch
    {
        1 => PhraseLength.OneBar,
        2 => PhraseLength.TwoBars,
        4 => PhraseLength.FourBars,
        _ => throw new ArgumentException("Length must be 1, 2, or 4 bars.")
    };

    public static int ParseKeyDirection(string value) => value.ToLowerInvariant() switch
    {
        "up" => 1,
        "down" => -1,
        _ => throw new ArgumentException("Key direction must be up or down.")
    };

    public static MusicalRole ParseRole(string value) => value.ToLowerInvariant() switch
    {
        "bass" => MusicalRole.Bass,
        "middle" or "mid" => MusicalRole.Middle,
        "high" => MusicalRole.High,
        _ => throw new ArgumentException("Role must be bass, middle, or high.")
    };

    public static GrooveSelection ParseGrooveSelection(string value) => value.ToLowerInvariant() switch
    {
        "auto" => GrooveSelection.Auto,
        "foundation" => GrooveSelection.Foundation,
        "offbeat" => GrooveSelection.Offbeat,
        "anticipation" => GrooveSelection.Anticipation,
        "long-short" => GrooveSelection.LongShort,
        "sparse-answer" => GrooveSelection.SparseAnswer,
        "broken" => GrooveSelection.Broken,
        _ => throw new ArgumentException("Unknown groove category.")
    };

    public static MotifShape ParseMotifShape(string value) => value.ToLowerInvariant() switch
    {
        "auto" => MotifShape.Auto,
        "pedal" => MotifShape.Pedal,
        "root-fifth" => MotifShape.RootFifth,
        "walking" => MotifShape.Walking,
        "call-response" => MotifShape.CallResponse,
        "arch" => MotifShape.Arch,
        "pickup" => MotifShape.Pickup,
        "riff" => MotifShape.Riff,
        _ => throw new ArgumentException("Unknown motif shape.")
    };

    public static bool TryParseLibraryNumber(string value, out int number) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number);

    public static int ParseLibraryNumber(string value) => TryParseLibraryNumber(value, out var number)
        ? number : throw new ArgumentException("Library selection must look like: load #2.");
}
