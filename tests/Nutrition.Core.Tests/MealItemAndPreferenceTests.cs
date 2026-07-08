using Nutrition.Core.Domain.Entities;
using Nutrition.Core.Domain.Enums;
using Nutrition.Core.Domain.Exceptions;
using Nutrition.Core.Domain.ValueObjects;

namespace Nutrition.Core.Tests;

public sealed class MealItemAndPreferenceTests
{
    [Fact]
    public void MealItem_TrimsProductName_AndAllowsUpdates()
    {
        var item = new MealItem(Guid.NewGuid(), "  Greek yogurt  ", new Portion(150, PortionUnit.Gram),
            new NutritionFacts(120, 10, 2, 8), new ConfidenceScore(0.8m),
            new NutritionSource(NutritionSourceType.UserProvided, "manual"));

        item.UpdatePortion(new Portion(200, PortionUnit.Gram), new NutritionFacts(160, 14, 3, 10));
        item.UpdateSource(new ConfidenceScore(0.6m),
            new NutritionSource(NutritionSourceType.LocalHistory, "history:1"));

        Assert.Equal("Greek yogurt", item.ProductName);
        Assert.Equal(200, item.Portion.Amount);
        Assert.Equal(160, item.Nutrition.Calories);
        Assert.True(item.RequiresConfirmation);
        Assert.Equal(NutritionSourceType.LocalHistory, item.Source.Type);
    }

    [Fact]
    public void FoodProduct_TrimsOptionalFields()
    {
        var product = new FoodProduct(Guid.NewGuid(), "  Barilla fusilli  ", new NutritionFacts(350, 12, 1.5m, 70),
            "  Barilla  ", "  12345  ");

        Assert.Equal("Barilla fusilli", product.Name);
        Assert.Equal("Barilla", product.Brand);
        Assert.Equal("12345", product.Barcode);
    }

    [Fact]
    public void UserFoodPreference_DeduplicatesCaseInsensitively()
    {
        var preferences = new UserFoodPreference(Guid.NewGuid(), isVegetarian: true, isVegan: false);

        preferences.AddAllergen("Peanut");
        preferences.AddAllergen(" peanut ");
        preferences.AddAllergen(" ");
        preferences.AddDislikedFood("Mushroom");
        preferences.AddDislikedFood(" mushroom ");

        Assert.Single(preferences.Allergens);
        Assert.Single(preferences.DislikedFoods);
    }
}