namespace Nutrition.Shared.Dtos;

public sealed class GetMealKbjuRequestDto
{
    public Guid UserId { get; init; }

    public Guid MealEntryId { get; init; }
}
