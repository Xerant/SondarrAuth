namespace Sondarr.Media.Shared.Storage;

public sealed record PresignedPartUrl(int PartNumber, string Url);
public sealed record UploadedPart(int PartNumber, string ETag, long SizeBytes);
public sealed record CompletedPart(int PartNumber, string ETag);
public sealed record StoredObject(string Key, long SizeBytes, string ContentType);

/// <summary>
/// Object storage for artist media (AWS S3; any S3-compatible store works via
/// <c>S3:ServiceUrl</c>). Browsers upload straight to storage through presigned
/// multipart URLs — file bytes never pass through the API.
/// </summary>
public interface IMediaStorage
{
    /// <returns>The storage upload id.</returns>
    Task<string> InitiateMultipartUploadAsync(string key, string contentType, CancellationToken ct = default);

    Task<IReadOnlyList<PresignedPartUrl>> GetPartUploadUrlsAsync(
        string key, string uploadId, IReadOnlyCollection<int> partNumbers, TimeSpan lifetime, CancellationToken ct = default);

    /// <summary>Parts already stored — lets an interrupted upload resume.</summary>
    Task<IReadOnlyList<UploadedPart>> ListUploadedPartsAsync(string key, string uploadId, CancellationToken ct = default);

    Task CompleteMultipartUploadAsync(string key, string uploadId, IReadOnlyCollection<CompletedPart> parts, CancellationToken ct = default);
    Task AbortMultipartUploadAsync(string key, string uploadId, CancellationToken ct = default);

    /// <returns>Null when the object does not exist.</returns>
    Task<StoredObject?> GetObjectAsync(string key, CancellationToken ct = default);

    Task DeleteObjectAsync(string key, CancellationToken ct = default);

    /// <summary>Public playback/display URL (CDN in front of the bucket).</summary>
    string GetPublicUrl(string key);
}
