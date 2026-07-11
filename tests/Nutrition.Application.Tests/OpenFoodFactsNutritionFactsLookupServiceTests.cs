using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nutrition.Application.Infrastructure.OpenFoodFacts;

namespace Nutrition.Application.Tests;

public sealed class OpenFoodFactsNutritionFactsLookupServiceTests
{
    [Fact]
    public async Task SearchAsync_ReturnsEmpty_On503WithoutException()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var service = CreateService(handler);

        var result = await service.SearchAsync("milk", CancellationToken.None);

        Assert.Empty(result);
        Assert.Equal(1, handler.CallsCount);
    }

    [Fact]
    public async Task SearchAsync_FallsBackToLegacySearch_WhenSearchALiciousIsUnavailable()
    {
        const string payload = """
                               { "products": [{ "code": "12345678", "product_name": "Milk", "nutriments": { "energy-kcal_100g": 42, "proteins_100g": 3.4, "fat_100g": 1, "carbohydrates_100g": 5 } }] }
                               """;
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri!.Host == "search.openfoodfacts.org"
                ? new HttpResponseMessage(HttpStatusCode.BadGateway)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                });
        var service = CreateService(handler, enableLegacyFallback: true);

        var result = await service.SearchAsync("milk", CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(2, handler.CallsCount);
        Assert.Equal("Milk", result.Single().ProductName);
    }

    [Fact]
    public async Task SearchAsync_CachesSuccessfulResponse_AndSkipsSecondHttpCall()
    {
        const string payload = """
                               {
                                 "hits": [
                                   {
                                     "code": "12345678",
                                     "product_name": "Milk",
                                     "brands": ["Brand"],
                                     "nutriments": {
                                       "energy-kcal_100g": 42,
                                       "proteins_100g": 3.4,
                                       "fat_100g": 1.0,
                                       "carbohydrates_100g": 5.0
                                     }
                                   }
                                 ]
                               }
                               """;

        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Contains("q=milk", request.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        var service = CreateService(handler);

        var first = await service.SearchAsync("milk", CancellationToken.None);
        var second = await service.SearchAsync("milk", CancellationToken.None);

        Assert.Single(first);
        Assert.Single(second);
        Assert.Equal("Brand", first.Single().Brand);
        Assert.Equal(1, handler.CallsCount);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_OnNonSuccessStatus()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var service = CreateService(handler);

        var result = await service.SearchAsync("bread", CancellationToken.None);

        Assert.Empty(result);
        Assert.Equal(1, handler.CallsCount);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmpty_WhenRateLimiterRejectsTextQuery()
    {
        var handler = new StubHttpMessageHandler(_
            => throw new InvalidOperationException("HTTP must not be called when throttled."));
        var service = CreateService(handler, rateLimiter: new AlwaysRejectRateLimiter());

        var result = await service.SearchAsync("yogurt", CancellationToken.None);

        Assert.Empty(result);
        Assert.Equal(0, handler.CallsCount);
    }

    [Fact]
    public async Task SearchAsync_UsesAbstractionContract_WithConcreteInfrastructureImplementation()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var service = CreateService(handler);

        var abstraction = (Nutrition.Application.Abstractions.Services.INutritionFactsLookupService)service;
        var result = await abstraction.SearchAsync("12345678", CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task SearchAsync_LooksUpBarcodeAndMapsPayload()
    {
        const string payload = """
                               {
                                 "product": {
                                   "code": "978020137962",
                                   "product_name_en": "Yogurt",
                                   "brands": "BrandX",
                                   "nutriments": {
                                     "energy-kj_100g": 418.4,
                                     "proteins": "3.2",
                                     "fat": "1,1",
                                     "carbohydrates_100g": 6.4
                                   }
                                 }
                               }
                               """;

        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Contains("/api/v2/product/978020137962.json", request.RequestUri!.AbsoluteUri,
                StringComparison.OrdinalIgnoreCase);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        });

        var service = CreateService(handler);

        var result = await service.SearchAsync("978020137962", CancellationToken.None);

        var product = Assert.Single(result);
        Assert.Equal("978020137962", product.ProductId);
        Assert.Equal("Yogurt", product.ProductName);
        Assert.Equal("BrandX", product.Brand);
        Assert.Equal(100m, product.NutritionFacts.Calories);
        Assert.Equal(3.2m, product.NutritionFacts.Protein);
        Assert.Equal(1.1m, product.NutritionFacts.Fat);
        Assert.Equal(6.4m, product.NutritionFacts.Carbs);
        Assert.Equal("OFF:978020137962", product.SourceReference);
        Assert.Equal(1, handler.CallsCount);
    }

    private static OpenFoodFactsNutritionFactsLookupService CreateService(StubHttpMessageHandler handler,
        IOpenFoodFactsRateLimiter? rateLimiter = null, bool enableLegacyFallback = false)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://world.openfoodfacts.org/") };

        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new OpenFoodFactsOptions
        {
            BaseUrl = "https://world.openfoodfacts.org/",
            SearchBaseUrl = "https://search.openfoodfacts.org/",
            SearchPageSize = 15,
            CacheTtlHours = 6,
            SearchRequestsPerMinute = 10,
            HttpTimeoutSeconds = 20,
            EnableLegacyCgiFallback = enableLegacyFallback
        });

        return new OpenFoodFactsNutritionFactsLookupService(httpClient, cache,
            NullLogger<OpenFoodFactsNutritionFactsLookupService>.Instance,
            rateLimiter ?? new InMemoryOpenFoodFactsRateLimiter(options), options);
    }

    private sealed class AlwaysRejectRateLimiter : IOpenFoodFactsRateLimiter
    {
        public bool TryAcquireSearchSlot()
            => false;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public int CallsCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallsCount++;
            return Task.FromResult(_responseFactory(request));
        }
    }
}
