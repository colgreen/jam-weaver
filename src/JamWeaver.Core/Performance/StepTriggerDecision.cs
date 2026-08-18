using JamWeaver.Core.Sequencer;

namespace JamWeaver.Core.Performance;

public static class StepTriggerDecision
{
    public static bool ShouldTrigger(Pattern pattern, ulong loopIndex, int stepIndex)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        if ((uint)stepIndex >= (uint)pattern.Steps.Length) throw new ArgumentOutOfRangeException(nameof(stepIndex));
        return ShouldTrigger(PatternTriggerKey.Create(pattern), pattern.Steps[stepIndex], loopIndex, stepIndex);
    }

    public static bool ShouldTrigger(ulong triggerKey, PatternStep step, ulong loopIndex, int stepIndex)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (stepIndex < 0) throw new ArgumentOutOfRangeException(nameof(stepIndex));
        var probability = step.Probability.Value;
        if (probability <= 0) return false;
        if (probability >= 1) return true;

        var hash = triggerKey;
        hash = Mix(hash, loopIndex);
        hash = Mix(hash, (ulong)stepIndex);
        hash = PatternTriggerKey.Avalanche(hash);
        var sample = (hash >> 11) * (1.0 / (1UL << 53));
        return sample < probability;
    }

    private static ulong Mix(ulong hash, ulong value)
    {
        hash ^= value;
        return hash * 1099511628211UL;
    }

}
