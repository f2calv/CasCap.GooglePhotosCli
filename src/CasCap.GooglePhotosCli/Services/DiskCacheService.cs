using CasCap.Common.Extensions;
using CasCap.Logic;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace CasCap.Services
{
    public class DiskCacheService
    {
        readonly ILogger _logger;

        public DiskCacheService(ILogger<DiskCacheService> logger)
        {
            _logger = logger;
        }

        readonly AsyncDuplicateLock locker = new();

        public string CacheRoot { get; set; } = string.Empty;

        public bool IsEnabled { get; set; } = true;

        public string CacheSize()
        {
            var size = Utils.CalculateFolderSize(CacheRoot);
            if (size > 1024)
            {
                var s = size / 1024;
                return $"{s:###,###,##0}kb";
            }
            else
                return $"0kb";
        }

        public (int files, int directories) CacheClear()
        {
            var di = new DirectoryInfo(CacheRoot);
            var files = 0;
            foreach (var file in di.GetFiles())
            {
                file.Delete();
                files++;
            }
            var directories = 0;
            foreach (var dir in di.GetDirectories())
            {
                dir.Delete(true);
                directories++;
            }
            return (files, directories);
        }

        public async Task<T> GetAsync<T>(string key, Func<Task<T>> createItem = null, bool skipCache = false, CancellationToken token = default) where T : class
        {
            //Debug.WriteLine(key);
            var (output, fromCache) = await GetAsyncV2(key, createItem, skipCache, token);
            return output;
        }

        //V2 returns a boolean indicating the source of the data
        public async Task<(T output, bool fromCache)> GetAsyncV2<T>(string key, Func<Task<T>> createItem = null, bool skipCache = false, CancellationToken token = default) where T : class
        {
            var fromCache = false;
            key = $"{CacheRoot}/{key}";
            T cacheEntry;
            if (IsEnabled && File.Exists(key) && !skipCache)
            {
                var json = File.ReadAllText(key);
                cacheEntry = null;
                try
                {
                    cacheEntry = json.FromJSON<T>();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    Debugger.Break();
                }
                _logger.LogDebug($"{key}\tretrieved cacheEntry from local cache");
                fromCache = true;
            }
            else
            {
                //if we use Func and go create the cacheEntry, then we lock here to prevent multiple going at the same time
                //https://www.hanselman.com/blog/EyesWideOpenCorrectCachingIsAlwaysHard.aspx
                using (await AsyncDuplicateLock.LockAsync(key))
                {
                    // Key not in cache, so get data.
                    cacheEntry = await createItem();
                    _logger.LogDebug($"{key}\tattempted to populate a new cacheEntry object");
                    if (cacheEntry != null && IsEnabled)
                        File.WriteAllText(key, cacheEntry.ToJSON());
                }
            }
            return (cacheEntry, fromCache);
        }

        public void Delete(string key)
        {
            var path = Path.Combine(CacheRoot, key);
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}