# API Contracts (MVP)

Документ фиксирует рабочие контракты для MVP AI Food Logging Assistant на основе `PRD.md`, `CLEAN_ARCHITECTURE.md` и `AGENTIC_ARCHITECTURE.md`.

## 1) Инварианты домена (обязательные правила)

1. `confidenceScore` всегда в диапазоне `[0..1]`.
2. Если `confidenceScore < 0.70`, запись помечается как требующая подтверждения пользователя.
3. Значения КБЖУ не могут быть отрицательными.
4. Порция (`amount`) должна быть строго больше `0`.
5. У каждого рассчитанного КБЖУ должен быть источник (`source.type` + `source.reference`).

## 2) Контракт разбора ввода (Agent -> API)

### `POST /api/v1/intake/parse`

Назначение: принять мультимодальный ввод и вернуть черновик приёма пищи.

#### Request

```json
{
  "userId": "uuid",
  "mealTypeHint": "Breakfast|Lunch|Dinner|Snack|null",
  "inputChannel": "Text|Voice|PhotoLabel|PhotoDish|Barcode",
  "text": "string|null",
  "voiceTranscription": "string|null",
  "imageUrls": ["https://..."],
  "clientTime": "2026-06-25T09:30:00+03:00"
}
```

#### Response

```json
{
  "draftId": "uuid",
  "mealType": "Breakfast",
  "loggedAtUtc": "2026-06-25T06:30:00Z",
  "items": [
    {
      "itemId": "uuid",
      "productName": "Greek yogurt",
      "portion": {
        "amount": 200,
        "unit": "Gram"
      },
      "nutrition": {
        "calories": 118,
        "protein": 20,
        "fat": 0.8,
        "carbs": 7.2
      },
      "confidenceScore": 0.91,
      "source": {
        "type": "Usda",
        "reference": "USDA:170859"
      },
      "requiresConfirmation": false
    }
  ],
  "totalNutrition": {
    "calories": 118,
    "protein": 20,
    "fat": 0.8,
    "carbs": 7.2
  },
  "requiresUserConfirmation": false,
  "clarificationQuestions": []
}
```

## 3) Контракт подтверждения и сохранения записи

### `POST /api/v1/meals/confirm`

Назначение: подтвердить/отредактировать черновик и сохранить meal entry.

#### Request

```json
{
  "draftId": "uuid",
  "overrides": [
    {
      "itemId": "uuid",
      "portion": {
        "amount": 250,
        "unit": "Gram"
      }
    }
  ]
}
```

#### Response

```json
{
  "mealEntryId": "uuid",
  "savedAtUtc": "2026-06-25T06:31:02Z",
  "status": "Saved"
}
```

## 4) Контракт сводки за день

### `GET /api/v1/meals/day-summary?date=2026-06-25`

#### Response

```json
{
  "date": "2026-06-25",
  "consumed": {
    "calories": 1460,
    "protein": 98,
    "fat": 47,
    "carbs": 154
  },
  "goal": {
    "calories": 2000,
    "protein": 130,
    "fat": 60,
    "carbs": 220
  },
  "remaining": {
    "calories": 540,
    "protein": 32,
    "fat": 13,
    "carbs": 66
  }
}
```

## 5) Контракт рекомендаций следующего приёма пищи

### `GET /api/v1/recommendations/next-meal?date=2026-06-25`

#### Response

```json
{
  "recommendations": [
    {
      "title": "Ужин с упором на белок",
      "rationale": "Недобор белка 32г при оставшемся лимите 540 ккал",
      "suggestedNutrition": {
        "calories": 480,
        "protein": 35,
        "fat": 16,
        "carbs": 45
      },
      "confidenceScore": 0.82
    }
  ]
}
```

## 6) Доменные типы в `Nutrition.Core`

В `src/Nutrition.Core` используются следующие основные доменные контракты:

- `MealEntry` - агрегат приёма пищи.
- `MealItem` - элемент приёма пищи (продукт + порция + КБЖУ + confidence + source).
- `FoodProduct` - каталоговый продукт (название, бренд, штрихкод, КБЖУ на 100г).
- `DailyNutritionGoal` - целевые КБЖУ пользователя на день.
- `UserFoodPreference` - предпочтения и ограничения пользователя.
- `NutritionFacts`, `Portion`, `ConfidenceScore`, `NutritionSource` - value objects.
- `MealType`, `InputChannel`, `PortionUnit`, `NutritionSourceType` - перечисления домена.

Эти контракты используются как основа для Application use-cases и API DTO, без утечки инфраструктурных деталей в Domain слой.
