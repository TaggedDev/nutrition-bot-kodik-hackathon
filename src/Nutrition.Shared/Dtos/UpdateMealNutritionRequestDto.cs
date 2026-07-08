namespace Nutrition.Shared.Dtos;

public sealed class UpdateMealNutritionRequestDto
{
    public Guid UserId { get; init; }

    public Guid MealEntryId { get; init; }

    public NutritionDto TotalNutrition { get; init; } = new();
}