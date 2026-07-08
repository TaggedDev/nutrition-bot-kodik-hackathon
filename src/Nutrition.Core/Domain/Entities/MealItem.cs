using Nutrition.Core.Domain.Exceptions;
using Nutrition.Core.Domain.ValueObjects;

namespace Nutrition.Core.Domain.Entities;

public sealed class MealItem
{
    public MealItem(Guid id, string productName, Portion portion, NutritionFacts nutrition, ConfidenceScore confidence,
        NutritionSource source)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Meal item id is required.");
        }

        if (string.IsNullOrWhiteSpace(productName))
        {
            throw new DomainValidationException("Product name is required.");
        }

        Id = id;
        ProductName = productName.Trim();
        Portion = portion;
        Nutrition = nutrition;
        Confidence = confidence;
        Source = source;
    }

    public Guid Id { get; }

    public string ProductName { get; }

    public Portion Portion { get; private set; }

    public NutritionFacts Nutrition { get; private set; }

    public ConfidenceScore Confidence { get; private set; }

    public NutritionSource Source { get; private set; }

    public bool RequiresConfirmation => Confidence.RequiresConfirmation;

    public void UpdatePortion(Portion portion, NutritionFacts recalculatedNutrition)
    {
        Portion = portion;
        Nutrition = recalculatedNutrition;
    }

    public void UpdateSource(ConfidenceScore confidence, NutritionSource source)
    {
        Confidence = confidence;
        Source = source;
    }
}