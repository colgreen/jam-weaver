namespace JamWeaver.Core.Generation;

public static class EuclideanRhythm
{
    public static bool[] Create(int steps, int hits, int rotation = 0)
    {
        if (steps is < 1 or > 256) throw new ArgumentOutOfRangeException(nameof(steps));
        if (hits is < 1 || hits > steps) throw new ArgumentOutOfRangeException(nameof(hits));
        rotation = ((rotation % steps) + steps) % steps;
        var rhythm = new bool[steps];
        for (var i = 0; i < steps; i++) rhythm[(i + rotation) % steps] = ((i * hits) % steps) < hits;
        return rhythm;
    }

    public static int ChooseDownbeatRotation(int steps, int hits, Redzen.Random.IRandomSource random)
    {
        var rotations = Enumerable.Range(0, steps).Where(rotation => Create(steps, hits, rotation)[0]).ToArray();
        return rotations[random.Next(rotations.Length)];
    }
}
