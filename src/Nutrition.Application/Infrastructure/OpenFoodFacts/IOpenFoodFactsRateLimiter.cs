namespace Nutrition.Application.Infrastructure.OpenFoodFacts;

public interface IOpenFoodFactsRateLimiter
{
    bool TryAcquireSearchSlot();
}