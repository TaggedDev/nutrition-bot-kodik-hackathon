using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nutrition.Infrastructure.Identity;

namespace Nutrition.Web.Tests;

internal sealed class FakeUserManager : UserManager<ApplicationUser>
{
    public FakeUserManager() : base(new DummyUserStore(),
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(),
        Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(),
        new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
        new ServiceCollection().BuildServiceProvider(), NullLogger<UserManager<ApplicationUser>>.Instance)
    {
    }

    public IdentityResult CreateResult { get; set; } = IdentityResult.Success;
    public ApplicationUser? UserByEmail { get; set; }
    public ApplicationUser? UserByClaimsPrincipal { get; set; }
    public ApplicationUser? CreatedUser { get; private set; }
    public string? CreatedPassword { get; private set; }
    public string? LookedUpEmail { get; private set; }

    public override Task<IdentityResult> CreateAsync(ApplicationUser user, string password)
    {
        CreatedUser = user;
        CreatedPassword = password;
        return Task.FromResult(CreateResult);
    }

    public override Task<ApplicationUser?> FindByEmailAsync(string email)
    {
        LookedUpEmail = email;
        return Task.FromResult(UserByEmail);
    }

    public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
    {
        return Task.FromResult(UserByClaimsPrincipal);
    }

    private sealed class DummyUserStore : IUserStore<ApplicationUser>, IUserEmailStore<ApplicationUser>,
        IUserPasswordStore<ApplicationUser>
    {
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.Id.ToString());

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.UserName);

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName,
            CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken)
        {
            user.Email = email;
            return Task.CompletedTask;
        }

        public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.Email);

        public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(false);

        public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.NormalizedEmail);

        public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail,
            CancellationToken cancellationToken)
        {
            user.NormalizedEmail = normalizedEmail;
            return Task.CompletedTask;
        }

        public Task SetPasswordHashAsync(ApplicationUser user, string? passwordHash,
            CancellationToken cancellationToken)
        {
            user.PasswordHash = passwordHash;
            return Task.CompletedTask;
        }

        public Task<string?> GetPasswordHashAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.PasswordHash);

        public Task<bool> HasPasswordAsync(ApplicationUser user, CancellationToken cancellationToken)
            => Task.FromResult(user.PasswordHash is not null);

        public void Dispose()
        {
        }
    }
}

internal sealed class FakeSignInManager : SignInManager<ApplicationUser>
{
    public FakeSignInManager(FakeUserManager userManager) : base(userManager,
        new HttpContextAccessor { HttpContext = new DefaultHttpContext() }, new FakeClaimsPrincipalFactory(),
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
        NullLogger<SignInManager<ApplicationUser>>.Instance,
        new AuthenticationSchemeProvider(Microsoft.Extensions.Options.Options.Create(new AuthenticationOptions())),
        new AlwaysConfirmedUserConfirmation())
    {
    }

    public ApplicationUser? SignedInUser { get; private set; }
    public bool IsPersistent { get; private set; }
    public string? AuthenticationMethod { get; private set; }
    public ApplicationUser? PasswordSignInUser { get; private set; }
    public string? PasswordSignInPassword { get; private set; }
    public SignInResult PasswordSignInResult { get; set; } = SignInResult.Success;
    public bool SignOutCalled { get; private set; }

    public override Task SignInAsync(ApplicationUser user, bool isPersistent, string? authenticationMethod = null)
    {
        SignedInUser = user;
        IsPersistent = isPersistent;
        AuthenticationMethod = authenticationMethod;
        return Task.CompletedTask;
    }

    public override Task<SignInResult> PasswordSignInAsync(ApplicationUser user, string password, bool isPersistent,
        bool lockoutOnFailure)
    {
        PasswordSignInUser = user;
        PasswordSignInPassword = password;
        IsPersistent = isPersistent;
        return Task.FromResult(PasswordSignInResult);
    }

    public override Task SignOutAsync()
    {
        SignOutCalled = true;
        return Task.CompletedTask;
    }

    private sealed class FakeClaimsPrincipalFactory : IUserClaimsPrincipalFactory<ApplicationUser>
    {
        public Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
        {
            var identity = new ClaimsIdentity(IdentityConstants.ApplicationScheme);
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
            return Task.FromResult(new ClaimsPrincipal(identity));
        }
    }

    private sealed class AlwaysConfirmedUserConfirmation : IUserConfirmation<ApplicationUser>
    {
        public Task<bool> IsConfirmedAsync(UserManager<ApplicationUser> manager, ApplicationUser user)
        {
            return Task.FromResult(true);
        }
    }
}