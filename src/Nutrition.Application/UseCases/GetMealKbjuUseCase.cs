using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Application.Abstractions.UseCases;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.UseCases;

public sealed class GetMealKbjuUseCase : IGetMealKbjuUseCase
{
    private readonly IMealKbjuRepository _repository;

    public GetMealKbjuUseCase(IMealKbjuRepository repository)
    {
        _repository = repository;
    }

    public async Task<GetMealKbjuResponseDto?> ExecuteAsync(
        GetMealKbjuRequestDto request,
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

        return new GetMealKbjuResponseDto
        {
            Meal = meal
        };
    }
}
