using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Abstractions.Repositories;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Core.Domain.Entities;
using Nutrition.Core.Domain.Enums;
using Nutrition.Shared.Dtos;

namespace Nutrition.Infrastructure.Identity;

public sealed class ProfileService : IProfileService
{
    private readonly NutritionIdentityDbContext _dbContext;
    private readonly IUserDailyGoalRepository _goalRepository;
    private readonly IUserMealEntryRepository _mealEntryRepository;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileService(
        IUserMealEntryRepository mealEntryRepository,
        IUserDailyGoalRepository goalRepository,
        UserManager<ApplicationUser> userManager,
        NutritionIdentityDbContext dbContext)
    {
        _mealEntryRepository = mealEntryRepository;
        _goalRepository = goalRepository;
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<ProfileResponseDto?> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user is null
            ? null
            : new ProfileResponseDto(user.Id, user.Email ?? string.Empty, user.FirstName, user.SecondName);
    }

    public async Task<ProfileHistoryResponseDto> GetUserHistoryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entries = await _mealEntryRepository.GetByUserIdAsync(userId, cancellationToken);
        return new ProfileHistoryResponseDto(entries.Select(ToDto).ToArray(), Sum(entries));
    }

    public async Task<NutritionSummaryDto> GetUserDailySummaryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entries = await _mealEntryRepository.GetByUserIdAndDateAsync(userId, DateTimeOffset.UtcNow, cancellationToken);
        return Sum(entries);
    }

    public async Task<ProfileSummaryByTypeResponseDto> GetUserSummaryByMealTypeAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entries = await _mealEntryRepository.GetByUserIdAsync(userId, cancellationToken);
        var summaryByType = entries
            .GroupBy(entry => entry.MealType)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var summary = Sum(group);
                return new MealEntrySummaryByTypeDto(
                    group.Key.ToString(),
                    summary.Calories,
                    summary.Protein,
                    summary.Fat,
                    summary.Carbs,
                    group.Count());
            })
            .ToArray();

        return new ProfileSummaryByTypeResponseDto(summaryByType, Sum(entries));
    }

    public async Task<UserDailyGoalDto?> GetUserDailyGoalAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByUserIdAsync(userId, cancellationToken);
        return goal is null ? null : ToDto(goal);
    }

    public async Task<UserDailyGoalDto> CreateUserDailyGoalAsync(Guid userId, CreateDailyGoalRequestDto request, CancellationToken cancellationToken = default)
    {
        var existing = await _goalRepository.GetByUserIdAsync(userId, cancellationToken);
        if (existing is not null)
        {
            existing.Update(request.TargetCalories, request.TargetProtein, request.TargetFat, request.TargetCarbs);
            await _goalRepository.UpdateAsync(existing, cancellationToken);
            await _goalRepository.SaveChangesAsync(cancellationToken);
            return ToDto(existing);
        }

        var goal = new UserDailyGoal(userId, request.TargetCalories, request.TargetProtein, request.TargetFat, request.TargetCarbs);
        await _goalRepository.AddAsync(goal, cancellationToken);
        await _goalRepository.SaveChangesAsync(cancellationToken);
        return ToDto(goal);
    }

    public async Task<UserDailyGoalDto> UpdateUserDailyGoalAsync(Guid userId, UpdateDailyGoalRequestDto request, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByUserIdAsync(userId, cancellationToken);
        if (goal is null)
        {
            goal = new UserDailyGoal(userId, request.TargetCalories, request.TargetProtein, request.TargetFat, request.TargetCarbs);
            await _goalRepository.AddAsync(goal, cancellationToken);
        }
        else
        {
            goal.Update(request.TargetCalories, request.TargetProtein, request.TargetFat, request.TargetCarbs);
            await _goalRepository.UpdateAsync(goal, cancellationToken);
        }

        await _goalRepository.SaveChangesAsync(cancellationToken);
        return ToDto(goal);
    }

    public async Task<UserMealEntryDto> CreateUserMealEntryAsync(Guid userId, CreateUserMealEntryRequestDto request, CancellationToken cancellationToken = default)
    {
        var mealType = ParseMealType(request.MealType);
        var entry = new UserMealEntry(
            Guid.NewGuid(),
            userId,
            request.ProductName,
            request.Brand ?? string.Empty,
            request.Calories,
            request.Protein,
            request.Fat,
            request.Carbs,
            mealType,
            DateTimeOffset.UtcNow);

        await _mealEntryRepository.AddAsync(entry, cancellationToken);
        await _mealEntryRepository.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task<UserMealEntryDto?> UpdateUserMealEntryAsync(Guid userId, Guid entryId, UpdateUserMealEntryRequestDto request, CancellationToken cancellationToken = default)
    {
        var entry = await _mealEntryRepository.GetByIdAsync(entryId, cancellationToken);
        if (entry is null || entry.UserId != userId)
        {
            return null;
        }

        entry.Update(
            request.ProductName,
            request.Brand ?? string.Empty,
            request.Calories,
            request.Protein,
            request.Fat,
            request.Carbs,
            ParseMealType(request.MealType));

        await _mealEntryRepository.SaveChangesAsync(cancellationToken);
        return ToDto(entry);
    }

    public async Task DeleteUserMealEntryAsync(Guid userId, Guid entryId, CancellationToken cancellationToken = default)
    {
        var entry = await _mealEntryRepository.GetByIdAsync(entryId, cancellationToken);
        if (entry is null || entry.UserId != userId)
        {
            return;
        }

        await _mealEntryRepository.DeleteAsync(entryId, cancellationToken);
        await _mealEntryRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteUserAccountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        var entries = _dbContext.UserMealEntries.Where(entry => entry.UserId == userId);
        _dbContext.UserMealEntries.RemoveRange(entries);

        var goal = await _dbContext.UserDailyGoals.FirstOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (goal is not null)
        {
            _dbContext.UserDailyGoals.Remove(goal);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _userManager.DeleteAsync(user);
    }

    private static UserMealEntryDto ToDto(UserMealEntry entry)
    {
        return new UserMealEntryDto(
            entry.Id,
            entry.ProductName,
            entry.Brand,
            entry.Calories,
            entry.Protein,
            entry.Fat,
            entry.Carbs,
            entry.MealType.ToString(),
            entry.CreatedAtUtc);
    }

    private static UserDailyGoalDto ToDto(UserDailyGoal goal)
    {
        return new UserDailyGoalDto(goal.TargetCalories, goal.TargetProtein, goal.TargetFat, goal.TargetCarbs);
    }

    private static NutritionSummaryDto Sum(IEnumerable<UserMealEntry> entries)
    {
        return new NutritionSummaryDto(
            entries.Sum(entry => entry.Calories),
            entries.Sum(entry => entry.Protein),
            entries.Sum(entry => entry.Fat),
            entries.Sum(entry => entry.Carbs));
    }

    private static MealType ParseMealType(string value)
    {
        return Enum.TryParse<MealType>(value, ignoreCase: true, out var mealType)
            ? mealType
            : MealType.Snack;
    }
}
