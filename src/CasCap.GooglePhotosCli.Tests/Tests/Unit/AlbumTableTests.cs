using CasCap.Commands;

namespace CasCap.GooglePhotosCli.Tests;

/// <summary>Covers console rendering of API responses which carry optional fields.</summary>
[Trait("Category", "Rendering")]
public sealed class AlbumTableTests
{
    /// <summary>
    /// Google omits mediaItemsCount for an empty album, and 'albums add' creates exactly that.
    /// The table renderer dereferences every cell, so a null aborted the whole listing.
    /// </summary>
    /// <remarks>Regression test for <see href="https://github.com/f2calv/CasCap.GooglePhotosCli/issues/79" />.</remarks>
    [Fact]
    public void AlbumTable_RendersAlbumWithoutMediaItemCount()
    {
        var albums = new List<Album>
        {
            new() { Id = "album-1", Title = "populated", MediaItemsCount = 3 },
            new() { Id = "album-2", Title = "empty", MediaItemsCount = null }
        };

        var table = CommandBase.BuildAlbumTable(albums);

        Assert.Contains("populated", table);
        Assert.Contains("empty", table);
        Assert.Contains("album-2", table);
    }

    [Fact]
    public void AlbumTable_RendersAlbumWithoutTitle()
    {
        var albums = new List<Album> { new() { Id = "album-1", Title = null!, MediaItemsCount = null } };

        var table = CommandBase.BuildAlbumTable(albums);

        Assert.Contains("album-1", table);
    }

    [Fact]
    public void AlbumTable_RendersHeadersWhenEmpty()
    {
        var table = CommandBase.BuildAlbumTable([]);

        Assert.Contains("Title", table);
    }
}
