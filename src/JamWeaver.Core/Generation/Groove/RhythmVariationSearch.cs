using System.Numerics;
using JamWeaver.Core.Generation.Phrase;
using Redzen.Random;

namespace JamWeaver.Core.Generation.Groove;

public readonly record struct RhythmVariationResult(ushort Mask, RhythmFeatureVector Features, string Relaxation);

public static class RhythmVariationSearch
{
    public const int CandidateCount = 64;

    public static RhythmVariationResult Find(ushort source, GrooveTemplate template, GrooveSimilarity similarity,
        PhraseActivity activity, bool latterHalfOnly, IRandomSource random)
    {
        var targetHits = activity switch { PhraseActivity.Sparse => 4, PhraseActivity.Medium => 6, PhraseActivity.Busy => 8, _ => 6 };
        var (minDistance, maxDistance) = similarity switch
        {
            GrooveSimilarity.Close => (1, 3), GrooveSimilarity.Related => (3, 7), GrooveSimilarity.Contrast => (6, 12), _ => (1, 7)
        };
        var allowed = latterHalfOnly ? (ushort)0xff00 : (ushort)0xffff;
        var candidates = new List<(ushort Mask, RhythmFeatureVector Features, int Score)>();
        for (var i = 0; i < CandidateCount; i++)
        {
            var mask = source;
            var edits = similarity switch { GrooveSimilarity.Close => 1, GrooveSimilarity.Related => 2 + random.Next(2), _ => 3 + random.Next(3) };
            for (var edit = 0; edit < edits; edit++)
            {
                var removable = Enumerable.Range(0, 16).Where(s => Has(mask, s) && !Has(template.RequiredOnsets, s)
                    && (Has(template.MovableOnsets, s) || !Has(template.Onsets, s)) && Has(allowed, s)).ToArray();
                var rests = Enumerable.Range(0, 16).Where(s => !Has(mask, s) && Has(allowed, s)).ToArray();
                if (removable.Length > 0 && rests.Length > 0)
                {
                    mask &= (ushort)~(1 << removable[random.Next(removable.Length)]);
                    mask |= (ushort)(1 << rests[random.Next(rests.Length)]);
                }
            }
            while (BitOperations.PopCount(mask) < targetHits)
            {
                var rests = Enumerable.Range(0, 16).Where(s => !Has(mask, s) && Has(allowed, s)).ToArray();
                if (rests.Length == 0) break; mask |= (ushort)(1 << rests[random.Next(rests.Length)]);
            }
            while (BitOperations.PopCount(mask) > targetHits)
            {
                var removable = Enumerable.Range(0, 16).Where(s => Has(mask, s) && !Has(template.RequiredOnsets, s)
                    && (Has(template.MovableOnsets, s) || !Has(template.Onsets, s)) && Has(allowed, s)).ToArray();
                if (removable.Length == 0) break; mask &= (ushort)~(1 << removable[random.Next(removable.Length)]);
            }
            mask |= template.RequiredOnsets;
            var features = RhythmMetrics.Measure(mask, source);
            if (features.MaximumRestGap <= 8 && features.MaximumOnsetCluster <= 4)
            {
                var distancePenalty = features.HammingDistance < minDistance ? (minDistance - features.HammingDistance) * 20
                    : features.HammingDistance > maxDistance ? (features.HammingDistance - maxDistance) * 20 : 0;
                var score = distancePenalty + Math.Abs(features.HitCount - targetHits) * 12 + features.HalfBalance * 2
                    + Math.Abs(features.WeightedSyncopation - RhythmMetrics.Measure(source).WeightedSyncopation);
                candidates.Add((mask, features, score));
            }
        }
        var inBand = candidates.Where(c => c.Features.HammingDistance >= minDistance && c.Features.HammingDistance <= maxDistance).ToArray();
        var pool = inBand.Length > 0 ? inBand : candidates.ToArray();
        if (pool.Length == 0) return new(source, RhythmMetrics.Measure(source, source), "source-fallback");
        var winner = pool.OrderBy(c => c.Score).ThenBy(c => c.Mask).First();
        return new(winner.Mask, winner.Features, inBand.Length > 0 ? "none" : "distance-band");
    }

    private static bool Has(ushort mask, int step) => (mask & (1 << step)) != 0;
}
