using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Sondarr.Auth.Shared.Models;
using System;
using System.IdentityModel.Tokens.Jwt;

namespace Sondarr.Auth.Shared
{
    /// <summary>
    /// Maps the endpoints that establish and clear the cross-site session cookie. Any of the
    /// Sondarr sites can mount these to become a valid place to "log in" -- once set, the cookie
    /// (scoped to the shared parent domain) is sent automatically by the browser to every other
    /// site, and <see cref="SupabaseAuthenticationExtensions.AddSupabaseAuthentication(Microsoft.Extensions.DependencyInjection.IServiceCollection, IConfiguration)"/>
    /// picks it up as a fallback to the Authorization header.
    /// </summary>
    public static class SessionEndpointExtensions
    {
        /// <summary>
        /// Maps "{basePath}/session" (POST, sets the cookie from a Supabase access token) and
        /// "{basePath}/logout" (POST, clears the cookie).
        /// </summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="configuration">The application configuration containing Supabase settings.</param>
        /// <param name="basePath">The route prefix for the two endpoints. Defaults to "/auth".</param>
        /// <param name="sectionName">The configuration section name containing Supabase settings.</param>
        public static IEndpointRouteBuilder MapSondarrSessionEndpoints(this IEndpointRouteBuilder endpoints, IConfiguration configuration, string basePath = "/auth", string sectionName = "Supabase")
        {
            endpoints.MapPost($"{basePath}/session", (CreateSessionRequest request, HttpContext httpContext) =>
            {
                if (string.IsNullOrWhiteSpace(request.AccessToken))
                {
                    return Results.BadRequest(new { message = "accessToken is required" });
                }

                DateTime expiresAtUtc;
                try
                {
                    var validationParameters = SupabaseAuthenticationExtensions.CreateTokenValidationParameters(configuration, sectionName);
                    var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
                    handler.ValidateToken(request.AccessToken, validationParameters, out var validatedToken);
                    expiresAtUtc = validatedToken.ValidTo;
                }
                catch (SecurityTokenException)
                {
                    return Results.BadRequest(new { message = "accessToken is not a valid Supabase token" });
                }
                catch (ArgumentException)
                {
                    return Results.BadRequest(new { message = "accessToken is malformed" });
                }

                var cookieName = SondarrAuthCookies.GetCookieName(configuration, sectionName);
                var cookieOptions = SondarrAuthCookies.BuildCookieOptions(configuration, sectionName, expiresAtUtc);
                httpContext.Response.Cookies.Append(cookieName, request.AccessToken, cookieOptions);

                return Results.Ok(new { message = "Session cookie set" });
            });

            endpoints.MapPost($"{basePath}/logout", (HttpContext httpContext) =>
            {
                var cookieName = SondarrAuthCookies.GetCookieName(configuration, sectionName);
                // Deleting a cookie requires the same Domain/Path/Secure/SameSite attributes it
                // was set with -- the Expires value itself is irrelevant for Delete().
                var cookieOptions = SondarrAuthCookies.BuildCookieOptions(configuration, sectionName);
                httpContext.Response.Cookies.Delete(cookieName, cookieOptions);

                return Results.Ok(new { message = "Session cookie cleared" });
            });

            return endpoints;
        }
    }
}
