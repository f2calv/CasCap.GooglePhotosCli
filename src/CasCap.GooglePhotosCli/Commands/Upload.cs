namespace CasCap.Commands;

/// <summary>Uploads local media files to Google Photos.</summary>
[Command(Description = "Upload media items to your Google Photos account.")]
internal sealed class Upload(ILogger<Upload> logger, IConsole console, GooglePhotosService googlePhotosSvc)
    : CommandBase(logger, console, googlePhotosSvc)
{
    private ChildProgressBar? _childPbar;

    /// <summary>Path to a media file or to the root of a folder tree.</summary>
    [Required]
    [Option("-s|--source", Description = "Path to media item or folder root.")]
    public string Path { get; } = default!;

    /// <summary>Wildcard filter applied while enumerating the source folder.</summary>
    [Option("--pattern", Description = "Inclusive folder wildcard filter (defaults to all Google supported extensions).")]
    public string SearchPattern { get; } = "*.*";

    /// <summary>Deletes each local file after it has been uploaded successfully.</summary>
    [Option("-d|--delete", Description = "Delete the local media file after a successful upload.")]
    public bool DeleteLocal { get; }

    /// <summary>Title of the album every uploaded media item is added to.</summary>
    [Option("-t|--title", Description = "Upload into the album with this title.")]
    public string? AlbumTitle { get; }

    /// <summary>Derives album titles from the folder names below the source root.</summary>
    [Option("-h|--hierarchy", Description = "Upload into albums named after the source folder names.")]
    public bool AlbumHierarchy { get; }

    /// <summary>Uploads without asking for confirmation.</summary>
    [Option("-y|--yes", Description = "Upload without prompting for confirmation.")]
    public bool AutoConfirm { get; }

    //TODO: re-instate WEBP conversion on upload. It was removed with SixLabors.ImageSharp, whose
    //split licence is unsuitable for a freely distributed tool.
    //See https://github.com/f2calv/CasCap.GooglePhotosCli/issues

    /// <inheritdoc/>
    public override async Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken cancellationToken)
    {
        var exitCode = await base.OnExecuteAsync(app, cancellationToken);
        if (exitCode != 0) return exitCode;

        _googlePhotosSvc.UploadProgressEvent += OnUploadProgress;

        var rootPath = System.IO.Path.GetFullPath(Path);
        var items = SelectUploadableFiles(rootPath);
        if (items.Count == 0) return 0;

        var totalBytes = items.Sum(p => p.FileInfo!.Length);
        var totalMegabytes = totalBytes / 1024d / 1024d;
        if (!AutoConfirm && !Prompt.GetYesNo($"Upload {items.Count} file(s), {totalMegabytes:#,##0.0} MB?", false, ConsoleColor.Cyan))
            return 0;

        if (!await UploadMediaBytesAsync(items, cancellationToken)) return 1;

        var albumsByTitle = await ResolveAlbumsAsync(items, cancellationToken);
        if (albumsByTitle is null) return 1;

        _console.Write($"Adding {items.Count} media item(s) to your library...");
        var uploadItems = items.Select(p => (p.UploadToken!, p.FileInfo!.Name)).ToList();
        var response = await _googlePhotosSvc.AddMediaItemsAsync(uploadItems, cancellationToken: cancellationToken);
        if (response?.NewMediaItemResults is null)
        {
            _console.Error.WriteLine(" failed.");
            return 1;
        }
        _console.WriteLine(" done.");

        var failures = 0;
        foreach (var result in response.NewMediaItemResults)
        {
            var item = items.FirstOrDefault(p => p.UploadToken == result.UploadToken);
            if (item is null) continue;
            //Google reports per-item creation failures inside an otherwise successful batch response.
            if (result.Status?.Message == "Success")
                item.MediaItem = result.MediaItem;
            else
            {
                failures++;
                _logger.LogWarning("{ClassName} media item creation failed with status {StatusMessage}",
                    nameof(Upload), result.Status?.Message);
            }
        }
        if (failures > 0)
            _console.Error.WriteLine($"{failures} of {items.Count} media item(s) could not be created.");

        await AssignToAlbumsAsync(items, albumsByTitle, cancellationToken);

        if (DeleteLocal)
            DeleteUploadedFiles(items);

        _console.WriteLine("Upload completed.");
        return failures > 0 ? 1 : 0;
    }

    private List<MediaFileItem> SelectUploadableFiles(string rootPath)
    {
        _console.Write("Checking for file(s)... ");
        var allFiles = GetFiles(rootPath, SearchPattern);
        if (allFiles.Count == 0)
        {
            _console.WriteLine($"0 files found at {rootPath}");
            return [];
        }

        _console.WriteLine($"located {allFiles.Count} file(s), breakdown of file types;");
        _console.WriteLine();

        var headers = new[]
        {
            new ColumnHeader("File Extension"),
            new ColumnHeader("Count", Alignment.Right),
            new ColumnHeader("Size (MB)", Alignment.Right),
            new ColumnHeader("Status")
        };
        var table = new Table(headers) { Config = TableConfiguration.Markdown() };
        var byExtension = allFiles
            .GroupBy(p => p.Extension, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var group in byExtension)
        {
            var status = GooglePhotosService.IsFileUploadableByExtension(group.Key)
                ? string.Empty
                : "Unsupported file extension, will not be uploaded.";
            table.AddRow(group.Key, group.Count(), (group.Sum(p => p.Length) / 1024d / 1024d).ToString("0.0"), status);
        }
        _console.Write(table.ToString());
        _console.WriteLine();

        var items = allFiles
            .Where(p => GooglePhotosService.IsFileUploadable(p.FullName))
            .Select(p => new MediaFileItem { FileInfo = p, RelativePath = GetRelativePath(rootPath, p) })
            .ToList();
        if (items.Count == 0)
        {
            _console.WriteLine("0 uploadable file(s).");
            return items;
        }

        foreach (var item in items)
            item.Albums = GetAlbumTitles(item);

        _console.WriteLine($"{items.Count} file(s) to be uploaded;");
        _console.WriteLine();
        var summary = new Table("Relative Path", "Size (KB)", "Album(s)") { Config = TableConfiguration.Markdown() };
        foreach (var item in items)
            summary.AddRow(item.RelativePath, (item.FileInfo!.Length / 1024d).ToString("#,##0"), string.Join(", ", item.Albums));
        _console.Write(summary.ToString());
        _console.WriteLine();
        return items;
    }

    private string[] GetAlbumTitles(MediaFileItem item)
    {
        if (!string.IsNullOrWhiteSpace(AlbumTitle))
            return [AlbumTitle];
        if (!AlbumHierarchy)
            return [];
        var directory = System.IO.Path.GetDirectoryName(item.RelativePath);
        return string.IsNullOrEmpty(directory)
            ? []
            : directory.Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
    }

    private async Task<bool> UploadMediaBytesAsync(List<MediaFileItem> items, CancellationToken cancellationToken)
    {
        _console.WriteLine($"Now uploading {items.Count} file(s)...");
        using var pbar = new ProgressBar(items.Count, $"Uploading {items.Count} media item(s)...", PbarOptions);
        var uploaded = 0;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sizeInKb = (int)Math.Max(1, item.FileInfo!.Length / 1024);
            _childPbar = pbar.Spawn(sizeInKb, $"{item.FileInfo.Name} : 0 of {sizeInKb} Kb", ChildPbarOptions);
            try
            {
                var uploadToken = await _googlePhotosSvc.UploadMediaAsync(item.FileInfo.FullName, cancellationToken: cancellationToken);
                if (string.IsNullOrWhiteSpace(uploadToken))
                {
                    _console.Error.WriteLine($"Upload failed for '{item.RelativePath}'.");
                    return false;
                }
                item.UploadToken = uploadToken;
            }
            finally
            {
                _childPbar.Dispose();
                _childPbar = null;
            }
            uploaded++;
            pbar.Tick($"Uploaded {uploaded} of {items.Count}");
        }
        return true;
    }

    private async Task<Dictionary<string, Album>?> ResolveAlbumsAsync(List<MediaFileItem> items, CancellationToken cancellationToken)
    {
        var requiredTitles = items
            .SelectMany(p => p.Albums)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var albumsByTitle = new Dictionary<string, Album>(StringComparer.OrdinalIgnoreCase);
        if (requiredTitles.Count == 0) return albumsByTitle;

        var existingAlbums = await _googlePhotosSvc.GetAlbumsAsync(cancellationToken: cancellationToken);
        //Album titles are not unique in Google Photos, so an ambiguous title cannot be resolved safely.
        var duplicates = Albums.GetAlbumDuplicates(existingAlbums)
            .Where(p => requiredTitles.Contains(p.Title, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (duplicates.Count > 0)
        {
            _console.Error.WriteLine("Duplicate album titles present, unable to assign media item(s) to albums:");
            foreach (var album in duplicates)
                _console.Error.WriteLine($"    {album.Title}");
            _console.Error.WriteLine("Please rename or merge the above albums to continue.");
            return null;
        }

        foreach (var title in requiredTitles)
        {
            var album = existingAlbums.FirstOrDefault(p => p.Title.Equals(title, StringComparison.OrdinalIgnoreCase))
                ?? await _googlePhotosSvc.CreateAlbumAsync(title, cancellationToken);
            if (album is null)
            {
                _console.Error.WriteLine($"Unable to create album '{title}'.");
                return null;
            }
            albumsByTitle[title] = album;
        }
        return albumsByTitle;
    }

    private async Task AssignToAlbumsAsync(
        List<MediaFileItem> items,
        Dictionary<string, Album> albumsByTitle,
        CancellationToken cancellationToken)
    {
        if (albumsByTitle.Count == 0) return;

        _console.WriteLine("Adding media item(s) to albums...");
        var table = new Table("Album Name", "Status") { Config = TableConfiguration.Markdown() };
        foreach (var (title, album) in albumsByTitle)
        {
            var ids = items
                .Where(p => p.MediaItem is not null && p.Albums.Contains(title, StringComparer.OrdinalIgnoreCase))
                .Select(p => p.MediaItem!.Id)
                .ToList();
            if (ids.Count == 0) continue;
            var added = await _googlePhotosSvc.AddMediaItemsToAlbumAsync(album.Id, ids, cancellationToken);
            table.AddRow(album.Title, added ? $"{ids.Count} media item(s) added" : "failed");
        }
        _console.Write(table.ToString());
        _console.WriteLine();
    }

    private void DeleteUploadedFiles(List<MediaFileItem> items)
    {
        foreach (var item in items.Where(p => p.MediaItem is not null))
        {
            try
            {
                File.Delete(item.FileInfo!.FullName);
                _console.WriteLine($"Deleted '{item.RelativePath}'.");
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "{ClassName} could not delete an uploaded file", nameof(Upload));
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "{ClassName} was not permitted to delete an uploaded file", nameof(Upload));
            }
        }
    }

    private void OnUploadProgress(object? sender, UploadProgressEventArgs e)
    {
        if (_childPbar is null) return;
        var uploadedKb = (int)(e.UploadedBytes / 1024);
        _childPbar.Tick(uploadedKb, $"{e.FileName} : {uploadedKb} of {e.TotalBytes / 1024} Kb");
    }
}
