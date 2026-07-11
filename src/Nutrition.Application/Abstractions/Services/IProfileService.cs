using Nutrition.Core.Domain.Entities;
using Nutrition.Core.Domain.Enums;
using Nutrition.Shared.Dtos;

namespace Nutrition.Application.Abstractions.Services;

public interface IProfileService
{
    Task<ProfileResponseDto?> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ProfileResponseDto?> UpdateUserProfileAsync(Guid userId, UpdateProfileRequestDto request,
        CancellationToken cancellationToken = default);

    Task<ProfileHistoryResponseDto> GetUserHistoryAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<NutritionSummaryDto> GetUserDailySummaryAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<ProfileSummaryByTypeResponseDto> GetUserSummaryByMealTypeAsync(Guid userId,
        CancellationToken cancellationToken = default);

    Task<ProfileDayResponseDto> GetUserDayAsync(Guid userId, DateOnly date, int utcOffsetMinutes = 0,
        CancellationToken cancellationToken = default);

    Task<ProfileStatisticsResponseDto> GetUserStatisticsAsync(Guid userId, int rangeDays, DateOnly endDate,
        CancellationToken cancellationToken = default);

    Task<string> ExportUserDailyCsvAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<DeleteAccountRequestResponseDto> RequestUserAccountDeletionAsync(Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserDailyGoalDto?> GetUserDailyGoalAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserDailyGoalDto> CreateUserDailyGoalAsync(Guid userId, CreateDailyGoalRequestDto request,
        CancellationToken cancellationToken = default);

    Task<UserDailyGoalDto> UpdateUserDailyGoalAsync(Guid userId, UpdateDailyGoalRequestDto request,
        CancellationToken cancellationToken = default);

    Task<UserMealEntryDto> CreateUserMealEntryAsync(Guid userId, CreateUserMealEntryRequestDto request,
        CancellationToken cancellationToken = default);

    Task<UserMealEntryDto?> UpdateUserMealEntryAsync(Guid userId, Guid entryId, UpdateUserMealEntryRequestDto request,
        CancellationToken cancellationToken = default);

    Task DeleteUserMealEntryAsync(Guid userId, Guid entryId, CancellationToken cancellationToken = default);

    Task DeleteUserAccountAsync(Guid userId, CancellationToken cancellationToken = default);
}