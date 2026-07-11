using Nutrition.Core.Domain.Entities;
using Nutrition.Core.Domain.Enums;

namespace Nutrition.Application.Abstractions.Repositories;

public interface IUserMealEntryRepository
{
    Task<UserMealEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserMealEntry>> GetByUserIdAsync(Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserMealEntry>> GetByUserIdAndMealTypeAsync(Guid userId, MealType mealType,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserMealEntry>> GetByUserIdAndDateAsync(Guid userId, DateTimeOffset date,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<UserMealEntry>> GetByUserIdAndRangeAsync(Guid userId, DateTimeOffset startUtc,
        DateTimeOffset endUtc, CancellationToken cancellationToken = default);

    Task AddAsync(UserMealEntry entry, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}