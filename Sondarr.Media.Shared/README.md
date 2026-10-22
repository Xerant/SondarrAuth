# Sondarr.Media.Shared

Media upload rules shared by every Sondarr service that writes artist media: today the
Artist Portal API and the Admin API. These are security rules (key ownership, size
limits, content types), so they live in one package rather than being copied.

## What's in it

| Type | Purpose |
|---|---|
| `IMediaUploadService` / `MediaUploadService` | Browser → S3 multipart flow for one artist: initiate, part URLs, list parts (resume), complete (re-checks real size), abort, and `RequireUploadedObjectAsync` before attaching a key to a record. |
| `IMediaStorage` / `S3MediaStorage` | S3 (or S3-compatible) presigned multipart, metadata, delete, public CDN URL. |
| `UploadRules`, `UploadKind` | Accepted content types and size limits per kind (MP3 ≤ 200 MB, artwork ≤ 10 MB, …). |
| `StorageKeys` | `artists/{artistId}/{kind}/{random}.{ext}`. `EnsureOwned(key, artistId)` is the ownership check. `Parse(key)` is for callers allowed to act for any artist (admin only). |
| `SongRules` | Field rules for a new `songs` row (title, album, genre, duration). |
| `ArtistSlugs` | `artists.slug` generation. |
| `MediaException` | Expected failures, carrying the platform error code (`VALIDATION_ERROR`, `FORBIDDEN`, `NOT_FOUND`, `UPSTREAM_ERROR`, `NOT_CONFIGURED`) and HTTP status. |

## Usage

```csharp
builder.Services.AddSondarrMedia(builder.Configuration);   // binds "S3" and "Uploads"
```

Map `MediaException` in the host's exception handler:

```csharp
MediaException e => (e.StatusCode, ErrorResponseDto.Create(e.Code, e.Message, e.Details)),
```

The caller decides which artist is acting. The Artist Portal passes the signed-in
user's artist; the Admin API passes the artist named in the request (and audits it).
**Never** pass a client-supplied artist id outside the Admin API.

## Config

```json
"S3": { "BucketName": "", "Region": "ap-southeast-2", "PublicBaseUrl": "https://<cloudfront>", "AccessKeyId": "", "SecretAccessKey": "" },
"Uploads": { "PartSizeBytes": 8388608, "PartUrlExpiryMinutes": 60, "MaxPartUrlsPerRequest": 50 }
```

Leave the access keys empty to use the default AWS credential chain. The bucket's CORS
must allow `PUT` from each site's origin and expose `ETag`.

## Publishing

Tag `media-v1.0.0` (or run the "Publish Sondarr.Media.Shared" workflow manually) to push
to GitHub Packages.
