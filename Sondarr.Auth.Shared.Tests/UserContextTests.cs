using System.Security.Claims;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Sondarr.Auth.Shared.Models;
using Xunit;

namespace Sondarr.Auth.Shared.Tests;

public class UserContextTests
{
    private static Claim[] BaseClaims(params Claim[] extra)
    {
        var now = DateTimeOffset.UtcNow;
        var claims = new List<Claim>
        {
            new("sub", "user-123"),
            new("email", "test@example.com"),
            new("email_verified", "true"),
            new("iss", "https://xgztnswiiisfmblgrezi.supabase.co/auth/v1"),
            new("aud", "authenticated"),
            new("role", "authenticated"),
            new("exp", now.AddHours(1).ToUnixTimeSeconds().ToString()),
            new("iat", now.ToUnixTimeSeconds().ToString()),
            new("nbf", now.ToUnixTimeSeconds().ToString()),
        };
        claims.AddRange(extra);
        return claims.ToArray();
    }

    [Fact]
    public void FromClaims_MapsStandardClaims()
    {
        var user = UserContext.FromClaims(BaseClaims());

        Assert.Equal("user-123", user.UserId);
        Assert.Equal("test@example.com", user.Email);
        Assert.True(user.EmailVerified);
        Assert.Equal("https://xgztnswiiisfmblgrezi.supabase.co/auth/v1", user.Issuer);
        Assert.Equal("authenticated", user.Audience);
    }

    [Fact]
    public void FromClaims_DoesNotTreatSupabasePostgresRoleAsAppRole()
    {
        // The standard "role" claim from Supabase is always the Postgres role
        // ("authenticated"), not an application role -- it must not leak into Roles.
        var user = UserContext.FromClaims(BaseClaims());

        Assert.Empty(user.Roles);
    }

    [Fact]
    public void FromClaims_PopulatesRolesFromUserRoleClaim()
    {
        var user = UserContext.FromClaims(BaseClaims(new Claim("user_role", "admin")));

        Assert.Equal(new[] { "admin" }, user.Roles);
    }

    [Fact]
    public void FromClaims_SplitsCommaSeparatedUserRoleClaim()
    {
        var user = UserContext.FromClaims(BaseClaims(new Claim("user_role", "admin, moderator")));

        Assert.Equal(new[] { "admin", "moderator" }, user.Roles);
    }

    [Fact]
    public void FromClaims_ParsesNestedJsonCustomClaims()
    {
        var user = UserContext.FromClaims(BaseClaims(new Claim("app_metadata", "{\"provider\":\"email\",\"count\":3}")));

        var element = Assert.IsType<JsonElement>(user.CustomClaims["app_metadata"]);
        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        Assert.Equal("email", element.GetProperty("provider").GetString());
        Assert.Equal(3, element.GetProperty("count").GetInt32());
    }

    [Fact]
    public void FromClaims_CombinesRepeatedClaimsIntoArray()
    {
        // Supabase "amr" after MFA: the JWT handler emits one claim per array element.
        var token = new JsonWebTokenHandler().CreateToken(
            "{\"sub\":\"user-123\",\"aal\":\"aal2\",\"amr\":[{\"method\":\"oauth\",\"timestamp\":1},{\"method\":\"totp\",\"timestamp\":2}]}");
        var claims = new JsonWebTokenHandler().ReadJsonWebToken(token).Claims.ToList();
        Assert.Equal(2, claims.Count(c => c.Type == "amr"));

        var user = UserContext.FromClaims(claims);

        var amr = Assert.IsType<JsonElement>(user.CustomClaims["amr"]);
        Assert.Equal(JsonValueKind.Array, amr.ValueKind);
        Assert.Equal(new[] { "oauth", "totp" }, amr.EnumerateArray().Select(e => e.GetProperty("method").GetString()));
        Assert.Equal("aal2", user.CustomClaims["aal"]);
    }

    [Fact]
    public void FromClaims_KeepsPlainStringCustomClaimsAsStrings()
    {
        var user = UserContext.FromClaims(BaseClaims(new Claim("session_id", "abc-not-json")));

        Assert.Equal("abc-not-json", user.CustomClaims["session_id"]);
    }

    [Theory]
    [InlineData("admin", "ADMIN", true)]
    [InlineData("admin", "listener", false)]
    public void HasRole_IsCaseInsensitive(string actualRole, string queriedRole, bool expected)
    {
        var user = UserContext.FromClaims(BaseClaims(new Claim("user_role", actualRole)));

        Assert.Equal(expected, user.HasRole(queriedRole));
    }

    [Fact]
    public void HasAnyRole_ReturnsTrueWhenOneMatches()
    {
        var user = UserContext.FromClaims(BaseClaims(new Claim("user_role", "listener")));

        Assert.True(user.HasAnyRole("admin", "listener"));
        Assert.False(user.HasAnyRole("admin", "moderator"));
    }

    [Fact]
    public void IsTokenValid_ReturnsFalseWhenExpired()
    {
        var expired = new Claim("exp", DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString());
        var user = UserContext.FromClaims(BaseClaims().Where(c => c.Type != "exp").Append(expired));

        Assert.False(user.IsTokenValid());
    }

    [Fact]
    public void IsTokenValid_ReturnsTrueWithinWindow()
    {
        var user = UserContext.FromClaims(BaseClaims());

        Assert.True(user.IsTokenValid());
    }
}
