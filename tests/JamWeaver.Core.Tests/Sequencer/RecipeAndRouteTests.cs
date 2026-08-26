
namespace JamWeaver.Core.Tests.Sequencer;

public sealed class RecipeAndRouteTests
{
    [Fact]
    public void Recipe_parameters_are_sorted_and_typed()
    {
        var recipe = new GeneratorRecipe("test", 1, 42, null,
        [
            KeyValuePair.Create("role", RecipeValue.FromText("Bass")),
            KeyValuePair.Create("density", RecipeValue.FromNumber(.5)),
            KeyValuePair.Create("hits", RecipeValue.FromInteger(5))
        ]);
        Assert.Equal(["density", "hits", "role"], recipe.Parameters.Keys);
        Assert.Equal(RecipeValueKind.Integer, recipe.Parameters["hits"].Kind);
    }

    [Fact]
    public void Recipe_rejects_ordinal_duplicate_keys()
    {
        var parameters = new[]
        {
            KeyValuePair.Create("hits", RecipeValue.FromInteger(4)),
            KeyValuePair.Create("hits", RecipeValue.FromInteger(5))
        };
        Assert.Throws<ArgumentException>(() => new GeneratorRecipe("test", 1, 1, null, parameters));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void Numeric_recipe_value_must_be_finite(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => RecipeValue.FromNumber(value));

    [Fact]
    public void Route_is_device_independent_value()
    {
        var route = new MidiRoute("  E-MU Xmidi 2x2  ", new MidiChannel(10));
        Assert.Equal("E-MU Xmidi 2x2", route.OutputPortName);
        Assert.Equal(10, route.Channel.Number);
    }
}
