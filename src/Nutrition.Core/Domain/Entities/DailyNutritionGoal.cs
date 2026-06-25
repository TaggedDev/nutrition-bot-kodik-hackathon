using Nutrition.Core.Domain.Exceptions;
using Nutrition.Core.Domain.ValueObjects;

namespace Nutrition.Core.Domain.Entities;

public sealed class DailyNutritionGoal
{
    public DailyNutritionGoal(Guid userId, NutritionFacts target)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainValidationException("User id is required.");
        }

        UserId = userId;
        Target = target;
    }

    public Guid UserId { get; }

    public NutritionFacts Target { get; }

    public bool IsExceededBy(NutritionFacts consumed)
    {
        return consumed.Calories > Target.Calories
               || consumed.Protein > Target.Protein
               || consumed.Fat > Target.Fat
               || consumed.Carbs > Target.Carbs;
    }
}
