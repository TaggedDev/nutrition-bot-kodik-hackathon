using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Application.Abstractions.UseCases;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.UseCases;

public sealed class GetMealNutritionUseCase : IGetMealNutritionUseCase
{
    private readonly IMealNutritionRepository _repository;

    public GetMealNutritionUseCase(IMealNutritionRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetMealNutritionResponseDto?> ExecuteAsync(
        GetMealNutritionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty || request.MealEntryId == Guid.Empty)
        {
            return null;
        }

        var meal = await _repository.GetByIdAsync(request.UserId, request.MealEntryId, cancellationToken);
        if (meal is null)
        {
            return null;
        }

        return new GetMealNutritionResponseDto
        {
            Meal = meal
        };
    }
}
