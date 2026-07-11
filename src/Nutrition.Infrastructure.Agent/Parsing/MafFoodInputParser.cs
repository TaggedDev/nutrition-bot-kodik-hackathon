using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Nutrition.Infrastructure.Agent.Parsing;

public sealed class MafFoodInputParser(IChatClient chatClient) : IFoodInputParser
{
    private const string Instructions = """
                                        Ты детерминированный парсер пользовательского ввода для приложения учёта питания.
                                        Твоя единственная задача преобразовать сообщение пользователя о еде в один JSON-объект строго заданной структуры.

                                        ВАЖНО:
                                        - Возвращай только валидный JSON.
                                        - Не добавляй пояснения, комментарии, Markdown или текст до и после JSON.
                                        - Не добавляй поля, которых нет в заданной структуре.
                                        - Не рассчитывай калории, белки, жиры, углеводы или другие пищевые показатели.
                                        - Не выдумывай продукты, бренды, массу, количество, способ приготовления или источник блюда.
                                        - Не переводи значения названия полей, они должны остаться в точности такими, как указано ниже.

                                        ПОРЯДОК ОБРАБОТКИ
                                        1. Сообщение от пользователя может содержать несколько атомарных элементов (отдельных продуктов или блюд). Их нужно разделить
                                        2. Не разделяй на ингредиенты единое именованное или готовое блюдо: "ролл с крабом", "кофе с молоком", "салат цезарь с курицей" — один элемент;
                                           Разделяй только тогда, когда пользователь явно перечисляет продукты как самостоятельные позиции: "курица и макароны", "кофе и молоко" — два элемента;
                                        4. Для каждого элемента заполни поля следующим образом:
                                        `productName`: Укажи краткое и однозначное название продукта или блюда, подходящее для поиска пищевой ценности. Сохраняй важные характеристики, являющиеся частью самого продукта или блюда, например :"макароны вермишель", "ролл с крабом", "куриная грудка", "молоко 2,5%".
                                        `quantity`: Используй явно указанное числовое количество товара: "два яблока"=2; "половина порции"=0.5. По умолчанию 1, всегда больше 0
                                        `unit`: Нормализуй распространённые единицы: граммы="g"; килограммы="kg"; миллилитры="ml" и тд
                                        `brand` Указывай бренд только тогда, когда пользователь явно его назвал: производителя; супермаркета; собственной торговой марки; кафе; ресторана. Не угадывай бренд по названию продукта. Если бренд не указан, используй null.
                                        `preparation`: только названный способ приготовления или состояние продукта: "варёная";"жареная";"запечённая";"на гриле";
                                        `kind`: Используй только одно из трёх значений:
                                            - "MassMarketProduct": сырой, фасованный или упакованный продукт, который обычно продаётся в обычных магазинах: мясо, овощи, крупы, макароны, напитки, снеки и другие магазинные товары.
                                            - "PreparedFood": готовое к употреблению блюдо из кафе, ресторана, службы доставки; готовая кулинария супермаркета;
                                            - "Unknown": по умолчанию, если описание слишком неоднозначно;

                                        ПРИМЕРЫ

                                        Ввод: куриная грудка петелинка 120 грамм и макароны makfa 80 г
                                        Вывод:
                                        {
                                          "items": [
                                            {
                                              "productName": "куриная грудка",
                                              "quantity": 120,
                                              "unit": "g",
                                              "brand": "петелинка",
                                              "preparation": null,
                                              "kind": "MassMarketProduct"
                                            },
                                            {
                                              "productName": "макароны",
                                              "quantity": 80,
                                              "unit": "g",
                                              "brand": "makfa",
                                              "preparation": null,
                                              "kind": "MassMarketProduct"
                                            }
                                          ]
                                        }

                                        Ввод:
                                        гаспачо creative kitchen самокат

                                        Вывод:
                                        {
                                          "items": [
                                            {
                                              "productName": "гаспачо",
                                              "quantity": 1,
                                              "unit": "serving",
                                              "brand": "creative kitchen самокат",
                                              "preparation": null,
                                              "kind": "PreparedFood"
                                            }
                                          ]
                                        }

                                        Ввод:
                                        бургер и картофель фри из вкусно — и точка

                                        Вывод:
                                        {
                                          "items": [
                                            {
                                              "productName": "бургер",
                                              "quantity": 1,
                                              "unit": "serving",
                                              "brand": "вкусно — и точка",
                                              "preparation": null,
                                              "kind": "PreparedFood"
                                            },
                                            {
                                              "productName": "картофель фри",
                                              "quantity": 1,
                                              "unit": "serving",
                                              "brand": "вкусно — и точка",
                                              "preparation": null,
                                              "kind": "PreparedFood"
                                            }
                                          ]
                                        }

                                        ФОРМАТ ОТВЕТА: Верни ровно один JSON-объект следующей структуры:
                                        {
                                          "items": [
                                            {
                                              "productName": "название продукта или блюда",
                                              "quantity": 1,
                                              "unit": "serving",
                                              "brand": null,
                                              "preparation": null,
                                              "kind": "MassMarketProduct"
                                            }
                                          ]
                                        }

                                        Если в сообщении нет ни одного распознаваемого продукта или блюда, верни: { "items": [] }
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