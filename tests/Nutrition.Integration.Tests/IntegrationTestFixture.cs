using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nutrition.Infrastructure.Agent.DeepSeek;
using Testcontainers.PostgreSql;
using WireMock.Server;

namespace Nutrition.Integration.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
    public const string Name = "nutrition-integration";
}

public sealed class IntegrationTestFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("nutrition_tests")
        .WithUsername("nutrition")
        .WithPassword("nutrition")
        .Build();

    public WireMockServer ExternalApis { get; private set; } = null!;
    public NutritionWebApplicationFactory Factory { get; private set; } = null!;
    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        ExternalApis = WireMockServer.Start();
        Factory = new NutritionWebApplicationFactory(_postgres.GetConnectionString(), ExternalApis.Url!);
        _ = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        ExternalApis.Stop();
        ExternalApis.Dispose();
        await _postgres.DisposeAsync();
    }
}

public sealed class NutritionWebApplicationFactory(
    string connectionString,
    string externalApiUrl,
    string? deepSeekUrl = null,
    string? tavilyUrl = null,
    string deepSeekApiKey = "integration-test-key",
    string tavilyApiKey = "integration-test-key")
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:IdentityDb"] = connectionString,
                ["OpenFoodFacts:BaseUrl"] = externalApiUrl + "/",
                ["OpenFoodFacts:SearchBaseUrl"] = externalApiUrl + "/",
                ["OpenFoodFacts:EnableLegacyCgiFallback"] = "false",
                ["DeepSeek:ApiKey"] = deepSeekApiKey,
                ["DeepSeek:BaseUrl"] = deepSeekUrl ?? externalApiUrl,
                ["Tavily:ApiKey"] = tavilyApiKey,
                ["Tavily:BaseUrl"] = tavilyUrl ?? externalApiUrl + "/"
            }));
        builder.ConfigureServices(services => services.PostConfigure<DeepSeekOptions>(options =>
        {
            options.ApiKey = deepSeekApiKey;
            options.BaseUrl = deepSeekUrl ?? externalApiUrl;
            options.Model = "test-model";
        }));
    }
}
