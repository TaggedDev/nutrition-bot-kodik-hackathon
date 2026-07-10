using Nutrition.Core.Domain.Entities;

namespace Nutrition.Application.Abstractions.Repositories;

public interface IUserDailyGoalRepository
{
    Task<UserDailyGoal?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(UserDailyGoal goal, CancellationToken cancellationToken = default);

    Task UpdateAsync(UserDailyGoal goal, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
