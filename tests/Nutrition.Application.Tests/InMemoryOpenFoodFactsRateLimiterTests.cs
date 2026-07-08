using Microsoft.Extensions.Options;
using Nutrition.Application.Infrastructure.OpenFoodFacts;

namespace Nutrition.Application.Tests;

public sealed class InMemoryOpenFoodFactsRateLimiterTests
{
    [Fact]
    public void TryAcquireSearchSlot_ReturnsFalse_WhenLimitIsReached()
    {
        var limiter = new InMemoryOpenFoodFactsRateLimiter(Options.Create(new OpenFoodFactsOptions
        {
            SearchRequestsPerMinute = 1
        }));

        var first = limiter.TryAcquireSearchSlot();
        var second = limiter.TryAcquireSearchSlot();

        Assert.True(first);
        Assert.False(second);
    }
}