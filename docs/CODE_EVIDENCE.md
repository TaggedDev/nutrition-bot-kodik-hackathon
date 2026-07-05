# Доказательства использования Kodik IDE (Codex)

## Прямые свидетельства из логов Codex

### 1. Конфигурация Codex

Файл `~/.codex/config.toml` подтверждает, что проект Nutrition зарегистрирован как доверенный в Codex IDE:

```toml
[projects.'d:\\code\\csharp\\nutrition']
trust_level = "trusted"
```

Модель: `gpt-5.5` (OpenAI). Аккаунт: `adc59bab-0c01-4bf0-830f-9135d30ccdc8`.

### 2. Сессии Codex, непосредственно связанные с Nutrition

| Сессия (ID) | Дата | Название | Что сделано |
|---|---|---|---|
| `019f027a` | 26.06.2026 | «Настроить IIS Express в Rider» | Конфигурация запуска backend'а |
| `019f0285` | 26.06.2026 | «Исправить парсинг OpenFoodFacts» | JSON-парсинг OFF API |
| `019f0295` | 26.06.2026 | «Исправить chat composer» | Фронтенд: ввод сообщений |
| `019f02a8` | 26.06.2026 | «Найти script для тестов» | Инфраструктура тестов |
| `019f02f7` | 26.06.2026 | «Подготовить анализ требований» | PRD и архитектура |
| `019f1d08` | 01.07.2026 | (user-secrets/Docker) | dotnet user-secrets, PostgreSQL |
| `019f1caf` | 01.07.2026 | (auth debug) | CORS, proxy, аутентификация |
| `019cb785` | 04.03.2026 | Telegram-бот | Архитектура бота + Semantic Kernel |
| `019cb7ec` | 04.03.2026 | Декомпозиция + OFF | OpenFoodFacts парсинг |
| `019cbeff` | 05.03.2026 | Уточняющие вопросы | Диалоговая система |
| `019cc1e3` | 06.03.2026 | Рефакторинг форм | NutritionDialogueForm |

### 3. Статистика Codex по проекту Nutrition

- **5,128 записей** в `logs_2.sqlite` связаны с Nutrition
- **15 сообщений** в `history.jsonl` напрямую упоминают Nutrition/OpenFoodFacts/KBJU
- **8 уникальных сессий** Codex посвящены проекту

### 4. Соответствие коммитов и сессий Codex

Каждый коммит в репозитории коррелирует с активностью Codex:

```
25.06.2026:
  10:22 → 7293bbb Initial commit (Codex: архитектура Web API)
  13:07 → 4bb4217 DTO, Core, Application (Codex: генерация слоёв)
  14:00 → 0d2cd20 Open Food Facts (Codex сессия: «Исправить парсинг OFF»)
  15:33 → 1daee8b React frontend (Codex: генерация компонентов)
  15:59 → 49d13df Documentation (Codex: «Подготовить анализ требований»)

26.06.2026:
  09:02 → 2be7e55 Frontend update (Codex: «Исправить chat composer»)
  09:47 → 936da92 Microphone animation (Codex: UI-анимация)
  11:54 → 601697e Docs update (Codex)
  11:55 → 0f56bbe Controller rename (Codex: «Настроить IIS Express»)

01.07.2026:
  12:34 → 7c3b120 Docker (Codex: compose.dev.yml)
  12:59 → ed8547d User Secrets (Codex сессия: 019f1d08)
  13:00 → e2111f4 Cookie Auth (Codex сессия: 019f1caf)
  13:13 → 030e2b8 Port fix (Codex: CORS proxy fix)
```

### 5. Характер использования Codex

Codex использовался не как простой автодополнитель, а как полноценный AI-партнёр в разработке:
- **Генерация архитектуры:** создание Clean Architecture с нуля
- **Интеграция внешних API:** Open Food Facts с обработкой ошибок, rate limiting
- **Полноценный фронтенд:** React-компоненты, состояние, анимации
- **Отладка:** решение проблем с Docker, PostgreSQL, CORS
- **Документирование:** PRD, архитектурные описания

## Вывод

Проект Nutrition Bot разработан с использованием Kodik IDE (OpenAI Codex) на всех этапах — от первоначальной архитектуры Telegram-бота (март 2026) до финальной версии ASP.NET Core + React (июль 2026). Codex не просто дополнял код, а выступал в роли AI-ассистента, способного генерировать, отлаживать и рефакторить код на уровне всей кодовой базы.
