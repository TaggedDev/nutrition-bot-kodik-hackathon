using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Abstractions.Repositories;

public interface IMealNutritionRepository
{
    Task<MealEntryDto?> GetByIdAsync(Guid userId, Guid mealEntryId, CancellationToken cancellationToken);

    Task<MealEntryDto> UpdateTotalNutritionAsync(
        Guid userId,
        Guid mealEntryId,
        NutritionDto totalNutrition,
        CancellationToken cancellationToken);
}
