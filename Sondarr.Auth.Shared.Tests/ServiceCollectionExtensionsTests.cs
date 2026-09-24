using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sondarr.Auth.Shared.Services;
using Xunit;

namespace Sondarr.Auth.Shared.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSondarrAuthServices_RegistersUserContextServiceAndAccessor()
    {
        var services = new ServiceCollection();

        services.AddSondarrAuthServices();
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IUserContextService>());
        Assert.NotNull(provider.GetService<IHttpContextAccessor>());
    }

    [Fact]
    public void AddUserContextService_RegistersUserContextServiceAndAccessor()
    {
        var services = new ServiceCollection();

        services.AddUserContextService();
        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IUserContextService>());
        Assert.NotNull(provider.GetService<IHttpContextAccessor>());
    }
}

public class SupabaseAuthenticationExtensionsTests
{
    private static IConfiguration BuildConfig(string? jwtSecret, string sectionName = "Supabase", bool includeIssuer = true)
    {
        var data = new Dictionary<string, string?>
        {
            [$"{sectionName}:Audience"] = "authenticated",
        };
        if (includeIssuer)
        {
            // Issuer doubles as a JWKS source ({Issuer}/.well-known/jwks.json).
            data[$"{sectionName}:Issuer"] = "https://xgztnswiiisfmblgrezi.supabase.co/auth/v1";
        }
        if (jwtSecret != null)
        {
            data[$"{sectionName}:JwtSecret"] = jwtSecret;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    [Fact]
    public void AddSupabaseAuthentication_Throws_WhenNoSigningKeySourceConfigured()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(jwtSecret: null, includeIssuer: false);

        Assert.Throws<InvalidOperationException>(() => services.AddSupabaseAuthentication(config));
    }

    [Fact]
    public void AddSupabaseAuthentication_Succeeds_WhenJwtSecretPresent()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(jwtSecret: "a-secret-at-least-16-bytes-long");

        var result = services.AddSupabaseAuthentication(config);

        Assert.Same(services, result);
    }

    [Fact]
    public void AddSupabaseAuthentication_WithCustomSection_Throws_WhenNoSigningKeySourceConfigured()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(jwtSecret: null, sectionName: "CustomAuth", includeIssuer: false);

        Assert.Throws<InvalidOperationException>(() => services.AddSupabaseAuthentication(config, "CustomAuth"));
    }

    [Fact]
    public void AddSupabaseAuthentication_Succeeds_WithOnlyJwksSource()
    {
        var services = new ServiceCollection();
        var config = BuildConfig(jwtSecret: null);

        var result = services.AddSupabaseAuthentication(config);

        Assert.Same(services, result);
    }
}
