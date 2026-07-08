using Nutrition.Core.Domain.Exceptions;

namespace Nutrition.Core.Domain.Entities;

public sealed class UserDailyGoal
{
    public UserDailyGoal(Guid userId, decimal targetCalories, decimal targetProtein, decimal targetFat, decimal targetCarbs)
    {
        if (userId == Guid.Empty)
            throw new DomainValidationException("User id is required.");
        if (targetCalories < 0 || targetProtein < 0 || targetFat < 0 || targetCarbs < 0)
            throw new DomainValidationException("Target nutrition values cannot be negative.");

        UserId = userId;
        TargetCalories = targetCalories;
        TargetProtein = targetProtein;
        TargetFat = targetFat;
        TargetCarbs = targetCarbs;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; }

    public decimal TargetCalories { get; private set; }

    public decimal TargetProtein { get; private set; }

    public decimal TargetFat { get; private set; }

    public decimal TargetCarbs { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(decimal targetCalories, decimal targetProtein, decimal targetFat, decimal targetCarbs)
    {
        if (targetCalories < 0 || targetProtein < 0 || targetFat < 0 || targetCarbs < 0)
            throw new DomainValidationException("Target nutrition values cannot be negative.");

        TargetCalories = targetCalories;
        TargetProtein = targetProtein;
        TargetFat = targetFat;
        TargetCarbs = targetCarbs;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public bool IsExceeded(decimal currentCalories, decimal currentProtein, decimal currentFat, decimal currentCarbs)
    {
        return currentCalories > TargetCalories || 
               currentProtein > TargetProtein || 
               currentFat > TargetFat || 
               currentCarbs > TargetCarbs;
    }
}
