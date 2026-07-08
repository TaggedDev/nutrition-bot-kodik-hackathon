using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Application.UseCases;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Tests;

public sealed class GetMealNutritionUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsNull_WhenRequestIsInvalid()
    {
        var repository = new RecordingMealNutritionRepository();
        var useCase = new GetMealNutritionUseCase(repository);

        var result = await useCase.ExecuteAsync(new GetMealNutritionRequestDto(), CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, repository.GetCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsMeal_WhenRepositoryFindsEntry()
    {
        var meal = new MealEntryDto
        {
            MealEntryId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MealType = "Lunch",
            LoggedAtUtc = DateTimeOffset.UtcNow,
            TotalNutrition = new NutritionDto { Calories = 500, Protein = 40, Fat = 10, Carbs = 50 }
        };
        var repository = new RecordingMealNutritionRepository(meal);
        var useCase = new GetMealNutritionUseCase(repository);
        var request = new GetMealNutritionRequestDto { UserId = meal.UserId, MealEntryId = meal.MealEntryId };

        var result = await useCase.ExecuteAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Same(meal, result!.Meal);
        Assert.Equal(1, repository.GetCalls);
    }

    private sealed class RecordingMealNutritionRepository : IMealNutritionRepository
    {
        private readonly MealEntryDto? _meal;

        public RecordingMealNutritionRepository(MealEntryDto? meal = null)
        {
            _meal = meal;
        }

        public int GetCalls { get; private set; }
        public int UpdateCalls { get; private set; }

        public Task<MealEntryDto?> GetByIdAsync(Guid userId, Guid mealEntryId, CancellationToken cancellationToken)
        {
            GetCalls++;
            return Task.FromResult(_meal);
        }

        public Task<MealEntryDto> UpdateTotalNutritionAsync(Guid userId, Guid mealEntryId, NutritionDto totalNutrition,
            CancellationToken cancellationToken)
        {
            UpdateCalls++;
            return Task.FromResult(_meal ?? new MealEntryDto
            {
                MealEntryId = mealEntryId,
                UserId = userId,
                MealType = "Lunch",
                LoggedAtUtc = DateTimeOffset.UtcNow,
                TotalNutrition = totalNutrition
            });
        }
    }
}