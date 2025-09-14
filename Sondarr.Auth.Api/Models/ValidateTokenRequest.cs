namespace Sondarr.Auth.Api.Models
{
    /// <summary>
    /// Represents a request to validate a JWT token.
    /// </summary>
    public class ValidateTokenRequest
    {
        /// <summary>
        /// Gets or sets the JWT token to validate.
        /// </summary>
        public string Token { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the response from token validation.
    /// </summary>
    public class ValidateTokenResponse
    {
        /// <summary>
        /// Gets or sets a value indicating whether the token is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the response message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the user information if the token is valid.
        /// </summary>
        public UserInfo? User { get; set; }

        /// <summary>
        /// Gets or sets any errors that occurred during validation.
        /// </summary>
        public IList<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Creates a successful validation response.
        /// </summary>
        /// <param name="user">The user information from the token.</param>
        /// <returns>A successful validation response.</returns>
        public static ValidateTokenResponse SuccessResponse(UserInfo user)
        {
            return new ValidateTokenResponse
            {
                IsValid = true,
                Message = "Token is valid",
                User = user
            };
        }

        /// <summary>
        /// Creates a failed validation response.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="errors">Additional error details.</param>
        /// <returns>A failed validation response.</returns>
        public static ValidateTokenResponse FailureResponse(string message, params string[] errors)
        {
            return new ValidateTokenResponse
            {
                IsValid = false,
                Message = message,
                Errors = errors.ToList()
            };
        }
    }
}
