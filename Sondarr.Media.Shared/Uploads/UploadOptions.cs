namespace Sondarr.Media.Shared.Uploads;

/// <summary>Config section "Uploads". Storage location itself lives in the "S3" section.</summary>
public sealed class UploadOptions
{
    public const string SectionName = "Uploads";

    /// <summary>Multipart chunk size. S3 requires ≥ 5 MiB for every part but the last.</summary>
    public long PartSizeBytes { get; set; } = 8 * 1024 * 1024;

    /// <summary>Lifetime of each presigned part URL.</summary>
    public int PartUrlExpiryMinutes { get; set; } = 60;

    /// <summary>Most part URLs signed in one request.</summary>
    public int MaxPartUrlsPerRequest { get; set; } = 50;
}
