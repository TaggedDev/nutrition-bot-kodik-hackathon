using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Infrastructure.Agent.DeepSeek;
using Nutrition.Infrastructure.Agent.Matching;
using Nutrition.Infrastructure.Agent.NutritionLookup;
using Nutrition.Infrastructure.Agent.Parsing;
using Nutrition.Infrastructure.Agent.WebSearch;

namespace Nutrition.Infrastructure.Agent;

public static class DependencyInjection
{
    public static IServiceCollection AddNutritionAgent(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DeepSeekOptions>(options =>
        {
            configuration.GetSection(DeepSeekOptions.SectionName).Bind(options);
            options.ApiKey = FirstNonEmpty(configuration["DEEPSEEK_API_KEY"], configuration["DeepSeek:ApiKey"],
                options.ApiKey);
            options.BaseUrl = FirstNonEmpty(configuration["DEEPSEEK_BASE_URL"], configuration["DeepSeek:BaseUrl"],
                options.BaseUrl);
            options.Model = FirstNonEmpty(configuration["DEEPSEEK_MODEL"], configuration["DeepSeek:Model"],
                options.Model);

            if (int.TryParse(configuration["DEEPSEEK_TIMEOUT_SECONDS"], out var timeoutSeconds))
            {
                options.TimeoutSeconds = timeoutSeconds;
            }
        });

        services.Configure<TavilyOptions>(options =>
        {
            configuration.GetSection(TavilyOptions.SectionName).Bind(options);
            options.ApiKey = FirstNonEmpty(configuration["TAVILY_API_KEY"], configuration["Tavily:ApiKey"],
                options.ApiKey);
            options.BaseUrl = FirstNonEmpty(configuration["TAVILY_BASE_URL"], configuration["Tavily:BaseUrl"],
                options.BaseUrl);

            if (int.TryParse(configuration["TAVILY_TIMEOUT_SECONDS"], out var timeoutSeconds))
            {
                options.TimeoutSeconds = timeoutSeconds;
            }
        });

        services.AddHttpClient<DeepSeekChatClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DeepSeekOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 180));
        });

        services.AddSingleton<Microsoft.Extensions.AI.IChatClient>(serviceProvider
            => serviceProvider.GetRequiredService<DeepSeekChatClient>());

        services.AddScoped<IFoodInputParser, MafFoodInputParser>();
        services.AddScoped<IFoodMatcher, OpenFoodFactsFoodMatcher>();
        services.AddScoped<INutritionEvidenceExtractor, MafNutritionEvidenceExtractor>();
        services.AddSingleton<ITavilyQueryBuilder, TavilyQueryBuilder>();
        services.AddHttpClient<IWebSearchService, TavilyWebSearchService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<TavilyOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 60));
            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.ApiKey);
            }
        });
        services.AddScoped<INutritionChatQueryService, NutritionChatQueryService>();

        return services;
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
