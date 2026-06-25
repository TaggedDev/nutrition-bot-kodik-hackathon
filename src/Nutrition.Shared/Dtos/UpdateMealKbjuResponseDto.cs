namespace Nutrition.Shared.Dtos;

public sealed class UpdateMealKbjuResponseDto
{
    public Guid MealEntryId { get; init; }

    public KbjuDto TotalKbju { get; init; } = new();

    public DateTimeOffset UpdatedAtUtc { get; init; }
}
