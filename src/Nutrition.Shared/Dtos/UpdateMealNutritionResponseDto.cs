namespace Nutrition.Shared.Dtos;

public sealed class UpdateMealNutritionResponseDto
{
    public Guid MealEntryId { get; init; }

    public NutritionDto TotalNutrition { get; init; } = new();

    public DateTimeOffset UpdatedAtUtc { get; init; }
}
