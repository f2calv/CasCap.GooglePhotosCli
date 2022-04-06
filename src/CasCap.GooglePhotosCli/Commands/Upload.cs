using BetterConsoleTables;
using CasCap.Common.Extensions;
using CasCap.Models;
using CasCap.Services;
using McMaster.Extensions.CommandLineUtils;
using MimeTypes;
using ShellProgressBar;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
namespace CasCap.Commands;

[Command(Description = "Upload media items to Google Photos account.")]
internal class Upload : CommandBase
{
    public Upload(IConsole console, DiskCacheService diskCacheSvc, GooglePhotosService googlePhotosSvc) : base(console, diskCacheSvc, googlePhotosSvc)
    {
        _googlePhotosSvc.UploadProgressEvent += _googlePhotosSvc_UploadProgressEvent;
    }

    [Required]
    [Option("-s|--source", Description = "Path to media item or folder root.")]
    public string path { get; }

    [Option("--pattern", Description = "Inclusive folder wildcard filter (defaults to all google supported extensions).")]
    public string searchPattern { get; } = "*.*";//autodetect

    [Option("--webp", Description = "Convert and upload media items in WEBP format.")]
    public bool webp { get; }//how to set webp value?

    [Option("-d|--delete", Description = "Delete local media album after successful upload.")]
    public bool deleteLocal { get; }

    [Option("-t|--title", Description = "Upload to album with this title.")]
    public string albumTitle { get; }

    [Option("-h|--hierarchy", Description = "Upload to albums based on folder names.")]
    public bool albumHierarchy { get; }

    [Option("-y|--yes", Description = "Assume Yes.")]
    public bool AutoConfirm { get; }

    //create album if not found?

    void _googlePhotosSvc_UploadProgressEvent(object sender, UploadProgressArgs e)
    {
        var str = $"{e.fileName} : {(int)e.uploadedBytes.GetSizeInKB()} of {(int)e.totalBytes.GetSizeInKB()} Kb";
        Debug.WriteLine(str);
        childPBar.Tick((int)e.uploadedBytes, str);
    }

    public async override Task<int> OnExecuteAsync(CommandLineApplication app)
    {
        await base.OnExecuteAsync(app);

        var rootPath = Path.GetFullPath(path);

        _console.Write($"Checking for file(s)... ");
        var allFileInfos = GetFiles(path, searchPattern);
        var items = new List<MyMediaFileItem>(allFileInfos.Count);

        if (allFileInfos.IsNullOrEmpty())
            _console.WriteLine($" 0 files found at {rootPath}");
        else
        {
            var checkForUploadableFileTypes = allFileInfos.GroupBy(p => Path.GetExtension(p.Name), StringComparer.InvariantCultureIgnoreCase)
                .Select(g => new
                {
                    Extension = g.Key,
                    MimeType = MimeTypeMap.GetMimeType(g.Key),
                    Count = g.Count(),
                    TotalBytes = g.Sum(p => p.Length)
                })
                .ToList();
            _console.WriteLine($"located {allFileInfos.Count} file(s), breakdown of file types;");
            _console.WriteLine();
            //todo: do we also analyse the files with ImageSharp/Exif?

            var headers = new[] { new ColumnHeader("File Extension"), new ColumnHeader("Mime Type"), new ColumnHeader("Count", Alignment.Right), new ColumnHeader("Size (MB)", Alignment.Right), new ColumnHeader("Status") };
            var table = new Table(headers) { Config = TableConfiguration.Markdown() };
            foreach (var f in checkForUploadableFileTypes.OrderBy(p => p.Extension))
            {
                var status = string.Empty;
                if (!GooglePhotosService.IsFileUploadableByExtension(f.Extension))
                    status = "Unsupported file extension, will not be uploaded.";
                table.AddRow(f.Extension, f.MimeType, f.Count, f.TotalBytes.GetSizeInMB().ToString("0.0"), status);
            }
            //below summary row breaks the progress bar somehow
            //table.AddRow(string.Empty, string.Empty, allFileInfos.Count, allFileInfos.Sum(p => p.Length.GetSizeInMB()).ToString("0.0"), string.Empty);
            Console.Write(table.ToString());
            _console.WriteLine();

            //add all uploadable files into a new collection
            foreach (var fileInfo in allFileInfos)
                if (GooglePhotosService.IsFileUploadable(fileInfo.FullName))
                    items.Add(new MyMediaFileItem { fileInfo = fileInfo });
        }
        if (items.IsNullOrEmpty())
        {
            _console.WriteLine($"{items.Count} uploadable file(s)");
            return 0;
        }

        {
            //extract album information from the folder structure (if requested)
            _console.WriteLine($"{items.Count} file(s) to be uploaded;");
            _console.WriteLine();

            var headers = new[] { new ColumnHeader("Relative Path"), new ColumnHeader("Size (KB)", Alignment.Right), new ColumnHeader("Album(s)") };
            var table = new Table(headers) { Config = TableConfiguration.Markdown() };
            foreach (var item in items)
            {
                item.relPath = GetRelPath(rootPath, item.fileInfo);
                item.albums = GetAlbums(item);
                table.AddRow(item.relPath, item.fileInfo.Length.GetSizeInKB().ToString("#,###,###"), string.Join(", ", item.albums));
            }
            Console.Write(table.ToString());
            _console.WriteLine();
        }


        string[] GetAlbums(MyMediaFileItem item)
        {
            var albums = item.relPath.Substring(0, item.relPath.LastIndexOf(item.fileInfo.Name));
            if (albums.StartsWith(Path.DirectorySeparatorChar)) albums = albums.Substring(1);
            if (albums.EndsWith(Path.DirectorySeparatorChar)) albums = albums.Substring(0, albums.Length - 1);
            var myAlbums = albums.Split(Path.DirectorySeparatorChar);
            return myAlbums;
        }


        //note: if we are uploading a crazy amount of data ProgressBar only supports int for ticks, so may break :/
        var totalBytes = items.Sum(p => p.fileInfo.Length);
        if (totalBytes > int.MaxValue)
            throw new Exception($"Unable to upload more than {((long)int.MaxValue).GetSizeInMB()} in one session!");

        var totalKBytes = totalBytes.GetSizeInKB();

        if (!AutoConfirm && !Prompt.GetYesNo($"Hit (Y)es to upload {items.Count} files, {totalBytes.GetSizeInMB():###,###} MB...", false, ConsoleColor.Cyan))
            return 0;
        else
            _console.WriteLine($"Now uploading {items.Count} files, {totalBytes.GetSizeInMB():###,###} MB...");

        var dtStart = DateTime.UtcNow;
        var estimatedDuration = TimeSpan.FromMilliseconds(items.Count * 2_000);//set gu-estimatedDuration

        pbar = new ProgressBar((int)totalBytes, $"Uploading {items.Count} media item(s)...", pbarOptions)
        {
            EstimatedDuration = estimatedDuration
        };

        //do we upload an assign to library(and albums) as we progress?
        //or
        //do we upload all and get the uploadTokens, then assign to the library(and albums) in a second step?
        //...which gives the user to bomb out if a file isn't successfully uploaded?
        var uploadedFileCount = 0;
        var uploadedTotalBytes = 0;
        foreach (var item in items)
        {
            var str = $"{item.fileInfo.Name} : 0 of {(int)item.fileInfo.Length.GetSizeInKB()} Kb";
            childPBar = pbar.Spawn((int)item.fileInfo.Length, str, childPbarOptions);

            //todo: pass Action or Func for a callback instead of raising an event?
            var uploadToken = await _googlePhotosSvc.UploadMediaAsync(item.fileInfo.FullName/*, callback: child.Tick()*/);
            if (!string.IsNullOrWhiteSpace(uploadToken))
                item.uploadToken = uploadToken;
            else
            {
                Debugger.Break();
                //todo: how to handle upload failure here?
            }

            childPBar.Dispose();

            uploadedFileCount++;
            uploadedTotalBytes += (int)item.fileInfo.Length;
            pbar.Tick(uploadedTotalBytes, $"Uploaded {uploadedFileCount} of {items.Count}");
            //if (Interlocked.Read(ref iteration) % 25 == 0)
            {
                var tsTaken = DateTime.UtcNow.Subtract(dtStart).TotalMilliseconds;
                var timePerCombination = tsTaken / uploadedFileCount;
                pbar.EstimatedDuration = TimeSpan.FromMilliseconds((items.Count - uploadedFileCount) * timePerCombination);
            }
        }

        pbar.Dispose();

        //album duplicate checking needs to happen first
        var requiredAlbumTitles = items.SelectMany(p => p.albums).Distinct(StringComparer.OrdinalIgnoreCase).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        //allAlbums = await _diskCacheSvc.GetAsync($"albums.json", () => _googlePhotosSvc.GetAlbumsAsync());
        allAlbums = await _googlePhotosSvc.GetAlbumsAsync();
        if (!requiredAlbumTitles.IsNullOrEmpty())
            DoDuplicateAlbumsExist();
        var dAlbums = await GetOrCreateAlbums();
        if (dAlbums is null) return 1;


        _console.Write($"Adding {items.Count} media item(s) to your library...");
        var uploadItems = items.Select(p => (p.uploadToken, p.fileInfo.Name)).ToList();
        var res = await _googlePhotosSvc.AddMediaItemsAsync(uploadItems);
        if (res is object)
        {
            _console.WriteLine($" done! :)");
            //iterate over results and assign the MediaItem object to our collection
            foreach (var newMediaItem in res.newMediaItemResults)
            {
                var item = items.FirstOrDefault(p => p.uploadToken == newMediaItem.uploadToken);
                if (item is null)
                    throw new Exception("could this happen?");
                if (newMediaItem.status is object && newMediaItem.status.message == "Success")
                    item.mediaItem = newMediaItem.mediaItem;
                else
                {
                    Debugger.Break();
                    //todo: handle error?
                }
            }

            //todo: delete local files (if required)
            //todo: delete empty folders?
            if (deleteLocal)
                foreach (var item in items.Where(p => p.mediaItem is object))
                {
                    _console.Write($"Deleting '{item.fileInfo.FullName}'...");
                    File.Delete(item.fileInfo.FullName);//todo: try...catch here?
                    _console.WriteLine($" deleted!");
                }

            if (dAlbums.Count > 0)
            {
                _console.WriteLine($"Adding media item(s) to albums...");
                //todo: put progress bar here?
                var table = new Table("Album Name", "Status") { Config = TableConfiguration.Markdown() };
                foreach (var kvp in dAlbums)
                {
                    var ids = items.Where(p => p.albums.Contains(kvp.Value.title, StringComparer.OrdinalIgnoreCase)).Select(p => p.mediaItem.id).ToList();
                    if (await _googlePhotosSvc.AddMediaItemsToAlbumAsync(kvp.Value.id, ids))
                        table.AddRow(kvp.Value.title, $"{ids.Count} media item(s) added");
                    else
                        Debugger.Break();
                }
                Console.Write(table.ToString());
                _console.WriteLine();
            }

            _console.WriteLine($"Upload completed, exiting.");
        }
        else
            _console.WriteLine($" failed :(");
        //todo: now handle albums
        //todo: do we add media items to local cache here?

        return 0;

        bool DoDuplicateAlbumsExist()
        {
            var duplicateAlbumsByTitle = GetAlbumDuplicates(allAlbums);

            //album titles in google photos don't need to be unique, but we can't assign photos to an existing album
            //if duplicate titles exist that match one of our required album titles... 
            if (duplicateAlbumsByTitle.Count > 0 && duplicateAlbumsByTitle.Any(p => requiredAlbumTitles.Contains(p.title, StringComparer.OrdinalIgnoreCase)))
            {
                _console.WriteLine($"Duplicate album titles present, unable to assign media item(s) to albums.");
                foreach (var album in duplicateAlbumsByTitle)
                    _console.WriteLine($"{album.title}");
                _console.WriteLine($"Please rename or merge the above albums to continue.");
                return false;
            }
            return true;
        }

        async Task<Dictionary<string, Album>> GetOrCreateAlbums()
        {
            //great there are no duplicate titles, lets get/create the missing albums
            var d = new Dictionary<string, Album>();
            foreach (var title in requiredAlbumTitles)
            {
                var album = allAlbums.FirstOrDefault(p => p.title.Equals(title, StringComparison.OrdinalIgnoreCase));
                if (album is null)
                    album = await _googlePhotosSvc.CreateAlbumAsync(title);
                d.Add(title, album);
            }
            return d;
        }
    }
}