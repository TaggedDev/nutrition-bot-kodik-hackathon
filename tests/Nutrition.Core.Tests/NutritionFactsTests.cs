using Nutrition.Core.Domain.Exceptions;
using Nutrition.Core.Domain.ValueObjects;

namespace Nutrition.Core.Tests;

public sealed class NutritionFactsTests
{
    [Fact]
    public void Constructor_Throws_WhenAnyValueIsNegative()
    {
        Assert.Throws<DomainValidationException>(() => new NutritionFacts(-1, 0, 0, 0));
        Assert.Throws<DomainValidationException>(() => new NutritionFacts(0, -1, 0, 0));
        Assert.Throws<DomainValidationException>(() => new NutritionFacts(0, 0, -1, 0));
        Assert.Throws<DomainValidationException>(() => new NutritionFacts(0, 0, 0, -1));
    }

    [Fact]
    public void Add_SumsAllMacros()
    {
        var left = new NutritionFacts(100, 10, 5, 20);
        var right = new NutritionFacts(50, 3, 1, 4);

        var result = left.Add(right);

        Assert.Equal(150, result.Calories);
        Assert.Equal(13, result.Protein);
        Assert.Equal(6, result.Fat);
        Assert.Equal(24, result.Carbs);
    }

    [Fact]
    public void Multiply_ScalesMacros()
    {
        var facts = new NutritionFacts(100, 10, 5, 20);

        var result = facts.Multiply(1.5m);

        Assert.Equal(150, result.Calories);
        Assert.Equal(15, result.Protein);
        Assert.Equal(7.5m, result.Fat);
        Assert.Equal(30, result.Carbs);
    }
}
