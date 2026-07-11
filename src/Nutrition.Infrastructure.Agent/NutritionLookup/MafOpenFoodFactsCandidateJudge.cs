using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Nutrition.Shared.Dtos;

namespace Nutrition.Infrastructure.Agent.NutritionLookup;

public sealed class MafOpenFoodFactsCandidateJudge : IOpenFoodFactsCandidateJudge
{
    private const string Instructions = """
                                        You judge whether OpenFoodFacts candidates match the requested food.
                                        Use only the provided request and candidates.
                                        Accept candidates only when product name/brand clearly match the request.
                                        For prepared food from cafes/restaurants/delivery, be strict: reject generic groceries or different brands.
                                        Return JSON only. Do not explain.
                                        """;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static MafOpenFoodFactsCandidateJudge()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private readonly ChatClientAgent _agent;

    public MafOpenFoodFactsCandidateJudge(IChatClient chatClient)
    {
        _agent = new ChatClientAgent(chatClient, Instructions, name: "nutrition-off-candidate-judge",
            description: "Checks whether OpenFoodFacts candidates match a requested food object.");
    }

    public async Task<IReadOnlyCollection<ProductNutritionDto>> SelectAcceptableAsync(FoodUnit foodUnit,
        IReadOnlyCollection<ProductNutritionDto> candidates, CancellationToken cancellationToken)
    {
        if (candidates.Count == 0)
        {
            return Array.Empty<ProductNutritionDto>();
        }

        var prompt = BuildPrompt(foodUnit, candidates.Take(3).ToArray());
        var options = new ChatClientAgentRunOptions(new ChatOptions
        {
            Temperature = 0,
            MaxOutputTokens = 500,
            ResponseFormat = ChatResponseFormat.ForJsonSchema<JudgeResponse>(JsonOptions,
                schemaName: "open_food_facts_candidate_judge_response",
                schemaDescription: "Accepted OpenFoodFacts candidate ids for the requested food.")
        });

        HashSet<string> acceptedIds;
        try
        {
            var response = await _agent.RunAsync<JudgeResponse>(prompt, session: null, serializerOptions: JsonOptions,
                options: options, cancellationToken: cancellationToken);
            acceptedIds =
                response.Result?.AcceptedProductIds?.Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase) ??
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            acceptedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        if (acceptedIds.Count == 0)
        {
            return Array.Empty<ProductNutritionDto>();
        }

        return candidates.Where(candidate => acceptedIds.Contains(candidate.ProductId)).Take(3).ToArray();
    }

    private static string BuildPrompt(FoodUnit foodUnit, IReadOnlyCollection<ProductNutritionDto> candidates)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Requested food:");
        builder.AppendLine($"name: {foodUnit.ProductName}");
        builder.AppendLine($"brand: {foodUnit.Brand ?? "(none)"}");
        builder.AppendLine($"kind: {foodUnit.Kind}");
        builder.AppendLine();
        builder.AppendLine("Candidates:");

        foreach (var candidate in candidates)
        {
            builder.AppendLine($"id: {candidate.ProductId}");
            builder.AppendLine($"name: {candidate.ProductName}");
            builder.AppendLine($"brand: {candidate.Brand ?? "(none)"}");
            builder.AppendLine($"source: {candidate.SourceReference}");
            builder.AppendLine();
        }

        builder.AppendLine("Return acceptedProductIds. Return an empty array when none clearly match.");
        return builder.ToString();
    }

    public sealed class JudgeResponse
    {
        public IReadOnlyCollection<string> AcceptedProductIds { get; init; } = Array.Empty<string>();
    }
}