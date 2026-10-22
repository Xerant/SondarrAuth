namespace Sondarr.Media.Shared.Storage;

/// <summary>
/// Config section "S3". Works with AWS S3 and S3-compatible stores (DigitalOcean
/// Spaces, MinIO) via <see cref="ServiceUrl"/>.
/// </summary>
public sealed class S3Settings
{
    public const string SectionName = "S3";

    public string BucketName { get; set; } = string.Empty;
    public string Region { get; set; } = "us-east-1";

    /// <summary>Optional custom endpoint, e.g. <c>https://syd1.digitaloceanspaces.com</c> or <c>http://localhost:9000</c>.</summary>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>Needed by MinIO and some S3-compatible stores.</summary>
    public bool ForcePathStyle { get; set; }

    /// <summary>
    /// Optional static credentials. Leave empty to use the default AWS credential
    /// chain (env vars, instance/task role).
    /// </summary>
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;

    /// <summary>
    /// Public base URL objects are served from — a CloudFront distribution in front
    /// of the (private) bucket. Stored song URLs are <c>{PublicBaseUrl}/{key}</c>.
    /// </summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BucketName) && !string.IsNullOrWhiteSpace(PublicBaseUrl);
}
