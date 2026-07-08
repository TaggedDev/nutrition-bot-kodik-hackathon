namespace Nutrition.Shared.Dtos;

public sealed class GetMealNutritionRequestDto
{
    public Guid UserId { get; init; }

    public Guid MealEntryId { get; init; }
}