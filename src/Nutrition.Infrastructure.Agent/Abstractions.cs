namespace Nutrition.Infrastructure.Agent;

/// <summary>
/// Абстракция для агента, который парсит человеческий ввод и разбивает его на пищевые единицы.
/// </summary>
public interface IFoodInputParser
{
    /// <summary>
    /// Парсит человеческий ввод и возвращает структурированные пищевые единицы.
    /// </summary>
    /// <param name="userInput">Человеческий ввод (напр. "2 яйца и 150г греческого йогурта")</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Коллекция распарсенных пищевых единиц</returns>
    Task<IReadOnlyCollection<FoodUnit>> ParseAsync(string userInput, CancellationToken cancellationToken);
}

/// <summary>
/// Структурированная пищевая единица после парсинга.
/// </summary>
public record FoodUnit
{
    /// <summary>
    /// Название продукта (напр. "яйцо", "йогурт греческий")
    /// </summary>
    public required string ProductName { get; init; }

    /// <summary>
    /// Количество (напр. 2, 150)
    /// </summary>
    public decimal Quantity { get; init; }

    /// <summary>
    /// Единица измерения (напр. "шт", "г", "мл")
    /// </summary>
    public required string Unit { get; init; }

    /// <summary>
    /// Опциональный бренд продукта
    /// </summary>
    public string? Brand { get; init; }

    /// <summary>
    /// Опциональная информация о подготовке (напр. "вареное", "сырое")
    /// </summary>
    public string? Preparation { get; init; }
}

/// <summary>
/// Абстракция для поиска лучшего матча продукта в OpenFoodFacts для пищевой единицы.
/// </summary>
public interface IFoodMatcher
{
    /// <summary>
    /// Ищет лучший матч продукта для пищевой единицы в OpenFoodFacts.
    /// </summary>
    /// <param name="foodUnit">Пищевая единица для поиска</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Лучший матч продукта или null если не найден</returns>
    Task<FoodMatchResult?> FindBestMatchAsync(FoodUnit foodUnit, CancellationToken cancellationToken);
}

/// <summary>
/// Результат матчинга пищевой единицы с продуктом из OpenFoodFacts.
/// </summary>
public record FoodMatchResult
{
    /// <summary>
    /// Исходная пищевая единица
    /// </summary>
    public required FoodUnit SourceUnit { get; init; }

    /// <summary>
    /// ID продукта в OpenFoodFacts
    /// </summary>
    public required string ProductId { get; init; }

    /// <summary>
    /// Название найденного продукта
    /// </summary>
    public required string ProductName { get; init; }

    /// <summary>
    /// Оценка уверенности в матче (0-1)
    /// </summary>
    public decimal ConfidenceScore { get; init; }

    /// <summary>
    /// Количество найденных кандидатов (для выбора пользователем, если нужно)
    /// </summary>
    public int TotalCandidates { get; init; }
}
