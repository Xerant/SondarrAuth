using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;

namespace Sondarr.Auth.Shared
{
    /// <summary>
    /// Shared configuration for the cross-site session cookie used to carry the Supabase
    /// access token between sites under the same parent domain (e.g. *.sondarr.com).
    /// Used by both <see cref="SupabaseAuthenticationExtensions"/> (reading the cookie as a
    /// fallback to the Authorization header) and <see cref="SessionEndpointExtensions"/>
    /// (writing/clearing the cookie).
    /// </summary>
    public static class SondarrAuthCookies
    {
        /// <summary>
        /// The cookie name used when "Supabase:Cookie:Name" is not configured.
        /// </summary>
        public const string DefaultCookieName = "sb-access-token";

        /// <summary>
        /// Gets the configured session cookie name, falling back to <see cref="DefaultCookieName"/>.
        /// </summary>
        public static string GetCookieName(IConfiguration configuration, string sectionName = "Supabase")
        {
            var name = configuration.GetSection(sectionName)["Cookie:Name"];
            return string.IsNullOrWhiteSpace(name) ? DefaultCookieName : name;
        }

        /// <summary>
        /// Builds the <see cref="CookieOptions"/> for the session cookie from
        /// "Supabase:Cookie:Domain" / "Supabase:Cookie:Secure" configuration.
        /// A missing or empty Domain produces a host-only cookie (useful for local dev,
        /// where a shared parent domain like ".sondarr.com" doesn't resolve).
        /// </summary>
        public static CookieOptions BuildCookieOptions(IConfiguration configuration, string sectionName = "Supabase", DateTimeOffset? expires = null)
        {
            var supabaseConfig = configuration.GetSection(sectionName);
            var domain = supabaseConfig["Cookie:Domain"];

            // Secure defaults to true unless explicitly set to "false" -- cookies carrying an
            // access token should never travel over plain HTTP outside of local dev.
            var secure = !bool.TryParse(supabaseConfig["Cookie:Secure"], out var configuredSecure) || configuredSecure;

            return new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                // Lax is sufficient (and preferable to None) here: subdomains of the same
                // registrable domain are "same-site" to each other per the cookie spec, so a
                // Lax cookie set on site1.sondarr.com is still sent on requests to site2.sondarr.com.
                SameSite = SameSiteMode.Lax,
                Domain = string.IsNullOrWhiteSpace(domain) ? null : domain,
                Path = "/",
                Expires = expires
            };
        }
    }
}
