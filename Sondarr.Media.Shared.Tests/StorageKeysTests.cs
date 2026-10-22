using Sondarr.Media.Shared.Catalogue;
using Sondarr.Media.Shared.Uploads;

namespace Sondarr.Media.Shared.Tests;

public class StorageKeysTests
{
    private static readonly Guid ArtistId = Guid.NewGuid();

    [Theory]
    [InlineData(UploadKind.Audio, ".mp3")]
    [InlineData(UploadKind.Artwork, ".jpg")]
    [InlineData(UploadKind.Avatar, ".png")]
    [InlineData(UploadKind.Banner, ".webp")]
    public void Created_keys_are_accepted_for_their_owner_and_kind(UploadKind kind, string extension)
    {
        var key = StorageKeys.Create(ArtistId, kind, extension);

        Assert.StartsWith($"artists/{ArtistId:D}/", key);
        Assert.Equal(kind, StorageKeys.EnsureOwned(key, ArtistId, kind));
        Assert.Equal((ArtistId, kind), StorageKeys.Parse(key));
    }

    [Fact]
    public void Another_artists_key_is_forbidden()
    {
        var key = StorageKeys.Create(Guid.NewGuid(), UploadKind.Audio, ".mp3");

        var error = Assert.Throws<MediaException>(() => StorageKeys.EnsureOwned(key, ArtistId, UploadKind.Audio));
        Assert.Equal(MediaException.Forbidden, error.Code);
        Assert.Equal(403, error.StatusCode);
    }

    [Fact]
    public void Wrong_kind_is_rejected()
    {
        var key = StorageKeys.Create(ArtistId, UploadKind.Artwork, ".jpg");

        var error = Assert.Throws<MediaException>(() => StorageKeys.EnsureOwned(key, ArtistId, UploadKind.Audio));
        Assert.Equal(MediaException.ValidationError, error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("songs/abc.mp3")]
    [InlineData("artists/{0}/audio/../../other/0123456789abcdef0123456789abcdef.mp3")]
    [InlineData("artists/{0}/audio/0123456789abcdef0123456789abcdef.exe")]
    [InlineData("artists/{0}/secrets/0123456789abcdef0123456789abcdef.mp3")]
    [InlineData("/artists/{0}/audio/0123456789abcdef0123456789abcdef.mp3")]
    [InlineData("artists/{0}/audio/0123456789abcdef0123456789abcdef.mp3\nx")]
    [InlineData("artists/{0}/audio/0123456789abcdef0123456789abcdef.mp3\n")]
    public void Malformed_keys_are_rejected(string? template)
    {
        var key = template is null ? null : string.Format(template, ArtistId);

        Assert.Equal(MediaException.ValidationError, Assert.Throws<MediaException>(() => StorageKeys.EnsureOwned(key, ArtistId)).Code);
        Assert.Equal(MediaException.ValidationError, Assert.Throws<MediaException>(() => StorageKeys.Parse(key)).Code);
    }

    [Theory]
    [InlineData("audio/mp3", "audio/mpeg")]
    [InlineData("AUDIO/MPEG", "audio/mpeg")]
    [InlineData("image/jpg", "image/jpeg")]
    [InlineData("image/png; charset=binary", "image/png")]
    [InlineData(null, "")]
    public void Content_types_are_normalised(string? input, string expected) =>
        Assert.Equal(expected, UploadRules.NormalizeContentType(input));

    [Theory]
    [InlineData("Beyoncé & The Band", "beyonce-the-band")]
    [InlineData("  DJ   Shadow  ", "dj-shadow")]
    [InlineData("!!!", "artist")]
    [InlineData("日本", "artist")]
    public void Stage_names_slugify(string stageName, string expected) =>
        Assert.Equal(expected, ArtistSlugs.Slugify(stageName));

    [Fact]
    public void Slugs_are_capped_at_60_characters()
    {
        var slug = ArtistSlugs.Slugify(new string('a', 59) + " " + new string('b', 40));

        Assert.True(slug.Length <= 60);
        Assert.False(slug.EndsWith('-'));
    }

    [Fact]
    public async Task Taken_slugs_get_a_numeric_suffix()
    {
        var taken = new HashSet<string> { "dj-shadow", "dj-shadow-2" };

        Assert.Equal("dj-shadow-3", await ArtistSlugs.UniqueAsync("DJ Shadow", s => Task.FromResult(taken.Contains(s))));
    }
}
