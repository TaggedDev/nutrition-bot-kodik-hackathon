using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Infrastructure.Identity;
using Nutrition.Shared.Dtos;
using Nutrition.Web.Controllers;

namespace Nutrition.Web.Tests;

public sealed class ProfileControllerTests
{
    [Fact]
    public async Task DayAsync_ReturnsUnauthorized_WhenUserCannotBeResolved()
    {
        var controller = CreateController(user: null, service: new FakeProfileService());

        var result = await controller.DayAsync(new DateOnly(2026, 7, 8), 180, CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task DayAsync_ReturnsDayPayload_ForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "alice@example.com",
            UserName = "alice@example.com",
            FirstName = "Alice",
            SecondName = "Smith"
        };
        var service = new FakeProfileService();
        var controller = CreateController(user, service);
        var date = new DateOnly(2026, 7, 8);

        var result = await controller.DayAsync(date, 180, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<ProfileDayResponseDto>(ok.Value);
        Assert.Equal(date, service.DayDate);
        Assert.Equal(180, service.DayUtcOffsetMinutes);
        Assert.Equal(userId, service.DayUserId);
        Assert.Equal(date, payload.Date);
        Assert.NotNull(payload.Goal);
        Assert.Equal(35, payload.Goal.LunchPercent);
        Assert.Contains(payload.Meals, meal => meal.MealType == "Lunch");
    }

    [Fact]
    public async Task CreateEntryAsync_PassesServingMetadata_ToProfileService()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "alice@example.com",
            UserName = "alice@example.com",
            FirstName = "Alice",
            SecondName = "Smith"
        };
        var service = new FakeProfileService();
        var controller = CreateController(user, service);
        var request = new CreateUserMealEntryRequestDto("Творог", "Домик", 180, 20, 8, 10, "Breakfast", 150, "150 г",
            "OpenFoodFacts", "off:123", new DateTimeOffset(2026, 7, 8, 7, 30, 0, TimeSpan.Zero));

        var result = await controller.CreateEntryAsync(request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(userId, service.CreateEntryUserId);
        Assert.Same(request, service.CreateEntryRequest);
    }

    private static ProfileController CreateController(ApplicationUser? user, IProfileService service)
    {
        var userManager = new FakeUserManager { UserByClaimsPrincipal = user };
        var controller = new ProfileController(service, userManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[]
                        {
                            new Claim(ClaimTypes.NameIdentifier,
                                user?.Id.ToString() ?? Guid.NewGuid().ToString())
                        }, "Test"))
                }
            }
        };

        return controller;
    }

    private sealed class FakeProfileService : IProfileService
    {
        public Guid? DayUserId { get; private set; }
        public DateOnly? DayDate { get; private set; }
        public int? DayUtcOffsetMinutes { get; private set; }
        public Guid? CreateEntryUserId { get; private set; }
        public CreateUserMealEntryRequestDto? CreateEntryRequest { get; private set; }

        public Task<ProfileResponseDto?> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProfileResponseDto?>(new ProfileResponseDto(userId, "alice@example.com", "Alice",
                "Smith"));

        public Task<ProfileResponseDto?> UpdateUserProfileAsync(Guid userId, UpdateProfileRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult<ProfileResponseDto?>(new ProfileResponseDto(userId, "alice@example.com",
                request.FirstName, request.SecondName));

        public Task<ProfileHistoryResponseDto> GetUserHistoryAsync(Guid userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProfileHistoryResponseDto(Array.Empty<UserMealEntryDto>(), EmptySummary()));

        public Task<NutritionSummaryDto> GetUserDailySummaryAsync(Guid userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(EmptySummary());

        public Task<ProfileSummaryByTypeResponseDto> GetUserSummaryByMealTypeAsync(Guid userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(
                new ProfileSummaryByTypeResponseDto(Array.Empty<MealEntrySummaryByTypeDto>(), EmptySummary()));

        public Task<ProfileDayResponseDto> GetUserDayAsync(Guid userId, DateOnly date, int utcOffsetMinutes = 0,
            CancellationToken cancellationToken = default)
        {
            DayUserId = userId;
            DayDate = date;
            DayUtcOffsetMinutes = utcOffsetMinutes;
            var meals = new[]
            {
                new MealEntriesByTypeDto("Breakfast", Array.Empty<UserMealEntryDto>(), EmptySummary()),
                new MealEntriesByTypeDto("Lunch", Array.Empty<UserMealEntryDto>(), EmptySummary()),
                new MealEntriesByTypeDto("Dinner", Array.Empty<UserMealEntryDto>(), EmptySummary()),
                new MealEntriesByTypeDto("Snack", Array.Empty<UserMealEntryDto>(), EmptySummary())
            };
            return Task.FromResult(new ProfileDayResponseDto(date, DefaultGoal(), meals, EmptySummary()));
        }

        public Task<ProfileStatisticsResponseDto> GetUserStatisticsAsync(Guid userId, int rangeDays, DateOnly endDate,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ProfileStatisticsResponseDto(2300, Array.Empty<ProfileStatisticsDayDto>()));

        public Task<string> ExportUserDailyCsvAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(
                "date,totalCalories,proteinGrams,fatGrams,carbsGrams,breakfastCalories,lunchCalories,dinnerCalories,snackCalories\n");

        public Task<DeleteAccountRequestResponseDto> RequestUserAccountDeletionAsync(Guid userId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new DeleteAccountRequestResponseDto(true, DateTimeOffset.UtcNow));

        public Task<UserDailyGoalDto?> GetUserDailyGoalAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<UserDailyGoalDto?>(DefaultGoal());

        public Task<UserDailyGoalDto> CreateUserDailyGoalAsync(Guid userId, CreateDailyGoalRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ToGoal(request));

        public Task<UserDailyGoalDto> UpdateUserDailyGoalAsync(Guid userId, UpdateDailyGoalRequestDto request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(ToGoal(request));

        public Task<UserMealEntryDto> CreateUserMealEntryAsync(Guid userId, CreateUserMealEntryRequestDto request,
            CancellationToken cancellationToken = default)
        {
            CreateEntryUserId = userId;
            CreateEntryRequest = request;
            return Task.FromResult(ToEntry(request));
        }

        public Task<UserMealEntryDto?> UpdateUserMealEntryAsync(Guid userId, Guid entryId,
            UpdateUserMealEntryRequestDto request, CancellationToken cancellationToken = default)
            => Task.FromResult<UserMealEntryDto?>(new UserMealEntryDto(entryId, request.ProductName,
                request.Brand ?? string.Empty, request.Calories, request.Protein, request.Fat, request.Carbs,
                request.MealType, request.ServingGrams, request.PortionLabel ?? string.Empty,
                request.SourceType ?? string.Empty, request.SourceReference ?? string.Empty,
                request.LoggedAtUtc ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        public Task DeleteUserMealEntryAsync(Guid userId, Guid entryId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteUserAccountAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        private static UserMealEntryDto ToEntry(CreateUserMealEntryRequestDto request)
            => new(Guid.NewGuid(), request.ProductName, request.Brand ?? string.Empty, request.Calories,
                request.Protein, request.Fat, request.Carbs, request.MealType, request.ServingGrams,
                request.PortionLabel ?? string.Empty, request.SourceType ?? string.Empty,
                request.SourceReference ?? string.Empty, request.LoggedAtUtc ?? DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);

        private static NutritionSummaryDto EmptySummary()
            => new(0, 0, 0, 0);

        private static UserDailyGoalDto DefaultGoal()
            => new(2300, 150, 77, 288, 25, 35, 30, 10);

        private static UserDailyGoalDto ToGoal(CreateDailyGoalRequestDto request)
            => new(request.TargetCalories, request.TargetProtein, request.TargetFat, request.TargetCarbs,
                request.BreakfastPercent ?? 25, request.LunchPercent ?? 35, request.DinnerPercent ?? 30,
                request.SnackPercent ?? 10);

        private static UserDailyGoalDto ToGoal(UpdateDailyGoalRequestDto request)
            => new(request.TargetCalories, request.TargetProtein, request.TargetFat, request.TargetCarbs,
                request.BreakfastPercent ?? 25, request.LunchPercent ?? 35, request.DinnerPercent ?? 30,
                request.SnackPercent ?? 10);
    }
}