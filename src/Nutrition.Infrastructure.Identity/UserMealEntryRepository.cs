using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Core.Domain.Entities;
using Nutrition.Core.Domain.Enums;

namespace Nutrition.Infrastructure.Identity;

public sealed class UserMealEntryRepository : IUserMealEntryRepository
{
    private readonly NutritionIdentityDbContext _context;

    public UserMealEntryRepository(NutritionIdentityDbContext context)
    {
        _context = context;
    }

    public async Task<UserMealEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.UserMealEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserMealEntry>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserMealEntries
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserMealEntry>> GetByUserIdAndMealTypeAsync(Guid userId, MealType mealType, CancellationToken cancellationToken = default)
    {
        return await _context.UserMealEntries
            .Where(e => e.UserId == userId && e.MealType == mealType)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<UserMealEntry>> GetByUserIdAndDateAsync(Guid userId, DateTimeOffset date, CancellationToken cancellationToken = default)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        return await _context.UserMealEntries
            .Where(e => e.UserId == userId && e.CreatedAtUtc >= startOfDay && e.CreatedAtUtc < endOfDay)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(UserMealEntry entry, CancellationToken cancellationToken = default)
    {
        await _context.UserMealEntries.AddAsync(entry, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entry = await GetByIdAsync(id, cancellationToken);
        if (entry is not null)
        {
            _context.UserMealEntries.Remove(entry);
        }
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
