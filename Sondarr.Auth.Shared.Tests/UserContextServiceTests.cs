using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sondarr.Auth.Shared.Services;
using Xunit;

namespace Sondarr.Auth.Shared.Tests;

public class UserContextServiceTests
{
    private static IHttpContextAccessor Accessor(HttpContext? httpContext)
    {
        return new TestHttpContextAccessor { HttpContext = httpContext };
    }

    private static HttpContext AuthenticatedContext(string userId = "user-123", params string[] roles)
    {
        var claims = new List<Claim>
        {
            new("sub", userId),
            new("email", "test@example.com"),
        };
        claims.AddRange(roles.Select(r => new Claim("user_role", r)));

        var identity = new ClaimsIdentity(claims, "TestAuthType");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        return context;
    }

    private static HttpContext UnauthenticatedContext()
    {
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
    }

    [Fact]
    public void GetCurrentUser_ReturnsNullWhenNotAuthenticated()
    {
        var service = new UserContextService(Accessor(UnauthenticatedContext()));

        Assert.Null(service.GetCurrentUser());
    }

    [Fact]
    public void GetCurrentUser_ReturnsNullWhenNoHttpContext()
    {
        var service = new UserContextService(Accessor(null));

        Assert.Null(service.GetCurrentUser());
    }

    [Fact]
    public void GetCurrentUser_ReturnsUserWhenAuthenticated()
    {
        var service = new UserContextService(Accessor(AuthenticatedContext()));

        var user = service.GetCurrentUser();

        Assert.NotNull(user);
        Assert.Equal("user-123", user!.UserId);
    }

    [Fact]
    public void GetCurrentUserRequired_ThrowsWhenNotAuthenticated()
    {
        var service = new UserContextService(Accessor(UnauthenticatedContext()));

        Assert.Throws<UnauthorizedAccessException>(() => service.GetCurrentUserRequired());
    }

    [Fact]
    public void GetCurrentUserIdRequired_ThrowsWhenNotAuthenticated()
    {
        var service = new UserContextService(Accessor(UnauthenticatedContext()));

        Assert.Throws<UnauthorizedAccessException>(() => service.GetCurrentUserIdRequired());
    }

    [Fact]
    public void HasRole_ReflectsUserRoleClaim()
    {
        var service = new UserContextService(Accessor(AuthenticatedContext(roles: "admin")));

        Assert.True(service.HasRole("admin"));
        Assert.False(service.HasRole("moderator"));
    }

    [Fact]
    public void HasRole_ReturnsFalseWhenNotAuthenticated()
    {
        var service = new UserContextService(Accessor(UnauthenticatedContext()));

        Assert.False(service.HasRole("admin"));
    }

    [Fact]
    public void IsAuthenticated_ReflectsHttpContextState()
    {
        var authenticated = new UserContextService(Accessor(AuthenticatedContext()));
        var anonymous = new UserContextService(Accessor(UnauthenticatedContext()));

        Assert.True(authenticated.IsAuthenticated());
        Assert.False(anonymous.IsAuthenticated());
    }
}
