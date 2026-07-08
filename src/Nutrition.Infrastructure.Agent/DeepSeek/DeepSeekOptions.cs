namespace Nutrition.Infrastructure.Agent.DeepSeek;

public sealed class DeepSeekOptions
{
    public const string SectionName = "DeepSeek";

    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    public string Model { get; set; } = "deepseek-chat";

    public int TimeoutSeconds { get; set; } = 60;
}