using System.Text.RegularExpressions;

namespace Sondarr.Media.Shared.Uploads;

public enum UploadKind
{
    Audio,
    Artwork,
    Avatar,
    Banner
}

public sealed record UploadRule(UploadKind Kind, long MaxBytes, IReadOnlyDictionary<string, string> ExtensionsByContentType);

/// <summary>
/// What each upload kind accepts. MVP audio is MP3 only; lossless (WAV/FLAC)
/// arrives with the server-side compression pipeline.
/// </summary>
public static class UploadRules
{
    private const long Megabyte = 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> Mp3 = new Dictionary<string, string>
    {
        ["audio/mpeg"] = ".mp3"
    };

    private static readonly IReadOnlyDictionary<string, string> Images = new Dictionary<string, string>
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    public static UploadRule For(UploadKind kind) => kind switch
    {
        UploadKind.Audio => new(kind, 200 * Megabyte, Mp3),
        UploadKind.Artwork => new(kind, 10 * Megabyte, Images),
        UploadKind.Avatar => new(kind, 5 * Megabyte, Images),
        UploadKind.Banner => new(kind, 15 * Megabyte, Images),
        _ => throw MediaException.Validation($"Unsupported upload kind '{kind}'.")
    };

    /// <summary>Browsers disagree on a few MIME names; fold them to one.</summary>
    public static string NormalizeContentType(string? contentType)
    {
        var value = (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        return value switch
        {
            "audio/mp3" or "audio/mpeg3" or "audio/x-mpeg-3" => "audio/mpeg",
            "image/jpg" or "image/pjpeg" => "image/jpeg",
            _ => value
        };
    }
}

/// <summary>
/// S3 object keys are <c>artists/{artistId}/{kind}/{random}{ext}</c>. The artist
/// id in the key is the ownership check: a key is only accepted for the artist
/// whose id it carries, so a client can't complete or attach someone else's upload.
/// </summary>
public static partial class StorageKeys
{
    [GeneratedRegex(
        @"^artists/(?<artist>[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/(?<kind>audio|artwork|avatar|banner)/[0-9a-f]{32}\.(mp3|jpg|png|webp)\z",
        RegexOptions.CultureInvariant)]
    private static partial Regex KeyPattern();

    public static string Create(Guid artistId, UploadKind kind, string extension) =>
        $"artists/{artistId:D}/{Segment(kind)}/{Guid.NewGuid():N}{extension}";

    /// <summary>Throws unless <paramref name="key"/> is well-formed, of <paramref name="kind"/>, and owned by <paramref name="artistId"/>.</summary>
    /// <returns>The upload kind encoded in the key.</returns>
    public static UploadKind EnsureOwned(string? key, Guid artistId, UploadKind? kind = null)
    {
        var (keyArtistId, keyKind) = Parse(key);
        if (keyArtistId != artistId)
            throw MediaException.NotOwned("This upload belongs to a different artist.", new { key });

        if (kind is { } expected && keyKind != expected)
            throw MediaException.Validation($"Expected a {Segment(expected)} upload.", new { key });

        return keyKind;
    }

    /// <summary>
    /// The artist and kind a well-formed key carries. Only for callers allowed to act
    /// for any artist (the admin API); everyone else checks with <see cref="EnsureOwned"/>.
    /// </summary>
    public static (Guid ArtistId, UploadKind Kind) Parse(string? key)
    {
        var match = KeyPattern().Match(key ?? string.Empty);
        if (!match.Success || !Guid.TryParse(match.Groups["artist"].Value, out var artistId))
            throw MediaException.Validation("Invalid upload key.", new { key });

        return (artistId, Enum.Parse<UploadKind>(match.Groups["kind"].Value, ignoreCase: true));
    }

    private static string Segment(UploadKind kind) => kind.ToString().ToLowerInvariant();
}
