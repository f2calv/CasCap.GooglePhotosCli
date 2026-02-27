using BetterConsoleTables;
using CasCap.Abstractions;
using ShellProgressBar;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CasCap.Commands;

[HelpOption("--help")]
internal abstract class CommandBase
{
    private readonly string[] skipFileNames = ["color_pop.jpg", "effects.jpg"];
    //string[] skipFileNames = new string[] { };

    protected string duplicateFolder = null;

    private List<MediaItem> allMediaItems { get; set; } = new();//todo: make this a private and always use the dictionary values instead
    protected Dictionary<string, MediaItem> dMediaItems { get; set; } = new();//this is the primary reference to mediaItem

    protected List<Album> allAlbums { get; set; }
    private Dictionary<string, Album> dAlbums { get; set; } = new();

    private Dictionary<string, Dictionary<string, MediaItem>> dMediaItemsByAlbum { get; set; } = new();//reference to main MediaItem
    private Dictionary<GooglePhotosContentCategoryType, Dictionary<string, MediaItem>> dMediaItemsByCategory = new();//reference to main MediaItem

    protected ProgressBar pbar;
    protected ChildProgressBar childPBar;

    protected readonly IConsole _console;
    protected readonly ILocalCache _localCache;
    protected readonly GooglePhotosService _googlePhotosSvc;

    private string _diskCacheFolder;
    private readonly string _optionsFilePath;
    private readonly string _configFilePath;

    protected CommandBase(IConsole console, ILocalCache localCache, IOptions<CachingOptions> cachingOptions, GooglePhotosService googlePhotosSvc)
    {
        _console = console;
        _localCache = localCache;
        _googlePhotosSvc = googlePhotosSvc;
        _googlePhotosSvc.PagingEvent += GooglePhotosSvc_PagingEvent;

        _diskCacheFolder = cachingOptions.Value.DiskCacheFolder;
        _optionsFilePath = Path.Combine(_diskCacheFolder, $"{nameof(GooglePhotosOptions)}.json");
        _configFilePath = Path.Combine(_diskCacheFolder, $"{nameof(AppConfig)}.json");
    }

    public virtual void GooglePhotosSvc_PagingEvent(object sender, PagingEventArgs e)
    {
        var str = $"Page {e.pageNumber}\t{e.recordCount}\t+{e.pageSize}";
        if (e.minDate.HasValue && e.maxDate.HasValue)
            str += $"\t{e.minDate.Value:yyyy-MM-dd HH:mm} to {e.maxDate.Value:yyyy-MM-dd HH:mm} ({e.minDate.Value.GetTimeDifference(e.maxDate.Value)})";
        _console.WriteLine(str);
    }

    public virtual async Task<int> OnExecuteAsync(CommandLineApplication app)
    {
        await FastLogin();
        await ReadConfig();
        return 0;
    }

    private async Task FastLogin()
    {
        var hasLoggedInBefore = File.Exists(_optionsFilePath);
        if (hasLoggedInBefore)
        {
            var str = await File.ReadAllTextAsync(_optionsFilePath);
            _options = str.FromJson<GooglePhotosOptions>();
        }

        if (!hasLoggedInBefore || !await _googlePhotosSvc.LoginAsync(_options))
        {
            //_console.WriteLine("Please call login first...");
            await Login();
            return;
        }

        _userPath = Path.Combine(_options.FileDataStoreFullPathOverride, _options.User);
        if (!Directory.Exists(_userPath))
        {
            Directory.CreateDirectory(_userPath);
            _console.WriteLine($"creating user folder {_userPath}");
        }
        _diskCacheFolder = _userPath;
    }

    async Task Login()
    {
        //_console.WriteLine(_optionsFilePath);
        var hasLoggedInBefore = File.Exists(_optionsFilePath);
        if (hasLoggedInBefore)
        {
            hasLoggedInBefore = Prompt.GetYesNo("You have logged-in before, use same log-in details?", true, _promptColor, _promptBgColor);
            //_console.WriteLine("You have logged-in before, using persisted authentication details...");
            //hasLoggedInBefore = true;
        }
        var saveDetails = false;

        if (!hasLoggedInBefore)
        {
            _options = new GooglePhotosOptions
            {
                User = Prompt.GetString("What is your email?", promptColor: _promptColor, promptBgColor: _promptBgColor),
                //todo: validate the above input?

                ClientId = Prompt.GetString("What is your ClientId?", promptColor: _promptColor, promptBgColor: _promptBgColor),
                //todo: validate the above input?

                ClientSecret = Prompt.GetString("What is your ClientSecret?", promptColor: _promptColor, promptBgColor: _promptBgColor),
                //todo: validate the above input?

                //todo: create this extension in mcmasterlib - PromptGetStringArray() ?
                //options.Scopes = new[] { GooglePhotosScope.ReadOnly };
                Scopes = [GooglePhotosScope.Access, GooglePhotosScope.Sharing],
                FileDataStoreFullPathOverride = _diskCacheFolder
            };

            saveDetails = Prompt.GetYesNo("Persist these log-in details for next time?", false, _promptColor, _promptBgColor);
        }
        else
        {
            var str = await File.ReadAllTextAsync(_optionsFilePath);
            _options = str.FromJson<GooglePhotosOptions>();
        }

        var success = await _googlePhotosSvc.LoginAsync(_options);
        if (success)
        {
            if (!hasLoggedInBefore && saveDetails)
                await File.WriteAllTextAsync(_optionsFilePath, _options.ToJson());
            _console.WriteLine("now logged in!! :)");
        }
        else
            _console.WriteLine("login failed :)");
    }

    protected static void DisplayAlbums(List<Album> albums)
    {
        var headers = new[] { new ColumnHeader("#"), new ColumnHeader("Title"), new ColumnHeader("Items", Alignment.Right), new ColumnHeader("Id") };
        var table = new Table(headers) { Config = TableConfiguration.Markdown() };
        var i = 1;
        foreach (var album in albums)
        {
            table.AddRow(i, album.title, album.mediaItemsCount, album.id);
            i++;
        }
        Console.Write(table.ToString());
    }

    protected ProgressBarOptions pbarOptions { get; set; } = new ProgressBarOptions
    {
        ProgressCharacter = '─',
        ForegroundColor = ConsoleColor.Yellow,
        ForegroundColorDone = ConsoleColor.DarkGreen,
        BackgroundColor = ConsoleColor.DarkGray,
        BackgroundCharacter = '\u2593',
        ProgressBarOnBottom = true,
        ShowEstimatedDuration = true,
    };

    protected ProgressBarOptions childPbarOptions { get; set; } = new ProgressBarOptions
    {
        ProgressCharacter = '─',
        ForegroundColor = ConsoleColor.Yellow,
        ForegroundColorDone = ConsoleColor.DarkGreen,
        BackgroundColor = ConsoleColor.DarkGray,
        BackgroundCharacter = '\u2593',
        DisplayTimeInRealTime = true,
        CollapseWhenFinished = true,
    };

    protected ConsoleColor _promptColor = ConsoleColor.White;
    protected ConsoleColor _promptBgColor = ConsoleColor.DarkGreen;

    protected GooglePhotosOptions _options;
    protected AppConfig _config { get; set; }

    protected string _userPath;

    protected async Task ReadConfig()
    {
        if (!File.Exists(_configFilePath))
        {
            _config = new AppConfig();
            await WriteConfig();
        }
        else
        {
            var str = await File.ReadAllTextAsync(_configFilePath);
            _config = str.FromJson<AppConfig>();
        }
    }

    protected Task WriteConfig() => File.WriteAllTextAsync(_configFilePath, _config.ToJson());

    protected async Task<bool> SyncMediaItems()
    {
        //allMediaItems = await _diskCacheSvc.GetAsync<List<MediaItem>>($"{nameof(allMediaItems)}.json", () => _googlePhotosSvc.GetMediaItemsAsync());
        //TODO: revert to use caching line above here!
        allMediaItems = await _googlePhotosSvc.GetMediaItemsAsync().ToListAsync();
        _config.latestMediaItemCreation = allMediaItems.Max(p => p.mediaMetadata.creationTime);

        //check for duplicate MediaItemIds - it shouldn't be possible to have but somehow I had them...
        var duplicateMediaItemIds = allMediaItems.GroupBy(x => x.id).Where(g => g.Count() > 1).Select(y => y.Key).ToList();
        if (!duplicateMediaItemIds.IsNullOrEmpty())
        {
            _console.WriteLine("Search for and tidy-up the following id duplicates on Google Photos ...this shouldn't be possible but happens!?");
            _console.WriteLine();
            foreach (var id in duplicateMediaItemIds)
            {
                foreach (var mi in allMediaItems.Where(p => p.id == id))
                    _console.WriteLine($"\thttps://photos.google.com/search/{mi.filename}\t{mi.productUrl}");
            }
            _console.WriteLine();
            if (Prompt.GetYesNo("Please confirm if you've been able to clean up the above duplicates, i.e. you must delete some!", true, _promptColor, _promptBgColor))
            {
                //either we delete the entire cache ad re-get or we manually remove the duplicates
                //_diskCacheSvc.Delete("mediaItems.json");
                var records = new List<MediaItem>();
                foreach (var id in duplicateMediaItemIds)
                {
                    var record = allMediaItems.FirstOrDefault(p => p.id == id);
                    if (record != null)
                        records.Add(record);
                    else
                        throw new GenericException($"Cannot find duplicate mediaItem id {id}");
                }
                allMediaItems.RemoveAll(p => duplicateMediaItemIds.Contains(p.id));
                allMediaItems.AddRange(records);
                _localCache.Set("mediaItems", allMediaItems);
            }
            else
            {
                //if we can't delete any, then just remove them before we create a dictionary
                //todo: ask Google why duplicates?
                allMediaItems.RemoveAll(p => duplicateMediaItemIds.Contains(p.id));
            }
        }
        dMediaItems = allMediaItems.ToDictionary(k => k.id, v => v);

        //check for new data every 12 hours
        var ts = DateTime.UtcNow.Subtract(_config.lastCheck);
        if (ts.TotalHours > 12)
        {
            //var endDate = DateTime.UtcNow/*.AddDays(1)*/.Date;
            //var startDate = mediaItems.Max(p => p.mediaMetadata.creationTime).Date;
            //if (startDate < endDate)

            _console.WriteLine($"{ts.TotalHours} hours since last mediaItems sync, now checking for new mediaItems...");
            //get any new photos and add to the mediaItems cache
            var newItems = await _googlePhotosSvc.GetMediaItemsByDateRangeAsync(_config.latestMediaItemCreation, DateTime.UtcNow).ToListAsync();
            if (!newItems.IsNullOrEmpty())
            {
                _console.WriteLine($"{newItems.Count} recent mediaItems discovered...");
                var counter = 0;
                foreach (var mi in newItems)
                {
                    if (dMediaItems.TryAdd(mi.id, mi))
                    {
                        allMediaItems.Add(mi);
                        counter++;
                    }
                    else
                    {
                        //_logger.LogWarning();//todo: re-enable logging?
                        Debug.WriteLine(mi);
                    }
                }
                if (counter > 0)
                    _localCache.Set("mediaItems", allMediaItems);
                _console.WriteLine($"added {counter} new mediaItems to local cache");
            }
        }

        _config.latestMediaItemCreation = allMediaItems.Max(p => p.mediaMetadata.creationTime);
        _config.lastCheck = DateTime.UtcNow;
        return true;
    }

    protected async Task<bool> SyncAlbums()
    {
        allAlbums = _localCache.Get<List<Album>>("albums") ?? await _googlePhotosSvc.GetAlbumsAsync();
        _localCache.Set("albums", allAlbums);
        foreach (var a in allAlbums)
        {
            //_console.Write(album.title);
            var album = _localCache.Get<Album>($"album_{a.id}") ?? await _googlePhotosSvc.GetAlbumAsync(a.id);
            _localCache.Set($"album_{a.id}", album);
            //_console.WriteLine($"\t{alb.mediaItemsCount}");

            //var mediaItemsA = await _diskCacheSvc.GetAsync($"album_mediaItems_{a.id}.json", () => _googlePhotosSvc.GetMediaItemsByAlbumAsync(a.id).ToListAsync());
            //TODO: revert to use caching line above here!
            var mediaItemsA = await _googlePhotosSvc.GetMediaItemsByAlbumAsync(a.id).ToListAsync();

            //todo: we probably need a dictionary check here also... as you never know with this API!
            var d = new Dictionary<string, MediaItem>();
            var ids = mediaItemsA.Select(p => p.id).ToList();
            var foundInLookup = dMediaItems.Values.Where(p => ids.Contains(p.id)).ToList();
            if (ids.Count != foundInLookup.Count)
            {
                foreach (var id in ids)
                {
                    var mi = GetMI(id);
                    if (mi is object)
                        d.TryAdd(id, mi);
                    else
                    {
                        //Debugger.Break();//why can't it find the media item!? BURST images don't appear to be returned in the main query?
                        var obj = await _googlePhotosSvc.GetMediaItemByIdAsync(id);//why is this object not returned in the main query?
                        if (obj is object)
                        {
                            _console.WriteLine(obj.filename);
                            if (dMediaItems.TryAdd(id, obj))//hmmm do we re-save the main mediaItems cache now?
                                allMediaItems.Add(obj);
                            else
                                throw new GenericException("should never get hit");
                            mi = obj;
                            d.TryAdd(id, GetMI(id));
                            foundInLookup.Add(mi);
                        }
                        else
                            Debugger.Break();
                    }
                }
                if (ids.Count != foundInLookup.Count)
                    Debugger.Break();//we still have missing images!?
                else
                {
                    //todo: re-save cache
                    _localCache.Set("mediaItems", allMediaItems);
                    dMediaItems = allMediaItems.ToDictionary(k => k.id, v => v);
                }
            }
            else
                d = allMediaItems.ToDictionary(k => k.id, v => GetMI(v.id));

            dMediaItemsByAlbum.Add(a.id, d);
        }
        dAlbums = allAlbums.ToDictionary(k => k.id, v => v);
        return true;
    }

    /// <summary>
    /// Performs a search per-category to see how Google has classified the images.
    /// </summary>
    /// <returns></returns>
    protected async Task<bool> SyncMediaItemsByCategory()
    {
        foreach (var category in EnumExtensions.GetAllItems<GooglePhotosContentCategoryType>())
        {
            //_console.WriteLine();
            //_console.Write(category);
            //var mis = await _diskCacheSvc.GetAsync($"mediaItems_{category}.json", () => _googlePhotosSvc.GetMediaItemsByCategoryAsync(category));
            //TODO: revert to use caching line above here!
            var mis = await _googlePhotosSvc.GetMediaItemsByCategoryAsync(category).ToListAsync();
            //_console.WriteLine($"\t{mis.Count}");

            var d = new Dictionary<string, MediaItem>();
            var duplicateIds = mis.GroupBy(x => x.id).Where(g => g.Count() > 1).Select(y => y.Key).ToList();
            if (duplicateIds.Count > 0)
            {
                //for some reason google returns mutiple mediaItemIds here for a single category...
                //Debugger.Break();
                foreach (var item in mis)
                    d.TryAdd(item.id, item);
            }
            else
                d = mis.ToDictionary(k => k.id, v => GetMI(v.id));

            dMediaItemsByCategory.Add(category, d);
        }
        return true;
    }

    protected static List<Album> GetAlbumDuplicates(List<Album> albums)
    {
        //check for duplicate album names case-insensitive
        //todo: package up the this whole duplicate album by name check?
        var duplicateAlbumsByTitle = albums.GroupBy(p => p.title, StringComparer.InvariantCultureIgnoreCase)
            .Select(g => new
            {
                g.Key,
                Count = g.Count()
            })
            .Where(p => p.Count > 1).ToList();
        return albums.Where(p => duplicateAlbumsByTitle.Select(q => q.Key).Contains(p.title, StringComparer.OrdinalIgnoreCase)).ToList();
    }

    protected List<flattened> GetFlattened()
    {
        var lFlattened = new List<flattened>(allMediaItems.Count);
        var dFlattened = new Dictionary<string, flattened>(allMediaItems.Count);
        foreach (var mediaItem in allMediaItems)
        {
            var albumIds = new List<string>(dAlbums.Count);
            foreach (var kvp in dMediaItemsByAlbum)
                if (kvp.Value.ContainsKey(mediaItem.id))
                    albumIds.Add(kvp.Key);

            var contentCategoryTypes = new List<GooglePhotosContentCategoryType>(dMediaItemsByCategory.Count);
            foreach (var kvp in dMediaItemsByCategory)
                if (kvp.Value.ContainsKey(mediaItem.id))
                    contentCategoryTypes.Add(kvp.Key);

            var o = new flattened
            {
                id = mediaItem.id,
                description = mediaItem.description,
                mimeType = mediaItem.mimeType,
                filename = mediaItem.filename,

                creationTime = mediaItem.mediaMetadata.creationTime,
                width = mediaItem.mediaMetadata.width,
                height = mediaItem.mediaMetadata.height,

                focalLength = mediaItem.isVideo ? 0 : mediaItem.mediaMetadata.photo.focalLength,
                apertureFNumber = mediaItem.isVideo ? 0 : mediaItem.mediaMetadata.photo.apertureFNumber,
                isoEquivalent = mediaItem.isVideo ? 0 : mediaItem.mediaMetadata.photo.isoEquivalent,
                exposureTime = mediaItem.isVideo ? null : mediaItem.mediaMetadata.photo.exposureTime,

                fps = mediaItem.isVideo ? mediaItem.mediaMetadata.video.fps : 0,
                status = mediaItem.isVideo ? mediaItem.mediaMetadata.video.status : string.Empty,

                cameraMake = mediaItem.isVideo ? mediaItem.mediaMetadata.video.cameraMake : mediaItem.mediaMetadata.photo.cameraMake,
                cameraModel = mediaItem.isVideo ? mediaItem.mediaMetadata.video.cameraModel : mediaItem.mediaMetadata.photo.cameraModel,

                albumIds = albumIds.ToArray(),
                contentCategoryTypes = contentCategoryTypes.ToArray(),
            };
            if (dFlattened.TryAdd(o.id, o))
                lFlattened.Add(o);
            else
                throw new GenericException($"should never get hit?");
        }
        return lFlattened;
    }

    MediaItem GetMI(string id, [CallerMemberName] string caller = null)
    {
        if (dMediaItems.TryGetValue(id, out var mi))
            return mi;
        else
        {
            Debug.WriteLine($"{nameof(GetMI)} media item not found, caller={caller}");
            return null;
        }
        //throw new GenericException($"possible sync issue? cannot find mi id {id}");
    }

    protected static string GetRelPath(string rootPath, FileInfo fileInfo) => fileInfo.FullName.Replace(rootPath, string.Empty);

    protected static List<FileInfo> GetFiles(string path, string searchPattern = "*")//todo: move to Utils in CasCap.Common.Extensions lib
    {
        var l = new List<FileInfo>();
        try
        {
            if (!Directory.Exists(path))
                return l;
            else
            {
                try
                {
                    foreach (var file in Directory.GetFiles(path, searchPattern))
                    {
                        if (File.Exists(file))
                        {
                            var finfo = new FileInfo(file);
                            l.Add(finfo);
                        }
                    }
                    foreach (var dir in Directory.GetDirectories(path))
                        l.AddRange(GetFiles(dir, searchPattern));
                }
                catch (NotSupportedException e)
                {
                    throw new GenericException($"Unable to access folder: {e.Message}");
                }
            }
        }
        catch (UnauthorizedAccessException e)
        {
            throw new GenericException($"Unable to access folder: {e.Message}");
        }
        return l;
    }
}
