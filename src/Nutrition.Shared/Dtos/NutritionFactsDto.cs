namespace Nutrition.Shared.Dtos;

public sealed class NutritionFactsDto
{
    public decimal Calories { get; init; }

    public decimal Protein { get; init; }

    public decimal Fat { get; init; }

    public decimal Carbs { get; init; }
}