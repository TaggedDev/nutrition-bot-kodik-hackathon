using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Application.UseCases;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Tests;

public sealed class UpdateMealNutritionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsNull_WhenRequestIsInvalid()
    {
        var repository = new RecordingMealNutritionRepository();
        var useCase = new UpdateMealNutritionUseCase(repository);

        var result = await useCase.ExecuteAsync(new UpdateMealNutritionRequestDto(), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsNull_WhenNutritionIsNegative()
    {
        var repository = new RecordingMealNutritionRepository();
        var useCase = new UpdateMealNutritionUseCase(repository);

        var result = await useCase.ExecuteAsync(
            new UpdateMealNutritionRequestDto
            {
                UserId = Guid.NewGuid(),
                MealEntryId = Guid.NewGuid(),
                TotalNutrition = new NutritionDto { Calories = -1 }
            },
            CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, repository.UpdateCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsUpdatedMealAndTimestamp()
    {
        var repository = new RecordingMealNutritionRepository();
        var useCase = new UpdateMealNutritionUseCase(repository);
        var request = new UpdateMealNutritionRequestDto
        {
            UserId = Guid.NewGuid(),
            MealEntryId = Guid.NewGuid(),
            TotalNutrition = new NutritionDto { Calories = 620, Protein = 42, Fat = 20, Carbs = 58 }
        };

        var before = DateTimeOffset.UtcNow;
        var result = await useCase.ExecuteAsync(request, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        Assert.NotNull(result);
        Assert.Equal(request.MealEntryId, result!.MealEntryId);
        Assert.Equal(request.TotalNutrition.Calories, result.TotalNutrition.Calories);
        Assert.InRange(result.UpdatedAtUtc, before.AddSeconds(-1), after.AddSeconds(1));
        Assert.Equal(1, repository.UpdateCalls);
    }

    private sealed class RecordingMealNutritionRepository : IMealNutritionRepository
    {
        public int GetCalls { get; private set; }
        public int UpdateCalls { get; private set; }

        public Task<MealEntryDto?> GetByIdAsync(Guid userId, Guid mealEntryId, CancellationToken cancellationToken)
        {
            GetCalls++;
            return Task.FromResult<MealEntryDto?>(null);
        }

        public Task<MealEntryDto> UpdateTotalNutritionAsync(Guid userId, Guid mealEntryId, NutritionDto totalNutrition, CancellationToken cancellationToken)
        {
            UpdateCalls++;
            return Task.FromResult(new MealEntryDto
            {
                MealEntryId = mealEntryId,
                UserId = userId,
                MealType = "Snack",
                LoggedAtUtc = DateTimeOffset.UtcNow,
                TotalNutrition = totalNutrition
            });
        }
    }
}
