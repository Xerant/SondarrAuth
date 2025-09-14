using Microsoft.AspNetCore.Mvc;
using Sondarr.Auth.Shared.Attributes;
using Sondarr.Auth.Shared.Services;

namespace Examples
{
    /// <summary>
    /// Example controller demonstrating how to use Sondarr.Auth.Shared in other microservices.
    /// This example shows various authentication and authorization patterns.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MicroserviceExampleController : ControllerBase
    {
        private readonly IUserContextService _userContextService;
        private readonly ILogger<MicroserviceExampleController> _logger;

        public MicroserviceExampleController(
            IUserContextService userContextService, 
            ILogger<MicroserviceExampleController> logger)
        {
            _userContextService = userContextService;
            _logger = logger;
        }

        /// <summary>
        /// Public endpoint - no authentication required.
        /// </summary>
        [HttpGet("public")]
        public IActionResult GetPublicData()
        {
            return Ok(new { message = "This is public data", timestamp = DateTime.UtcNow });
        }

        /// <summary>
        /// Protected endpoint - requires authentication.
        /// </summary>
        [HttpGet("protected")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult GetProtectedData()
        {
            var user = _userContextService.GetCurrentUserRequired();
            
            _logger.LogInformation("User {UserId} accessed protected data", user.UserId);
            
            return Ok(new 
            { 
                message = "This is protected data", 
                user = new 
                {
                    id = user.UserId,
                    email = user.Email,
                    name = user.FullName
                },
                timestamp = DateTime.UtcNow 
            });
        }

        /// <summary>
        /// Admin-only endpoint - requires admin role.
        /// </summary>
        [HttpGet("admin")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        [RequireRole("admin")]
        public IActionResult GetAdminData()
        {
            var user = _userContextService.GetCurrentUserRequired();
            
            _logger.LogInformation("Admin {UserId} accessed admin data", user.UserId);
            
            return Ok(new 
            { 
                message = "This is admin-only data", 
                user = new 
                {
                    id = user.UserId,
                    email = user.Email,
                    roles = user.Roles
                },
                timestamp = DateTime.UtcNow 
            });
        }

        /// <summary>
        /// Moderator or Admin endpoint - requires either role.
        /// </summary>
        [HttpGet("moderator")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        [RequireAnyRole("moderator", "admin")]
        public IActionResult GetModeratorData()
        {
            var user = _userContextService.GetCurrentUserRequired();
            
            _logger.LogInformation("Moderator/Admin {UserId} accessed moderator data", user.UserId);
            
            return Ok(new 
            { 
                message = "This is moderator/admin data", 
                user = new 
                {
                    id = user.UserId,
                    email = user.Email,
                    roles = user.Roles
                },
                timestamp = DateTime.UtcNow 
            });
        }

        /// <summary>
        /// Custom authorization logic example.
        /// </summary>
        [HttpGet("custom")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult GetCustomData()
        {
            var user = _userContextService.GetCurrentUserRequired();
            
            // Custom business logic for authorization
            if (!user.EmailVerified)
            {
                return Forbid("Email verification required");
            }

            if (user.HasRole("premium") || user.HasRole("admin"))
            {
                return Ok(new 
                { 
                    message = "Premium data access granted", 
                    data = "Sensitive premium information",
                    user = new 
                    {
                        id = user.UserId,
                        email = user.Email,
                        isPremium = true
                    }
                });
            }

            return Ok(new 
            { 
                message = "Basic data access", 
                data = "Basic information",
                user = new 
                {
                    id = user.UserId,
                    email = user.Email,
                    isPremium = false
                }
            });
        }

        /// <summary>
        /// User profile endpoint with detailed user information.
        /// </summary>
        [HttpGet("profile")]
        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult GetUserProfile()
        {
            var user = _userContextService.GetCurrentUserRequired();
            
            return Ok(new 
            {
                id = user.UserId,
                email = user.Email,
                emailVerified = user.EmailVerified,
                phone = user.Phone,
                phoneVerified = user.PhoneVerified,
                fullName = user.FullName,
                avatarUrl = user.AvatarUrl,
                roles = user.Roles,
                tokenInfo = new 
                {
                    isValid = user.IsTokenValid(),
                    expiresAt = user.ExpiresAt,
                    issuedAt = user.IssuedAt,
                    issuer = user.Issuer,
                    audience = user.Audience
                },
                customClaims = user.CustomClaims
            });
        }
    }
}
