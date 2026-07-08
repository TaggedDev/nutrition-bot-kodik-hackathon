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
