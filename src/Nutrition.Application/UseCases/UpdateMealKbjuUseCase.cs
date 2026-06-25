using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Application.Abstractions.UseCases;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.UseCases;

public sealed class UpdateMealKbjuUseCase : IUpdateMealKbjuUseCase
{
    private readonly IMealKbjuRepository _repository;

    public UpdateMealKbjuUseCase(IMealKbjuRepository repository)
    {
        _repository = repository;
    }

    public async Task<UpdateMealKbjuResponseDto?> ExecuteAsync(
        UpdateMealKbjuRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty || request.MealEntryId == Guid.Empty)
        {
            return null;
        }

        if (request.TotalKbju.Calories < 0
            || request.TotalKbju.Protein < 0
            || request.TotalKbju.Fat < 0
            || request.TotalKbju.Carbs < 0)
        {
            return null;
        }

        var updatedMeal = await _repository.UpdateTotalKbjuAsync(
            request.UserId,
            request.MealEntryId,
            request.TotalKbju,
            cancellationToken);

        return new UpdateMealKbjuResponseDto
        {
            MealEntryId = updatedMeal.MealEntryId,
            TotalKbju = updatedMeal.TotalKbju,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
