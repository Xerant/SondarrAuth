using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Sondarr.Auth.Shared.Models
{
    /// <summary>
    /// Represents the authenticated user context extracted from JWT claims.
    /// This class provides a strongly-typed way to access user information
    /// across microservices without directly manipulating claims.
    /// </summary>
    public class UserContext
    {
        /// <summary>
        /// Gets or sets the unique user identifier from the JWT 'sub' claim.
        /// </summary>
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's email address from the JWT 'email' claim.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's email verification status from the JWT 'email_verified' claim.
        /// </summary>
        public bool EmailVerified { get; set; }

        /// <summary>
        /// Gets or sets the user's phone number from the JWT 'phone' claim.
        /// </summary>
        public string? Phone { get; set; }

        /// <summary>
        /// Gets or sets the user's phone verification status from the JWT 'phone_verified' claim.
        /// </summary>
        public bool PhoneVerified { get; set; }

        /// <summary>
        /// Gets or sets the user's full name from the JWT 'name' claim.
        /// </summary>
        public string? FullName { get; set; }

        /// <summary>
        /// Gets or sets the user's avatar URL from the JWT 'picture' claim.
        /// </summary>
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// Gets or sets the user's roles from the JWT 'role' claim.
        /// </summary>
        public IList<string> Roles { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the JWT issuer from the 'iss' claim.
        /// </summary>
        public string Issuer { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the JWT audience from the 'aud' claim.
        /// </summary>
        public string Audience { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the JWT expiration time from the 'exp' claim.
        /// </summary>
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the JWT issued at time from the 'iat' claim.
        /// </summary>
        public DateTime IssuedAt { get; set; }

        /// <summary>
        /// Gets or sets the JWT not before time from the 'nbf' claim.
        /// </summary>
        public DateTime NotBefore { get; set; }

        /// <summary>
        /// Gets or sets the JWT ID from the 'jti' claim.
        /// </summary>
        public string? JwtId { get; set; }

        /// <summary>
        /// Gets or sets additional custom claims that are not part of the standard properties.
        /// </summary>
        public IDictionary<string, string> CustomClaims { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Creates a UserContext instance from a collection of JWT claims.
        /// </summary>
        /// <param name="claims">The JWT claims to extract user information from.</param>
        /// <returns>A populated UserContext instance.</returns>
        public static UserContext FromClaims(IEnumerable<Claim> claims)
        {
            var userContext = new UserContext();
            var claimsList = claims.ToList();

            // Extract standard claims
            userContext.UserId = claimsList.FirstOrDefault(c => c.Type == "sub")?.Value ?? string.Empty;
            userContext.Email = claimsList.FirstOrDefault(c => c.Type == "email")?.Value ?? string.Empty;
            userContext.Phone = claimsList.FirstOrDefault(c => c.Type == "phone")?.Value;
            userContext.FullName = claimsList.FirstOrDefault(c => c.Type == "name")?.Value;
            userContext.AvatarUrl = claimsList.FirstOrDefault(c => c.Type == "picture")?.Value;
            userContext.Issuer = claimsList.FirstOrDefault(c => c.Type == "iss")?.Value ?? string.Empty;
            userContext.Audience = claimsList.FirstOrDefault(c => c.Type == "aud")?.Value ?? string.Empty;
            userContext.JwtId = claimsList.FirstOrDefault(c => c.Type == "jti")?.Value;

            // Extract boolean claims
            userContext.EmailVerified = bool.TryParse(claimsList.FirstOrDefault(c => c.Type == "email_verified")?.Value, out var emailVerified) && emailVerified;
            userContext.PhoneVerified = bool.TryParse(claimsList.FirstOrDefault(c => c.Type == "phone_verified")?.Value, out var phoneVerified) && phoneVerified;

            // Extract role claims
            userContext.Roles = claimsList
                .Where(c => c.Type == "role" || c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            // Extract datetime claims
            if (long.TryParse(claimsList.FirstOrDefault(c => c.Type == "exp")?.Value, out var exp))
            {
                userContext.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(exp).DateTime;
            }

            if (long.TryParse(claimsList.FirstOrDefault(c => c.Type == "iat")?.Value, out var iat))
            {
                userContext.IssuedAt = DateTimeOffset.FromUnixTimeSeconds(iat).DateTime;
            }

            if (long.TryParse(claimsList.FirstOrDefault(c => c.Type == "nbf")?.Value, out var nbf))
            {
                userContext.NotBefore = DateTimeOffset.FromUnixTimeSeconds(nbf).DateTime;
            }

            // Extract custom claims (exclude standard ones)
            var standardClaimTypes = new HashSet<string>
            {
                "sub", "email", "phone", "name", "picture", "iss", "aud", "jti",
                "email_verified", "phone_verified", "role", "exp", "iat", "nbf",
                ClaimTypes.Role
            };

            userContext.CustomClaims = claimsList
                .Where(c => !standardClaimTypes.Contains(c.Type))
                .ToDictionary(c => c.Type, c => c.Value);

            return userContext;
        }

        /// <summary>
        /// Checks if the user has a specific role.
        /// </summary>
        /// <param name="role">The role to check for.</param>
        /// <returns>True if the user has the specified role, false otherwise.</returns>
        public bool HasRole(string role)
        {
            return Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the user has any of the specified roles.
        /// </summary>
        /// <param name="roles">The roles to check for.</param>
        /// <returns>True if the user has any of the specified roles, false otherwise.</returns>
        public bool HasAnyRole(params string[] roles)
        {
            return roles.Any(role => HasRole(role));
        }

        /// <summary>
        /// Checks if the JWT token is still valid (not expired and not before its valid time).
        /// </summary>
        /// <returns>True if the token is valid, false otherwise.</returns>
        public bool IsTokenValid()
        {
            var now = DateTime.UtcNow;
            return now >= NotBefore && now <= ExpiresAt;
        }
    }
}
