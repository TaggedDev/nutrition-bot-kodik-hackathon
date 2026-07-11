using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Core.Domain.Entities;

namespace Nutrition.Infrastructure.Identity;

public sealed class UserDailyGoalRepository : IUserDailyGoalRepository
{
    private readonly NutritionIdentityDbContext _context;

    public UserDailyGoalRepository(NutritionIdentityDbContext context)
    {
        _context = context;
    }

    public async Task<UserDailyGoal?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserDailyGoals.FirstOrDefaultAsync(g => g.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(UserDailyGoal goal, CancellationToken cancellationToken = default)
    {
        await _context.UserDailyGoals.AddAsync(goal, cancellationToken);
    }

    public async Task UpdateAsync(UserDailyGoal goal, CancellationToken cancellationToken = default)
    {
        _context.UserDailyGoals.Update(goal);
        await Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}