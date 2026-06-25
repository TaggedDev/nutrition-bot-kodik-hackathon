namespace Nutrition.Shared.Dtos;

public sealed class GetMealKbjuResponseDto
{
    public MealEntryDto Meal { get; init; } = new();
}
