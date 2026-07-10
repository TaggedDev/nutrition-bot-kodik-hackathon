using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text;
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

    public async Task<ProfileResponseDto?> UpdateUserProfileAsync(Guid userId, UpdateProfileRequestDto request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.Users.FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        user.FirstName = string.IsNullOrWhiteSpace(request.FirstName) ? user.FirstName : request.FirstName.Trim();
        user.SecondName = string.IsNullOrWhiteSpace(request.SecondName) ? user.SecondName : request.SecondName.Trim();

        await _userManager.UpdateAsync(user);
        return new ProfileResponseDto(user.Id, user.Email ?? string.Empty, user.FirstName, user.SecondName);
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

    public async Task<ProfileDayResponseDto> GetUserDayAsync(Guid userId, DateOnly date, int utcOffsetMinutes = 0, CancellationToken cancellationToken = default)
    {
        var offset = TimeSpan.FromMinutes(utcOffsetMinutes);
        var localStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), offset);
        var utcStart = localStart.ToUniversalTime();
        var utcEnd = utcStart.AddDays(1);
        var entries = await _mealEntryRepository.GetByUserIdAndRangeAsync(userId, utcStart, utcEnd, cancellationToken);
        var goal = await GetUserDailyGoalAsync(userId, cancellationToken);
        var meals = Enum.GetValues<MealType>()
            .Select(mealType =>
            {
                var mealEntries = entries
                    .Where(entry => entry.MealType == mealType)
                    .OrderByDescending(entry => entry.LoggedAtUtc)
                    .ToArray();

                return new MealEntriesByTypeDto(
                    mealType.ToString(),
                    mealEntries.Select(ToDto).ToArray(),
                    Sum(mealEntries));
            })
            .ToArray();

        return new ProfileDayResponseDto(date, goal, meals, Sum(entries));
    }

    public async Task<ProfileStatisticsResponseDto> GetUserStatisticsAsync(Guid userId, int rangeDays, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var normalizedRange = rangeDays is 14 or 30 ? rangeDays : 7;
        var startDate = endDate.AddDays(-(normalizedRange - 1));
        var utcStart = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var utcEnd = new DateTimeOffset(endDate.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var entries = await _mealEntryRepository.GetByUserIdAndRangeAsync(userId, utcStart, utcEnd, cancellationToken);
        var entriesByDate = entries
            .GroupBy(entry => DateOnly.FromDateTime(entry.LoggedAtUtc.UtcDateTime.Date))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var goal = await GetUserDailyGoalAsync(userId, cancellationToken);

        var items = Enumerable.Range(0, normalizedRange)
            .Select(offset =>
            {
                var date = startDate.AddDays(offset);
                if (!entriesByDate.TryGetValue(date, out var dayEntries) || dayEntries.Length == 0)
                {
                    return new ProfileStatisticsDayDto(date, 0, 0, 0, 0, 0, 0, 0, 0, false);
                }

                var summary = Sum(dayEntries);
                return new ProfileStatisticsDayDto(
                    date,
                    summary.Calories,
                    summary.Protein,
                    summary.Fat,
                    summary.Carbs,
                    SumMealCalories(dayEntries, MealType.Breakfast),
                    SumMealCalories(dayEntries, MealType.Lunch),
                    SumMealCalories(dayEntries, MealType.Dinner),
                    SumMealCalories(dayEntries, MealType.Snack),
                    true);
            })
            .ToArray();

        return new ProfileStatisticsResponseDto(goal?.TargetCalories ?? 2100, items);
    }

    public async Task<string> ExportUserDailyCsvAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entries = await _mealEntryRepository.GetByUserIdAsync(userId, cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("date,totalCalories,proteinGrams,fatGrams,carbsGrams,breakfastCalories,lunchCalories,dinnerCalories,snackCalories");

        foreach (var group in entries.GroupBy(entry => DateOnly.FromDateTime(entry.LoggedAtUtc.UtcDateTime.Date)).OrderBy(group => group.Key))
        {
            var dayEntries = group.ToArray();
            var summary = Sum(dayEntries);
            builder
                .Append(group.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(ToCsvNumber(summary.Calories)).Append(',')
                .Append(ToCsvNumber(summary.Protein)).Append(',')
                .Append(ToCsvNumber(summary.Fat)).Append(',')
                .Append(ToCsvNumber(summary.Carbs)).Append(',')
                .Append(ToCsvNumber(SumMealCalories(dayEntries, MealType.Breakfast))).Append(',')
                .Append(ToCsvNumber(SumMealCalories(dayEntries, MealType.Lunch))).Append(',')
                .Append(ToCsvNumber(SumMealCalories(dayEntries, MealType.Dinner))).Append(',')
                .Append(ToCsvNumber(SumMealCalories(dayEntries, MealType.Snack))).AppendLine();
        }

        return builder.ToString();
    }

    public Task<DeleteAccountRequestResponseDto> RequestUserAccountDeletionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new DeleteAccountRequestResponseDto(true, DateTimeOffset.UtcNow));
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
            existing.Update(
                request.TargetCalories,
                request.TargetProtein,
                request.TargetFat,
                request.TargetCarbs,
                request.BreakfastPercent ?? existing.BreakfastPercent,
                request.LunchPercent ?? existing.LunchPercent,
                request.DinnerPercent ?? existing.DinnerPercent,
                request.SnackPercent ?? existing.SnackPercent);
            await _goalRepository.UpdateAsync(existing, cancellationToken);
            await _goalRepository.SaveChangesAsync(cancellationToken);
            return ToDto(existing);
        }

        var goal = new UserDailyGoal(
            userId,
            request.TargetCalories,
            request.TargetProtein,
            request.TargetFat,
            request.TargetCarbs,
            request.BreakfastPercent ?? UserDailyGoal.DefaultBreakfastPercent,
            request.LunchPercent ?? UserDailyGoal.DefaultLunchPercent,
            request.DinnerPercent ?? UserDailyGoal.DefaultDinnerPercent,
            request.SnackPercent ?? UserDailyGoal.DefaultSnackPercent);
        await _goalRepository.AddAsync(goal, cancellationToken);
        await _goalRepository.SaveChangesAsync(cancellationToken);
        return ToDto(goal);
    }

    public async Task<UserDailyGoalDto> UpdateUserDailyGoalAsync(Guid userId, UpdateDailyGoalRequestDto request, CancellationToken cancellationToken = default)
    {
        var goal = await _goalRepository.GetByUserIdAsync(userId, cancellationToken);
        if (goal is null)
        {
            goal = new UserDailyGoal(
                userId,
                request.TargetCalories,
                request.TargetProtein,
                request.TargetFat,
                request.TargetCarbs,
                request.BreakfastPercent ?? UserDailyGoal.DefaultBreakfastPercent,
                request.LunchPercent ?? UserDailyGoal.DefaultLunchPercent,
                request.DinnerPercent ?? UserDailyGoal.DefaultDinnerPercent,
                request.SnackPercent ?? UserDailyGoal.DefaultSnackPercent);
            await _goalRepository.AddAsync(goal, cancellationToken);
        }
        else
        {
            goal.Update(
                request.TargetCalories,
                request.TargetProtein,
                request.TargetFat,
                request.TargetCarbs,
                request.BreakfastPercent ?? goal.BreakfastPercent,
                request.LunchPercent ?? goal.LunchPercent,
                request.DinnerPercent ?? goal.DinnerPercent,
                request.SnackPercent ?? goal.SnackPercent);
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
            Math.Max(0, request.ServingGrams),
            request.PortionLabel,
            request.SourceType,
            request.SourceReference,
            request.LoggedAtUtc ?? DateTimeOffset.UtcNow,
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
            ParseMealType(request.MealType),
            Math.Max(0, request.ServingGrams),
            request.PortionLabel,
            request.SourceType,
            request.SourceReference,
            request.LoggedAtUtc ?? entry.LoggedAtUtc);

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
            entry.ServingGrams,
            entry.PortionLabel,
            entry.SourceType,
            entry.SourceReference,
            entry.LoggedAtUtc,
            entry.CreatedAtUtc);
    }

    private static UserDailyGoalDto ToDto(UserDailyGoal goal)
    {
        return new UserDailyGoalDto(
            goal.TargetCalories,
            goal.TargetProtein,
            goal.TargetFat,
            goal.TargetCarbs,
            goal.BreakfastPercent,
            goal.LunchPercent,
            goal.DinnerPercent,
            goal.SnackPercent);
    }

    private static NutritionSummaryDto Sum(IEnumerable<UserMealEntry> entries)
    {
        return new NutritionSummaryDto(
            entries.Sum(entry => entry.Calories),
            entries.Sum(entry => entry.Protein),
            entries.Sum(entry => entry.Fat),
            entries.Sum(entry => entry.Carbs));
    }

    private static decimal SumMealCalories(IEnumerable<UserMealEntry> entries, MealType mealType)
    {
        return entries.Where(entry => entry.MealType == mealType).Sum(entry => entry.Calories);
    }

    private static string ToCsvNumber(decimal value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static MealType ParseMealType(string value)
    {
        return Enum.TryParse<MealType>(value, ignoreCase: true, out var mealType)
            ? mealType
            : MealType.Snack;
    }
}
