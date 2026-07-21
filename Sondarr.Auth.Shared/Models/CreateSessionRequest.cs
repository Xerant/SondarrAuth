namespace Sondarr.Auth.Shared.Models
{
    /// <summary>
    /// Request body for establishing a cross-site session cookie from a Supabase access token
    /// already obtained by the client (e.g. via the Supabase JS SDK sign-in call).
    /// </summary>
    public class CreateSessionRequest
    {
        /// <summary>
        /// Gets or sets the Supabase access token (JWT) to store in the session cookie.
        /// </summary>
        public string AccessToken { get; set; } = string.Empty;
    }
}
