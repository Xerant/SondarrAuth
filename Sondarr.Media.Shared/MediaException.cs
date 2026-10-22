namespace Sondarr.Media.Shared;

/// <summary>
/// Expected media failures (bad file type, someone else's key, missing upload,
/// storage down). <see cref="Code"/> uses the platform's error codes, so a host's
/// exception handler can write <c>{ code, message, details }</c> with
/// <see cref="StatusCode"/> unchanged.
/// </summary>
public sealed class MediaException : Exception
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string Forbidden = "FORBIDDEN";
    public const string NotFound = "NOT_FOUND";
    public const string UpstreamError = "UPSTREAM_ERROR";
    public const string NotConfigured = "NOT_CONFIGURED";

    private MediaException(int statusCode, string code, string message, object? details, Exception? inner)
        : base(message, inner)
    {
        StatusCode = statusCode;
        Code = code;
        Details = details;
    }

    public int StatusCode { get; }
    public string Code { get; }
    public object? Details { get; }

    public static MediaException Validation(string message, object? details = null) =>
        new(400, ValidationError, message, details, null);

    public static MediaException NotOwned(string message, object? details = null) =>
        new(403, Forbidden, message, details, null);

    public static MediaException Missing(string message, object? details = null) =>
        new(404, NotFound, message, details, null);

    public static MediaException Upstream(string message, Exception? inner = null) =>
        new(502, UpstreamError, message, null, inner);

    public static MediaException Unconfigured(string message) =>
        new(503, NotConfigured, message, null, null);
}
