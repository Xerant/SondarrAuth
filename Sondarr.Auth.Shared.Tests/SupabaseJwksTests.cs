using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Sondarr.Auth.Shared;
using Xunit;

namespace Sondarr.Auth.Shared.Tests;

public class SupabaseJwksTests
{
    private const string Secret = "jwks-test-legacy-secret-32-bytes-long!";
    private const string Issuer = "https://xgztnswiiisfmblgrezi.supabase.co/auth/v1";
    private const string Audience = "authenticated";
    private const string JwksUrl = Issuer + "/.well-known/jwks.json";

    private static IConfiguration BuildConfig(Dictionary<string, string?>? overrides = null)
    {
        var data = new Dictionary<string, string?>
        {
            ["Supabase:JwtSecret"] = Secret,
            ["Supabase:Issuer"] = Issuer,
            ["Supabase:Audience"] = Audience,
        };
        foreach (var (key, value) in overrides ?? new())
        {
            data[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static ECDsaSecurityKey CreateEcKey(string kid) =>
        new(ECDsa.Create(ECCurve.NamedCurves.nistP256)) { KeyId = kid };

    private static string JwksJson(params ECDsaSecurityKey[] keys)
    {
        var entries = keys.Select(key =>
        {
            var jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(key);
            return $$"""{"kty":"EC","crv":"P-256","x":"{{jwk.X}}","y":"{{jwk.Y}}","kid":"{{key.KeyId}}","alg":"ES256","use":"sig"}""";
        });
        return $$"""{"keys":[{{string.Join(",", entries)}}]}""";
    }

    private static string CreateToken(SecurityKey key, string algorithm)
    {
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: new[] { new Claim("sub", "user-123") },
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, algorithm));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static TokenValidationParameters CreateParameters(IConfiguration config, SupabaseJwksProvider provider) =>
        SupabaseAuthenticationExtensions.CreateTokenValidationParameters(config, "Supabase", _ => provider);

    private static ClaimsPrincipal Validate(string token, TokenValidationParameters parameters) =>
        new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, parameters, out _);

    [Fact]
    public void Es256Token_WithKidInJwks_Validates()
    {
        var key = CreateEcKey("ec-key-1");
        var handler = new FakeJwksHandler(JwksJson(key));
        var provider = new SupabaseJwksProvider(new HttpClient(handler), JwksUrl);

        var principal = Validate(CreateToken(key, SecurityAlgorithms.EcdsaSha256), CreateParameters(BuildConfig(), provider));

        Assert.Equal("user-123", principal.FindFirst("sub")?.Value);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public void Es256Token_WithoutJwtSecretConfigured_Validates()
    {
        var key = CreateEcKey("ec-key-1");
        var provider = new SupabaseJwksProvider(new HttpClient(new FakeJwksHandler(JwksJson(key))), JwksUrl);
        var config = BuildConfig(new() { ["Supabase:JwtSecret"] = null });

        var principal = Validate(CreateToken(key, SecurityAlgorithms.EcdsaSha256), CreateParameters(config, provider));

        Assert.Equal("user-123", principal.FindFirst("sub")?.Value);
    }

    [Fact]
    public void Hs256Token_WithoutKid_ValidatesWithSecret_WithoutFetchingJwks()
    {
        var handler = new FakeJwksHandler(JwksJson());
        var provider = new SupabaseJwksProvider(new HttpClient(handler), JwksUrl);
        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));

        var principal = Validate(CreateToken(secretKey, SecurityAlgorithms.HmacSha256), CreateParameters(BuildConfig(), provider));

        Assert.Equal("user-123", principal.FindFirst("sub")?.Value);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public void Hs256Token_WithKidNotInJwks_FallsBackToSecret()
    {
        var provider = new SupabaseJwksProvider(new HttpClient(new FakeJwksHandler(JwksJson(CreateEcKey("ec-key-1")))), JwksUrl);
        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)) { KeyId = "legacy-hs256" };

        var principal = Validate(CreateToken(secretKey, SecurityAlgorithms.HmacSha256), CreateParameters(BuildConfig(), provider));

        Assert.Equal("user-123", principal.FindFirst("sub")?.Value);
    }

    [Fact]
    public void Es256Token_SignedByUnknownKey_IsRejected()
    {
        var provider = new SupabaseJwksProvider(new HttpClient(new FakeJwksHandler(JwksJson(CreateEcKey("ec-key-1")))), JwksUrl);
        var attackerKey = CreateEcKey("ec-key-1"); // same kid, different key material

        Assert.ThrowsAny<SecurityTokenException>(() =>
            Validate(CreateToken(attackerKey, SecurityAlgorithms.EcdsaSha256), CreateParameters(BuildConfig(), provider)));
    }

    [Fact]
    public void JwksFetchFailure_RejectsToken_InsteadOfThrowingHttpError()
    {
        var key = CreateEcKey("ec-key-1");
        var provider = new SupabaseJwksProvider(new HttpClient(new FakeJwksHandler(statusCode: HttpStatusCode.InternalServerError)), JwksUrl);

        Assert.ThrowsAny<SecurityTokenException>(() =>
            Validate(CreateToken(key, SecurityAlgorithms.EcdsaSha256), CreateParameters(BuildConfig(), provider)));
    }

    [Fact]
    public void UnknownKid_RefetchesJwks_ToPickUpRotatedKey()
    {
        var oldKey = CreateEcKey("ec-key-1");
        var rotatedKey = CreateEcKey("ec-key-2");
        var time = new FakeTimeProvider();
        var handler = new FakeJwksHandler(JwksJson(oldKey));
        var provider = new SupabaseJwksProvider(new HttpClient(handler), JwksUrl, time);

        Assert.NotEmpty(provider.GetSigningKeys("ec-key-1"));

        // Supabase rotates in a new key; the hourly cache hasn't expired yet.
        handler.Json = JwksJson(oldKey, rotatedKey);
        time.Advance(TimeSpan.FromMinutes(6));

        Assert.NotEmpty(provider.GetSigningKeys("ec-key-2"));
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public void UnknownKid_RefetchIsThrottled()
    {
        var time = new FakeTimeProvider();
        var handler = new FakeJwksHandler(JwksJson(CreateEcKey("ec-key-1")));
        var provider = new SupabaseJwksProvider(new HttpClient(handler), JwksUrl, time);

        provider.GetSigningKeys("ec-key-1");
        time.Advance(TimeSpan.FromMinutes(1));
        for (var i = 0; i < 10; i++)
        {
            Assert.Empty(provider.GetSigningKeys($"made-up-kid-{i}"));
        }

        Assert.Equal(1, handler.RequestCount);
    }

    [Theory]
    [InlineData("Supabase:JwksUrl", "https://keys.example.com/jwks.json", "https://keys.example.com/jwks.json")]
    [InlineData("Supabase:Url", "https://xgztnswiiisfmblgrezi.supabase.co/", JwksUrl)]
    [InlineData("Supabase:Issuer", Issuer, JwksUrl)]
    public void JwksUrl_IsResolvedFromConfig(string configKey, string configValue, string expectedUrl)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Supabase:Audience"] = Audience,
            [configKey] = configValue,
        }).Build();
        string? requestedUrl = null;

        SupabaseAuthenticationExtensions.CreateTokenValidationParameters(config, "Supabase", url =>
        {
            requestedUrl = url;
            return new SupabaseJwksProvider(new HttpClient(new FakeJwksHandler(JwksJson())), url);
        });

        Assert.Equal(expectedUrl, requestedUrl);
    }

    private sealed class FakeJwksHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public FakeJwksHandler(string json = "", HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            Json = json;
            _statusCode = statusCode;
        }

        public string Json { get; set; }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(Json, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
