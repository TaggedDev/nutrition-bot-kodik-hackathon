using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Infrastructure.Agent.DeepSeek;
using Nutrition.Infrastructure.Agent.Matching;
using Nutrition.Infrastructure.Agent.Parsing;

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