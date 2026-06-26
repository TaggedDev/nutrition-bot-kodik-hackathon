using Microsoft.Extensions.Options;

namespace Nutrition.Application.Infrastructure.OpenFoodFacts;

public sealed class InMemoryOpenFoodFactsRateLimiter : IOpenFoodFactsRateLimiter
{
    private readonly object _sync = new();
    private readonly Queue<DateTimeOffset> _searchRequestTimestamps = new();
    private readonly OpenFoodFactsOptions _options;

    public InMemoryOpenFoodFactsRateLimiter(IOptions<OpenFoodFactsOptions> options)
    {
        _options = options.Value;
    }

    public bool TryAcquireSearchSlot()
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-1);

        lock (_sync)
        {
            while (_searchRequestTimestamps.Count > 0 && _searchRequestTimestamps.Peek() < windowStart)
            {
                _searchRequestTimestamps.Dequeue();
            }

            if (_searchRequestTimestamps.Count >= _options.SearchRequestsPerMinute)
            {
                return false;
            }

            _searchRequestTimestamps.Enqueue(now);
            return true;
        }
    }
}
