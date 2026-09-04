namespace CasCap.Commands;

/// <summary>Shared behaviour for every command, covering login, paging feedback and file helpers.</summary>
[HelpOption("--help")]
internal abstract class CommandBase(ILogger logger, IConsole console, Lazy<GooglePhotosService> googlePhotosSvc)
{
    /// <summary>Logger available to inheriting commands.</summary>
    protected readonly ILogger _logger = logger;

    /// <summary>Console used for all user-facing presentation.</summary>
    protected readonly IConsole _console = console;

    /// <summary>Google Photos Library API client, activated on first use.</summary>
    protected GooglePhotosService _googlePhotosSvc => googlePhotosSvc.Value;

    /// <summary>Progress bar styling shared by every long-running command.</summary>
    protected static ProgressBarOptions PbarOptions { get; } = new()
    {
        ProgressCharacter = '─',
        ForegroundColor = ConsoleColor.Yellow,
        ForegroundColorDone = ConsoleColor.DarkGreen,
        BackgroundColor = ConsoleColor.DarkGray,
        BackgroundCharacter = '\u2593',
        ProgressBarOnBottom = true,
        ShowEstimatedDuration = true,
    };

    /// <summary>Child progress bar styling shared by every long-running command.</summary>
    protected static ProgressBarOptions ChildPbarOptions { get; } = new()
    {
        ProgressCharacter = '─',
        ForegroundColor = ConsoleColor.Yellow,
        ForegroundColorDone = ConsoleColor.DarkGreen,
        BackgroundColor = ConsoleColor.DarkGray,
        BackgroundCharacter = '\u2593',
        DisplayTimeInRealTime = true,
        CollapseWhenFinished = true,
    };

    /// <summary>Authenticates against Google Photos, reporting progress to the console.</summary>
    /// <returns><see langword="true"/> when the user is authenticated.</returns>
    public virtual async Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken cancellationToken)
    {
        _googlePhotosSvc.PagingEvent += OnPagingEvent;
        if (!await _googlePhotosSvc.LoginAsync(cancellationToken))
        {
            _console.Error.WriteLine("Login failed. Check the configured OAuth client and requested scopes.");
            return 1;
        }
        return 0;
    }

    /// <summary>Reports API paging progress while a long listing runs.</summary>
    protected virtual void OnPagingEvent(object? sender, PagingEventArgs e)
    {
        var message = $"Page {e.PageNumber}\t{e.RecordCount}\t+{e.PageSize}";
        if (e.MinDate.HasValue && e.MaxDate.HasValue)
            message += $"\t{e.MinDate.Value:yyyy-MM-dd HH:mm} to {e.MaxDate.Value:yyyy-MM-dd HH:mm}";
        _console.WriteLine(message);
    }

    /// <summary>Renders a Markdown table of albums to the console.</summary>
    protected void DisplayAlbums(IReadOnlyList<Album> albums)
    {
        var headers = new[]
        {
            new ColumnHeader("#"),
            new ColumnHeader("Title"),
            new ColumnHeader("Items", Alignment.Right),
            new ColumnHeader("Id")
        };
        var table = new Table(headers) { Config = TableConfiguration.Markdown() };
        for (var i = 0; i < albums.Count; i++)
            table.AddRow(i + 1, albums[i].Title, albums[i].MediaItemsCount, albums[i].Id);
        _console.Write(table.ToString());
    }

    /// <summary>Returns the path of <paramref name="fileInfo"/> relative to <paramref name="rootPath"/>.</summary>
    protected static string GetRelativePath(string rootPath, FileInfo fileInfo)
        => Path.GetRelativePath(rootPath, fileInfo.FullName);

    /// <summary>Recursively enumerates files below <paramref name="path"/> matching <paramref name="searchPattern"/>.</summary>
    /// <remarks>Inaccessible sub-directories are skipped rather than aborting the enumeration.</remarks>
    protected static List<FileInfo> GetFiles(string path, string searchPattern = "*")
    {
        if (!Directory.Exists(path))
            return [];

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };
        return [.. new DirectoryInfo(path).EnumerateFiles(searchPattern, enumerationOptions)];
    }
}
