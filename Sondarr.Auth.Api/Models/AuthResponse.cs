namespace Sondarr.Auth.Api.Models
{
    /// <summary>
    /// Represents the response from authentication operations.
    /// </summary>
    public class AuthResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the operation was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the response message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the JWT token if authentication was successful.
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// Gets or sets the token expiration time in seconds.
        /// </summary>
        public long? ExpiresIn { get; set; }

        /// <summary>
        /// Gets or sets the user information if authentication was successful.
        /// </summary>
        public UserInfo? User { get; set; }

        /// <summary>
        /// Gets or sets any errors that occurred during the operation.
        /// </summary>
        public IList<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Creates a successful authentication response.
        /// </summary>
        /// <param name="token">The JWT token.</param>
        /// <param name="expiresIn">The token expiration time in seconds.</param>
        /// <param name="user">The user information.</param>
        /// <returns>A successful authentication response.</returns>
        public static AuthResponse SuccessResponse(string token, long expiresIn, UserInfo user)
        {
            return new AuthResponse
            {
                Success = true,
                Message = "Authentication successful",
                Token = token,
                ExpiresIn = expiresIn,
                User = user
            };
        }

        /// <summary>
        /// Creates a failed authentication response.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="errors">Additional error details.</param>
        /// <returns>A failed authentication response.</returns>
        public static AuthResponse FailureResponse(string message, params string[] errors)
        {
            return new AuthResponse
            {
                Success = false,
                Message = message,
                Errors = errors.ToList()
            };
        }
    }

    /// <summary>
    /// Represents user information in authentication responses.
    /// </summary>
    public class UserInfo
    {
        /// <summary>
        /// Gets or sets the user's unique identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user's email address.
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the user's email is verified.
        /// </summary>
        public bool EmailVerified { get; set; }

        /// <summary>
        /// Gets or sets the user's full name.
        /// </summary>
        public string? FullName { get; set; }

        /// <summary>
        /// Gets or sets the user's avatar URL.
        /// </summary>
        public string? AvatarUrl { get; set; }

        /// <summary>
        /// Gets or sets the user's roles.
        /// </summary>
        public IList<string> Roles { get; set; } = new List<string>();
    }
}
