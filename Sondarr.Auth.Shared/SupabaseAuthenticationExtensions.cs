using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sondarr.Auth.Shared
{
    /// <summary>
    /// Provides extension methods for setting up Supabase JWT authentication.
    /// This class enables easy configuration of JWT Bearer authentication for validating
    /// tokens issued by Supabase across microservices in a polyrepo architecture.
    /// </summary>
    public static class SupabaseAuthenticationExtensions
    {
        /// <summary>
        /// Configures JWT Bearer authentication to validate tokens issued by Supabase.
        /// Reads configuration from the "Supabase" section of appsettings.json.
        /// This method sets up comprehensive token validation including issuer, audience,
        /// signing key validation, and lifetime validation with zero clock skew for security.
        /// </summary>
        /// <param name="services">The IServiceCollection to add services to.</param>
        /// <param name="configuration">The application configuration containing Supabase settings.</param>
        /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
        /// <exception cref="InvalidOperationException">Thrown when neither a JWT secret nor a JWKS source is configured.</exception>
        public static IServiceCollection AddSupabaseAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            return services.AddSupabaseAuthentication(configuration, "Supabase");
        }

        /// <summary>
        /// Builds the <see cref="TokenValidationParameters"/> used to validate Supabase-issued JWTs,
        /// reading the signing key sources, issuer, and audience from the given configuration section.
        /// Useful for validating an arbitrary token string outside of the ASP.NET Core JWT Bearer
        /// pipeline (e.g. a "validate this token for me" utility endpoint).
        /// </summary>
        /// <remarks>
        /// Supabase signs tokens either with the legacy HS256 shared secret (<c>JwtSecret</c>) or with
        /// asymmetric ES256/RS256 keys published at the project's JWKS endpoint. The signing key is
        /// resolved per token so both verify during a key rotation:
        /// <list type="bullet">
        /// <item>No <c>kid</c> header: the shared secret.</item>
        /// <item><c>kid</c> header: the matching JWKS key, falling back to the shared secret if the JWKS
        /// has no such key (Supabase can put a <c>kid</c> on HS256 tokens too).</item>
        /// </list>
        /// The JWKS URL is <c>JwksUrl</c> if set, otherwise derived from <c>Url</c>
        /// (<c>{Url}/auth/v1/.well-known/jwks.json</c>), otherwise from <c>Issuer</c>
        /// (<c>{Issuer}/.well-known/jwks.json</c>).
        /// </remarks>
        /// <param name="configuration">The application configuration containing Supabase settings.</param>
        /// <param name="sectionName">The configuration section name containing Supabase settings.</param>
        /// <returns>Token validation parameters matching the rules used by <see cref="AddSupabaseAuthentication(IServiceCollection, IConfiguration)"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when neither a JWT secret nor a JWKS source is configured.</exception>
        public static TokenValidationParameters CreateTokenValidationParameters(IConfiguration configuration, string sectionName = "Supabase")
        {
            return CreateTokenValidationParameters(configuration, sectionName, SupabaseJwksProvider.For);
        }

        internal static TokenValidationParameters CreateTokenValidationParameters(
            IConfiguration configuration,
            string sectionName,
            Func<string, SupabaseJwksProvider> jwksProviderFactory)
        {
            var supabaseConfig = configuration.GetSection(sectionName);
            var jwtSecret = supabaseConfig["JwtSecret"];
            var jwksUrl = GetJwksUrl(supabaseConfig);

            if (string.IsNullOrEmpty(jwtSecret) && jwksUrl == null)
            {
                throw new InvalidOperationException(
                    $"No Supabase signing key source is configured in section '{sectionName}'. " +
                    "Set JwtSecret (legacy HS256) and/or Url, Issuer, or JwksUrl (asymmetric keys via JWKS).");
            }

            SecurityKey[] secretKeys = string.IsNullOrEmpty(jwtSecret)
                ? Array.Empty<SecurityKey>()
                : new SecurityKey[] { new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)) };
            var jwksProvider = jwksUrl == null ? null : jwksProviderFactory(jwksUrl);

            return new TokenValidationParameters
            {
                // Resolves the signing key per token: shared secret for legacy HS256 tokens,
                // JWKS for asymmetric ES256/RS256 tokens (see remarks above).
                ValidateIssuerSigningKey = true,
                IssuerSigningKeyResolver = (_, _, kid, _) => ResolveSigningKeys(kid, secretKeys, jwksProvider),

                // Only the algorithms Supabase issues -- rejects "none" and anything unexpected.
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256, SecurityAlgorithms.EcdsaSha256, SecurityAlgorithms.RsaSha256 },

                // Validates that the "iss" (issuer) claim is the expected Supabase URL.
                ValidateIssuer = true,
                ValidIssuer = supabaseConfig["Issuer"],

                // Validates that the "aud" (audience) claim is the expected value.
                ValidateAudience = true,
                ValidAudience = supabaseConfig["Audience"],

                // Validates the token's expiration.
                ValidateLifetime = true,

                // Ensures there is no clock skew. Recommended for security.
                ClockSkew = TimeSpan.Zero
            };
        }

        private static IEnumerable<SecurityKey> ResolveSigningKeys(string? kid, SecurityKey[] secretKeys, SupabaseJwksProvider? jwksProvider)
        {
            if (string.IsNullOrEmpty(kid) || jwksProvider == null)
            {
                return secretKeys;
            }

            var jwksKeys = jwksProvider.GetSigningKeys(kid).ToList();
            return jwksKeys.Count > 0 ? jwksKeys : secretKeys;
        }

        private static string? GetJwksUrl(IConfigurationSection supabaseConfig)
        {
            var explicitUrl = supabaseConfig["JwksUrl"];
            if (!string.IsNullOrEmpty(explicitUrl))
            {
                return explicitUrl;
            }

            var projectUrl = supabaseConfig["Url"];
            if (!string.IsNullOrEmpty(projectUrl))
            {
                return $"{projectUrl.TrimEnd('/')}/auth/v1/.well-known/jwks.json";
            }

            var issuer = supabaseConfig["Issuer"];
            if (!string.IsNullOrEmpty(issuer))
            {
                return $"{issuer.TrimEnd('/')}/.well-known/jwks.json";
            }

            return null;
        }

        /// <summary>
        /// Configures JWT Bearer authentication with custom configuration section name.
        /// Useful when Supabase configuration is stored under a different section name.
        /// </summary>
        /// <param name="services">The IServiceCollection to add services to.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="sectionName">The configuration section name containing Supabase settings.</param>
        /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
        /// <exception cref="InvalidOperationException">Thrown when neither a JWT secret nor a JWKS source is configured.</exception>
        public static IServiceCollection AddSupabaseAuthentication(this IServiceCollection services, IConfiguration configuration, string sectionName)
        {
            var validationParameters = CreateTokenValidationParameters(configuration, sectionName);
            var cookieName = SondarrAuthCookies.GetCookieName(configuration, sectionName);

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = validationParameters;
                options.Events = new JwtBearerEvents
                {
                    // Falls back to the cross-site session cookie (see SessionEndpointExtensions)
                    // when there's no Authorization header -- lets a browser navigating between
                    // sites under the shared parent domain stay authenticated without the
                    // frontend having to manually attach a bearer token.
                    OnMessageReceived = context =>
                    {
                        var hasAuthHeader = !string.IsNullOrEmpty(context.Request.Headers.Authorization.ToString());
                        if (!hasAuthHeader && context.Request.Cookies.TryGetValue(cookieName, out var cookieToken) && !string.IsNullOrEmpty(cookieToken))
                        {
                            context.Token = cookieToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

            return services;
        }
    }
}
