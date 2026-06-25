using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Abstractions.Repositories;

public interface IMealKbjuRepository
{
    Task<MealEntryDto?> GetByIdAsync(Guid userId, Guid mealEntryId, CancellationToken cancellationToken);

    Task<MealEntryDto> UpdateTotalKbjuAsync(
        Guid userId,
        Guid mealEntryId,
        KbjuDto totalKbju,
        CancellationToken cancellationToken);
}
