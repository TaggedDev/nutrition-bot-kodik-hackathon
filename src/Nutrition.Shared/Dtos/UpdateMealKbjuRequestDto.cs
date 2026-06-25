namespace Nutrition.Shared.Dtos;

public sealed class UpdateMealKbjuRequestDto
{
    public Guid UserId { get; init; }

    public Guid MealEntryId { get; init; }

    public KbjuDto TotalKbju { get; init; } = new();
}
