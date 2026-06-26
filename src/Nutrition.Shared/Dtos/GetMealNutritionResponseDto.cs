namespace Nutrition.Shared.Dtos;

public sealed class GetMealNutritionResponseDto
{
    public MealEntryDto Meal { get; init; } = new();
}
