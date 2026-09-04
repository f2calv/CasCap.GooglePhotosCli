namespace CasCap.Commands;

/// <summary>Media item commands.</summary>
/// <remarks>Only media items created by this tool are visible to the Google Photos Library API.</remarks>
[Command("mediaitems", Description = "Manage media items created by this tool.")]
[Subcommand(typeof(Upload))]
[Subcommand(typeof(List))]
internal sealed class MediaItems(ILogger<MediaItems> logger, IConsole console, Lazy<GooglePhotosService> googlePhotosSvc)
    : CommandBase(logger, console, googlePhotosSvc)
{
    /// <inheritdoc/>
    public override Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken cancellationToken)
    {
        app.ShowHelp();
        return Task.FromResult(1);
    }

    /// <summary>Lists the media items created by this tool.</summary>
    [Command(Description = "List media items created by this tool.")]
    private sealed class List(ILogger<List> logger, IConsole console, Lazy<GooglePhotosService> googlePhotosSvc)
        : CommandBase(logger, console, googlePhotosSvc)
    {
        /// <inheritdoc/>
        public override async Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken cancellationToken)
        {
            var exitCode = await base.OnExecuteAsync(app, cancellationToken);
            if (exitCode != 0) return exitCode;

            var mediaItems = await _googlePhotosSvc
                .GetMediaItemsAsync(cancellationToken: cancellationToken)
                .ToListAsync(cancellationToken);
            if (mediaItems.Count == 0)
            {
                _console.WriteLine("No media items found. Only media items created by this tool are visible.");
                return 0;
            }

            var table = new Table("#", "File Name", "Mime Type", "Id") { Config = TableConfiguration.Markdown() };
            for (var i = 0; i < mediaItems.Count; i++)
                table.AddRow(i + 1, mediaItems[i].Filename, mediaItems[i].MimeType, mediaItems[i].Id);
            _console.Write(table.ToString());
            return 0;
        }
    }
}
