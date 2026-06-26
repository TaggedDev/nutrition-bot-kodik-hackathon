using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Application.Abstractions.UseCases;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.UseCases;

public sealed class UpdateMealNutritionUseCase : IUpdateMealNutritionUseCase
{
    private readonly IMealNutritionRepository _repository;

    public UpdateMealNutritionUseCase(IMealNutritionRepository repository)
    {
        _repository = repository;
    }

    public async Task<UpdateMealNutritionResponseDto?> ExecuteAsync(
        UpdateMealNutritionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty || request.MealEntryId == Guid.Empty)
        {
            return null;
        }

        if (request.TotalNutrition.Calories < 0
            || request.TotalNutrition.Protein < 0
            || request.TotalNutrition.Fat < 0
            || request.TotalNutrition.Carbs < 0)
        {
            return null;
        }

        var updatedMeal = await _repository.UpdateTotalKbjuAsync(
            request.UserId,
            request.MealEntryId,
            request.TotalNutrition,
            cancellationToken);

        return new UpdateMealNutritionResponseDto
        {
            MealEntryId = updatedMeal.MealEntryId,
            TotalNutrition = updatedMeal.TotalNutrition,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
