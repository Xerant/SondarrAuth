namespace Sondarr.Media.Shared.Catalogue;

/// <summary>Song fields after <see cref="SongRules.Normalize"/>: trimmed, blanks as null.</summary>
public sealed record SongFields(string Title, string? Album, string Genre, int DurationSeconds);

/// <summary>
/// What a new <c>songs</c> row needs, whoever creates it (the artist, or an admin
/// on their behalf). Request DTOs repeat these limits as attributes for early
/// 400s; this is the check that counts.
/// </summary>
public static class SongRules
{
    public const int MaxTitleLength = 200;
    public const int MaxAlbumLength = 200;
    public const int MaxGenreLength = 50;
    public const int MaxDurationSeconds = 4 * 60 * 60;

    /// <exception cref="MediaException">A field is missing or out of range.</exception>
    public static SongFields Normalize(string? title, string? album, string? genre, int durationSeconds)
    {
        var errors = new Dictionary<string, string[]>();

        var t = title?.Trim() ?? string.Empty;
        if (t.Length is 0 or > MaxTitleLength)
            errors["title"] = [$"Title is required (at most {MaxTitleLength} characters)."];

        var a = string.IsNullOrWhiteSpace(album) ? null : album.Trim();
        if (a is { Length: > MaxAlbumLength })
            errors["album"] = [$"Album is at most {MaxAlbumLength} characters."];

        var g = genre?.Trim() ?? string.Empty;
        if (g.Length is 0 or > MaxGenreLength)
            errors["genre"] = [$"Genre is required (at most {MaxGenreLength} characters)."];

        if (durationSeconds is < 1 or > MaxDurationSeconds)
            errors["durationSeconds"] = [$"Duration must be between 1 second and {MaxDurationSeconds / 3600} hours."];

        if (errors.Count > 0)
            throw MediaException.Validation("One or more validation errors occurred.", errors);

        return new SongFields(t, a, g, durationSeconds);
    }
}
