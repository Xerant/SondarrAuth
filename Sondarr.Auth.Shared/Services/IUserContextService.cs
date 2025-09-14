using Sondarr.Auth.Shared.Models;

namespace Sondarr.Auth.Shared.Services
{
    /// <summary>
    /// Service interface for accessing the current user's context from JWT claims.
    /// This service provides a clean abstraction for accessing user information
    /// across microservices without directly manipulating HTTP context or claims.
    /// </summary>
    public interface IUserContextService
    {
        /// <summary>
        /// Gets the current user's context from the JWT token.
        /// </summary>
        /// <returns>The current user's context, or null if no user is authenticated.</returns>
        UserContext? GetCurrentUser();

        /// <summary>
        /// Gets the current user's context from the JWT token.
        /// </summary>
        /// <returns>The current user's context.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when no user is authenticated.</exception>
        UserContext GetCurrentUserRequired();

        /// <summary>
        /// Gets the current user's ID from the JWT token.
        /// </summary>
        /// <returns>The current user's ID, or null if no user is authenticated.</returns>
        string? GetCurrentUserId();

        /// <summary>
        /// Gets the current user's ID from the JWT token.
        /// </summary>
        /// <returns>The current user's ID.</returns>
        /// <exception cref="UnauthorizedAccessException">Thrown when no user is authenticated.</exception>
        string GetCurrentUserIdRequired();

        /// <summary>
        /// Gets the current user's email from the JWT token.
        /// </summary>
        /// <returns>The current user's email, or null if no user is authenticated.</returns>
        string? GetCurrentUserEmail();

        /// <summary>
        /// Gets the current user's roles from the JWT token.
        /// </summary>
        /// <returns>The current user's roles, or empty list if no user is authenticated.</returns>
        IList<string> GetCurrentUserRoles();

        /// <summary>
        /// Checks if the current user has a specific role.
        /// </summary>
        /// <param name="role">The role to check for.</param>
        /// <returns>True if the user has the specified role, false otherwise.</returns>
        bool HasRole(string role);

        /// <summary>
        /// Checks if the current user has any of the specified roles.
        /// </summary>
        /// <param name="roles">The roles to check for.</param>
        /// <returns>True if the user has any of the specified roles, false otherwise.</returns>
        bool HasAnyRole(params string[] roles);

        /// <summary>
        /// Checks if a user is currently authenticated.
        /// </summary>
        /// <returns>True if a user is authenticated, false otherwise.</returns>
        bool IsAuthenticated();
    }
}
