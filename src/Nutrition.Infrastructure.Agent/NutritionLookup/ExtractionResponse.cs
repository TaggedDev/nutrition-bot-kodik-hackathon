namespace Nutrition.Infrastructure.Agent.NutritionLookup;

public sealed class ExtractionResponse
{
    public IReadOnlyCollection<NutritionEvidenceCandidate> Candidates { get; init; } =
        Array.Empty<NutritionEvidenceCandidate>();
}