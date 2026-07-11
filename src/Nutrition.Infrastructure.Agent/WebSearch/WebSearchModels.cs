namespace Nutrition.Infrastructure.Agent.WebSearch;

public interface IWebSearchService
{
    Task<WebSearchResponse> SearchAsync(WebSearchRequest request, CancellationToken cancellationToken);
}

public sealed record WebSearchRequest(string Query, int MaxResults = 5, WebSearchDepth Depth = WebSearchDepth.Basic,
    string? Country = null);

public enum WebSearchDepth
{
    Basic = 0,
    Advanced = 1
}

public sealed record WebSearchResponse(IReadOnlyCollection<WebSearchResult> Results, string? RequestId,
    int? CreditsUsed);

public sealed record WebSearchResult(string Title, Uri Url, string Content, decimal RelevanceScore);