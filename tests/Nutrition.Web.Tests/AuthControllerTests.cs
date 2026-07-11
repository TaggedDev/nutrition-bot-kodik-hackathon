using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Nutrition.Infrastructure.Identity;
using Nutrition.Shared.Dtos;
using Nutrition.Web.Controllers;

namespace Nutrition.Web.Tests;

public sealed class AuthControllerTests
{
    [Fact]
    public async Task RegisterAsync_ReturnsBadRequest_WhenRequestIsInvalid()
    {
        var userManager = new FakeUserManager();
        var signInManager = new FakeSignInManager(userManager);
        var controller = new AuthController(userManager, signInManager);

        var result = await controller.RegisterAsync(new RegisterRequestDto("", "", "", ""));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<AuthErrorResponseDto>(badRequest.Value);
        Assert.Contains("Email is required.", error.Errors);
        Assert.Contains("First name is required.", error.Errors);
        Assert.Contains("Second name is required.", error.Errors);
        Assert.Contains("Password is required.", error.Errors);
        Assert.Null(userManager.CreatedUser);
        Assert.Null(signInManager.SignedInUser);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsOk_AndSignsIn_WhenCreationSucceeds()
    {
        var userManager = new FakeUserManager();
        var signInManager = new FakeSignInManager(userManager);
        var controller = new AuthController(userManager, signInManager);

        var request = new RegisterRequestDto("  alice@example.com  ", "  Alice  ", "  Smith  ", "Secret123");
        var result = await controller.RegisterAsync(request);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var user = Assert.IsType<CurrentUserResponseDto>(ok.Value);

        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal("Alice", user.FirstName);
        Assert.Equal("Smith", user.SecondName);
        Assert.Equal("alice@example.com", userManager.CreatedUser!.Email);
        Assert.Equal("Secret123", userManager.CreatedPassword);
        Assert.NotEqual(Guid.Empty, userManager.CreatedUser!.Id);
        Assert.Same(userManager.CreatedUser, signInManager.SignedInUser);
        Assert.True(signInManager.IsPersistent);
    }

    [Fact]
    public async Task RegisterAsync_ReturnsBadRequest_WhenCreationFails()
    {
        var userManager = new FakeUserManager
        {
            CreateResult = IdentityResult.Failed(new IdentityError { Description = "Password too weak." })
        };
        var signInManager = new FakeSignInManager(userManager);
        var controller = new AuthController(userManager, signInManager);

        var result = await controller.RegisterAsync(new RegisterRequestDto("a@example.com", "Alice", "Smith", "weak"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<AuthErrorResponseDto>(badRequest.Value);
        Assert.Contains("Password too weak.", error.Errors);
        Assert.Null(signInManager.SignedInUser);
    }

    [Fact]
    public async Task LoginAsync_ReturnsBadRequest_WhenCredentialsAreBlank()
    {
        var userManager = new FakeUserManager();
        var signInManager = new FakeSignInManager(userManager);
        var controller = new AuthController(userManager, signInManager);

        var result = await controller.LoginAsync(new LoginRequestDto("", ""));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result.Result);
        var error = Assert.IsType<AuthErrorResponseDto>(badRequest.Value);
        Assert.Contains("Email and password are required.", error.Errors);
    }

    [Fact]
    public async Task LoginAsync_ReturnsUnauthorized_WhenUserIsMissing()
    {
        var userManager = new FakeUserManager();
        var signInManager = new FakeSignInManager(userManager);
        var controller = new AuthController(userManager, signInManager);

        var result = await controller.LoginAsync(new LoginRequestDto("missing@example.com", "Secret123"));

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Equal("missing@example.com", userManager.LookedUpEmail);
    }

    [Fact]
    public async Task LoginAsync_ReturnsUnauthorized_WhenPasswordIsInvalid()
    {
        var userManager = new FakeUserManager
        {
            UserByEmail = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = "alice@example.com",
                UserName = "alice@example.com",
                FirstName = "Alice",
                SecondName = "Smith"
            }
        };
        var signInManager = new FakeSignInManager(userManager)
        {
            PasswordSignInResult = Microsoft.AspNetCore.Identity.SignInResult.Failed
        };
        var controller = new AuthController(userManager, signInManager);

        var result = await controller.LoginAsync(new LoginRequestDto("alice@example.com", "wrong"));

        Assert.IsType<UnauthorizedResult>(result.Result);
        Assert.Equal("alice@example.com", userManager.LookedUpEmail);
        Assert.Equal("wrong", signInManager.PasswordSignInPassword);
    }

    [Fact]
    public async Task LoginAsync_ReturnsOk_WhenPasswordIsValid()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "alice@example.com",
            UserName = "alice@example.com",
            FirstName = "Alice",
            SecondName = "Smith"
        };
        var userManager = new FakeUserManager { UserByEmail = user };
        var signInManager = new FakeSignInManager(userManager)
        {
            PasswordSignInResult = Microsoft.AspNetCore.Identity.SignInResult.Success
        };
        var controller = new AuthController(userManager, signInManager);

        var result = await controller.LoginAsync(new LoginRequestDto("alice@example.com", "Secret123"));

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CurrentUserResponseDto>(ok.Value);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal("alice@example.com", response.Email);
        Assert.Same(user, signInManager.PasswordSignInUser);
        Assert.False(signInManager.IsPersistent);
    }

    [Fact]
    public async Task LoginAsync_UsesPersistentCookie_WhenRememberMeIsEnabled()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "alice@example.com",
            UserName = "alice@example.com",
            FirstName = "Alice",
            SecondName = "Smith"
        };
        var userManager = new FakeUserManager { UserByEmail = user };
        var signInManager = new FakeSignInManager(userManager)
        {
            PasswordSignInResult = Microsoft.AspNetCore.Identity.SignInResult.Success
        };
        var controller = new AuthController(userManager, signInManager);

        var result = await controller.LoginAsync(new LoginRequestDto("alice@example.com", "Secret123", true));

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.True(signInManager.IsPersistent);
    }

    [Fact]
    public async Task MeAsync_ReturnsUnauthorized_WhenUserCannotBeResolved()
    {
        var userManager = new FakeUserManager();
        var signInManager = new FakeSignInManager(userManager);
        var controller = new AuthController(userManager, signInManager);

        var result = await controller.MeAsync();

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public async Task MeAsync_ReturnsCurrentUser_WhenUserExists()
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "alice@example.com",
            UserName = "alice@example.com",
            FirstName = "Alice",
            SecondName = "Smith"
        };
        var userManager = new FakeUserManager { UserByClaimsPrincipal = user };
        var signInManager = new FakeSignInManager(userManager);
        var controller = new AuthController(userManager, signInManager);
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) },
                        "Test"))
            }
        };

        var result = await controller.MeAsync();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CurrentUserResponseDto>(ok.Value);
        Assert.Equal(user.Id, response.UserId);
        Assert.Equal(user.Email, response.Email);
    }

    [Fact]
    public async Task LogoutAsync_InvokesSignOut()
    {
        var userManager = new FakeUserManager();
        var signInManager = new FakeSignInManager(userManager);
        var controller = new AuthController(userManager, signInManager);

        var result = await controller.LogoutAsync();

        Assert.IsType<NoContentResult>(result);
        Assert.True(signInManager.SignOutCalled);
    }
}