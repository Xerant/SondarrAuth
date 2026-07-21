using System.IdentityModel.Tokens.Jwt;
using System.Net;
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

public class CookieAuthenticationFallbackTests : IAsyncLifetime
{
    private const string Secret = "cookie-fallback-test-secret-32-bytes-long";
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
        });

        builder.Services.AddAuthorization();
        builder.Services.AddSupabaseAuthentication(builder.Configuration);

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();
        _app.MapGet("/protected", () => "ok").RequireAuthorization();

        await _app.StartAsync();

        // Use a handler that doesn't follow the container's own cookie jar so each
        // test controls exactly what's sent, and CheckCertificateRevocationList/etc.
        // don't get in the way of the in-memory TestServer transport.
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
    }

    private static string CreateToken()
    {
        var claims = new[] { new Claim("sub", "user-123") };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public async Task Protected_ReturnsUnauthorized_WithNoCredentials()
    {
        var response = await _client.GetAsync("/protected");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_Succeeds_WithBearerHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CreateToken());

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Protected_Succeeds_WithSessionCookieAndNoHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Add("Cookie", $"{SondarrAuthCookies.DefaultCookieName}={CreateToken()}");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Protected_PrefersHeaderOverCookie_WhenBothPresent()
    {
        // A garbage Authorization header should win over a valid cookie -- the header,
        // once present, is authoritative and the cookie fallback must not kick in.
        var request = new HttpRequestMessage(HttpMethod.Get, "/protected");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "garbage-token");
        request.Headers.Add("Cookie", $"{SondarrAuthCookies.DefaultCookieName}={CreateToken()}");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
