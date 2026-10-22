using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Sondarr.Media.Shared.Catalogue;

/// <summary>
/// URL slugs for <c>artists.slug</c> (UNIQUE, NOT NULL). Both the Artist Portal
/// (onboarding) and the admin API (unclaimed catalogue artists) create artists, so
/// the slug shape is shared.
/// </summary>
public static partial class ArtistSlugs
{
    public const int MaxLength = 60;

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugChars();

    public static string Slugify(string value)
    {
        // Strip accents ("Beyoncé" → "beyonce"), then collapse anything else to hyphens.
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var ascii = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                ascii.Append(c);
        }

        var slug = NonSlugChars().Replace(ascii.ToString().ToLowerInvariant(), "-").Trim('-');
        if (slug.Length > MaxLength)
            slug = slug[..MaxLength].TrimEnd('-');

        return slug.Length == 0 ? "artist" : slug;
    }

    /// <summary>
    /// <c>name</c>, then <c>name-2</c> … <c>name-20</c>, then a random suffix. The
    /// unique constraint still decides a race; callers map that to a conflict.
    /// </summary>
    public static async Task<string> UniqueAsync(string stageName, Func<string, Task<bool>> exists)
    {
        var baseSlug = Slugify(stageName);
        for (var i = 1; i <= 20; i++)
        {
            var candidate = i == 1 ? baseSlug : $"{baseSlug}-{i}";
            if (!await exists(candidate))
                return candidate;
        }

        return $"{baseSlug}-{Guid.NewGuid().ToString("N")[..6]}";
    }
}
