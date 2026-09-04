namespace CasCap.Models;

/// <summary>Pairs a local file with the Google Photos media item it was uploaded from or downloaded to.</summary>
internal sealed class MediaFileItem
{
    /// <summary>Gets or sets the local file, once it exists on disk.</summary>
    public FileInfo? FileInfo { get; set; }

    /// <summary>Gets or sets the path relative to the command's root folder.</summary>
    public string? RelativePath { get; set; }

    /// <summary>Gets or sets the album titles this file belongs to.</summary>
    public string[] Albums { get; set; } = [];

    /// <summary>Gets or sets the upload token returned after the media bytes were uploaded.</summary>
    public string? UploadToken { get; set; }

    /// <summary>Gets or sets the media item, once it has been created or retrieved.</summary>
    public MediaItem? MediaItem { get; set; }
}
