# Nutrition Bot — AI-инструмент для удобного логирования питания

**Хакатон Kodik IDE, июнь-июль 2026**

## О проекте

Nutrition Bot — это AI-ориентированный инструмент для быстрого и удобного добавления приёмов пищи в любом формате: текст, голос, фото упаковки или штрихкода. Проект создан в рамках хакатона Kodik IDE и использует возможности AI-ассистента Codex для ускорения разработки.

**Ключевая идея:** пользователь тратит 10-20 секунд на запись приёма пищи вместо нескольких минут ручного поиска продуктов и расчёта КБЖУ.

## Текущий этап разработки (MVP)

Реализовано:
- **Backend:** ASP.NET Core 8 Web API с Clean Architecture (Domain → Application → Infrastructure → Web)
- **Интеграция с Open Food Facts:** поиск продуктов по тексту и штрихкоду с расчётом КБЖУ (калории, белки, жиры, углеводы), confidence score, rate limiting и кеширование
- **Аутентификация:** Cookie-based auth (register/login/logout) через ASP.NET Core Identity + PostgreSQL
- **Use Cases:** GetMealNutrition, UpdateMealNutrition (пока с mock-репозиторием)
- **Frontend:** React SPA с чат-интерфейсом, поддержка текстового ввода, прикрепления файлов, записи голоса
- **Docker:** docker-compose для локальной разработки (backend + PostgreSQL + pgAdmin)

В процессе:
- Подключение production-хранилища для meal-данных
- Полный мультимодальный пайплайн (voice → STT, photo → OCR, LLM-парсер)
- Endpoint'ы подтверждения meal entry, дневной сводки и рекомендаций

## Технический стек

| Слой | Технология |
|------|-----------|
| Backend API | ASP.NET Core 8, C# |
| Архитектура | Clean Architecture (Domain / Application / Infrastructure / Web) |
| База данных | PostgreSQL (через EF Core) |
| Аутентификация | ASP.NET Core Identity, Cookie Auth |
| Frontend | React 18 + TypeScript + Vite |
| Внешние API | Open Food Facts (REST) |
| Контейнеризация | Docker, Docker Compose |
| AI-ассистент | OpenAI Codex (Kodik IDE) — использовался на всех этапах разработки |
| Тестирование | xUnit |

## Структура проекта

```
Nutrition/
├── docs/                    # Документация (PRD, архитектура, API-контракты)
├── src/
│   ├── Nutrition.Core/              # Domain-сущности и бизнес-правила
│   ├── Nutrition.Application/       # Use Cases, интерфейсы, сервисы
│   ├── Nutrition.Infrastructure.Identity/  # Identity + EF Core
│   ├── Nutrition.Shared/            # DTO-контракты
│   ├── Nutrition.Web/               # ASP.NET Core API
│   └── nutrition-frontend/          # React SPA
├── tests/
│   └── Nutrition.Application.Tests/ # Unit-тесты
├── compose.dev.yml           # Docker Compose для разработки
└── Nutrition.Web.sln         # .NET Solution
```

## Интеграционные и браузерные тесты

Backend integration tests запускают изолированный PostgreSQL Testcontainer и подменяют OFF, Tavily и DeepSeek на HTTP-уровне:

```powershell
dotnet test tests/Nutrition.Integration.Tests/Nutrition.Integration.Tests.csproj --filter "Category!=LiveSmoke"
```

Browser E2E поднимают `compose.e2e.yml`, используют настоящие auth/profile API и PostgreSQL, стабилизируя только ответ nutrition search:

```powershell
cd src/nutrition-frontend
npx playwright install chromium
npm run test:e2e
```

Opt-in live smoke требует `RUN_LIVE_SMOKE=1`, `DEEPSEEK_API_KEY` и `TAVILY_API_KEY` и запускается с фильтром `Category=LiveSmoke`.

## Автор

**Денис Миков** — участник хакатона Kodik IDE
- Email: denis.mikov.06@gmail.com
- GitHub: [TaggedDev](https://github.com/TaggedDev)
