using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Sondarr.Auth.Api.Models;
using Sondarr.Auth.Shared.Models;
using Sondarr.Auth.Shared.Services;
using System.IdentityModel.Tokens.Jwt;

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
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthController> _logger;

        /// <summary>
        /// Initializes a new instance of the AuthController class.
        /// </summary>
        /// <param name="userContextService">The user context service for accessing current user information.</param>
        /// <param name="configuration">The application configuration, used to validate arbitrary tokens against Supabase settings.</param>
        /// <param name="logger">The logger for recording authentication events.</param>
        public AuthController(IUserContextService userContextService, IConfiguration configuration, ILogger<AuthController> logger)
        {
            _userContextService = userContextService ?? throw new ArgumentNullException(nameof(userContextService));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
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

                var validationParameters = Sondarr.Auth.Shared.SupabaseAuthenticationExtensions.CreateTokenValidationParameters(_configuration);

                // JwtSecurityTokenHandler remaps short claim types (e.g. "sub" -> a long
                // XML-namespace URI) by default. Disable that so claim types here match
                // what UserContext.FromClaims() and the JwtBearer middleware both expect.
                var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

                var principal = handler.ValidateToken(request.Token, validationParameters, out _);
                var userContext = UserContext.FromClaims(principal.Claims);
                var userInfo = MapToUserInfo(userContext);

                _logger.LogInformation("Token validated successfully for user {UserId}", userContext.UserId);

                return Ok(ValidateTokenResponse.SuccessResponse(userInfo));
            }
            catch (SecurityTokenExpiredException)
            {
                _logger.LogInformation("Token validation failed: token expired");
                return Ok(ValidateTokenResponse.FailureResponse("Token has expired"));
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                _logger.LogWarning("Token validation failed: invalid signature");
                return Ok(ValidateTokenResponse.FailureResponse("Token signature is invalid"));
            }
            catch (SecurityTokenInvalidIssuerException)
            {
                _logger.LogInformation("Token validation failed: invalid issuer");
                return Ok(ValidateTokenResponse.FailureResponse("Token issuer is invalid"));
            }
            catch (SecurityTokenInvalidAudienceException)
            {
                _logger.LogInformation("Token validation failed: invalid audience");
                return Ok(ValidateTokenResponse.FailureResponse("Token audience is invalid"));
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogInformation(ex, "Token validation failed");
                return Ok(ValidateTokenResponse.FailureResponse("Token is invalid"));
            }
            catch (ArgumentException ex)
            {
                // Thrown by the handler when the token string is not a well-formed JWT.
                _logger.LogInformation(ex, "Token validation failed: malformed token");
                return Ok(ValidateTokenResponse.FailureResponse("Token is malformed"));
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
