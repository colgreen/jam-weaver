using Redzen.Random;

namespace JamWeaver.Core.Tests.Generation;

public sealed class GenerationPrimitiveTests
{
    [Theory]
    [InlineData(-.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Normalized_amount_rejects_invalid_values(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new NormalizedAmount(value));

    [Fact]
    public void Redzen_seeded_sources_have_identical_sequences()
    {
        var first = RandomDefaults.CreateRandomSource(123UL);
        var second = RandomDefaults.CreateRandomSource(123UL);
        Assert.Equal(Enumerable.Range(0, 16).Select(_ => first.NextUInt()),
            Enumerable.Range(0, 16).Select(_ => second.NextUInt()));
    }

    [Theory]
    [InlineData(MusicalRole.Bass, 36, 52)]
    [InlineData(MusicalRole.Middle, 48, 72)]
    [InlineData(MusicalRole.High, 67, 88)]
    public void Every_resolved_role_pitch_stays_in_range(MusicalRole role, int minimum, int maximum)
    {
        foreach (var palette in Enum.GetValues<PitchPalette>())
        foreach (var root in Enumerable.Range(0, 12))
        foreach (var pitch in PentatonicPitchResolver.ValidPitches(new TonalContext(new RootPitchClass(root), palette), role))
        {
            var note = PentatonicPitchResolver.Resolve(pitch, new TonalContext(new RootPitchClass(root), palette), role);
            Assert.InRange(note.Value, minimum, maximum);
        }
    }

    [Theory]
    [InlineData(16, 5)]
    [InlineData(16, 16)]
    [InlineData(7, 3)]
    public void Euclidean_rhythm_has_requested_hits_and_downbeat_rotation(int steps, int hits)
    {
        var random = RandomDefaults.CreateRandomSource(42UL);
        var rotation = EuclideanRhythm.ChooseDownbeatRotation(steps, hits, random);
        var rhythm = EuclideanRhythm.Create(steps, hits, rotation);
        Assert.Equal(hits, rhythm.Count(hit => hit));
        Assert.True(rhythm[0]);
    }
}
