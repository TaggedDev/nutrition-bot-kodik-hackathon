using Nutrition.Core.Domain.Entities;
using Nutrition.Core.Domain.Enums;
using Nutrition.Core.Domain.Exceptions;
using Nutrition.Core.Domain.ValueObjects;

namespace Nutrition.Core.Tests;

public sealed class MealDomainTests
{
    [Fact]
    public void MealEntry_RejectsEmptyIdentifiers()
    {
        Assert.Throws<DomainValidationException>(()
            => new MealEntry(Guid.Empty, Guid.NewGuid(), MealType.Lunch, DateTimeOffset.UtcNow, InputChannel.Text));
        Assert.Throws<DomainValidationException>(()
            => new MealEntry(Guid.NewGuid(), Guid.Empty, MealType.Lunch, DateTimeOffset.UtcNow, InputChannel.Text));
    }

    [Fact]
    public void MealEntry_CalculatesTotalNutritionAndConfirmationState()
    {
        var meal = new MealEntry(Guid.NewGuid(), Guid.NewGuid(), MealType.Dinner, DateTimeOffset.UtcNow,
            InputChannel.Text);
        var firstItem = new MealItem(Guid.NewGuid(), "Chicken breast", new Portion(200, PortionUnit.Gram),
            new NutritionFacts(330, 62, 7, 0), new ConfidenceScore(0.95m),
            new NutritionSource(NutritionSourceType.Usda, "USDA:171077"));
        var secondItem = new MealItem(Guid.NewGuid(), "Rice", new Portion(150, PortionUnit.Gram),
            new NutritionFacts(180, 4, 1, 38), new ConfidenceScore(0.65m),
            new NutritionSource(NutritionSourceType.OpenFoodFacts, "OFF:123"));

        meal.AddItem(firstItem);
        meal.AddItem(secondItem);

        Assert.Equal(510, meal.TotalNutrition.Calories);
        Assert.Equal(66, meal.TotalNutrition.Protein);
        Assert.Equal(8, meal.TotalNutrition.Fat);
        Assert.Equal(38, meal.TotalNutrition.Carbs);
        Assert.True(meal.RequiresUserConfirmation);

        meal.RemoveItem(firstItem.Id);

        Assert.Single(meal.Items);
        Assert.Equal(180, meal.TotalNutrition.Calories);
        Assert.True(meal.RequiresUserConfirmation);
    }

    [Fact]
    public void DailyNutritionGoal_DetectsExcess()
    {
        var goal = new DailyNutritionGoal(Guid.NewGuid(), new NutritionFacts(2000, 150, 70, 250));

        Assert.False(goal.IsExceededBy(new NutritionFacts(1800, 100, 60, 200)));
        Assert.True(goal.IsExceededBy(new NutritionFacts(2100, 100, 60, 200)));
        Assert.True(goal.IsExceededBy(new NutritionFacts(1800, 160, 60, 200)));
    }
}