using Nutrition.Core.Domain.Exceptions;

namespace Nutrition.Core.Domain.ValueObjects;

public sealed record NutritionFacts
{
    public static readonly NutritionFacts Zero = new(0, 0, 0, 0);

    public NutritionFacts(decimal calories, decimal protein, decimal fat, decimal carbs)
    {
        if (calories < 0 || protein < 0 || fat < 0 || carbs < 0)
        {
            throw new DomainValidationException("Nutrition values must not be negative.");
        }

        Calories = calories;
        Protein = protein;
        Fat = fat;
        Carbs = carbs;
    }

    public decimal Calories { get; }

    public decimal Protein { get; }

    public decimal Fat { get; }

    public decimal Carbs { get; }

    public NutritionFacts Add(NutritionFacts other)
    {
        return new NutritionFacts(Calories + other.Calories, Protein + other.Protein, Fat + other.Fat,
            Carbs + other.Carbs);
    }

    public NutritionFacts Multiply(decimal factor)
    {
        if (factor <= 0)
        {
            throw new DomainValidationException("Nutrition multiplier must be greater than zero.");
        }

        return new NutritionFacts(Calories * factor, Protein * factor, Fat * factor, Carbs * factor);
    }
}