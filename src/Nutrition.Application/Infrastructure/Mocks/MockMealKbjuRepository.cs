using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Infrastructure.Mocks;

public sealed class MockMealKbjuRepository : IMealKbjuRepository
{
    private readonly Dictionary<Guid, MealEntryDto> _storage;

    public MockMealKbjuRepository()
    {
        var mealId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var userId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        _storage = new Dictionary<Guid, MealEntryDto>
        {
            [mealId] = new MealEntryDto
            {
                MealEntryId = mealId,
                UserId = userId,
                MealType = "Lunch",
                LoggedAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
                TotalKbju = new KbjuDto
                {
                    Calories = 620,
                    Protein = 42,
                    Fat = 20,
                    Carbs = 58
                },
                Items = new[]
                {
                    new MealItemDto
                    {
                        ItemId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                        ProductName = "Chicken breast",
                        PortionAmount = 200,
                        PortionUnit = "Gram",
                        ConfidenceScore = 0.95m,
                        SourceType = "Usda",
                        SourceReference = "USDA:171077",
                        Kbju = new KbjuDto
                        {
                            Calories = 330,
                            Protein = 62,
                            Fat = 7,
                            Carbs = 0
                        }
                    }
                }
            }
        };
    }

    public Task<MealEntryDto?> GetByIdAsync(Guid userId, Guid mealEntryId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_storage.TryGetValue(mealEntryId, out var meal))
        {
            return Task.FromResult<MealEntryDto?>(null);
        }

        if (meal.UserId != userId)
        {
            return Task.FromResult<MealEntryDto?>(null);
        }

        return Task.FromResult<MealEntryDto?>(meal);
    }

    public Task<MealEntryDto> UpdateTotalKbjuAsync(
        Guid userId,
        Guid mealEntryId,
        KbjuDto totalKbju,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_storage.TryGetValue(mealEntryId, out var existing) && existing.UserId == userId)
        {
            var updated = new MealEntryDto
            {
                MealEntryId = existing.MealEntryId,
                UserId = existing.UserId,
                MealType = existing.MealType,
                LoggedAtUtc = existing.LoggedAtUtc,
                Items = existing.Items,
                TotalKbju = totalKbju
            };

            _storage[mealEntryId] = updated;
            return Task.FromResult(updated);
        }

        var created = new MealEntryDto
        {
            MealEntryId = mealEntryId,
            UserId = userId,
            MealType = "Snack",
            LoggedAtUtc = DateTimeOffset.UtcNow,
            Items = Array.Empty<MealItemDto>(),
            TotalKbju = totalKbju
        };

        _storage[mealEntryId] = created;
        return Task.FromResult(created);
    }
}
