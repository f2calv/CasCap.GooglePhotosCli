namespace CasCap.Commands;

/// <summary>Album management commands.</summary>
/// <remarks>Only albums created by this tool are visible to the Google Photos Library API.</remarks>
[Command(Description = "Manage albums created by this tool.")]
[Subcommand(typeof(Add))]
[Subcommand(typeof(List))]
[Subcommand(typeof(Download))]
internal sealed class Albums(ILogger<Albums> logger, IConsole console, GooglePhotosService googlePhotosSvc)
    : CommandBase(logger, console, googlePhotosSvc)
{
    /// <inheritdoc/>
    public override Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken cancellationToken)
    {
        app.ShowHelp();
        return Task.FromResult(1);
    }

    /// <summary>Lists the albums created by this tool.</summary>
    [Command(Description = "List albums created by this tool.")]
    private sealed class List(ILogger<List> logger, IConsole console, GooglePhotosService googlePhotosSvc)
        : CommandBase(logger, console, googlePhotosSvc)
    {
        /// <summary>Restricts output to albums sharing a title with another album.</summary>
        [Option("--duplicates", Description = "Show only albums which share a title with another album.")]
        public bool DuplicatesOnly { get; }

        /// <inheritdoc/>
        public override async Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken cancellationToken)
        {
            var exitCode = await base.OnExecuteAsync(app, cancellationToken);
            if (exitCode != 0) return exitCode;

            var albums = await _googlePhotosSvc.GetAlbumsAsync(cancellationToken: cancellationToken);
            if (albums.Count == 0)
            {
                _console.WriteLine("No albums found. Only albums created by this tool are visible.");
                return 0;
            }

            if (!DuplicatesOnly)
            {
                DisplayAlbums(albums);
                return 0;
            }

            var duplicates = GetAlbumDuplicates(albums);
            _console.WriteLine($"{albums.Count} album(s) found, {duplicates.Count} duplicate album(s) detected.");
            if (duplicates.Count > 0)
                DisplayAlbums(duplicates);
            return 0;
        }
    }

    /// <summary>Creates a new empty album.</summary>
    [Command(Description = "Add a new album.")]
    private sealed class Add(ILogger<Add> logger, IConsole console, GooglePhotosService googlePhotosSvc)
        : CommandBase(logger, console, googlePhotosSvc)
    {
        /// <summary>Title of the album to create or retrieve.</summary>
        [Required]
        [Option("-t|--title", Description = "Album title.")]
        public string Title { get; } = default!;

        /// <inheritdoc/>
        public override async Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken cancellationToken)
        {
            var exitCode = await base.OnExecuteAsync(app, cancellationToken);
            if (exitCode != 0) return exitCode;

            var album = await _googlePhotosSvc.GetOrCreateAlbumAsync(Title, cancellationToken: cancellationToken);
            if (album is null)
            {
                _console.Error.WriteLine($"Unable to create album '{Title}'. Check that the EditAppCreatedData scope is granted.");
                return 1;
            }
            _console.WriteLine($"Created or retrieved '{album.Title}' with id '{album.Id}'.");
            return 0;
        }
    }

    /// <summary>Downloads the media items belonging to an album.</summary>
    [Command(Description = "Download the media items of an album created by this tool.")]
    private sealed class Download(ILogger<Download> logger, IConsole console, GooglePhotosService googlePhotosSvc)
        : CommandBase(logger, console, googlePhotosSvc)
    {
        /// <summary>Title of the album to download.</summary>
        [Required]
        [Option("-t|--title", Description = "Album title.")]
        public string Title { get; } = default!;

        /// <summary>Directory the media items are written to.</summary>
        [Option("-o|--output", Description = "Output path.")]
        public string? OutputPath { get; }

        /// <summary>Creates a missing output directory without prompting.</summary>
        [Option("-y|--yes", Description = "Create the output directory without prompting.")]
        public bool AutoConfirm { get; }

        /// <summary>Scales each image to this maximum width, preserving the aspect ratio.</summary>
        [Option("-w|--maxwidth", Description = "Scale the image with this max width, preserving the aspect ratio.")]
        public int? MaxWidth { get; }

        /// <summary>Scales each image to this maximum height, preserving the aspect ratio.</summary>
        [Option("-h|--maxheight", Description = "Scale the image with this max height, preserving the aspect ratio.")]
        public int? MaxHeight { get; }

        /// <summary>Crops each image to the exact maximum width and height.</summary>
        [Option("--crop", Description = "Crop the image to the exact values of max width and max height.")]
        public bool Crop { get; }

        /// <summary>Retains EXIF metadata, excluding location, in the downloaded image.</summary>
        [Option("--exif", Description = "Download the image retaining all the EXIF metadata except the location metadata.")]
        public bool Exif { get; }

        /// <summary>Re-downloads media items which already exist locally.</summary>
        [Option("--overwrite", Description = "Re-download the media item even if it exists locally.")]
        public bool Overwrite { get; }

        /// <inheritdoc/>
        public override async Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken cancellationToken)
        {
            var exitCode = await base.OnExecuteAsync(app, cancellationToken);
            if (exitCode != 0) return exitCode;

            var rootPath = Path.GetFullPath(OutputPath ?? Directory.GetCurrentDirectory());
            if (!Directory.Exists(rootPath)
                && !AutoConfirm
                && !Prompt.GetYesNo($"Directory '{rootPath}' does not exist, create?", true))
                return 0;
            Directory.CreateDirectory(rootPath);

            var album = await _googlePhotosSvc.GetAlbumByTitleAsync(Title, cancellationToken: cancellationToken);
            if (album is null)
            {
                _console.Error.WriteLine($"Album with title '{Title}' not found. Only albums created by this tool are visible.");
                return 1;
            }

            var mediaItems = await _googlePhotosSvc
                .GetMediaItemsByAlbumAsync(album.Id, cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken);
            if (mediaItems.Count == 0)
            {
                _console.WriteLine($"Album '{Title}' exists but contains no media items.");
                return 0;
            }

            var existingFiles = GetFiles(rootPath)
                .ToLookup(p => p.Name, StringComparer.OrdinalIgnoreCase);
            var items = new List<MediaFileItem>(mediaItems.Count);
            foreach (var mediaItem in mediaItems)
            {
                var existing = existingFiles[mediaItem.Filename].FirstOrDefault();
                if (!Overwrite && existing is not null) continue;
                items.Add(new MediaFileItem
                {
                    MediaItem = mediaItem,
                    Albums = [Title],
                    RelativePath = existing is null ? mediaItem.Filename : GetRelativePath(rootPath, existing)
                });
            }

            if (items.Count == 0)
            {
                _console.WriteLine($"No new media items exist. Use --{nameof(Overwrite).ToLowerInvariant()} to re-download everything.");
                return 0;
            }

            using (var pbar = new ProgressBar(items.Count, $"Downloading {items.Count} media item(s)...", PbarOptions))
            {
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    //Google expires baseUrl an hour after the media item was read.
                    if (item.MediaItem!.SyncDate < DateTime.UtcNow.AddHours(-1))
                    {
                        _console.Error.WriteLine("Media item URLs expired mid-download, re-run the command to continue.");
                        return 1;
                    }

                    var bytes = await _googlePhotosSvc.DownloadBytesAsync(
                        item.MediaItem, MaxWidth, MaxHeight, Crop, Exif, cancellationToken: cancellationToken);
                    if (bytes is null)
                    {
                        _logger.LogWarning("{ClassName} download returned no bytes for media item {MediaItemId}",
                            nameof(Download), item.MediaItem.Id);
                        continue;
                    }

                    var fullPath = Path.Combine(rootPath, item.RelativePath!);
                    await File.WriteAllBytesAsync(fullPath, bytes, cancellationToken);
                    item.FileInfo = new FileInfo(fullPath);
                    pbar.Tick();
                }
            }

            var downloaded = items.Where(p => p.FileInfo is not null).ToList();
            var megabytes = downloaded.Sum(p => p.FileInfo!.Length) / 1024d / 1024d;
            _console.WriteLine($"Downloaded {downloaded.Count} media item(s) to {rootPath}, {megabytes:#,##0.0} MB.");
            return 0;
        }
    }

    /// <summary>Returns every album whose title is shared with at least one other album.</summary>
    /// <remarks>Album titles are not unique in Google Photos, which blocks title-based album assignment.</remarks>
    internal static List<Album> GetAlbumDuplicates(IReadOnlyCollection<Album> albums)
    {
        var duplicateTitles = albums
            .GroupBy(p => p.Title, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return [.. albums.Where(p => duplicateTitles.Contains(p.Title))];
    }
}
