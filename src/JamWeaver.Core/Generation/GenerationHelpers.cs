using JamWeaver.Core.Midi;
using JamWeaver.Core.Sequencer;
using Redzen.Random;

namespace JamWeaver.Core.Generation;

internal static class GenerationHelpers
{
    public static NoteGate Gate(NormalizedAmount amount) => new(.15 + (.85 * amount.Value));

    public static MidiValue Velocity(IRandomSource random, int baseVelocity, NormalizedAmount variation)
    {
        var spread = (int)Math.Round(variation.Value * 12, MidpointRounding.AwayFromZero);
        var value = spread == 0 ? baseVelocity : baseVelocity + random.Next(-spread, spread + 1);
        return new MidiValue(Math.Clamp(value, 1, 127));
    }

    public static KeyValuePair<string, RecipeValue> P(string key, long value) => KeyValuePair.Create(key, RecipeValue.FromInteger(value));
    public static KeyValuePair<string, RecipeValue> P(string key, double value) => KeyValuePair.Create(key, RecipeValue.FromNumber(value));
    public static KeyValuePair<string, RecipeValue> P(string key, string value) => KeyValuePair.Create(key, RecipeValue.FromText(value));
}
