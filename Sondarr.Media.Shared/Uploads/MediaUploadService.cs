using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sondarr.Media.Shared.Storage;

namespace Sondarr.Media.Shared.Uploads;

/// <summary>
/// Browser → S3 multipart uploads for one artist. The API only signs and verifies;
/// file bytes go straight to the bucket. Flow: initiate → part-urls (batched) →
/// PUT parts → complete. <see cref="ListPartsAsync"/> lets an interrupted upload resume.
/// </summary>
/// <remarks>
/// The caller decides which artist is acting — the Artist Portal from the signed-in
/// user, the admin API from the request. Every key-taking method checks the key
/// belongs to that artist.
/// </remarks>
public interface IMediaUploadService
{
    Task<InitiateUploadResponse> InitiateAsync(Guid artistId, InitiateUploadRequest request, CancellationToken ct = default);
    Task<PartUrlsResponse> GetPartUrlsAsync(Guid artistId, PartUrlsRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<UploadedPartDto>> ListPartsAsync(Guid artistId, string key, string uploadId, CancellationToken ct = default);
    Task<CompletedUploadResponse> CompleteAsync(Guid artistId, CompleteUploadRequest request, CancellationToken ct = default);
    Task AbortAsync(Guid artistId, UploadRefRequest request, CancellationToken ct = default);

    /// <summary>
    /// Validates that <paramref name="key"/> is a finished upload of <paramref name="kind"/>
    /// owned by <paramref name="artistId"/> — call before attaching a key to any record.
    /// </summary>
    Task<StoredObject> RequireUploadedObjectAsync(Guid artistId, string key, UploadKind kind, CancellationToken ct = default);
}

public sealed class MediaUploadService(
    IMediaStorage storage,
    IOptions<UploadOptions> options,
    ILogger<MediaUploadService> logger) : IMediaUploadService
{
    private readonly UploadOptions _options = options.Value;

    public async Task<InitiateUploadResponse> InitiateAsync(Guid artistId, InitiateUploadRequest request, CancellationToken ct = default)
    {
        var kind = request.Kind ?? throw MediaException.Validation("Kind is required.");
        var rule = UploadRules.For(kind);

        var contentType = UploadRules.NormalizeContentType(request.ContentType);
        if (!rule.ExtensionsByContentType.TryGetValue(contentType, out var extension))
        {
            throw MediaException.Validation(
                $"Unsupported file type for {kind} uploads.",
                new { contentType = request.ContentType, allowed = rule.ExtensionsByContentType.Keys });
        }

        if (request.SizeBytes > rule.MaxBytes)
        {
            throw MediaException.Validation(
                $"File is too large. {kind} uploads are limited to {rule.MaxBytes / (1024 * 1024)} MB.",
                new { sizeBytes = request.SizeBytes, maxBytes = rule.MaxBytes });
        }

        var key = StorageKeys.Create(artistId, kind, extension);
        var uploadId = await storage.InitiateMultipartUploadAsync(key, contentType, ct);

        logger.LogInformation(
            "Started {Kind} upload {Key} for artist {ArtistId} ({SizeBytes} bytes, file {FileName})",
            kind, key, artistId, request.SizeBytes, request.FileName);

        return new InitiateUploadResponse
        {
            Key = key,
            UploadId = uploadId,
            PartSizeBytes = _options.PartSizeBytes,
            PartCount = PartCount(request.SizeBytes)
        };
    }

    public async Task<PartUrlsResponse> GetPartUrlsAsync(Guid artistId, PartUrlsRequest request, CancellationToken ct = default)
    {
        var kind = StorageKeys.EnsureOwned(request.Key, artistId);

        var partNumbers = request.PartNumbers.Distinct().Order().ToList();
        if (partNumbers.Count > _options.MaxPartUrlsPerRequest)
            throw MediaException.Validation($"Request at most {_options.MaxPartUrlsPerRequest} part URLs at a time.");

        // Parts past what the kind's size limit needs are never legitimate.
        var maxParts = PartCount(UploadRules.For(kind).MaxBytes);
        if (partNumbers.Any(n => n < 1 || n > maxParts))
            throw MediaException.Validation($"Part numbers must be between 1 and {maxParts}.");

        var lifetime = TimeSpan.FromMinutes(_options.PartUrlExpiryMinutes);
        var urls = await storage.GetPartUploadUrlsAsync(request.Key, request.UploadId, partNumbers, lifetime, ct);

        return new PartUrlsResponse
        {
            Parts = urls.Select(u => new PartUrlDto { PartNumber = u.PartNumber, Url = u.Url }).ToList(),
            ExpiresAt = DateTime.UtcNow.Add(lifetime)
        };
    }

    public async Task<IReadOnlyList<UploadedPartDto>> ListPartsAsync(Guid artistId, string key, string uploadId, CancellationToken ct = default)
    {
        StorageKeys.EnsureOwned(key, artistId);

        var parts = await storage.ListUploadedPartsAsync(key, uploadId, ct);
        return parts
            .Select(p => new UploadedPartDto { PartNumber = p.PartNumber, ETag = p.ETag, SizeBytes = p.SizeBytes })
            .ToList();
    }

    public async Task<CompletedUploadResponse> CompleteAsync(Guid artistId, CompleteUploadRequest request, CancellationToken ct = default)
    {
        var kind = StorageKeys.EnsureOwned(request.Key, artistId);

        var parts = request.Parts
            .OrderBy(p => p.PartNumber)
            .Select(p => new CompletedPart(p.PartNumber, p.ETag))
            .ToList();
        if (parts.Select(p => p.PartNumber).Distinct().Count() != parts.Count)
            throw MediaException.Validation("Each part number may appear only once.");

        await storage.CompleteMultipartUploadAsync(request.Key, request.UploadId, parts, ct);

        // Presigned part URLs can't cap part size, so the real size is only known
        // now. Anything over the limit is deleted rather than kept.
        var stored = await storage.GetObjectAsync(request.Key, ct)
            ?? throw MediaException.Upstream("Upload completed but the object could not be found in storage.");

        var rule = UploadRules.For(kind);
        if (stored.SizeBytes > rule.MaxBytes)
        {
            await storage.DeleteObjectAsync(request.Key, ct);
            throw MediaException.Validation(
                $"File is too large. {kind} uploads are limited to {rule.MaxBytes / (1024 * 1024)} MB.",
                new { sizeBytes = stored.SizeBytes, maxBytes = rule.MaxBytes });
        }

        logger.LogInformation("Completed upload {Key} for artist {ArtistId} ({SizeBytes} bytes)", request.Key, artistId, stored.SizeBytes);

        return new CompletedUploadResponse
        {
            Key = stored.Key,
            Url = storage.GetPublicUrl(stored.Key),
            SizeBytes = stored.SizeBytes,
            ContentType = stored.ContentType
        };
    }

    public async Task AbortAsync(Guid artistId, UploadRefRequest request, CancellationToken ct = default)
    {
        StorageKeys.EnsureOwned(request.Key, artistId);
        await storage.AbortMultipartUploadAsync(request.Key, request.UploadId, ct);
    }

    public async Task<StoredObject> RequireUploadedObjectAsync(Guid artistId, string key, UploadKind kind, CancellationToken ct = default)
    {
        StorageKeys.EnsureOwned(key, artistId, kind);

        var stored = await storage.GetObjectAsync(key, ct)
            ?? throw MediaException.Validation("Upload not found. Finish uploading the file first.", new { key });

        var rule = UploadRules.For(kind);
        if (stored.SizeBytes > rule.MaxBytes || !rule.ExtensionsByContentType.ContainsKey(UploadRules.NormalizeContentType(stored.ContentType)))
            throw MediaException.Validation("Uploaded file does not meet the requirements for this field.", new { key });

        return stored;
    }

    private int PartCount(long sizeBytes) => (int)Math.Max(1, (sizeBytes + _options.PartSizeBytes - 1) / _options.PartSizeBytes);
}
