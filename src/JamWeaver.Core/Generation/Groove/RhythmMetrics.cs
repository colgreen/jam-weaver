using System.Numerics;

namespace JamWeaver.Core.Generation.Groove;

public readonly record struct RhythmFeatureVector(int Version, int HitCount, double Density,
    int WeightedSyncopation, int DownbeatStrength, int MaximumRestGap, int MaximumOnsetCluster,
    int HalfBalance, int HammingDistance, int MovementDistance);

public static class RhythmMetrics
{
    public const int Version = 1;
    public const int AddRemovePenalty = 4;
    private static readonly int[] Strength = [4, 0, 1, 0, 3, 0, 1, 0, 4, 0, 1, 0, 3, 0, 1, 0];

    public static RhythmFeatureVector Measure(ushort mask, ushort reference = 0)
    {
        var hits = BitOperations.PopCount(mask);
        var sync = 0;
        for (var step = 0; step < 16; step++)
        {
            if (!Has(mask, step)) continue;
            for (var look = 1; look <= 3; look++)
            {
                var next = (step + look) & 15;
                if (Has(mask, next)) break;
                if (Strength[next] > Strength[step]) { sync += Strength[next] - Strength[step]; break; }
            }
        }
        var downbeats = Enumerable.Range(0, 4).Sum(beat => Has(mask, beat * 4) ? Strength[beat * 4] : 0);
        return new(Version, hits, hits / 16d, sync, downbeats, MaximumRun(mask, false),
            MaximumRun(mask, true), Math.Abs(BitOperations.PopCount((uint)(mask & 0xff)) - BitOperations.PopCount((uint)(mask >> 8))),
            BitOperations.PopCount((uint)(mask ^ reference)), DirectedMovement(reference, mask));
    }

    public static int DirectedMovement(ushort source, ushort target)
    {
        var a = Steps(source); var b = Steps(target);
        if (a.Length == 0 || b.Length == 0) return Math.Max(a.Length, b.Length) * AddRemovePenalty;
        if (a.Length > b.Length) return DirectedMovement(target, source);
        var states = new Dictionary<int, int> { [0] = 0 };
        foreach (var from in a)
        {
            var next = new Dictionary<int, int>();
            foreach (var state in states)
            for (var j = 0; j < b.Length; j++)
            {
                if ((state.Key & (1 << j)) != 0) continue;
                var distance = Math.Abs(from - b[j]); distance = Math.Min(distance, 16 - distance);
                var key = state.Key | (1 << j); var score = state.Value + distance;
                if (!next.TryGetValue(key, out var old) || score < old) next[key] = score;
            }
            states = next;
        }
        return states.Values.Min() + ((b.Length - a.Length) * AddRemovePenalty);
    }

    private static int[] Steps(ushort mask) => Enumerable.Range(0, 16).Where(i => Has(mask, i)).ToArray();
    private static bool Has(ushort mask, int step) => (mask & (1 << step)) != 0;
    private static int MaximumRun(ushort mask, bool onsets)
    {
        if ((onsets && mask == ushort.MaxValue) || (!onsets && mask == 0)) return 16;
        var best = 0; var current = 0;
        for (var i = 0; i < 32; i++)
        {
            if (Has(mask, i & 15) == onsets) { current++; best = Math.Max(best, Math.Min(current, 16)); }
            else current = 0;
        }
        return best;
    }
}
