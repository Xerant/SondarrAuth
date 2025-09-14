using Microsoft.AspNetCore.Http;
using Sondarr.Auth.Shared.Models;
using System.Security.Claims;

namespace Sondarr.Auth.Shared.Services
{
    /// <summary>
    /// Implementation of IUserContextService that extracts user context from JWT claims.
    /// This service provides access to user information from the current HTTP context
    /// in a microservice architecture.
    /// </summary>
    public class UserContextService : IUserContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Initializes a new instance of the UserContextService class.
        /// </summary>
        /// <param name="httpContextAccessor">The HTTP context accessor for accessing current request context.</param>
        public UserContextService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        /// <summary>
        /// Gets the current user's context from the JWT token.
        /// </summary>
        /// <returns>The current user's context, or null if no user is authenticated.</returns>
        public UserContext? GetCurrentUser()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.User?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return UserContext.FromClaims(httpContext.User.Claims);
        }

        /// <summary>
        /// Gets the current user's context from the JWT token.
        /// </summary>
        /// <returns>The current user's context.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when no user is authenticated.</exception>
        public UserContext GetCurrentUserRequired()
        {
            var user = GetCurrentUser();
            if (user == null)
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            return user;
        }

        /// <summary>
        /// Gets the current user's ID from the JWT token.
        /// </summary>
        /// <returns>The current user's ID, or null if no user is authenticated.</returns>
        public string? GetCurrentUserId()
        {
            var user = GetCurrentUser();
            return user?.UserId;
        }

        /// <summary>
        /// Gets the current user's ID from the JWT token.
        /// </summary>
        /// <returns>The current user's ID.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when no user is authenticated.</exception>
        public string GetCurrentUserIdRequired()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                throw new UnauthorizedAccessException("User is not authenticated.");
            }

            return userId;
        }

        /// <summary>
        /// Gets the current user's email from the JWT token.
        /// </summary>
        /// <returns>The current user's email, or null if no user is authenticated.</returns>
        public string? GetCurrentUserEmail()
        {
            var user = GetCurrentUser();
            return user?.Email;
        }

        /// <summary>
        /// Gets the current user's roles from the JWT token.
        /// </summary>
        /// <returns>The current user's roles, or empty list if no user is authenticated.</returns>
        public IList<string> GetCurrentUserRoles()
        {
            var user = GetCurrentUser();
            return user?.Roles ?? new List<string>();
        }

        /// <summary>
        /// Checks if the current user has a specific role.
        /// </summary>
        /// <param name="role">The role to check for.</param>
        /// <returns>True if the user has the specified role, false otherwise.</returns>
        public bool HasRole(string role)
        {
            var user = GetCurrentUser();
            return user?.HasRole(role) ?? false;
        }

        /// <summary>
        /// Checks if the current user has any of the specified roles.
        /// </summary>
        /// <param name="roles">The roles to check for.</param>
        /// <returns>True if the user has any of the specified roles, false otherwise.</returns>
        public bool HasAnyRole(params string[] roles)
        {
            var user = GetCurrentUser();
            return user?.HasAnyRole(roles) ?? false;
        }

        /// <summary>
        /// Checks if a user is currently authenticated.
        /// </summary>
        /// <returns>True if a user is authenticated, false otherwise.</returns>
        public bool IsAuthenticated()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            return httpContext?.User?.Identity?.IsAuthenticated == true;
        }
    }
}
