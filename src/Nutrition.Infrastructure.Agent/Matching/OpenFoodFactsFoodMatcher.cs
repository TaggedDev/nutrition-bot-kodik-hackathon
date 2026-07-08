using Nutrition.Application.Abstractions.Services;
using Nutrition.Infrastructure.Agent;

namespace Nutrition.Infrastructure.Agent.Matching;

/// <summary>
/// Реализация матчера для поиска пищевых единиц в OpenFoodFacts.
/// Использует существующий INutritionFactsLookupService для поиска кандидатов.
/// </summary>
public sealed class OpenFoodFactsFoodMatcher : IFoodMatcher
{
    private readonly INutritionFactsLookupService _lookupService;

    public OpenFoodFactsFoodMatcher(INutritionFactsLookupService lookupService)
    {
        _lookupService = lookupService ?? throw new ArgumentNullException(nameof(lookupService));
    }

    public Task<FoodMatchResult?> FindBestMatchAsync(FoodUnit foodUnit, CancellationToken cancellationToken)
    {
        // TODO: Реализовать логику матчинга:
        // 1. Генерировать варианты запросов (с брендом/без, разные варианты названия)
        // 2. Запрашивать кандидатов из OpenFoodFacts
        // 3. Вычислять скоры похожести (lexical similarity, brand match, nutrition completeness)
        // 4. Возвращать лучший матч или top-N вариантов
        // 5. Опционально: использовать LLM для переранжирования сомнительных случаев

        throw new NotImplementedException("OpenFoodFactsFoodMatcher требует реализации логики матчинга и скоринга.");
    }
}
