
namespace JamWeaver.Core.Tests;

public sealed class MidiValueTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(16, 15)]
    public void Channel_converts_to_zero_based(int number, int expected) => Assert.Equal(expected, new MidiChannel(number).ZeroBased);

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void Channel_rejects_out_of_range_values(int value) => Assert.Throws<ArgumentOutOfRangeException>(() => new MidiChannel(value));

    [Theory]
    [InlineData(-1)]
    [InlineData(128)]
    public void Seven_bit_value_rejects_out_of_range_values(int value) => Assert.Throws<ArgumentOutOfRangeException>(() => new MidiValue(value));
}
