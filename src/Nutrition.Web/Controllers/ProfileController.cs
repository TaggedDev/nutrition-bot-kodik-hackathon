using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Application.Abstractions.Services;
using Nutrition.Infrastructure.Identity;
using Nutrition.Shared.Dtos;

namespace Nutrition.Web.Controllers;

[Authorize]
[ApiController]
[Produces("application/json")]
[Route("api/v1/profile")]
public sealed class ProfileController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileController(IProfileService profileService, UserManager<ApplicationUser> userManager)
    {
        _profileService = profileService;
        _userManager = userManager;
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(ProfileResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponseDto>> MeAsync(CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        var profile = await _profileService.GetUserProfileAsync(userId.Value, cancellationToken);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    [HttpPatch("me")]
    [ProducesResponseType(typeof(ProfileResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileResponseDto>> UpdateMeAsync(UpdateProfileRequestDto request, CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        var profile = await _profileService.UpdateUserProfileAsync(userId.Value, request, cancellationToken);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(ProfileHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileHistoryResponseDto>> HistoryAsync(CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _profileService.GetUserHistoryAsync(userId.Value, cancellationToken));
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(NutritionSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NutritionSummaryDto>> SummaryAsync(CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _profileService.GetUserDailySummaryAsync(userId.Value, cancellationToken));
    }

    [HttpGet("summary-by-type")]
    [ProducesResponseType(typeof(ProfileSummaryByTypeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileSummaryByTypeResponseDto>> SummaryByTypeAsync(CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _profileService.GetUserSummaryByMealTypeAsync(userId.Value, cancellationToken));
    }

    [HttpGet("day")]
    [ProducesResponseType(typeof(ProfileDayResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileDayResponseDto>> DayAsync([FromQuery] DateOnly? date, [FromQuery] int utcOffsetMinutes, CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _profileService.GetUserDayAsync(userId.Value, date ?? DateOnly.FromDateTime(DateTime.UtcNow), utcOffsetMinutes, cancellationToken));
    }

    [HttpGet("goal")]
    [ProducesResponseType(typeof(UserDailyGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDailyGoalDto>> GoalAsync(CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        var goal = await _profileService.GetUserDailyGoalAsync(userId.Value, cancellationToken);
        return goal is null ? NoContent() : Ok(goal);
    }

    [HttpGet("goals")]
    [ProducesResponseType(typeof(UserDailyGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<UserDailyGoalDto>> GoalsAsync(CancellationToken cancellationToken)
    {
        return GoalAsync(cancellationToken);
    }

    [HttpPost("goal")]
    [ProducesResponseType(typeof(UserDailyGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDailyGoalDto>> CreateGoalAsync(CreateDailyGoalRequestDto request, CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _profileService.CreateUserDailyGoalAsync(userId.Value, request, cancellationToken));
    }

    [HttpPut("goal")]
    [ProducesResponseType(typeof(UserDailyGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDailyGoalDto>> UpdateGoalAsync(UpdateDailyGoalRequestDto request, CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _profileService.UpdateUserDailyGoalAsync(userId.Value, request, cancellationToken));
    }

    [HttpPatch("goals")]
    [ProducesResponseType(typeof(UserDailyGoalDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public Task<ActionResult<UserDailyGoalDto>> UpdateGoalsAsync(UpdateDailyGoalRequestDto request, CancellationToken cancellationToken)
    {
        return UpdateGoalAsync(request, cancellationToken);
    }

    [HttpGet("statistics")]
    [ProducesResponseType(typeof(ProfileStatisticsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileStatisticsResponseDto>> StatisticsAsync([FromQuery] int rangeDays = 7, [FromQuery] DateOnly? endDate = null, CancellationToken cancellationToken = default)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        var date = endDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(await _profileService.GetUserStatisticsAsync(userId.Value, rangeDays, date, cancellationToken));
    }

    [HttpGet("export/daily-csv")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/csv")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ExportDailyCsvAsync(CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        var csv = await _profileService.ExportUserDailyCsvAsync(userId.Value, cancellationToken);
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", "nutrimate-daily-export.csv");
    }

    [HttpPost("delete-request")]
    [ProducesResponseType(typeof(DeleteAccountRequestResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DeleteAccountRequestResponseDto>> DeleteRequestAsync(CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _profileService.RequestUserAccountDeletionAsync(userId.Value, cancellationToken));
    }

    [HttpPost("entry")]
    [ProducesResponseType(typeof(UserMealEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserMealEntryDto>> CreateEntryAsync(CreateUserMealEntryRequestDto request, CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await _profileService.CreateUserMealEntryAsync(userId.Value, request, cancellationToken));
    }

    [HttpPut("entry/{id:guid}")]
    [ProducesResponseType(typeof(UserMealEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserMealEntryDto>> UpdateEntryAsync(Guid id, UpdateUserMealEntryRequestDto request, CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        var entry = await _profileService.UpdateUserMealEntryAsync(userId.Value, id, request, cancellationToken);
        return entry is null ? NotFound() : Ok(entry);
    }

    [HttpDelete("entry/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteEntryAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        await _profileService.DeleteUserMealEntryAsync(userId.Value, id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("account")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DeleteAccountAsync(CancellationToken cancellationToken)
    {
        var userId = await GetCurrentUserIdAsync();
        if (userId is null)
        {
            return Unauthorized();
        }

        await _profileService.DeleteUserAccountAsync(userId.Value, cancellationToken);
        return NoContent();
    }

    private async Task<Guid?> GetCurrentUserIdAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return user?.Id;
    }
}
