namespace Nutrition.Infrastructure.Agent.NutritionLookup;

public sealed class NutritionEvidenceCandidate
{
    public string ProductName { get; init; } = string.Empty;

    public string? Brand { get; init; }

    public decimal? ServingSize { get; init; }

    public string? ServingUnit { get; init; }

    public NutritionValueBasis ValueBasis { get; init; }

    public decimal? Calories { get; init; }

    public decimal? Protein { get; init; }

    public decimal? Fat { get; init; }

    public decimal? Carbs { get; init; }

    public string SourceUrl { get; init; } = string.Empty;

    public decimal? Confidence { get; init; }
}