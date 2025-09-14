using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sondarr.Auth.Shared.Attributes;
using Sondarr.Auth.Shared.Models;
using Sondarr.Auth.Shared.Services;

namespace Sondarr.Auth.Api.Controllers
{
    /// <summary>
    /// Test controller to demonstrate authentication and authorization features.
    /// This controller provides various endpoints to test different authentication scenarios.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class TestController : ControllerBase
    {
        private readonly IUserContextService _userContextService;
        private readonly ILogger<TestController> _logger;

        /// <summary>
        /// Initializes a new instance of the TestController class.
        /// </summary>
        /// <param name="userContextService">The user context service for accessing current user information.</param>
        /// <param name="logger">The logger for recording test events.</param>
        public TestController(IUserContextService userContextService, ILogger<TestController> logger)
        {
            _userContextService = userContextService ?? throw new ArgumentNullException(nameof(userContextService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Public endpoint that doesn't require authentication.
        /// </summary>
        /// <returns>A public message.</returns>
        /// <response code="200">Returns a public message.</response>
        [HttpGet("public")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        public IActionResult GetPublicMessage()
        {
            _logger.LogInformation("Public endpoint accessed");
            return Ok(new
            {
                message = "This is a public endpoint. No authentication required.",
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Protected endpoint that requires authentication.
        /// </summary>
        /// <returns>User information for authenticated users.</returns>
        /// <response code="200">Returns user information.</response>
        /// <response code="401">If the user is not authenticated.</response>
        [HttpGet("protected")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetProtectedMessage()
        {
            try
            {
                var user = _userContextService.GetCurrentUserRequired();
                
                _logger.LogInformation("Protected endpoint accessed by user {UserId}", user.UserId);

                return Ok(new
                {
                    message = "This is a protected endpoint. Authentication required.",
                    user = new
                    {
                        id = user.UserId,
                        email = user.Email,
                        name = user.FullName,
                        roles = user.Roles
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Unauthorized attempt to access protected endpoint");
                return Unauthorized(new { message = "Authentication required" });
            }
        }

        /// <summary>
        /// Admin-only endpoint that requires the "admin" role.
        /// </summary>
        /// <returns>Admin-only information.</returns>
        /// <response code="200">Returns admin information.</response>
        /// <response code="401">If the user is not authenticated.</response>
        /// <response code="403">If the user doesn't have admin role.</response>
        [HttpGet("admin")]
        [Authorize]
        [RequireRole("admin")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult GetAdminMessage()
        {
            var user = _userContextService.GetCurrentUserRequired();
            
            _logger.LogInformation("Admin endpoint accessed by user {UserId}", user.UserId);

            return Ok(new
            {
                message = "This is an admin-only endpoint. Admin role required.",
                user = new
                {
                    id = user.UserId,
                    email = user.Email,
                    name = user.FullName,
                    roles = user.Roles
                },
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Moderator or Admin endpoint that requires either "moderator" or "admin" role.
        /// </summary>
        /// <returns>Moderator/Admin information.</returns>
        /// <response code="200">Returns moderator/admin information.</response>
        /// <response code="401">If the user is not authenticated.</response>
        /// <response code="403">If the user doesn't have required roles.</response>
        [HttpGet("moderator")]
        [Authorize]
        [RequireAnyRole("moderator", "admin")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public IActionResult GetModeratorMessage()
        {
            var user = _userContextService.GetCurrentUserRequired();
            
            _logger.LogInformation("Moderator endpoint accessed by user {UserId}", user.UserId);

            return Ok(new
            {
                message = "This is a moderator/admin endpoint. Moderator or Admin role required.",
                user = new
                {
                    id = user.UserId,
                    email = user.Email,
                    name = user.FullName,
                    roles = user.Roles
                },
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Endpoint that demonstrates custom role checking logic.
        /// </summary>
        /// <returns>Custom role check result.</returns>
        /// <response code="200">Returns role check result.</response>
        /// <response code="401">If the user is not authenticated.</response>
        [HttpGet("custom-role-check")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult CustomRoleCheck()
        {
            try
            {
                var user = _userContextService.GetCurrentUserRequired();
                var hasAdminRole = user.HasRole("admin");
                var hasModeratorRole = user.HasRole("moderator");
                var hasUserRole = user.HasRole("user");

                _logger.LogInformation("Custom role check performed for user {UserId}", user.UserId);

                return Ok(new
                {
                    message = "Custom role check results",
                    user = new
                    {
                        id = user.UserId,
                        email = user.Email,
                        name = user.FullName,
                        roles = user.Roles
                    },
                    roleChecks = new
                    {
                        isAdmin = hasAdminRole,
                        isModerator = hasModeratorRole,
                        isUser = hasUserRole,
                        hasAnyModeratorOrAdmin = user.HasAnyRole("moderator", "admin")
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Unauthorized attempt to access custom role check endpoint");
                return Unauthorized(new { message = "Authentication required" });
            }
        }

        /// <summary>
        /// Endpoint that demonstrates token validation information.
        /// </summary>
        /// <returns>Token validation information.</returns>
        /// <response code="200">Returns token information.</response>
        /// <response code="401">If the user is not authenticated.</response>
        [HttpGet("token-info")]
        [Authorize]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult GetTokenInfo()
        {
            try
            {
                var user = _userContextService.GetCurrentUserRequired();
                
                _logger.LogInformation("Token info requested by user {UserId}", user.UserId);

                return Ok(new
                {
                    message = "Token validation information",
                    tokenInfo = new
                    {
                        isValid = user.IsTokenValid(),
                        expiresAt = user.ExpiresAt,
                        issuedAt = user.IssuedAt,
                        notBefore = user.NotBefore,
                        issuer = user.Issuer,
                        audience = user.Audience,
                        jwtId = user.JwtId
                    },
                    user = new
                    {
                        id = user.UserId,
                        email = user.Email,
                        name = user.FullName,
                        roles = user.Roles
                    },
                    timestamp = DateTime.UtcNow
                });
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("Unauthorized attempt to access token info endpoint");
                return Unauthorized(new { message = "Authentication required" });
            }
        }
    }
}
