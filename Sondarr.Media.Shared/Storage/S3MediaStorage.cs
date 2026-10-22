using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Sondarr.Media.Shared.Storage;

public sealed class S3MediaStorage(IOptions<S3Settings> options, ILogger<S3MediaStorage> logger) : IMediaStorage, IDisposable
{
    private readonly S3Settings _settings = options.Value;
    private readonly Lazy<IAmazonS3> _client = new(() => CreateClient(options.Value));

    public async Task<string> InitiateMultipartUploadAsync(string key, string contentType, CancellationToken ct = default)
    {
        var response = await Call(s3 => s3.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            ContentType = contentType
        }, ct));

        return response.UploadId;
    }

    public async Task<IReadOnlyList<PresignedPartUrl>> GetPartUploadUrlsAsync(
        string key, string uploadId, IReadOnlyCollection<int> partNumbers, TimeSpan lifetime, CancellationToken ct = default)
    {
        var s3 = Client();
        var expires = DateTime.UtcNow.Add(lifetime);
        var protocol = _settings.ServiceUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? Protocol.HTTP : Protocol.HTTPS;

        var urls = new List<PresignedPartUrl>(partNumbers.Count);
        foreach (var partNumber in partNumbers)
        {
            var url = await s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                Verb = HttpVerb.PUT,
                UploadId = uploadId,
                PartNumber = partNumber,
                Expires = expires,
                Protocol = protocol
            });
            urls.Add(new PresignedPartUrl(partNumber, url));
        }

        return urls;
    }

    public async Task<IReadOnlyList<UploadedPart>> ListUploadedPartsAsync(string key, string uploadId, CancellationToken ct = default)
    {
        // Size limits keep every upload well under the 1000-parts-per-page default.
        var response = await Call(s3 => s3.ListPartsAsync(new ListPartsRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            UploadId = uploadId
        }, ct));

        return (response.Parts ?? [])
            .Select(p => new UploadedPart(p.PartNumber ?? 0, p.ETag, p.Size ?? 0))
            .ToList();
    }

    public async Task CompleteMultipartUploadAsync(string key, string uploadId, IReadOnlyCollection<CompletedPart> parts, CancellationToken ct = default)
    {
        await Call(s3 => s3.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            UploadId = uploadId,
            PartETags = parts.Select(p => new PartETag { PartNumber = p.PartNumber, ETag = p.ETag }).ToList()
        }, ct));
    }

    public async Task AbortMultipartUploadAsync(string key, string uploadId, CancellationToken ct = default)
    {
        await Call(s3 => s3.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
        {
            BucketName = _settings.BucketName,
            Key = key,
            UploadId = uploadId
        }, ct));
    }

    public async Task<StoredObject?> GetObjectAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var metadata = await Client().GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _settings.BucketName,
                Key = key
            }, ct);

            return new StoredObject(key, metadata.ContentLength, metadata.Headers.ContentType ?? string.Empty);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteObjectAsync(string key, CancellationToken ct = default)
    {
        await Call(s3 => s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _settings.BucketName,
            Key = key
        }, ct));
    }

    public string GetPublicUrl(string key)
    {
        EnsureConfigured();
        return $"{_settings.PublicBaseUrl.TrimEnd('/')}/{key}";
    }

    public void Dispose()
    {
        if (_client.IsValueCreated)
            _client.Value.Dispose();
    }

    private IAmazonS3 Client()
    {
        EnsureConfigured();
        return _client.Value;
    }

    private void EnsureConfigured()
    {
        if (!_settings.IsConfigured)
            throw MediaException.Unconfigured("Uploads are not available: S3 storage is not configured (S3:BucketName, S3:PublicBaseUrl).");
    }

    // Translates the S3 errors a client can cause into 4xx responses.
    private async Task<T> Call<T>(Func<IAmazonS3, Task<T>> operation)
    {
        try
        {
            return await operation(Client());
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchUpload")
        {
            throw MediaException.Missing("Upload not found. It may have expired or already been completed.");
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "InvalidPart" or "InvalidPartOrder" or "EntityTooSmall")
        {
            throw MediaException.Validation("Upload parts are missing or invalid. Retry the upload.", new { s3Error = ex.ErrorCode });
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex, "S3 request failed ({ErrorCode})", ex.ErrorCode);
            throw MediaException.Upstream("Storage request failed.", ex);
        }
    }

    private static IAmazonS3 CreateClient(S3Settings settings)
    {
        var config = new AmazonS3Config { ForcePathStyle = settings.ForcePathStyle };
        if (!string.IsNullOrWhiteSpace(settings.ServiceUrl))
        {
            config.ServiceURL = settings.ServiceUrl;
            config.AuthenticationRegion = settings.Region;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region);
        }

        return string.IsNullOrWhiteSpace(settings.AccessKeyId)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey), config);
    }
}
