namespace Nutrition.Application.Infrastructure.OpenFoodFacts;

public sealed class OpenFoodFactsOptions
{
    public const string SectionName = "OpenFoodFacts";

    public string BaseUrl { get; set; } = "https://world.openfoodfacts.org/";

    public string SearchBaseUrl { get; set; } = "https://search.openfoodfacts.org/";

    public int SearchPageSize { get; set; } = 15;

    public int CacheTtlHours { get; set; } = 12;

    public int SearchRequestsPerMinute { get; set; } = 8;

    public int HttpTimeoutSeconds { get; set; } = 20;

    public bool EnableLegacyCgiFallback { get; set; }
}
