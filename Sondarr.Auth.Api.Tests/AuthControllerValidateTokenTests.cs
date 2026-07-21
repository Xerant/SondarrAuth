using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Sondarr.Auth.Api.Controllers;
using Sondarr.Auth.Api.Models;
using Sondarr.Auth.Shared.Models;
using Sondarr.Auth.Shared.Services;
using Xunit;

namespace Sondarr.Auth.Api.Tests;

public class AuthControllerValidateTokenTests
{
    private const string Secret = "test-signing-secret-at-least-32-bytes-long";
    private const string Issuer = "https://xgztnswiiisfmblgrezi.supabase.co/auth/v1";
    private const string Audience = "authenticated";

    private static IConfiguration BuildConfig(string secret = Secret)
    {
        var data = new Dictionary<string, string?>
        {
            ["Supabase:JwtSecret"] = secret,
            ["Supabase:Issuer"] = Issuer,
            ["Supabase:Audience"] = Audience,
        };
        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static AuthController BuildController(string secret = Secret)
    {
        return new AuthController(new NoopUserContextService(), BuildConfig(secret), NullLogger<AuthController>.Instance);
    }

    private static string CreateToken(string signingSecret, string issuer, string audience, DateTime expires, params Claim[] extraClaims)
    {
        var claims = new List<Claim>
        {
            new("sub", "user-123"),
            new("email", "test@example.com"),
        };
        claims.AddRange(extraClaims);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void ValidateToken_ReturnsValid_ForCorrectlySignedToken()
    {
        var controller = BuildController();
        var token = CreateToken(Secret, Issuer, Audience, DateTime.UtcNow.AddHours(1), new Claim("user_role", "admin"));

        var result = controller.ValidateToken(new ValidateTokenRequest { Token = token });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ValidateTokenResponse>(ok.Value);
        Assert.True(response.IsValid);
        Assert.NotNull(response.User);
        Assert.Equal("user-123", response.User!.Id);
        Assert.Contains("admin", response.User.Roles);
    }

    [Fact]
    public void ValidateToken_ReturnsInvalid_ForExpiredToken()
    {
        var controller = BuildController();
        var token = CreateToken(Secret, Issuer, Audience, DateTime.UtcNow.AddMinutes(-5));

        var result = controller.ValidateToken(new ValidateTokenRequest { Token = token });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ValidateTokenResponse>(ok.Value);
        Assert.False(response.IsValid);
        Assert.Contains("expired", response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateToken_ReturnsInvalid_ForWrongSigningKey()
    {
        var controller = BuildController();
        var token = CreateToken("a-completely-different-secret-value-32b", Issuer, Audience, DateTime.UtcNow.AddHours(1));

        var result = controller.ValidateToken(new ValidateTokenRequest { Token = token });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ValidateTokenResponse>(ok.Value);
        Assert.False(response.IsValid);
    }

    [Fact]
    public void ValidateToken_ReturnsInvalid_ForWrongIssuer()
    {
        var controller = BuildController();
        var token = CreateToken(Secret, "https://someone-elses-project.supabase.co/auth/v1", Audience, DateTime.UtcNow.AddHours(1));

        var result = controller.ValidateToken(new ValidateTokenRequest { Token = token });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ValidateTokenResponse>(ok.Value);
        Assert.False(response.IsValid);
    }

    [Fact]
    public void ValidateToken_ReturnsInvalid_ForMalformedToken()
    {
        var controller = BuildController();

        var result = controller.ValidateToken(new ValidateTokenRequest { Token = "not-a-jwt" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ValidateTokenResponse>(ok.Value);
        Assert.False(response.IsValid);
    }

    [Fact]
    public void ValidateToken_ReturnsBadRequest_WhenTokenMissing()
    {
        var controller = BuildController();

        var result = controller.ValidateToken(new ValidateTokenRequest { Token = "" });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    private sealed class NoopUserContextService : IUserContextService
    {
        public UserContext? GetCurrentUser() => null;
        public UserContext GetCurrentUserRequired() => throw new UnauthorizedAccessException();
        public string? GetCurrentUserId() => null;
        public string GetCurrentUserIdRequired() => throw new UnauthorizedAccessException();
        public string? GetCurrentUserEmail() => null;
        public IList<string> GetCurrentUserRoles() => new List<string>();
        public bool HasRole(string role) => false;
        public bool HasAnyRole(params string[] roles) => false;
        public bool IsAuthenticated() => false;
    }
}
