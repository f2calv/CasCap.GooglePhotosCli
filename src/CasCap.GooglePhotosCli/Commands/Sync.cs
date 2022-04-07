using CasCap.Services;
using McMaster.Extensions.CommandLineUtils;
namespace CasCap.Commands;

[Command(Description = "Synchronise media item and album data from remote to local.")]
internal class Sync : CommandBase
{
    public Sync(IConsole console, DiskCacheService diskCacheSvc, GooglePhotosService googlePhotosSvc) : base(console, diskCacheSvc, googlePhotosSvc) { }

    public async override Task<int> OnExecuteAsync(CommandLineApplication app)
    {
        await base.OnExecuteAsync(app);

        if (!await SyncMediaItems()) return 1;
        if (!await SyncAlbums()) return 1;
        if (!await SyncMediaItemsByCategory()) return 1;

        return 0;
    }
}