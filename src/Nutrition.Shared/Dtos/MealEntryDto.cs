namespace Nutrition.Shared.Dtos;

public sealed class MealEntryDto
{
    public Guid MealEntryId { get; init; }

    public Guid UserId { get; init; }

    public string MealType { get; init; } = string.Empty;

    public DateTimeOffset LoggedAtUtc { get; init; }

    public NutritionDto TotalNutrition { get; init; } = new();

    public IReadOnlyCollection<MealItemDto> Items { get; init; } = Array.Empty<MealItemDto>();
}
