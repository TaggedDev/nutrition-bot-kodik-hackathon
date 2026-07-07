using Microsoft.Agents.AI;
using Nutrition.Infrastructure.Agent;

namespace Nutrition.Infrastructure.Agent.Parsing;

/// <summary>
/// Реализация парсера на базе Microsoft Agentic Framework.
/// Использует LLM для разбора человеческого ввода в структурированные пищевые единицы.
/// </summary>
public sealed class MafFoodInputParser : IFoodInputParser
{
    // TODO: Инициализировать агента с MAF
    // Требуется:
    // - Регистрация LLM-провайдера (OpenAI, Azure OpenAI, итд)
    // - Определение структурированного выхода (FoodUnit[])
    // - Настройка prompt для парсинга еды
    // - Валидация результата

    public Task<IReadOnlyCollection<FoodUnit>> ParseAsync(string userInput, CancellationToken cancellationToken)
    {
        throw new NotImplementedException("MafFoodInputParser требует инициализации MAF-агента и LLM-провайдера.");
    }
}
