# Clean Architecture Rules for Backend

## Goal

Backend code must be structured so that business logic is independent from frameworks, databases, HTTP, external APIs, AI models, file storage and UI.

The project is split into separate class libraries:

```text
src/
  NutritionAssistant.Api/
  NutritionAssistant.Application/
  NutritionAssistant.Domain/
  NutritionAssistant.Infrastructure/
  NutritionAssistant.Contracts/
```

The main rule:

> Business rules must live in the inner layers. Technical details must live in the outer layers.

The backend must be designed so that we can replace PostgreSQL, EF Core, Python AI service, React frontend, external food APIs, OCR provider or LLM provider without rewriting the domain model and core use cases.

---

# 1. Layer Responsibilities

## NutritionAssistant.Domain

The Domain project contains the core business model.

It answers the question:

> What is true in the nutrition domain regardless of database, API, UI or AI?

Domain contains:

* Entities
* Value Objects
* Domain Events
* Domain Exceptions
* Domain Services, only when business logic does not naturally belong to one entity
* Business invariants
* Business calculations

Examples:

```text
Meal
MealItem
FoodProduct
NutritionFacts
Portion
ConfidenceScore
MealType
NutritionGoal
UserFoodPreference
```

Domain may contain logic like:

```text
Calculate total meal nutrition
Validate that nutrition values are not negative
Validate that portion is positive
Validate that confidence score is between 0 and 1
Check whether meal item requires confirmation
Check whether daily nutrition goal is exceeded
```

Domain must not contain:

```text
DbContext
EF Core attributes
HTTP requests
Controllers
DTOs for API
JSON serialization logic
React-specific models
OpenFoodFacts client
USDA client
Python AI client
LLM prompts
File system access
Docker configuration
```

Domain must be the cleanest and most stable part of the system.

---

## NutritionAssistant.Application

The Application project contains use cases.

It answers the question:

> What can the user or system do with the domain?

Application orchestrates domain objects, validations, repositories and external service interfaces.

Application contains:

* Commands
* Queries
* Use Case Handlers
* Application Services
* Interfaces for repositories
* Interfaces for external services
* Application DTOs if they are internal to use cases
* Validation rules for commands
* Transaction boundaries
* Authorization checks at use-case level if needed

Examples:

```text
CreateMealDraftCommand
CreateMealDraftHandler
ConfirmMealDraftCommand
ConfirmMealDraftHandler
GetDaySummaryQuery
GetDaySummaryHandler
GenerateMealRecommendationQuery
GenerateMealRecommendationHandler
UpdateMealItemCommand
DeleteMealEntryCommand
```

Application defines interfaces such as:

```csharp
public interface IMealRepository
{
    Task<Meal?> GetByIdAsync(MealId id, CancellationToken cancellationToken);
    Task AddAsync(Meal meal, CancellationToken cancellationToken);
}

public interface IFoodSearchService
{
    Task<IReadOnlyList<FoodCandidate>> SearchAsync(string query, CancellationToken cancellationToken);
}

public interface IAiMealParser
{
    Task<MealDraftResult> ParseAsync(string userInput, CancellationToken cancellationToken);
}
```

Application may depend on:

```text
Domain
Contracts, only if Contracts are stable cross-boundary DTOs
```

Application must not depend on:

```text
Api
Infrastructure
EF Core
ASP.NET Core
PostgreSQL
HTTP client implementations
Python service implementation
React
Docker
```

Application is allowed to know that it needs an `IAiMealParser`, but it must not know that this parser is implemented by a Python FastAPI service.

Application is allowed to know that it needs an `IFoodSearchService`, but it must not know whether data comes from Open Food Facts, USDA, FatSecret, cache or local database.

---

## NutritionAssistant.Infrastructure

The Infrastructure project contains technical implementations.

It answers the question:

> How do we technically execute what Application asked for?

Infrastructure contains:

* EF Core DbContext
* EF Core configurations
* Repository implementations
* PostgreSQL access
* External HTTP clients
* Open Food Facts client
* USDA client
* Python AI service client
* OCR service client
* File export service
* Email service
* Cache implementation
* System clock implementation
* File storage implementation

Examples:

```text
NutritionDbContext
MealRepository
FoodProductRepository
OpenFoodFactsClient
UsdaFoodDataClient
PythonAiMealParserClient
PaddleOcrClient
ExcelExportService
RedisCacheService
```

Infrastructure implements interfaces from Application.

Example:

```csharp
public sealed class PythonAiMealParserClient : IAiMealParser
{
    // Calls Python FastAPI service through HTTP.
}
```

Infrastructure may depend on:

```text
Application
Domain
Contracts
```

Infrastructure must not be referenced by Domain.

Infrastructure must not contain business decisions.

Bad example:

```csharp
// Bad: business rule hidden in infrastructure
if (confidence < 0.6)
{
    mealItem.RequireConfirmation();
}
```

Good example:

```csharp
// Good: infrastructure only loads data.
// Application or Domain decides what to do with confidence.
var candidate = await foodApiClient.SearchAsync(query);
```

---

## NutritionAssistant.Api

The Api project contains the HTTP boundary.

It answers the question:

> How does the outside world talk to the application?

Api contains:

* Controllers or Minimal API endpoints
* HTTP request models
* HTTP response models
* Authentication setup
* Authorization policies
* Dependency Injection setup
* Middleware
* Filters
* Swagger/OpenAPI configuration
* Mapping between HTTP DTOs and Application commands/queries

Examples:

```text
MealsController
IntakeController
RecommendationsController
ExportsController
Program.cs
DependencyInjection.cs
ExceptionHandlingMiddleware
```

Api may depend on:

```text
Application
Infrastructure
Contracts
```

Api must not contain business logic.

Bad example:

```csharp
[HttpPost]
public async Task<IActionResult> CreateMeal(CreateMealRequest request)
{
    if (request.Confidence < 0.6)
    {
        request.RequiresConfirmation = true;
    }

    // Business rule in controller — bad.
}
```

Good example:

```csharp
[HttpPost]
public async Task<IActionResult> CreateMeal(CreateMealRequest request)
{
    var command = mapper.Map<CreateMealDraftCommand>(request);
    var result = await handler.Handle(command, cancellationToken);
    return Ok(result);
}
```

Controller responsibilities:

```text
Accept request
Validate basic HTTP model
Map request to command/query
Call Application use case
Map result to response
Return HTTP status
```

Controller must not:

```text
Calculate nutrition
Choose food source
Call EF Core directly
Call Python AI service directly
Call Open Food Facts directly
Contain domain rules
Contain long if/else business workflows
```

---

## NutritionAssistant.Contracts

The Contracts project contains stable cross-boundary models.

It answers the question:

> What data format is shared between processes or application boundaries?

Contracts may contain:

* Request/response DTOs shared between Api and frontend
* DTOs shared between C# backend and Python AI service
* DTOs for integration contracts
* API error models
* enums used in public contracts

Examples:

```text
CreateMealDraftRequest
CreateMealDraftResponse
MealDraftDto
MealItemDraftDto
NutritionFactsDto
FoodCandidateDto
AiMealParseRequest
AiMealParseResponse
ApiErrorResponse
```

Contracts must be simple.

Contracts should not contain business behavior.

Contracts may contain:

```text
properties
records
enums
simple validation attributes only when appropriate
```

Contracts must not contain:

```text
business calculations
EF Core attributes
repository interfaces
service interfaces
domain invariants
HTTP client logic
```

Important rule:

> Domain models and Contracts DTOs are not the same thing.

`Meal` is a domain entity.

`MealDto` is a transport model.

Do not use EF entities or Domain entities directly as API responses.

---

# 2. Strict Dependency Rules

## Allowed Dependencies

```text
NutritionAssistant.Domain
  -> no project dependencies

NutritionAssistant.Contracts
  -> no dependency on Api
  -> no dependency on Infrastructure
  -> no dependency on Application unless explicitly justified
  -> preferably no project dependencies

NutritionAssistant.Application
  -> Domain
  -> Contracts, optional

NutritionAssistant.Infrastructure
  -> Application
  -> Domain
  -> Contracts, optional

NutritionAssistant.Api
  -> Application
  -> Infrastructure
  -> Contracts
```

## Forbidden Dependencies

```text
Domain -> Application
Domain -> Infrastructure
Domain -> Api
Domain -> Contracts, unless there is a very strong reason

Application -> Infrastructure
Application -> Api
Application -> EF Core
Application -> ASP.NET Core
Application -> concrete external API clients

Infrastructure -> Api

Contracts -> Api
Contracts -> Infrastructure
Contracts -> EF Core
Contracts -> ASP.NET Core
```

## Dependency Direction

Allowed direction:

```text
Api  ───────────────┐
                    ↓
Infrastructure ─→ Application ─→ Domain
                    ↑
Contracts ──────────┘
```

Simpler mental model:

```text
Outer layers know inner layers.
Inner layers do not know outer layers.
```

Domain does not know where it is stored.

Application does not know how external services are implemented.

Infrastructure knows technical details.

Api knows HTTP details.

---

# 3. Rule for Adding New Code

When adding new code, ask these questions.

## Question 1: Is this pure business logic?

If yes, put it in `Domain`.

Examples:

```text
How to calculate total meal calories?
Can nutrition facts be negative?
Can a meal item have zero grams?
When does an item require confirmation?
What is a valid confidence score?
How to calculate remaining daily macros?
```

Put in:

```text
NutritionAssistant.Domain
```

---

## Question 2: Is this a user/system action?

If yes, put it in `Application`.

Examples:

```text
Create meal draft
Confirm meal draft
Update meal item
Delete meal
Get day summary
Generate recommendation
Import food from barcode
Export diary
```

Put in:

```text
NutritionAssistant.Application
```

Usually this becomes:

```text
Command
CommandHandler
Query
QueryHandler
Validator
```

---

## Question 3: Is this a technical implementation?

If yes, put it in `Infrastructure`.

Examples:

```text
Save meal to PostgreSQL
Load meal through EF Core
Call Open Food Facts API
Call USDA API
Call Python AI service
Call OCR service
Generate XLSX file
Use Redis cache
Read environment variables for external clients
```

Put in:

```text
NutritionAssistant.Infrastructure
```

---

## Question 4: Is this an HTTP endpoint?

If yes, put it in `Api`.

Examples:

```text
POST /api/intake/text
POST /api/intake/voice
POST /api/intake/image
GET /api/meals
GET /api/day-summary
GET /api/recommendations/dinner
```

Put in:

```text
NutritionAssistant.Api
```

---

## Question 5: Is this a DTO shared between boundaries?

If yes, put it in `Contracts`.

Examples:

```text
Request from React to C# backend
Response from C# backend to React
Request from C# backend to Python AI service
Response from Python AI service to C# backend
Public error response model
```

Put in:

```text
NutritionAssistant.Contracts
```

---

# 4. Examples by Feature

## Feature: Text Food Logging

User writes:

```text
"На завтрак съел 200 г творога 5%, банан и кофе с молоком"
```

Where to put code:

```text
Api:
  IntakeController
  POST /api/intake/text
  CreateTextIntakeRequest

Contracts:
  CreateTextIntakeRequest
  CreateMealDraftResponse
  MealDraftDto
  MealItemDraftDto

Application:
  CreateMealDraftFromTextCommand
  CreateMealDraftFromTextHandler
  IAiMealParser
  IFoodSearchService
  IMealDraftRepository

Domain:
  MealDraft
  MealItemDraft
  NutritionFacts
  Portion
  ConfidenceScore
  MealType

Infrastructure:
  PythonAiMealParserClient
  OpenFoodFactsClient
  UsdaFoodDataClient
  MealDraftRepository
```

Important rule:

```text
Controller must not call Python AI service directly.
Controller calls Application.
Application calls IAiMealParser interface.
Infrastructure implements IAiMealParser.
```

---

## Feature: Barcode Lookup

Where to put code:

```text
Api:
  BarcodeIntakeEndpoint

Contracts:
  BarcodeLookupRequest
  BarcodeLookupResponse

Application:
  CreateMealDraftFromBarcodeCommand
  CreateMealDraftFromBarcodeHandler
  IBarcodeFoodLookupService

Domain:
  FoodProduct
  NutritionFacts
  Portion

Infrastructure:
  OpenFoodFactsBarcodeLookupService
```

Bad:

```text
Barcode endpoint calls Open Food Facts directly.
```

Good:

```text
Barcode endpoint sends command to Application.
Application uses IBarcodeFoodLookupService.
Infrastructure implements OpenFoodFactsBarcodeLookupService.
```

---

## Feature: OCR from Package Photo

Where to put code:

```text
Api:
  POST /api/intake/image-label

Contracts:
  ImageLabelIntakeRequest
  ImageLabelIntakeResponse
  OcrNutritionTableDto

Application:
  CreateMealDraftFromImageLabelCommand
  CreateMealDraftFromImageLabelHandler
  IOcrService
  INutritionLabelParser

Domain:
  NutritionFacts
  Portion
  FoodProduct
  ConfidenceScore

Infrastructure:
  PaddleOcrService
  PythonNutritionLabelParserClient
```

Important rule:

```text
OCR is infrastructure.
Nutrition label interpretation is application workflow.
Nutrition validity is domain logic.
```

---

## Feature: Daily Summary

Where to put code:

```text
Api:
  GET /api/day-summary

Contracts:
  DaySummaryResponse
  MacroProgressDto

Application:
  GetDaySummaryQuery
  GetDaySummaryHandler
  IMealRepository

Domain:
  NutritionGoal
  NutritionFacts
  DailyNutritionSummary

Infrastructure:
  MealRepository
```

Important rule:

```text
Daily macro calculation should not live in controller.
```

---

## Feature: Recommendations

Where to put code:

```text
Api:
  GET /api/recommendations/dinner

Contracts:
  MealRecommendationResponse
  RecommendationOptionDto

Application:
  GenerateMealRecommendationQuery
  GenerateMealRecommendationHandler
  IRecommendationTextGenerator

Domain:
  NutritionGoal
  DailyNutritionSummary
  MacroDeficit
  MealRecommendationRule

Infrastructure:
  LlmRecommendationTextGenerator
```

Important rule:

```text
The decision that user needs more carbs or less protein is business logic.
The pretty natural language explanation can be generated by LLM in Infrastructure.
```

---

## Feature: Export to XLSX

Where to put code:

```text
Api:
  GET /api/export

Contracts:
  ExportRequest

Application:
  ExportNutritionDiaryQuery
  ExportNutritionDiaryHandler
  IExportFileGenerator

Domain:
  Meal
  MealItem
  NutritionFacts

Infrastructure:
  XlsxExportFileGenerator
```

Important rule:

```text
Application decides what data should be exported.
Infrastructure decides how XLSX is technically generated.
```

---

# 5. Validation Placement

Validation must happen on several levels.

## API validation

Checks HTTP-specific input.

Examples:

```text
Required fields
File size
Allowed content type
Valid date format
Valid enum value
```

Location:

```text
NutritionAssistant.Api
```

or request validators close to API boundary.

---

## Application validation

Checks use-case correctness.

Examples:

```text
User exists
Meal draft exists
User can edit this meal
Draft is not expired
Command contains at least one item
```

Location:

```text
NutritionAssistant.Application
```

---

## Domain validation

Checks business invariants.

Examples:

```text
Calories cannot be negative
Protein cannot be negative
Portion must be positive
Confidence must be between 0 and 1
Confirmed item cannot have unresolved critical fields
```

Location:

```text
NutritionAssistant.Domain
```

---

## Infrastructure validation

Checks external data correctness.

Examples:

```text
External API returned missing nutrients
External API returned invalid serving size
OCR text could not be parsed
Python AI service returned invalid JSON
```

Location:

```text
NutritionAssistant.Infrastructure
```

Important:

Infrastructure can detect invalid external data, but Application/Domain decide whether the use case can continue.

---

# 6. Mapping Rules

Do not expose Domain entities directly through HTTP.

Use mapping:

```text
HTTP Request DTO
  -> Application Command
  -> Domain Entity
  -> Application Result
  -> HTTP Response DTO
```

Example:

```text
CreateTextIntakeRequest
  -> CreateMealDraftFromTextCommand
  -> MealDraft
  -> CreateMealDraftResult
  -> CreateMealDraftResponse
```

Forbidden:

```text
Controller returns EF entity directly.
Controller returns Domain entity directly.
React sends EF entity shape.
Python service receives Domain entity directly.
```

---

# 7. Repository Rules

Repository interfaces belong to Application.

Repository implementations belong to Infrastructure.

Example:

```text
Application:
  IMealRepository

Infrastructure:
  EfCoreMealRepository
```

Application should not know about EF Core.

Bad:

```csharp
public sealed class CreateMealHandler
{
    private readonly NutritionDbContext _dbContext;
}
```

Good:

```csharp
public sealed class CreateMealHandler
{
    private readonly IMealRepository _mealRepository;
}
```

---

# 8. External Service Rules

External service interfaces belong to Application.

External service implementations belong to Infrastructure.

Examples:

```text
Application:
  IAiMealParser
  IFoodSearchService
  IBarcodeLookupService
  IOcrService
  IExportFileGenerator

Infrastructure:
  PythonAiMealParserClient
  OpenFoodFactsClient
  UsdaFoodSearchService
  PaddleOcrService
  XlsxExportFileGenerator
```

Api must not call external services directly.

Domain must not know external services exist.

---

# 9. AI and Agent Rules

Agent-related business boundaries must be strict.

The AI service may:

```text
Parse free-form text
Extract food candidates
Extract nutrition table from OCR text
Suggest likely products
Generate explanation text
```

The AI service must not:

```text
Save final meal entries
Delete user data
Change nutrition goals
Bypass validation
Invent nutrition values without source and confidence
Write directly to the main database
```

Application must validate all AI output.

Every AI result that affects state must include:

```text
source
confidence
is_estimated
missing_information
requires_user_confirmation
```

---

# 10. Naming Rules

Use business names, not technical names.

Good:

```text
CreateMealDraftFromTextCommand
ConfirmMealDraftHandler
NutritionFacts
Portion
ConfidenceScore
FoodCandidate
MealRecommendation
```

Bad:

```text
ProcessDataService
Manager
Helper
CommonUtils
DoStuff
AiResultProcessor
DataHandler
```

Class names should explain their role in the domain or application flow.

---

# 11. When to Create a New Class

Create a new class when:

```text
A concept has its own business meaning
A rule is reused
A method becomes too large
A class has more than one reason to change
A technical dependency needs to be hidden behind an interface
A use case deserves its own transaction boundary
```

Do not create a new class only to make architecture look complex.

Clean Architecture should reduce coupling, not create ceremony.

---

# 12. Anti-Patterns

## Fat Controller

Bad:

```text
Controller parses text
Controller calls AI
Controller calls food API
Controller calculates calories
Controller saves to database
```

Good:

```text
Controller maps request to command and calls use case.
```

---

## Anemic Application Layer

Bad:

```text
Application only forwards calls from Api to Infrastructure.
```

Good:

```text
Application coordinates the use case and owns the workflow.
```

---

## Business Logic in Infrastructure

Bad:

```text
Repository decides whether meal item requires confirmation.
```

Good:

```text
Domain or Application decides confirmation rules.
Repository only persists data.
```

---

## Prompt as Business Logic

Bad:

```text
The prompt says: "If confidence is lower than 0.6, require confirmation."
```

Good:

```text
The prompt may mention the format, but the actual threshold is checked in C# code.
```

---

## Shared Database Model Everywhere

Bad:

```text
EF entity is used as API response, domain model and database model.
```

Good:

```text
Domain entity, EF configuration and API DTO are separated.
```

---

# 13. Final Decision Checklist

When adding any new code, ask:

```text
Is this a business concept?
  -> Domain

Is this a user/system use case?
  -> Application

Is this a technical implementation?
  -> Infrastructure

Is this an HTTP boundary?
  -> Api

Is this a shared request/response contract?
  -> Contracts
```

If code imports EF Core, it cannot be in Domain or Application.

If code imports ASP.NET Core, it cannot be in Domain, Application or Infrastructure, except DI extension methods when explicitly allowed.

If code calls an external API, it belongs to Infrastructure.

If code contains business rules, it belongs to Domain or Application.

If code handles HTTP status codes, it belongs to Api.

If code is needed by React or Python service as a transport shape, it belongs to Contracts.

---

# 14. Project Reference Rules

Expected `.csproj` references:

```text
NutritionAssistant.Domain
  No project references.

NutritionAssistant.Contracts
  No project references, or only very carefully approved shared primitives.

NutritionAssistant.Application
  References:
    NutritionAssistant.Domain
    NutritionAssistant.Contracts

NutritionAssistant.Infrastructure
  References:
    NutritionAssistant.Application
    NutritionAssistant.Domain
    NutritionAssistant.Contracts

NutritionAssistant.Api
  References:
    NutritionAssistant.Application
    NutritionAssistant.Infrastructure
    NutritionAssistant.Contracts
```

Forbidden `.csproj` references:

```text
Domain -> anything
Application -> Infrastructure
Application -> Api
Infrastructure -> Api
Contracts -> Api
Contracts -> Infrastructure
Contracts -> Application
```

If a forbidden reference seems necessary, the design is probably wrong.

Use an interface in Application and an implementation in Infrastructure.

---

# 15. Main Rule

The architecture is correct when this is true:

```text
The domain model can be tested without database, HTTP server, Docker, React, Python, external APIs or LLM.
```

If this is not true, dependencies are pointing in the wrong direction.
