using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sondarr.Auth.Api.Models;
using Sondarr.Auth.Shared.Models;
using Sondarr.Auth.Shared.Services;

namespace Sondarr.Auth.Api.Controllers
{
    /// <summary>
    /// Controller for authentication-related operations.
    /// Provides endpoints for token validation and user information retrieval.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IUserContextService _userContextService;
        private readonly ILogger<AuthController> _logger;

        /// <summary>
        /// Initializes a new instance of the AuthController class.
        /// </summary>
        /// <param name="userContextService">The user context service for accessing current user information.</param>
        /// <param name="logger">The logger for recording authentication events.</param>
        public AuthController(IUserContextService userContextService, ILogger<AuthController> logger)
        {
            _userContextService = userContextService ?? throw new ArgumentNullException(nameof(userContextService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the current user's information from the JWT token.
        /// </summary>
        /// <returns>The current user's information.</returns>
        /// <response code="200">Returns the current user's information.</response>
        /// <response code="401">If the user is not authenticated.</response>
        [HttpGet("me")]
        [Authorize]
        [ProducesResponseType(typeof(UserInfo), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetCurrentUser()
        {
            try
            {
                var user = _userContextService.GetCurrentUserRequired();
                var userInfo = MapToUserInfo(user);

                _logger.LogInformation("User {UserId} retrieved their profile information", user.UserId);

                return Ok(userInfo);
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Unauthorized attempt to access user profile");
                return Unauthorized(new { message = "User is not authenticated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current user information");
                return StatusCode(500, new { message = "An error occurred while retrieving user information" });
            }
        }

        /// <summary>
        /// Validates a JWT token and returns user information if valid.
        /// </summary>
        /// <param name="request">The token validation request.</param>
        /// <returns>The validation result with user information if valid.</returns>
        /// <response code="200">Returns the validation result.</response>
        /// <response code="400">If the request is invalid.</response>
        [HttpPost("validate-token")]
        [ProducesResponseType(typeof(ValidateTokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ValidateToken([FromBody] ValidateTokenRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Token))
                {
                    return BadRequest(ValidateTokenResponse.FailureResponse("Token is required"));
                }

                // Note: In a real implementation, you would validate the token here
                // For now, we'll return a placeholder response
                // This would typically involve:
                // 1. Parsing the JWT token
                // 2. Validating the signature using Supabase JWT secret
                // 3. Checking expiration and other claims
                // 4. Extracting user information from claims

                _logger.LogInformation("Token validation requested");

                return Ok(ValidateTokenResponse.FailureResponse("Token validation not implemented in this example. Use the shared library for JWT validation in your microservices."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating token");
                return StatusCode(500, ValidateTokenResponse.FailureResponse("An error occurred while validating the token"));
            }
        }

        /// <summary>
        /// Gets the current user's roles.
        /// </summary>
        /// <returns>The current user's roles.</returns>
        /// <response code="200">Returns the current user's roles.</response>
        /// <response code="401">If the user is not authenticated.</response>
        [HttpGet("roles")]
        [Authorize]
        [ProducesResponseType(typeof(IList<string>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetUserRoles()
        {
            try
            {
                var roles = _userContextService.GetCurrentUserRoles();
                var userId = _userContextService.GetCurrentUserId();

                _logger.LogInformation("User {UserId} retrieved their roles: {Roles}", userId, string.Join(", ", roles));

                return Ok(roles);
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Unauthorized attempt to access user roles");
                return Unauthorized(new { message = "User is not authenticated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user roles");
                return StatusCode(500, new { message = "An error occurred while retrieving user roles" });
            }
        }

        /// <summary>
        /// Checks if the current user has a specific role.
        /// </summary>
        /// <param name="role">The role to check for.</param>
        /// <returns>True if the user has the role, false otherwise.</returns>
        /// <response code="200">Returns the role check result.</response>
        /// <response code="401">If the user is not authenticated.</response>
        [HttpGet("has-role/{role}")]
        [Authorize]
        [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult HasRole(string role)
        {
            try
            {
                var hasRole = _userContextService.HasRole(role);
                var userId = _userContextService.GetCurrentUserId();

                _logger.LogInformation("User {UserId} role check for '{Role}': {HasRole}", userId, role, hasRole);

                return Ok(hasRole);
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Unauthorized attempt to check user role");
                return Unauthorized(new { message = "User is not authenticated" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking user role");
                return StatusCode(500, new { message = "An error occurred while checking user role" });
            }
        }

        /// <summary>
        /// Health check endpoint for the authentication service.
        /// </summary>
        /// <returns>Service health status.</returns>
        /// <response code="200">Service is healthy.</response>
        [HttpGet("health")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public IActionResult HealthCheck()
        {
            return Ok(new
            {
                status = "healthy",
                service = "Sondarr.Auth.Api",
                timestamp = DateTime.UtcNow,
                version = "1.0.0"
            });
        }

        /// <summary>
        /// Maps a UserContext to UserInfo for API responses.
        /// </summary>
        /// <param name="userContext">The user context to map.</param>
        /// <returns>The mapped user information.</returns>
        private static UserInfo MapToUserInfo(UserContext userContext)
        {
            return new UserInfo
            {
                Id = userContext.UserId,
                Email = userContext.Email,
                EmailVerified = userContext.EmailVerified,
                FullName = userContext.FullName,
                AvatarUrl = userContext.AvatarUrl,
                Roles = userContext.Roles
            };
        }
    }
}
