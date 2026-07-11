# Агентская архитектура

## Актуальное состояние (2026-07-11)

Рабочий pipeline поиска: `MafFoodInputParser → OpenFoodFacts → Tavily Web Search → MafNutritionEvidenceExtractor`. Web Search запускается, когда OFF не вернул кандидатов или кандидат подготовленного блюда отклонён. Если исчерпаны все источники, API возвращает `serviceUnavailable`.

Приложение использует cookie-auth, PostgreSQL через `NutritionIdentityDbContext` и HTTP endpoints `/api/v1/profile/entry` и `/api/v1/profile/day` для сохранения и чтения записей по дню и типу приёма пищи. Описания in-memory repository ниже относятся к ранней версии проекта.

## Текущее состояние (на уровне кода)

Сейчас в репозитории реализован не полный агентный пайплайн из PRD, а базовый Nutrition-поток:

1. `Nutrition.Web` (ASP.NET Core API) принимает HTTP-запрос.
2. `Nutrition.Application` выполняет use-case и оркестрацию зависимостей.
3. `Nutrition.Application.Infrastructure.OpenFoodFacts` запрашивает внешние данные по продуктам.
4. `Nutrition.Shared` содержит DTO-контракты между слоями.
5. `Nutrition.Core` содержит доменные сущности, value objects и инварианты.

## Реально работающий поток

### 1) Поиск Nutrition по строке
- Endpoint: `GET /api/v1/Nutrition/search?query=...`
- Контроллер: `NutritionController`
- Сервис: `INutritionFactsLookupService` -> `OpenFoodFactsNutritionFactsLookupService`
- Источник данных: Open Food Facts (`https://world.openfoodfacts.org`)
- Результат: список `ProductNutritionDto` c `NutritionFacts`, `SourceType`, `SourceReference`, `ConfidenceScore`.

### 2) Use-cases для meal Nutrition
В Application уже есть use-cases и репозиторий для работы с записью приема пищи:
- `IGetMealNutritionUseCase` / `GetMealNutritionUseCase`
- `IUpdateMealNutritionUseCase` / `UpdateMealNutritionUseCase`
- `IMealNutritionRepository` (текущая реализация: `MockMealNutritionRepository`)

На текущем этапе эти use-cases не опубликованы отдельными HTTP endpoint'ами в `Nutrition.Web`.

## Что важно по правилам домена
- `ConfidenceScore` обязан быть в диапазоне `[0..1]`.
- Порог подтверждения: `< 0.70` (`ConfirmationThreshold`).
- Nutrition не могут быть отрицательными.
- Порция должна быть строго больше нуля.

## Ограничения текущей реализации
- Нет production-хранилища для meal данных (используется in-memory mock-репозиторий).
- Нет полного мультимодального пайплайна (voice/photo/OCR/LLM parser) в текущем коде.
- Нет endpoint'ов подтверждения meal entry, day summary и рекомендаций следующего приема пищи.

## Безопасность и эксплуатация
- В `Nutrition.Web` включен CORS policy `FrontendDev` для `localhost`/`127.0.0.1`.
- Swagger включается в `Development`.
- Внешний HTTP-клиент Open Food Facts зарегистрирован через `HttpClientFactory` с timeout 10 секунд.

