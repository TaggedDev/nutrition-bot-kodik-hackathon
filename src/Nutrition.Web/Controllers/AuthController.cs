using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Nutrition.Infrastructure.Identity;
using Nutrition.Shared.Dtos;

namespace Nutrition.Web.Controllers;

[ApiController]
[Produces("application/json")]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AuthController(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(CurrentUserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthErrorResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CurrentUserResponseDto>> RegisterAsync(RegisterRequestDto request)
    {
        if (!IsValidRegistrationRequest(request, out var validationErrors))
        {
            return BadRequest(new AuthErrorResponseDto(validationErrors));
        }

        var email = request.Email.Trim();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            SecondName = request.SecondName.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new AuthErrorResponseDto(result.Errors.Select(error => error.Description).ToArray()));
        }

        await _signInManager.SignInAsync(user, isPersistent: true);

        return Ok(ToCurrentUserResponse(user));
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(CurrentUserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponseDto>> LoginAsync(LoginRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new AuthErrorResponseDto(["Email and password are required."]));
        }

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
        {
            return Unauthorized();
        }

        var result = await _signInManager.PasswordSignInAsync(
            user, request.Password, isPersistent: true, lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            return Unauthorized();
        }

        return Ok(ToCurrentUserResponse(user));
    }

    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync()
    {
        await _signInManager.SignOutAsync();
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(CurrentUserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CurrentUserResponseDto>> MeAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(ToCurrentUserResponse(user));
    }

    private static CurrentUserResponseDto ToCurrentUserResponse(ApplicationUser user)
    {
        return new CurrentUserResponseDto(user.Id, user.Email ?? string.Empty, user.FirstName, user.SecondName);
    }

    private static bool IsValidRegistrationRequest(RegisterRequestDto request, out IReadOnlyCollection<string> errors)
    {
        var result = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            result.Add("Email is required.");
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            result.Add("First name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SecondName))
        {
            result.Add("Second name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            result.Add("Password is required.");
        }

        errors = result;
        return result.Count == 0;
    }
}