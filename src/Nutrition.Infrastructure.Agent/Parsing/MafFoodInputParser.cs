using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Nutrition.Infrastructure.Agent.Parsing;

public sealed class MafFoodInputParser(IChatClient chatClient) : IFoodInputParser
{
    private const string Instructions = """
                                        Ты разбираешь пользовательский запрос о еде в JSON-объект с полем items.
                                        Не ищи и не рассчитывай КБЖУ.

                                        Для каждой самостоятельной позиции верни productName, quantity, unit, brand, preparation и kind.
                                        quantity по умолчанию 1; unit по умолчанию serving; brand и preparation — null, если они не названы.
                                        MassMarketProduct — магазинный продукт; PreparedFood — блюдо ресторана, кафе, доставки или готовой кулинарии; Unknown — только при реальной неоднозначности.
                                        Не разделяй единое блюдо на ингредиенты. Явно перечисленные блюда верни отдельными items.
                                        Верни только JSON без Markdown. Если еды нет, верни { "items": [] }.
                                        """;

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    static MafFoodInputParser()
    {
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private readonly ChatClientAgent _agent = new(chatClient, Instructions, name: "nutrition-food-input-parser",
        description: "Формирует json модель для open food facts");

    public async Task<IReadOnlyCollection<FoodUnit>> ParseAsync(string userInput, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userInput)) return Array.Empty<FoodUnit>();

        var runOptions = new ChatClientAgentRunOptions(new ChatOptions
        {
            Temperature = 0,
            MaxOutputTokens = 600,
            ResponseFormat = ChatResponseFormat.ForJsonSchema<FoodUnitParseResponse>(_jsonOptions,
                schemaName: "food_unit_parse_response",
                schemaDescription: "Объекты еды для openfoodfacts из свободной модели ввода")
        });

        var response = await _agent.RunAsync<FoodUnitParseResponse>(userInput.Trim(), session: null,
            serializerOptions: _jsonOptions, options: runOptions, cancellationToken: cancellationToken);

        return Validate(response.Result);
    }

    private static IReadOnlyCollection<FoodUnit> Validate(FoodUnitParseResponse? response)
    {
        if (response?.Items is null)
            return Array.Empty<FoodUnit>();

        return response.Items.Where(item => !string.IsNullOrWhiteSpace(item.ProductName)).Select(item => new FoodUnit
        {
            ProductName = item.ProductName.Trim(),
            Quantity = item.Quantity <= 0 ? 1 : item.Quantity,
            Unit = string.IsNullOrWhiteSpace(item.Unit) ? "serving" : item.Unit.Trim(),
            Brand = string.IsNullOrWhiteSpace(item.Brand) ? null : item.Brand.Trim(),
            Preparation = string.IsNullOrWhiteSpace(item.Preparation) ? null : item.Preparation.Trim(),
            Kind = item.Kind
        }).ToArray();
    }
}
