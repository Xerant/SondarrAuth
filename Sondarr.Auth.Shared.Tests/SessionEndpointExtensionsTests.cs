using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Sondarr.Auth.Shared;
using Xunit;

namespace Sondarr.Auth.Shared.Tests;

public class SessionEndpointExtensionsTests : IAsyncLifetime
{
    private const string Secret = "session-endpoint-test-secret-32-bytes-long";
    private const string Issuer = "https://xgztnswiiisfmblgrezi.supabase.co/auth/v1";
    private const string Audience = "authenticated";

    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Supabase:JwtSecret"] = Secret,
            ["Supabase:Issuer"] = Issuer,
            ["Supabase:Audience"] = Audience,
            ["Supabase:Cookie:Domain"] = "", // host-only for the in-memory test server
        });

        _app = builder.Build();
        _app.MapSondarrSessionEndpoints(_app.Configuration);

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
    }

    private static string CreateToken(DateTime expires)
    {
        var claims = new[] { new Claim("sub", "user-123") };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: expires,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task PostSession_SetsCookie_ForValidToken()
    {
        var token = CreateToken(DateTime.UtcNow.AddHours(1));

        var response = await _client.PostAsJsonAsync("/auth/session", new { accessToken = token });

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookieValues));
        var setCookie = Assert.Single(setCookieValues);
        Assert.Contains($"{SondarrAuthCookies.DefaultCookieName}={token}", setCookie);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostSession_ReturnsBadRequest_ForInvalidToken()
    {
        var response = await _client.PostAsJsonAsync("/auth/session", new { accessToken = "not-a-jwt" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task PostSession_ReturnsBadRequest_ForExpiredToken()
    {
        var token = CreateToken(DateTime.UtcNow.AddMinutes(-5));

        var response = await _client.PostAsJsonAsync("/auth/session", new { accessToken = token });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostSession_ReturnsBadRequest_WhenAccessTokenMissing()
    {
        var response = await _client.PostAsJsonAsync("/auth/session", new { accessToken = "" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostLogout_ClearsCookie()
    {
        var response = await _client.PostAsync("/auth/logout", content: null);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var setCookieValues));
        var setCookie = Assert.Single(setCookieValues);

        // A cleared cookie is expressed as an empty value with an expiry in the past.
        Assert.StartsWith($"{SondarrAuthCookies.DefaultCookieName}=;", setCookie);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
    }
}
