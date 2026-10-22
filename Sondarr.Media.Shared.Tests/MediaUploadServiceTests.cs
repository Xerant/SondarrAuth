using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sondarr.Media.Shared.Catalogue;
using Sondarr.Media.Shared.Storage;
using Sondarr.Media.Shared.Uploads;

namespace Sondarr.Media.Shared.Tests;

public class MediaUploadServiceTests
{
    private readonly FakeStorage _storage = new();
    private readonly Guid _artistId = Guid.NewGuid();

    private MediaUploadService Service() =>
        new(_storage, Options.Create(new UploadOptions { PartSizeBytes = 8 * 1024 * 1024 }), NullLogger<MediaUploadService>.Instance);

    [Fact]
    public async Task Initiate_returns_an_owned_key_and_part_count()
    {
        var response = await Service().InitiateAsync(_artistId, new InitiateUploadRequest
        {
            Kind = UploadKind.Audio, ContentType = "audio/mp3", SizeBytes = 20 * 1024 * 1024
        });

        Assert.Equal(UploadKind.Audio, StorageKeys.EnsureOwned(response.Key, _artistId));
        Assert.EndsWith(".mp3", response.Key);
        Assert.Equal(3, response.PartCount);
    }

    [Theory]
    [InlineData(UploadKind.Audio, "audio/wav", 1000)]
    [InlineData(UploadKind.Audio, "audio/mpeg", 201L * 1024 * 1024)]
    [InlineData(UploadKind.Avatar, "image/gif", 1000)]
    [InlineData(UploadKind.Avatar, "image/png", 6L * 1024 * 1024)]
    public async Task Initiate_enforces_type_and_size(UploadKind kind, string contentType, long size)
    {
        var error = await Assert.ThrowsAsync<MediaException>(() => Service().InitiateAsync(_artistId, new InitiateUploadRequest
        {
            Kind = kind, ContentType = contentType, SizeBytes = size
        }));
        Assert.Equal(MediaException.ValidationError, error.Code);
    }

    [Fact]
    public async Task Part_numbers_beyond_the_size_limit_are_refused()
    {
        var key = StorageKeys.Create(_artistId, UploadKind.Audio, ".mp3");

        // 200 MB / 8 MB parts = 25 parts max for audio.
        await Assert.ThrowsAsync<MediaException>(() =>
            Service().GetPartUrlsAsync(_artistId, new PartUrlsRequest { Key = key, UploadId = "u", PartNumbers = [26] }));
        Assert.Empty(_storage.SignedParts);

        var ok = await Service().GetPartUrlsAsync(_artistId, new PartUrlsRequest { Key = key, UploadId = "u", PartNumbers = [25, 1, 1] });
        Assert.Equal([1, 25], ok.Parts.Select(p => p.PartNumber));
    }

    [Fact]
    public async Task Cannot_sign_parts_for_another_artists_upload()
    {
        var foreignKey = StorageKeys.Create(Guid.NewGuid(), UploadKind.Audio, ".mp3");

        var error = await Assert.ThrowsAsync<MediaException>(() =>
            Service().GetPartUrlsAsync(_artistId, new PartUrlsRequest { Key = foreignKey, UploadId = "u", PartNumbers = [1] }));
        Assert.Equal(MediaException.Forbidden, error.Code);
    }

    [Fact]
    public async Task Cannot_complete_another_artists_upload()
    {
        var foreignKey = StorageKeys.Create(Guid.NewGuid(), UploadKind.Audio, ".mp3");
        _storage.Put(foreignKey, 1000, "audio/mpeg");

        var error = await Assert.ThrowsAsync<MediaException>(() => Service().CompleteAsync(_artistId, new CompleteUploadRequest
        {
            Key = foreignKey, UploadId = "u", Parts = [new CompletedPartDto { PartNumber = 1, ETag = "e" }]
        }));
        Assert.Equal(MediaException.Forbidden, error.Code);
        Assert.Empty(_storage.Completed);
    }

    [Fact]
    public async Task Oversized_completed_upload_is_deleted()
    {
        var key = StorageKeys.Create(_artistId, UploadKind.Avatar, ".png");
        _storage.Put(key, 50L * 1024 * 1024, "image/png");

        await Assert.ThrowsAsync<MediaException>(() => Service().CompleteAsync(_artistId, new CompleteUploadRequest
        {
            Key = key, UploadId = "u", Parts = [new CompletedPartDto { PartNumber = 1, ETag = "e" }]
        }));

        Assert.Contains(key, _storage.Deleted);
    }

    [Fact]
    public async Task Required_object_must_exist_and_match_the_kind()
    {
        var key = StorageKeys.Create(_artistId, UploadKind.Audio, ".mp3");

        await Assert.ThrowsAsync<MediaException>(() => Service().RequireUploadedObjectAsync(_artistId, key, UploadKind.Audio));

        _storage.Put(key, 1000, "audio/mpeg");
        var stored = await Service().RequireUploadedObjectAsync(_artistId, key, UploadKind.Audio);
        Assert.Equal(1000, stored.SizeBytes);

        await Assert.ThrowsAsync<MediaException>(() => Service().RequireUploadedObjectAsync(_artistId, key, UploadKind.Artwork));
    }

    [Fact]
    public void Song_fields_are_trimmed_and_validated()
    {
        var fields = SongRules.Normalize(" Title ", "  ", " Indie ", 200);
        Assert.Equal(new SongFields("Title", null, "Indie", 200), fields);

        var error = Assert.Throws<MediaException>(() => SongRules.Normalize(" ", null, "", 0));
        var details = Assert.IsType<Dictionary<string, string[]>>(error.Details);
        Assert.Equal(["durationSeconds", "genre", "title"], details.Keys.Order());
    }
}

internal sealed class FakeStorage : IMediaStorage
{
    public Dictionary<string, StoredObject> Objects { get; } = [];
    public List<string> Deleted { get; } = [];
    public List<string> Completed { get; } = [];
    public List<IReadOnlyCollection<int>> SignedParts { get; } = [];

    public void Put(string key, long size, string contentType) => Objects[key] = new StoredObject(key, size, contentType);

    public Task<string> InitiateMultipartUploadAsync(string key, string contentType, CancellationToken ct = default) =>
        Task.FromResult("upload-1");

    public Task<IReadOnlyList<PresignedPartUrl>> GetPartUploadUrlsAsync(string key, string uploadId, IReadOnlyCollection<int> partNumbers, TimeSpan lifetime, CancellationToken ct = default)
    {
        SignedParts.Add(partNumbers);
        return Task.FromResult<IReadOnlyList<PresignedPartUrl>>(partNumbers.Select(n => new PresignedPartUrl(n, $"https://s3/{key}?part={n}")).ToList());
    }

    public Task<IReadOnlyList<UploadedPart>> ListUploadedPartsAsync(string key, string uploadId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<UploadedPart>>([]);

    public Task CompleteMultipartUploadAsync(string key, string uploadId, IReadOnlyCollection<CompletedPart> parts, CancellationToken ct = default)
    {
        Completed.Add(key);
        return Task.CompletedTask;
    }

    public Task AbortMultipartUploadAsync(string key, string uploadId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<StoredObject?> GetObjectAsync(string key, CancellationToken ct = default) =>
        Task.FromResult(Objects.GetValueOrDefault(key));

    public Task DeleteObjectAsync(string key, CancellationToken ct = default)
    {
        Deleted.Add(key);
        Objects.Remove(key);
        return Task.CompletedTask;
    }

    public string GetPublicUrl(string key) => $"https://cdn.test/{key}";
}
