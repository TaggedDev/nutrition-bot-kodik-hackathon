using Nutrition.Core.Domain.Exceptions;

namespace Nutrition.Core.Domain.Entities;

public sealed class UserDailyGoal
{
    public const decimal DefaultBreakfastPercent = 25;
    public const decimal DefaultLunchPercent = 35;
    public const decimal DefaultDinnerPercent = 30;
    public const decimal DefaultSnackPercent = 10;

    public UserDailyGoal(Guid userId, decimal targetCalories, decimal targetProtein, decimal targetFat,
        decimal targetCarbs, decimal breakfastPercent = DefaultBreakfastPercent,
        decimal lunchPercent = DefaultLunchPercent, decimal dinnerPercent = DefaultDinnerPercent,
        decimal snackPercent = DefaultSnackPercent)
    {
        Validate(userId, targetCalories, targetProtein, targetFat, targetCarbs, breakfastPercent, lunchPercent,
            dinnerPercent, snackPercent);

        UserId = userId;
        TargetCalories = targetCalories;
        TargetProtein = targetProtein;
        TargetFat = targetFat;
        TargetCarbs = targetCarbs;
        BreakfastPercent = breakfastPercent;
        LunchPercent = lunchPercent;
        DinnerPercent = dinnerPercent;
        SnackPercent = snackPercent;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; }

    public decimal TargetCalories { get; private set; }

    public decimal TargetProtein { get; private set; }

    public decimal TargetFat { get; private set; }

    public decimal TargetCarbs { get; private set; }

    public decimal BreakfastPercent { get; private set; }

    public decimal LunchPercent { get; private set; }

    public decimal DinnerPercent { get; private set; }

    public decimal SnackPercent { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(decimal targetCalories, decimal targetProtein, decimal targetFat, decimal targetCarbs,
        decimal breakfastPercent, decimal lunchPercent, decimal dinnerPercent, decimal snackPercent)
    {
        Validate(UserId, targetCalories, targetProtein, targetFat, targetCarbs, breakfastPercent, lunchPercent,
            dinnerPercent, snackPercent);

        TargetCalories = targetCalories;
        TargetProtein = targetProtein;
        TargetFat = targetFat;
        TargetCarbs = targetCarbs;
        BreakfastPercent = breakfastPercent;
        LunchPercent = lunchPercent;
        DinnerPercent = dinnerPercent;
        SnackPercent = snackPercent;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public bool IsExceeded(decimal currentCalories, decimal currentProtein, decimal currentFat, decimal currentCarbs)
    {
        return currentCalories > TargetCalories || currentProtein > TargetProtein || currentFat > TargetFat ||
               currentCarbs > TargetCarbs;
    }

    private static void Validate(Guid userId, decimal targetCalories, decimal targetProtein, decimal targetFat,
        decimal targetCarbs, decimal breakfastPercent, decimal lunchPercent, decimal dinnerPercent,
        decimal snackPercent)
    {
        if (userId == Guid.Empty) throw new DomainValidationException("User id is required.");
        if (targetCalories < 0 || targetProtein < 0 || targetFat < 0 || targetCarbs < 0)
            throw new DomainValidationException("Target nutrition values cannot be negative.");
        if (breakfastPercent < 0 || lunchPercent < 0 || dinnerPercent < 0 || snackPercent < 0)
            throw new DomainValidationException("Meal target percentages cannot be negative.");
        if (breakfastPercent + lunchPercent + dinnerPercent + snackPercent != 100)
            throw new DomainValidationException("Meal target percentages must sum to 100.");
    }
}