using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Nutrition.Infrastructure.Agent.Parsing;

public sealed class MafFoodInputParser(IChatClient chatClient) : IFoodInputParser
{
    private const string Instructions = """
                                        You are a food input parser for a nutrition application.
                                        Convert the user's free-form food message into JSON only.

                                        Rules:
                                        - Split compound meals into separate searchable food products.
                                        - Return product names suitable for nutrition lookup.
                                        - Preserve brand only when the user explicitly mentions it.
                                        - Put supermarket, manufacturer, cafe, restaurant, delivery service, or private-label names into brand when mentioned.
                                        - Classify each item as:
                                          MassMarketProduct: packaged or raw grocery product that can be bought in mass market stores.
                                          PreparedFood: ready-to-eat dish from a cafe, restaurant, delivery service, or store culinary/private label.
                                          Unknown: ambiguous item.
                                        - If quantity is unknown, use 1.
                                        - If unit is unknown, use "serving".
                                        - Do not calculate nutrition.
                                        - Do not include explanations, markdown, or extra fields.
                                        - Examples:
                                          "макароны вермишель макфа" -> productName "макароны вермишель", brand "макфа", kind "MassMarketProduct".
                                          "курица грудка 120 грамм петелинка и макароны makfa" -> two items, both MassMarketProduct, preserve brands.
                                          "гаспачо creative kitchen самокат" -> productName "гаспачо", brand "creative kitchen самокат", kind "PreparedFood".
                                          "суши ролл с крабом prime cafe" -> productName "суши ролл с крабом", brand "prime cafe", kind "PreparedFood".

                                        Return this exact JSON shape:
                                        {
                                          "items": [
                                            {
                                              "productName": "pasta",
                                              "quantity": 1,
                                              "unit": "serving",
                                              "brand": null,
                                              "preparation": null,
                                              "kind": "MassMarketProduct"
                                            }
                                          ]
                                        }
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
        description: "Parses free-form meal text into Open Food Facts search units.");

    public async Task<IReadOnlyCollection<FoodUnit>> ParseAsync(string userInput, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return Array.Empty<FoodUnit>();

        var runOptions = new ChatClientAgentRunOptions(new ChatOptions
        {
            Temperature = 0,
            MaxOutputTokens = 600,
            ResponseFormat = ChatResponseFormat.ForJsonSchema<FoodUnitParseResponse>(_jsonOptions,
                schemaName: "food_unit_parse_response",
                schemaDescription: "Food units parsed from a free-form user meal input.")
        });

        var response = await _agent.RunAsync<FoodUnitParseResponse>(userInput.Trim(), session: null,
            serializerOptions: _jsonOptions, options: runOptions, cancellationToken: cancellationToken);

        return Validate(response.Result);
    }

    private static IReadOnlyCollection<FoodUnit> Validate(FoodUnitParseResponse? response)
    {
        if (response?.Items is null)
        {
            return Array.Empty<FoodUnit>();
        }

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