using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sondarr.Auth.Shared.Attributes;
using Sondarr.Auth.Shared.Services;
using Xunit;

namespace Sondarr.Auth.Shared.Tests;

public class RequireRoleAttributeTests
{
    private static AuthorizationFilterContext BuildContext(string? userId, params string[] roles)
    {
        var claims = new List<Claim>();
        var isAuthenticated = userId != null;
        if (isAuthenticated)
        {
            claims.Add(new Claim("sub", userId!));
            claims.AddRange(roles.Select(r => new Claim("user_role", r)));
        }

        var identity = isAuthenticated
            ? new ClaimsIdentity(claims, "TestAuthType")
            : new ClaimsIdentity();

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };

        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(new TestHttpContextAccessor { HttpContext = httpContext });
        services.AddScoped<IUserContextService, UserContextService>();
        httpContext.RequestServices = services.BuildServiceProvider();

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    [Fact]
    public void OnAuthorization_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var attribute = new RequireRoleAttribute("admin");
        var context = BuildContext(userId: null);

        attribute.OnAuthorization(context);

        Assert.IsType<UnauthorizedResult>(context.Result);
    }

    [Fact]
    public void OnAuthorization_ReturnsForbid_WhenMissingRole()
    {
        var attribute = new RequireRoleAttribute("admin");
        var context = BuildContext("user-1", "listener");

        attribute.OnAuthorization(context);

        Assert.IsType<ForbidResult>(context.Result);
    }

    [Fact]
    public void OnAuthorization_AllowsRequest_WhenRolePresent()
    {
        var attribute = new RequireRoleAttribute("admin");
        var context = BuildContext("user-1", "admin");

        attribute.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void RequireAnyRole_AllowsRequest_WhenAnyRoleMatches()
    {
        var attribute = new RequireAnyRoleAttribute("admin", "moderator");
        var context = BuildContext("user-1", "moderator");

        attribute.OnAuthorization(context);

        Assert.Null(context.Result);
    }

    [Fact]
    public void RequireAnyRole_ThrowsWhenNoRolesSpecified()
    {
        Assert.Throws<ArgumentException>(() => new RequireAnyRoleAttribute());
    }
}
