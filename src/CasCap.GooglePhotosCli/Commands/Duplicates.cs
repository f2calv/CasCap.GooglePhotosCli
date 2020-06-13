using CasCap.Common.Extensions;
using CasCap.Models;
using CasCap.Services;
using CasCap.ViewModels;
using McMaster.Extensions.CommandLineUtils;
using ShellProgressBar;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace CasCap.Commands
{
    [Command(Description = "Analyse and identify potential duplicate media items in a Google Photos account.")]
    internal class Duplicates : CommandBase
    {
        public Duplicates(IConsole console, DiskCacheService diskCacheSvc, GooglePhotosService googlePhotosSvc) : base(console, diskCacheSvc, googlePhotosSvc) { }

        [Argument(0, Description = "Which media type do you wish to analyse?")]
        public MediaType type { get; }

        [Option(Description = "Force a cache refresh (this will use up credits!)")]
        public bool SkipCache { get; }

        public async override Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            await base.OnExecuteAsync(app);
            //await base.OnExecute(cancellationToken);

            if (!await SyncMediaItems()) return 1;
            if (!await SyncAlbums()) return 1;
            if (!await SyncMediaItemsByCategory()) return 1;

            var res = await _diskCacheSvc.GetAsync($"{nameof(ScoreResponse)}_{type}.json", () => GetScoreResponse());

            duplicateFolder = Path.Combine(_userPath, "duplicates");
            if (!Directory.Exists(duplicateFolder))
                Directory.CreateDirectory(duplicateFolder);

            //only show where the number of flags set is greater than or equal to minSetFlagsCount
            //can't really remember the point in the below investigation atm
            /*
            var minSetFlagsCount = 3;
            var mostCommonScenarios = res.dStats.Where(p => BitOperations.PopCount((ulong)p.Key) >= minSetFlagsCount).OrderByDescending(p => p.Value).ToList();
            foreach (var kvp in mostCommonScenarios)
            {
                var matchCount = res.dScore.Where(p => p.Value.propertyMatches.HasFlag(kvp.Key)).ToList();
                var str = $"{kvp.Value}\t{kvp.Key}\t{matchCount}";
                _console.WriteLine(str);
            }
            */

            _console.WriteLine();

            var counter = 1;
            foreach (var score in res.dScore.OrderByDescending(p => p.Value.count))
            {
                var ids = new List<string>();
                if (dMediaItems.TryGetValue(score.Key, out var mi))
                {
                    ids.Add(mi.id);
                    var str = $"{score.Value.count}\t{score.Value.propertyMatches}\t";
                    var query = new List<string>();
                    if (score.Value.propertyMatches.HasFlag(GroupByProperty.filename))
                        query.Add(mi.filename);

                    if (score.Value.propertyMatches.HasFlag(GroupByProperty.creationTime))
                        query.Add(mi.mediaMetadata.creationTime.ToString("yyyy-MM-dd"));

                    if (query.IsNullOrEmpty())
                        str += "??how to identify??";
                    else
                        str += string.Join(", ", query);
                    _console.WriteLine(str);
                }
                else
                    throw new Exception("should never get hit...?");

                //get latest versions (i.e. with valid product urls)
                var mediaItems = await _googlePhotosSvc.GetMediaItemsByIdsAsync(ids);
                await AnalyseExifs(mediaItems);


                //https://stackoverflow.com/questions/4580263/how-to-open-in-default-browser-in-c-sharp
                //var searchTerm = $"{filename} OR {Path.GetFileNameWithoutExtension(filename)}";
                //var query = System.Web.HttpUtility.HtmlEncode(searchTerm);
                //var url = $"https://photos.google.com/search/{query}";
                //if (_options.User.IndexOf("marinos") > -1)
                //    _console.WriteLine($"{url}");
                //else
                //{
                //    _console.WriteLine($"{z} of {fileNames.Length}\tHit any key to view potential duplicates for filename '{filename}'...");
                //    Console.ReadKey();
                //    //Process.Start($"https://photos.google.com/search/{filename}");
                //    Process.Start("explorer", url);
                //}


                Console.ReadKey();

                counter++;
                if (counter > 250) break;
            }

            {
                //if (Prompt.GetYesNo("Do you want to clear the local mediaItems cache?", false)
                //    Utils.DeleteAll();

                //below doesnt work because media items must have been created by the API ... ffs
                /*
                var albumName = $"{DateTime.UtcNow:yyyy-MM-dd} - duplicates";
                var album = await _googlePhotosSvc.GetOrCreateAlbumAsync(albumName);
                if (album is object)
                {
                    _console.WriteLine($"created duplicates album '{albumName}'");
                    var ids = duplicateGroupBy.SelectMany(p => p.mediaItems).Select(q => q.id).Distinct().ToArray();
                    if (await _googlePhotosSvc.AddMediaItemsToAlbumAsync(album.id, ids))
                    {
                        _console.WriteLine("added duplicates to album '{albumName}'");
                        Process.Start(album.productUrl);
                    }
                    else
                        _console.WriteLine($"unable to add duplicates to album '{albumName}'");
                }
                else
                {
                    _console.WriteLine($"unable to create duplicates album '{albumName}'");
                    return;
                }
                */
            }

            //compare post-download;
            //  file size (can't get this)
            //  exif tags (which tags are important and which can we disregard?)

            //todo: record all query history, either in a log, or a summary json file to show if we are approaching the daily limit?
            //todo: download/cache all items per album
            //todo: upload (nested) directory structure? w/console ui progress indicator? w/webp conversion?
            //todo: download all media items to a local cache?
            //todo: re-order all media items based on creationTime? (the default album order is when added)
            //todo: add Console.Clear to McMaster.Extensions.CommandLineUtils?

            await WriteConfig();
            return 0;
        }

        static long iteration = 1;

        async Task<ScoreResponse> GetScoreResponse()
        {
            await Task.Delay(0);

            var res = new ScoreResponse();

            var flattened = GetFlattened();

            //todo: for speed analyse flattened to see if Count(DISTINCT mimeType) > 1 - if mimeType only ever null OR jpg, then don't include in combinations (repeat for all other fields)
            //todo: split photos and video duplication check

            var lGroupByCombinations = Utils.GetAllCombinations<GroupByProperty>().ToList();

            if (type == MediaType.Photo)
            {
                lGroupByCombinations.RemoveAll(p => p.HasFlag(GroupByProperty.fps));
                lGroupByCombinations.RemoveAll(p => p.HasFlag(GroupByProperty.status));
                flattened = flattened.Where(p => p.mimeType.StartsWith("image", StringComparison.OrdinalIgnoreCase)).ToList();
            }
            if (type == MediaType.Video)
            {
                lGroupByCombinations.RemoveAll(p => p.HasFlag(GroupByProperty.focalLength));
                lGroupByCombinations.RemoveAll(p => p.HasFlag(GroupByProperty.apertureFNumber));
                lGroupByCombinations.RemoveAll(p => p.HasFlag(GroupByProperty.isoEquivalent));
                lGroupByCombinations.RemoveAll(p => p.HasFlag(GroupByProperty.exposureTime));
                flattened = flattened.Where(p => p.mimeType.StartsWith("video", StringComparison.OrdinalIgnoreCase)).ToList();
            }

            //flattened = flattened.Take(2_000).ToList();//make testing faster!

            var msg = $"Now processing {flattened.Count} records against {lGroupByCombinations.Count} property combinations.";



            var dtStart = DateTime.UtcNow;
            var estimatedDuration = TimeSpan.FromMilliseconds(lGroupByCombinations.Count * 25);
            if (false)
            {
                using var pbar = new ProgressBar(lGroupByCombinations.Count, msg, pbarOptions)
                {
                    EstimatedDuration = estimatedDuration
                };
                foreach (var myGroup in lGroupByCombinations)
                {
                    CalculateScore(myGroup);
                    UpdateProgress(pbar, myGroup);
                }
            }
            else
            {
                var cts = new CancellationTokenSource();

                var po = new ParallelOptions { CancellationToken = cts.Token, MaxDegreeOfParallelism = Environment.ProcessorCount };

                // Run a task so that we can cancel from another thread.
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                Task.Factory.StartNew(() =>
                {
                    if (Console.ReadKey().KeyChar == 'c')
                        cts.Cancel();
                    Console.WriteLine("press any key to exit");
                });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                try
                {
                    using var pbar = new ProgressBar(lGroupByCombinations.Count, msg, pbarOptions)
                    {
                        EstimatedDuration = estimatedDuration
                    };
                    Parallel.ForEach(lGroupByCombinations, po, (myGroup) =>
                    {
                        //using (var child = pbar.Spawn(1, myGroup.ToString(), childOptions))
                        //{
                        CalculateScore(myGroup);
                        UpdateProgress(pbar, myGroup);
                        //    child.Tick();
                        //todo: can't exit early now with progress bar?
                        po.CancellationToken.ThrowIfCancellationRequested();
                        //}
                        //pbar.Tick();
                    });
                }
                catch (OperationCanceledException e)
                {
                    Console.WriteLine(e.Message);
                }
                finally
                {
                    cts.Dispose();
                }
            }
            return res;

            void CalculateScore(GroupByProperty myGroup)
            {
                Interlocked.Increment(ref iteration);
                //https://stackoverflow.com/questions/33286297/linq-groupby-with-a-dynamic-group-of-columns
                var results = flattened
                    .GroupBy(item =>
                    new
                    {
                        filename = myGroup.HasFlag(GroupByProperty.filename) ? item.filename : null,
                        mimeType = myGroup.HasFlag(GroupByProperty.mimeType) ? item.mimeType : null,
                        dimensions = myGroup.HasFlag(GroupByProperty.dimensions) ? $"{item.height}x{item.width}" : null,
                        creationTime = myGroup.HasFlag(GroupByProperty.creationTime) ? item.creationTime : DateTime.MinValue,
                        description = myGroup.HasFlag(GroupByProperty.description) ? item.description : null,
                        //photo
                        focalLength = myGroup.HasFlag(GroupByProperty.focalLength) ? item.focalLength : float.MinValue,
                        apertureFNumber = myGroup.HasFlag(GroupByProperty.apertureFNumber) ? item.apertureFNumber : float.MinValue,
                        isoEquivalent = myGroup.HasFlag(GroupByProperty.isoEquivalent) ? item.isoEquivalent : int.MinValue,
                        exposureTime = myGroup.HasFlag(GroupByProperty.exposureTime) ? item.exposureTime : float.MinValue,
                        //video
                        fps = myGroup.HasFlag(GroupByProperty.fps) ? item.fps : float.MinValue,
                        status = myGroup.HasFlag(GroupByProperty.status) ? item.status : null,
                        //photo & video
                        cameraMake = myGroup.HasFlag(GroupByProperty.cameraMake) ? item.cameraMake : null,
                        cameraModel = myGroup.HasFlag(GroupByProperty.cameraModel) ? item.cameraModel : null,
                        //collections
                        albumIds = myGroup.HasFlag(GroupByProperty.albumIds) ? item.albumIds : null,
                        contentCategoryTypes = myGroup.HasFlag(GroupByProperty.contentCategoryTypes) ? item.contentCategoryTypes : null,
                    })
                    .Select(g => new
                    {
                        //g.Key.filename,
                        //g.Key.mimeType,
                        //g.Key.dimensions,
                        //g.Key.creationTime,
                        //g.Key.description,

                        //g.Key.focalLength,
                        //g.Key.apertureFNumber,
                        //g.Key.isoEquivalent,
                        //g.Key.exposureTime,

                        //g.Key.fps,
                        //g.Key.status,

                        //g.Key.cameraMake,
                        //g.Key.cameraModel,

                        //g.Key.albumIds,
                        //g.Key.contentCategoryTypes,
                        //quantity = i.Sum()
                        medaItemIds = g.Select(p => p.id).ToList(),
                        count = g.Count()
                    }).Where(p => p.count > 1).AsParallel().ToList();

                //res.dScore2.AddOrUpdate(myGroup, results.medaItemIds, (k, v) => v = result.medaItemIds);

                //Parallel.ForEach ??
                foreach (var result in results)
                {//todo: we can also record on what properties/enums the matches occurred (those properties could appear in an enum checkbox list?)
                    foreach (var id in result.medaItemIds)
                        _ = res.dScore.AddOrUpdate(id, new MediaItemScore { count = 1, propertyMatches = myGroup }, (k, v) =>
                        {
                            v.count++;
                            v.propertyMatches |= myGroup;
                            return v;
                        });
                    //record a count of matches per bitmask
                    _ = res.dStats.AddOrUpdate(myGroup, result.medaItemIds.Count, (k, v) =>
                    {
                        v = result.medaItemIds.Count;
                        return v;
                    });
                    //_ = res.dScore2.AddOrUpdate(myGroup, new HashSet<string>(result.medaItemIds), (k, v) =>
                    //{
                    //    //v.AddRange(result.medaItemIds);
                    //    foreach (var _id in result.medaItemIds)
                    //    {
                    //        if (!v.Contains(_id))
                    //            v.Add(_id);
                    //        else
                    //            Debugger.Break();
                    //    }
                    //    return v;
                    //});
                }
            }

            void UpdateProgress(ProgressBar pbar, GroupByProperty myGroup)
            {
                //pbar.Tick();
                pbar.Tick($"Iteration {iteration} of {lGroupByCombinations.Count} completed, {myGroup}");
                if (Interlocked.Read(ref iteration) % 25 == 0)
                {
                    var tsTaken = DateTime.UtcNow.Subtract(dtStart).TotalMilliseconds;
                    var timePerCombination = tsTaken / iteration;
                    pbar.EstimatedDuration = TimeSpan.FromMilliseconds((lGroupByCombinations.Count - iteration) * timePerCombination);
                }
            }
        }

        async Task AnalyseExifs(List<MediaItem> mediaItems)
        {
            _console.WriteLine($"Download byte data;");
            var j = 1;
            foreach (var mediaItem in mediaItems)
            {
                _console.WriteLine($"{j})\turl => {mediaItem.productUrl}");

                var filePath = Path.Combine(duplicateFolder, mediaItem.id);
                if (!File.Exists(filePath))
                {
                    _console.Write($"{j})\tdownloading {mediaItem.id} ...");
                    var bytes = await _googlePhotosSvc.DownloadBytes(mediaItem, download: true);
                    await File.WriteAllBytesAsync(filePath, bytes);
                    _console.WriteLine($"downloaded {((long)bytes.Length).GetSizeInMB()}MB");//todo: need a KB extension method here?
                }
                else
                    _console.WriteLine($"{j})\timage already exists {mediaItem.id}");
                j++;
            }

            _console.WriteLine();
            _console.WriteLine($"Extract EXIF data;");
            j = 1;

            var lTags = new List<(string mediaItemId, ExifTag tag, ExifDataType tagDataType, object tagValue)>();
            foreach (var mediaItem in mediaItems)
            {
                var filePath = Path.Combine(duplicateFolder, mediaItem.id);

                _console.WriteLine($"{j})\t{filePath}");
                using (var image = Image.Load(filePath))
                {
                    foreach (var tag in image.Metadata.ExifProfile.Values)
                    {
                        lTags.Add((mediaItem.id, tag.Tag, tag.DataType, tag.GetValue()));
                        //_console.WriteLine($"{tag.DataType}\t{tag.Tag}\t={tag.GetValue()}");
                    }
                }
                j++;
            }

            _console.WriteLine();
            _console.WriteLine($"Summarise/group EXIF data;");
            var uniqueTags = lTags.Select(p => p.tag).Distinct();
            foreach (var tag in uniqueTags)
            {
                var values = lTags.Where(p => p.tag == tag).Select(p => p.tagValue.ToString()).Distinct().ToList();
                _console.WriteLine($"{tag} ({values.Count})\t=\t{string.Join(", ", values)}");
            }
        }
    }
}