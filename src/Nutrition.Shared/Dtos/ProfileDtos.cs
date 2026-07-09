namespace Nutrition.Shared.Dtos;

public sealed record NutritionSummaryDto(
    decimal Calories,
    decimal Protein,
    decimal Fat,
    decimal Carbs);

public sealed record UserMealEntryDto(
    Guid Id,
    string ProductName,
    string Brand,
    decimal Calories,
    decimal Protein,
    decimal Fat,
    decimal Carbs,
    string MealType,
    decimal ServingGrams,
    string PortionLabel,
    string SourceType,
    string SourceReference,
    DateTimeOffset LoggedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record MealEntrySummaryByTypeDto(
    string MealType,
    decimal Calories,
    decimal Protein,
    decimal Fat,
    decimal Carbs,
    int Count);

public sealed record UserDailyGoalDto(
    decimal TargetCalories,
    decimal TargetProtein,
    decimal TargetFat,
    decimal TargetCarbs);

public sealed record ProfileResponseDto(
    Guid UserId,
    string Email,
    string FirstName,
    string SecondName);

public sealed record CreateDailyGoalRequestDto(
    decimal TargetCalories,
    decimal TargetProtein,
    decimal TargetFat,
    decimal TargetCarbs);

public sealed record UpdateDailyGoalRequestDto(
    decimal TargetCalories,
    decimal TargetProtein,
    decimal TargetFat,
    decimal TargetCarbs);

public sealed record CreateUserMealEntryRequestDto(
    string ProductName,
    string? Brand,
    decimal Calories,
    decimal Protein,
    decimal Fat,
    decimal Carbs,
    string MealType,
    decimal ServingGrams,
    string? PortionLabel,
    string? SourceType,
    string? SourceReference,
    DateTimeOffset? LoggedAtUtc);

public sealed record UpdateUserMealEntryRequestDto(
    string ProductName,
    string? Brand,
    decimal Calories,
    decimal Protein,
    decimal Fat,
    decimal Carbs,
    string MealType,
    decimal ServingGrams,
    string? PortionLabel,
    string? SourceType,
    string? SourceReference,
    DateTimeOffset? LoggedAtUtc);

public sealed record ProfileHistoryResponseDto(
    IReadOnlyCollection<UserMealEntryDto> Entries,
    NutritionSummaryDto TotalSummary);

public sealed record ProfileSummaryByTypeResponseDto(
    IReadOnlyCollection<MealEntrySummaryByTypeDto> SummaryByType,
    NutritionSummaryDto TotalSummary);

public sealed record MealEntriesByTypeDto(
    string MealType,
    IReadOnlyCollection<UserMealEntryDto> Entries,
    NutritionSummaryDto Summary);

public sealed record ProfileDayResponseDto(
    DateOnly Date,
    UserDailyGoalDto? Goal,
    IReadOnlyCollection<MealEntriesByTypeDto> Meals,
    NutritionSummaryDto TotalSummary);
