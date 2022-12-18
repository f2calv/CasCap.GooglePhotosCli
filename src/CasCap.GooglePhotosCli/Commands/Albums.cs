using CasCap.Common.Extensions;
using CasCap.Models;
using CasCap.Services;
using McMaster.Extensions.CommandLineUtils;
using ShellProgressBar;
using System.ComponentModel.DataAnnotations;
namespace CasCap.Commands;

[Command(Description = "Manage your media library albums.")]
[Subcommand(typeof(Add))]
[Subcommand(typeof(List))]
[Subcommand(typeof(Sync))]
[Subcommand(typeof(Download))]
internal class Albums : CommandBase
{
    public Albums(IConsole console, DiskCacheService diskCacheSvc, GooglePhotosService googlePhotosSvc) : base(console, diskCacheSvc, googlePhotosSvc) { }

    public async override Task<int> OnExecuteAsync(CommandLineApplication app)
    {
        await Task.Delay(0);
        //await base.OnExecuteAsync(app);
        //_console.Error.WriteLine("You must specify an action. See --help for more details.");
        app.ShowHelp();
        return 1;
    }

    [Command(Description = "List existing album details.")]
    class List : CommandBase
    {
        public List(IConsole console, DiskCacheService diskCacheSvc, GooglePhotosService googlePhotosSvc) : base(console, diskCacheSvc, googlePhotosSvc) { }

        [Option("--duplicates", Description = "Show only duplicate albums by title.")]
        public bool duplicatesOnly { get; }

        public async override Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            await base.OnExecuteAsync(app);
            //todo: use progress bars to display album batch retrieval better?
            //output data to CSV/HTML/? if HTML launch a browser?
            var albums = await _googlePhotosSvc.GetAlbumsAsync();
            if (albums.IsNullOrEmpty())
            {
                _console.WriteLine("Sorry, no album data available...");
                return 0;
            }
            if (duplicatesOnly)
            {
                var duplicates = GetAlbumDuplicates(albums);
                _console.WriteLine($"{albums.Count} album(s) found, {duplicates.Count} duplicate album(s) detected.");
                if (duplicates.IsNullOrEmpty())
                    return 0;
                DisplayAlbums(duplicates);
            }
            else
                DisplayAlbums(albums);

            return 0;
        }
    }

    [Command(Description = "Add new album.")]
    class Add : CommandBase
    {
        public Add(IConsole console, DiskCacheService diskCacheSvc, GooglePhotosService googlePhotosSvc) : base(console, diskCacheSvc, googlePhotosSvc) { }

        [Required]
        [Option("-t|--title", Description = "Album title")]
        public string title { get; }

        //[Option("--allowduplicate", Description = "Allow duplicate album creation by title.")]
        //public bool allowDuplicate { get; }

        public async override Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            await base.OnExecuteAsync(app);
            var album = await _googlePhotosSvc.GetOrCreateAlbumAsync(title);
            if (album is object)
                _console.WriteLine($"Created OR retrieved '{album.title}' with id '{album.id}'");
            else
                _console.WriteLine($"Sorry unable to create album '{album.title}', maybe you don't have the right permissions?");
            return 0;
        }
    }

    [Command(Description = "Refresh local album cache.")]
    class Sync : CommandBase
    {
        public Sync(IConsole console, DiskCacheService diskCacheSvc, GooglePhotosService googlePhotosSvc) : base(console, diskCacheSvc, googlePhotosSvc) { }

        public async override Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            await base.OnExecuteAsync(app);
            //todo: use progress bars to display album batch retrieval better?
            await SyncAlbums();
            //todo: improve the UI output here
            _console.WriteLine($"Sync completed.");
            return 0;
        }
    }

    [Command(Description = "Download album media items.")]
    class Download : CommandBase
    {
        public Download(IConsole console, DiskCacheService diskCacheSvc, GooglePhotosService googlePhotosSvc) : base(console, diskCacheSvc, googlePhotosSvc) { }

        [Required]
        [Option("-t|--title", Description = "Album title")]
        public string title { get; }

        [Option("-o|--output", Description = "Output path")]
        public string outputPath { get; }

        [Option("-y|--yes", Description = "Assume Yes.")]//todo: improve description
        public bool AutoConfirm { get; }

        [Option("-w|--maxwidth", Description = "Scale the image with this max width, preserving the aspect ratio.")]
        public int? maxWidth { get; }

        [Option("-h|--maxheight", Description = "Scale the image with this max height, preserving the aspect ratio.")]
        public int? maxHeight { get; }

        [Option("--crop", Description = "Crop the image to the exact values of max width and max height.")]
        public bool crop { get; }

        [Option("--exif", Description = "Download the image retaining all the EXIF metadata except the location metadata.")]
        public bool exif { get; }

        [Option("--overwrite", Description = "Re-download the media item even if it exists locally.")]
        public bool overwrite { get; }

        public async override Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            await base.OnExecuteAsync(app);

            var path = AppDomain.CurrentDomain.BaseDirectory;
            if (outputPath is object) path = outputPath;
            if (!path.EndsWith(Path.DirectorySeparatorChar)) path += Path.DirectorySeparatorChar;
            if (!Directory.Exists(path))
                if (!AutoConfirm && !Prompt.GetYesNo($"Directory '{path}' does not exist, create?", true))
                    return 0;
            Directory.CreateDirectory(path);//create the folder if doesn't exist

            var rootPath = Path.GetFullPath(path);

            var album = await _googlePhotosSvc.GetAlbumByTitleAsync(title);
            if (album is null)
            {
                _console.WriteLine($"Album with title '{title}' not found!");
                return 0;
            }
            var mediaItems = await _googlePhotosSvc.GetMediaItemsByAlbumAsync(album.id).ToListAsync();
            if (mediaItems.IsNullOrEmpty())
            {
                _console.WriteLine($"Album with title '{title}' exists, but contains no media items!");
                return 0;
            }
            //note: if the album has thousands of photos and they attempt to dl them all, after 1 hour the earlier baseUrl's will have expired...


            var allFileInfos = GetFiles(rootPath);

            var items = new List<MyMediaFileItem>(mediaItems.Count);
            foreach (var mediaItem in mediaItems)
            {
                //check if the file already exists locally
                var fileInfo = allFileInfos.FirstOrDefault(p => Path.GetFileName(p.FullName).Equals(mediaItem.filename, StringComparison.OrdinalIgnoreCase));
                var mi = new MyMediaFileItem { mediaItem = mediaItem, albums = new[] { title } };
                if (fileInfo is object)
                    mi.relPath = GetRelPath(rootPath, fileInfo);
                else
                    mi.relPath = Path.Combine(rootPath, mediaItem.filename);
                if (overwrite || fileInfo is null)
                {
                    //todo: check filename is unique - might have to create a txt file in the directory of known renames, otherwise file1.jpg, file1(1).jpg, will recurse
                    items.Add(mi);
                }
            }
            if (items.IsNullOrEmpty())
            {
                _console.WriteLine($"No new media items exist. Use argument --{nameof(overwrite)} to force a re-download/overwrite of all media items.");
                return 0;
            }
            //todo: display summary table of what will be downloaded?

            var dtStart = DateTime.UtcNow;
            var estimatedDuration = TimeSpan.FromMilliseconds(items.Count * 2_000);//set gu-estimatedDuration

            pbar = new ProgressBar(items.Count, $"Downloading {items.Count} media item(s)...", pbarOptions)
            {
                EstimatedDuration = estimatedDuration
            };

            foreach (var item in items)//in what file order do we download big albums?
            {
                if (item.mediaItem.syncDate < DateTime.UtcNow.AddHours(-1))
                    throw new Exception($"mediaitem has expired, refresh the item...");//todo: handle this better

                //todo: add child progress bar and HttpClient download progress meter https://github.com/dotnet/runtime/issues/16681
                var bytes = await _googlePhotosSvc.DownloadBytes(item.mediaItem, maxWidth, maxHeight, crop, exif);
                var fullPath = Path.Combine(rootPath, item.relPath);
                File.WriteAllBytes(fullPath, bytes);
                item.fileInfo = new FileInfo(fullPath);
                pbar.Tick();
            }
            pbar.Dispose();

            //todo: improve the UI output here
            _console.WriteLine($"Downloaded {items.Count} media item(s) to {rootPath}, {items.Sum(p => p.fileInfo.Length).GetSizeInMB():#,##0.0} MB.");
            return 0;
        }
    }
}