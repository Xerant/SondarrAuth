using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
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
        /// <exception cref="InvalidOperationException">Thrown when Supabase JWT Secret is not configured.</exception>
        public static IServiceCollection AddSupabaseAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            return services.AddSupabaseAuthentication(configuration, "Supabase");
        }

        /// <summary>
        /// Builds the <see cref="TokenValidationParameters"/> used to validate Supabase-issued JWTs,
        /// reading the JWT secret, issuer, and audience from the given configuration section.
        /// Useful for validating an arbitrary token string outside of the ASP.NET Core JWT Bearer
        /// pipeline (e.g. a "validate this token for me" utility endpoint).
        /// </summary>
        /// <param name="configuration">The application configuration containing Supabase settings.</param>
        /// <param name="sectionName">The configuration section name containing Supabase settings.</param>
        /// <returns>Token validation parameters matching the rules used by <see cref="AddSupabaseAuthentication(IServiceCollection, IConfiguration)"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown when Supabase JWT Secret is not configured.</exception>
        public static TokenValidationParameters CreateTokenValidationParameters(IConfiguration configuration, string sectionName = "Supabase")
        {
            var supabaseConfig = configuration.GetSection(sectionName);
            var jwtSecret = supabaseConfig["JwtSecret"];

            if (string.IsNullOrEmpty(jwtSecret))
            {
                throw new InvalidOperationException($"Supabase JWT Secret is not configured in section '{sectionName}'. Please check your settings.");
            }

            return new TokenValidationParameters
            {
                // Validates the signing key against the configured Supabase JWT secret.
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),

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

        /// <summary>
        /// Configures JWT Bearer authentication with custom configuration section name.
        /// Useful when Supabase configuration is stored under a different section name.
        /// </summary>
        /// <param name="services">The IServiceCollection to add services to.</param>
        /// <param name="configuration">The application configuration.</param>
        /// <param name="sectionName">The configuration section name containing Supabase settings.</param>
        /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
        /// <exception cref="InvalidOperationException">Thrown when Supabase JWT Secret is not configured.</exception>
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
