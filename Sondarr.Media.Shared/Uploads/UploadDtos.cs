using System.ComponentModel.DataAnnotations;

namespace Sondarr.Media.Shared.Uploads;

public sealed class InitiateUploadRequest
{
    [Required]
    public UploadKind? Kind { get; init; }

    [Required]
    [StringLength(100)]
    public string ContentType { get; init; } = string.Empty;

    [Range(1, long.MaxValue)]
    public long SizeBytes { get; init; }

    /// <summary>Original file name — logged only, never used in the storage key.</summary>
    [StringLength(255)]
    public string? FileName { get; init; }
}

public sealed class InitiateUploadResponse
{
    public string Key { get; init; } = string.Empty;
    public string UploadId { get; init; } = string.Empty;
    public long PartSizeBytes { get; init; }
    public int PartCount { get; init; }
}

public sealed class PartUrlsRequest
{
    [Required]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string UploadId { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<int> PartNumbers { get; init; } = [];
}

public sealed class PartUrlDto
{
    public int PartNumber { get; init; }
    public string Url { get; init; } = string.Empty;
}

public sealed class PartUrlsResponse
{
    public IReadOnlyList<PartUrlDto> Parts { get; init; } = [];
    public DateTime ExpiresAt { get; init; }
}

public sealed class UploadedPartDto
{
    public int PartNumber { get; init; }
    public string ETag { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
}

public sealed class CompletedPartDto
{
    [Range(1, 10_000)]
    public int PartNumber { get; init; }

    [Required]
    public string ETag { get; init; } = string.Empty;
}

public sealed class CompleteUploadRequest
{
    [Required]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string UploadId { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<CompletedPartDto> Parts { get; init; } = [];
}

public sealed class UploadRefRequest
{
    [Required]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string UploadId { get; init; } = string.Empty;
}

public sealed class CompletedUploadResponse
{
    public string Key { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public long SizeBytes { get; init; }
    public string ContentType { get; init; } = string.Empty;
}
