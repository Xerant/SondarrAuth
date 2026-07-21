using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Sondarr.Auth.Shared;
using Xunit;

namespace Sondarr.Auth.Shared.Tests;

public class SondarrAuthCookiesTests
{
    private static IConfiguration BuildConfig(Dictionary<string, string?> overrides)
    {
        return new ConfigurationBuilder().AddInMemoryCollection(overrides).Build();
    }

    [Fact]
    public void GetCookieName_ReturnsDefault_WhenNotConfigured()
    {
        var config = BuildConfig(new());

        Assert.Equal(SondarrAuthCookies.DefaultCookieName, SondarrAuthCookies.GetCookieName(config));
    }

    [Fact]
    public void GetCookieName_ReturnsConfiguredValue()
    {
        var config = BuildConfig(new() { ["Supabase:Cookie:Name"] = "my-session" });

        Assert.Equal("my-session", SondarrAuthCookies.GetCookieName(config));
    }

    [Fact]
    public void BuildCookieOptions_DefaultsToSecureAndHttpOnly()
    {
        var config = BuildConfig(new());

        var options = SondarrAuthCookies.BuildCookieOptions(config);

        Assert.True(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.Lax, options.SameSite);
        Assert.Equal("/", options.Path);
    }

    [Fact]
    public void BuildCookieOptions_TreatsEmptyDomainAsHostOnly()
    {
        var config = BuildConfig(new() { ["Supabase:Cookie:Domain"] = "" });

        var options = SondarrAuthCookies.BuildCookieOptions(config);

        Assert.Null(options.Domain);
    }

    [Fact]
    public void BuildCookieOptions_UsesConfiguredDomain()
    {
        var config = BuildConfig(new() { ["Supabase:Cookie:Domain"] = ".sondarr.com" });

        var options = SondarrAuthCookies.BuildCookieOptions(config);

        Assert.Equal(".sondarr.com", options.Domain);
    }

    [Fact]
    public void BuildCookieOptions_AllowsDisablingSecure()
    {
        var config = BuildConfig(new() { ["Supabase:Cookie:Secure"] = "false" });

        var options = SondarrAuthCookies.BuildCookieOptions(config);

        Assert.False(options.Secure);
    }

    [Fact]
    public void BuildCookieOptions_PassesThroughExpires()
    {
        var config = BuildConfig(new());
        var expires = DateTimeOffset.UtcNow.AddHours(1);

        var options = SondarrAuthCookies.BuildCookieOptions(config, expires: expires);

        Assert.Equal(expires, options.Expires);
    }
}
