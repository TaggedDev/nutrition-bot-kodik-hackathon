using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nutrition.Infrastructure.Identity;
using Nutrition.Shared.Dtos;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Matchers;

namespace Nutrition.Integration.Tests;

[Collection(IntegrationTestCollection.Name)]
public sealed class NutritionPipelineIntegrationTests(IntegrationTestFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Semolina_WebFallback_CanBeSavedToBreakfastAndReadFromPostgres()
    {
        fixture.ExternalApis.Reset();
        StubEmptyOpenFoodFacts();
        StubDeepSeekSequence(ParserResponse(("манная каша", 200, "g", null, "MassMarketProduct")),
            ExtractorResponse("Манная каша на молоке", 100, 3, 3.2m, 15, 0.94m, "https://example.test/semolina"));
        StubTavily("Манная каша", "https://example.test/semolina",
            "На 100 г: 100 ккал, белки 3 г, жиры 3.2 г, углеводы 15 г");
        using var client = await RegisterClientAsync();

        var search = await client.GetFromJsonAsync<NutritionChatSearchResponseDto>(
            "/api/v1/nutrition/search?query=" + Uri.EscapeDataString("манная каша 200 грамм"), JsonOptions);
        var candidate = Assert.Single(Assert.Single(search!.Clarifications).Candidates);
        Assert.Equal("WebSearch", candidate.SourceType);
        Assert.Equal("https://example.test/semolina", candidate.SourceReference);
        Assert.Equal(100, candidate.NutritionFacts.Calories);
        Assert.Contains(fixture.ExternalApis.LogEntries, entry => entry.RequestMessage.Path == "/search");

        var now = DateTimeOffset.UtcNow;
        var createResponse = await client.PostAsJsonAsync("/api/v1/profile/entry",
            new CreateUserMealEntryRequestDto(candidate.ProductName, candidate.Brand, 200, 6, 6.4m, 30, "Breakfast",
                200, "200 g", candidate.SourceType, candidate.SourceReference, now));
        createResponse.EnsureSuccessStatusCode();
        var saved = await createResponse.Content.ReadFromJsonAsync<UserMealEntryDto>(JsonOptions);

        var day = await client.GetFromJsonAsync<ProfileDayResponseDto>(
            $"/api/v1/profile/day?date={now:yyyy-MM-dd}&utcOffsetMinutes=0", JsonOptions);
        var breakfast = Assert.Single(day!.Meals, meal => meal.MealType == "Breakfast");
        var entry = Assert.Single(breakfast.Entries);
        Assert.Equal(200, entry.ServingGrams);
        Assert.Equal(200, entry.Calories);
        Assert.Empty(Assert.Single(day.Meals, meal => meal.MealType == "Lunch").Entries);

        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NutritionIdentityDbContext>();
        var persisted = await db.UserMealEntries.SingleAsync(item => item.Id == saved!.Id);
        Assert.Equal("Breakfast", persisted.MealType.ToString());
        Assert.Equal("https://example.test/semolina", persisted.SourceReference);
    }

    [Fact]
    public async Task ArigatoSet_UsesOfficialTanukiWebEvidence()
    {
        fixture.ExternalApis.Reset();
        StubEmptyOpenFoodFacts();
        StubDeepSeekSequence(ParserResponse(("set arigato", 1, "serving", "tanuki", "PreparedFood")),
            ExtractorResponse("Сет Аригато", 2310, 73, 107, 264, 0.96m,
                "https://tanukifamily.ru/tanuki/product/arigato-set", "PerServing", 910));
        StubTavily("Сет Аригато", "https://tanukifamily.ru/tanuki/product/arigato-set",
            "Порция 910 г. 2310 ккал, белки 73 г, жиры 107 г, углеводы 264 г");
        using var client = await RegisterClientAsync();

        var result = await client.GetFromJsonAsync<NutritionChatSearchResponseDto>(
            "/api/v1/nutrition/search?query=set%20arigato%20tanuki", JsonOptions);
        var candidate = Assert.Single(Assert.Single(result!.Clarifications).Candidates);

        Assert.Equal("https://tanukifamily.ru/tanuki/product/arigato-set", candidate.SourceReference);
        Assert.Equal(910, candidate.ServingSize);
        Assert.Equal(2310, candidate.NutritionFacts.Calories);
        var tavilyRequest = Assert.Single(fixture.ExternalApis.LogEntries,
            entry => entry.RequestMessage.Path == "/search" && entry.RequestMessage.Body is not null);
        var body = tavilyRequest.RequestMessage.Body!;
        Assert.Contains("tanuki", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("set arigato", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SemolinaWithJam_ReturnsThreeCandidatesForEachFoodUnit()
    {
        fixture.ExternalApis.Reset();
        StubEmptyOpenFoodFacts();
        StubDeepSeekSequence(ParserResponse(("манная каша", 1, "serving", null, "MassMarketProduct"),
            ("клубничное варенье", 1, "serving", null, "MassMarketProduct")));
        StubDeepSeek($"*extract nutrition facts*{JsonBodyText("манная каша")}*",
            ExtractorThree("каша", "https://example.test/porridge"));
        StubDeepSeek($"*extract nutrition facts*{JsonBodyText("клубничное варенье")}*",
            ExtractorThree("варенье", "https://example.test/jam"));
        StubTavily("Каша", "https://example.test/porridge", "КБЖУ каши", JsonBodyText("манная каша"));
        StubTavily("Варенье", "https://example.test/jam", "КБЖУ варенья", JsonBodyText("клубничное варенье"));
        using var client = await RegisterClientAsync();

        var result = await client.GetFromJsonAsync<NutritionChatSearchResponseDto>(
            "/api/v1/nutrition/search?query=" + Uri.EscapeDataString("манная каша с клубничным вареньем"), JsonOptions);

        Assert.Equal(2, result!.Clarifications.Count);
        Assert.All(result.Clarifications, clarification => Assert.Equal(3, clarification.Candidates.Count));
        Assert.Equal(6,
            result.Clarifications.SelectMany(item => item.Candidates).Select(item => item.ProductId).Distinct()
                .Count());
    }

    [Fact]
    public async Task EmptyFallback_ReturnsNotFoundWithoutServerError()
    {
        fixture.ExternalApis.Reset();
        StubEmptyOpenFoodFacts();
        StubDeepSeekSequence(ParserResponse(("манная каша", 1, "serving", null, "MassMarketProduct")));
        fixture.ExternalApis.Given(Request.Create().WithPath("/search").UsingPost())
            .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { results = Array.Empty<object>() }));
        using var client = await RegisterClientAsync();

        var response = await client.GetAsync("/api/v1/nutrition/search?query=" + Uri.EscapeDataString("манная каша"));
        var result = await response.Content.ReadFromJsonAsync<NutritionChatSearchResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(result!.Clarifications);
    }

    [Fact]
    public async Task OpenFoodFactsSearch_WhenPrimaryReturns502_UsesLegacyFallback()
    {
        fixture.ExternalApis.Reset();
        fixture.ExternalApis.Given(Request.Create().WithPath("/search").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(HttpStatusCode.BadGateway));
        fixture.ExternalApis.Given(Request.Create().WithPath("/cgi/search.pl").UsingGet()).RespondWith(Response.Create()
            .WithSuccess().WithBodyAsJson(new
            {
                products = new[]
                {
                    new
                    {
                        code = "off-curd-2",
                        product_name = "Творог 2%",
                        brands = "Простоквашино",
                        nutriments = new Dictionary<string, object>
                        {
                            ["energy-kcal_100g"] = 148.5m,
                            ["proteins_100g"] = 25.5m,
                            ["fat_100g"] = 3m,
                            ["carbohydrates_100g"] = 4.9m
                        }
                    }
                }
            }));
        StubDeepSeekSequence(ParserResponse(("творог", 150, "g", null, "MassMarketProduct")));
        using var client = await RegisterClientAsync();

        var response = await client.GetAsync("/api/v1/nutrition/search?query=" +
                                             Uri.EscapeDataString("творог 150 грамм"));
        var result = await response.Content.ReadFromJsonAsync<NutritionChatSearchResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candidate = Assert.Single(Assert.Single(result!.Clarifications).Candidates);
        Assert.Equal("off-curd-2", candidate.ProductId);
        Assert.Equal("OpenFoodFacts", candidate.SourceType);
        Assert.Equal(148.5m, candidate.NutritionFacts.Calories);
        Assert.Contains(fixture.ExternalApis.LogEntries, entry => entry.RequestMessage.Path == "/cgi/search.pl");
        Assert.DoesNotContain(fixture.ExternalApis.LogEntries,
            entry => entry.RequestMessage.Path == "/search" && entry.RequestMessage.Method == "POST");
    }

    [Fact]
    public async Task Latte_MalformedJudgeAndExtractor_FallsBackToAdvancedTavilyAndStructuredSnippet()
    {
        fixture.ExternalApis.Reset();
        fixture.ExternalApis.Given(Request.Create().WithPath("/search").UsingGet()).RespondWith(Response.Create()
            .WithSuccess().WithBodyAsJson(new
            {
                hits = new[]
                {
                    new
                    {
                        code = "off-latte",
                        product_name = "Latte drink",
                        brands = "Unrelated",
                        nutriments = new Dictionary<string, object>
                        {
                            ["energy-kcal_100g"] = 60m,
                            ["proteins_100g"] = 2m,
                            ["fat_100g"] = 2m,
                            ["carbohydrates_100g"] = 8m
                        }
                    }
                }
            }));
        StubDeepSeek("*food input parser*", ParserResponse(("кофе латте", 1, "serving", null, "PreparedFood")));
        StubDeepSeek("*extract nutrition facts*",
            ExtractorResponse("latte", 114, 7.45m, 4.66m, 10.45m, 0.9m, "https://example.test/latte"));
        StubTavily("Кофе Латте большая кружка", "https://example.test/latte",
            "На 100 г продукта; Белков, 7.45 г, 11%; Жиров, 4.66 г, 6%; Углеводов, 10.45 г, 3%; Калорийность, 114.00 ккал, 5%");
        using var client = await RegisterClientAsync();

        var response = await client.GetAsync("/api/v1/nutrition/search?query=" + Uri.EscapeDataString("кофе латте"));
        var result = await response.Content.ReadFromJsonAsync<NutritionChatSearchResponseDto>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var candidate = Assert.Single(Assert.Single(result!.Clarifications).Candidates);
        Assert.Equal("WebSearch", candidate.SourceType);
        Assert.Equal("Per100Grams", candidate.NutritionValueBasis);
        Assert.Equal(114m, candidate.NutritionFacts.Calories);
        Assert.Equal(7.45m, candidate.NutritionFacts.Protein);
        Assert.Equal(4.66m, candidate.NutritionFacts.Fat);
        Assert.Equal(10.45m, candidate.NutritionFacts.Carbs);

        var tavilyRequest = Assert.Single(fixture.ExternalApis.LogEntries,
            entry => entry.RequestMessage.Path == "/search" && entry.RequestMessage.Method == "POST");
        using var tavilyBody = JsonDocument.Parse(tavilyRequest.RequestMessage.Body!);
        Assert.Equal("advanced", tavilyBody.RootElement.GetProperty("search_depth").GetString());
        Assert.Contains("100 мл", tavilyBody.RootElement.GetProperty("query").GetString()!,
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpClient> RegisterClientAsync()
    {
        var client = fixture.Factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true, AllowAutoRedirect = false
            });
        var email = $"integration-{Guid.NewGuid():N}@example.test";
        var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequestDto(email, "Integration", "User", "Password1"));
        response.EnsureSuccessStatusCode();
        return client;
    }

    private void StubEmptyOpenFoodFacts()
        => fixture.ExternalApis.Given(Request.Create().WithPath("/search").UsingGet())
            .RespondWith(Response.Create().WithSuccess().WithBodyAsJson(new { hits = Array.Empty<object>() }));

    private void StubTavily(string title, string url, string content, string? queryPattern = null)
        => fixture.ExternalApis
            .Given(queryPattern is null ? Request.Create().WithPath("/search").UsingPost() : Request.Create()
                .WithPath("/search").UsingPost().WithBody(new WildcardMatcher($"*{queryPattern}*", true))).RespondWith(
                Response.Create().WithSuccess().WithBodyAsJson(new
                {
                    request_id = Guid.NewGuid().ToString("N"),
                    results = new[] { new { title, url, content, score = 0.95m } },
                    usage = new { credits = 1 }
                }));

    private void StubDeepSeekSequence(params string[] contents)
    {
        for (var index = 0; index < contents.Length; index++)
        {
            var marker = index == 0 ? "*food input parser*" :
                contents.Length == 3 && index == 1 ? "*judge whether OpenFoodFacts*" : "*extract nutrition facts*";
            fixture.ExternalApis
                .Given(Request.Create().WithPath("/chat/completions").UsingPost()
                    .WithBody(new WildcardMatcher(marker, true))).RespondWith(Response.Create().WithSuccess()
                    .WithBodyAsJson(new
                    {
                        id = $"test-{index}",
                        model = "test-model",
                        choices = new[] { new { message = new { content = contents[index] } } }
                    }));
        }
    }

    private void StubDeepSeek(string bodyPattern, string content)
        => fixture.ExternalApis
            .Given(Request.Create().WithPath("/chat/completions").UsingPost()
                .WithBody(new WildcardMatcher(bodyPattern, true))).RespondWith(Response.Create().WithSuccess()
                .WithBodyAsJson(new
                {
                    id = "test-specific",
                    model = "test-model",
                    choices = new[] { new { message = new { content } } }
                }));

    private static string JsonBodyText(string value)
        => JsonSerializer.Serialize(value)[1..^1];

    private static string ParserResponse(
        params (string Name, decimal Quantity, string Unit, string? Brand, string Kind)[] items)
        => JsonSerializer.Serialize(
            new
            {
                items = items.Select(item => new
                {
                    productName = item.Name,
                    quantity = item.Quantity,
                    unit = item.Unit,
                    brand = item.Brand,
                    preparation = (string?)null,
                    kind = item.Kind
                })
            }, JsonOptions);

    private static string ExtractorResponse(string name, decimal calories, decimal protein, decimal fat, decimal carbs,
        decimal confidence, string url, string basis = "Per100Grams", decimal? servingSize = 100)
        => JsonSerializer.Serialize(
            new
            {
                candidates = new[]
                {
                    new
                    {
                        productName = name,
                        brand = (string?)null,
                        servingSize,
                        servingUnit = "g",
                        valueBasis = basis,
                        calories,
                        protein,
                        fat,
                        carbs,
                        sourceUrl = url,
                        sourceIds = new[] { "S1" },
                        isExactProductMatch = true,
                        valuesExplicitlyStated = true,
                        confidence,
                        warnings = Array.Empty<string>()
                    }
                }
            }, JsonOptions);

    private static string ExtractorThree(string prefix, string url)
        => JsonSerializer.Serialize(
            new
            {
                candidates = Enumerable.Range(1, 3).Select(index => new
                {
                    productName = $"{prefix} {index}",
                    brand = (string?)null,
                    servingSize = 100m,
                    servingUnit = "g",
                    valueBasis = "Per100Grams",
                    calories = 100m + index,
                    protein = 3m,
                    fat = 2m,
                    carbs = 20m,
                    sourceUrl = url,
                    sourceIds = new[] { "S1" },
                    isExactProductMatch = true,
                    valuesExplicitlyStated = true,
                    confidence = 0.9m,
                    warnings = Array.Empty<string>()
                }).ToArray()
            }, JsonOptions);
}