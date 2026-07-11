namespace Nutrition.Infrastructure.Agent.WebSearch;

public sealed class TavilyOptions
{
    public const string SectionName = "Tavily";

    public string BaseUrl { get; set; } = "https://api.tavily.com/";

    public string ApiKey { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 15;
}