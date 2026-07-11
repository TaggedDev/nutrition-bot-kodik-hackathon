using System.Net.Http.Json;
using Nutrition.Shared.Dtos;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace Nutrition.Integration.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class LivePipelineSmokeTests(IntegrationTestFixture fixture)
{
    [Fact]
    [Trait("Category", "LiveSmoke")]
    public async Task Arigato_RealTavilyAndDeepSeek_ReturnPositiveNutritionFromTanukiDomain()
    {
        var deepSeekKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
        var tavilyKey = Environment.GetEnvironmentVariable("TAVILY_API_KEY");
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_LIVE_SMOKE"), "1", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(deepSeekKey) || string.IsNullOrWhiteSpace(tavilyKey))
        {
            return;
        }

        fixture.ExternalApis.Reset();
        fixture.ExternalApis.Given(Request.Create().WithPath("/search").UsingGet())
            .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { hits = Array.Empty<object>() }));
        await using var factory = new NutritionWebApplicationFactory(fixture.ConnectionString,
            fixture.ExternalApis.Url!,
            Environment.GetEnvironmentVariable("DEEPSEEK_BASE_URL") ?? "https://api.deepseek.com",
            Environment.GetEnvironmentVariable("TAVILY_BASE_URL") ?? "https://api.tavily.com/", deepSeekKey, tavilyKey);
        using var client = factory.CreateClient();

        var result = await client.GetFromJsonAsync<NutritionChatSearchResponseDto>(
            "/api/v1/nutrition/search?query=set%20arigato%20tanuki");
        var candidates = result!.Clarifications.SelectMany(item => item.Candidates).ToArray();

        Assert.NotEmpty(candidates);
        Assert.Contains(candidates,
            candidate => Uri.TryCreate(candidate.SourceReference, UriKind.Absolute, out var uri) &&
                         uri.Host.Contains("tanuki", StringComparison.OrdinalIgnoreCase) &&
                         candidate.NutritionFacts.Calories > 0 && candidate.NutritionFacts.Protein > 0);
    }
}