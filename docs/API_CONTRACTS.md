# API Contracts (текущее состояние)

Документ фиксирует контракты, которые реально отражены в коде на текущий момент.

## 1) Публичный HTTP контракт (`Nutrition.Web`)

### `GET /api/v1/Nutrition/search?query={text}`

Назначение: найти продукты и вернуть Nutrition (на 100г) из Open Food Facts.

#### Query parameters
- `query` (string, required) - поисковая строка.

#### Responses

- `200 OK` - список `ProductNutritionDto`.
- `400 Bad Request` - если `query` пустой или состоит только из пробелов.

#### Пример `200 OK`
```json
[
  {
    "productId": "3229820129488",
    "productName": "Skyr",
    "brand": "Lidl",
    "nutritionFacts": {
      "calories": 63,
      "protein": 11,
      "fat": 0.2,
      "carbs": 3.8
    },
    "sourceType": "OpenFoodFacts",
    "sourceReference": "OFF:3229820129488",
    "confidenceScore": 0.9
  }
]
```

## 2) DTO-контракты (`Nutrition.Shared`)

### `ProductNutritionDto`
```json
{
  "productId": "string",
  "productName": "string",
  "brand": "string|null",
  "nutritionFacts": {
    "calories": "decimal",
    "protein": "decimal",
    "fat": "decimal",
    "carbs": "decimal"
  },
  "sourceType": "string",
  "sourceReference": "string",
  "confidenceScore": "decimal"
}
```

### `MealEntryDto`
```json
{
  "mealEntryId": "guid",
  "userId": "guid",
  "mealType": "string",
  "loggedAtUtc": "datetimeoffset",
  "totalNutrition": {
    "calories": "decimal",
    "protein": "decimal",
    "fat": "decimal",
    "carbs": "decimal"
  },
  "items": [
    {
      "itemId": "guid",
      "productName": "string",
      "portionAmount": "decimal",
      "portionUnit": "string",
      "Nutrition": {
        "calories": "decimal",
        "protein": "decimal",
        "fat": "decimal",
        "carbs": "decimal"
      },
      "confidenceScore": "decimal",
      "sourceType": "string",
      "sourceReference": "string"
    }
  ]
}
```

## 3) Application-контракты (внутренние, не HTTP)

Сейчас в `Nutrition.Application` реализованы use-cases:

- `GetMealNutritionUseCase`
  - Request: `GetMealNutritionRequestDto { userId, mealEntryId }`
  - Response: `GetMealNutritionResponseDto { meal } | null`
- `UpdateMealNutritionUseCase`
  - Request: `UpdateMealNutritionRequestDto { userId, mealEntryId, totalNutrition }`
  - Response: `UpdateMealNutritionResponseDto { mealEntryId, totalNutrition, updatedAtUtc } | null`

`null` возвращается при невалидном запросе (например, пустые `Guid` или отрицательные значения Nutrition).

## 4) Доменные инварианты (`Nutrition.Core`)

- `ConfidenceScore` в диапазоне `[0..1]`.
- Порог подтверждения: `< 0.70`.
- Значения Nutrition не могут быть отрицательными.
- Порция должна быть строго больше `0`.

## 5) Ограничения текущей версии

- На уровне HTTP опубликован только endpoint поиска (`/api/v1/Nutrition/search`).
- Операции получения/обновления meal Nutrition пока доступны только как Application use-cases.
- В качестве репозитория для meal Nutrition используется `MockMealNutritionRepository` (in-memory).

