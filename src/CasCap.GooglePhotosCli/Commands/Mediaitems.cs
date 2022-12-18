using BetterConsoleTables;
using CasCap.Common.Extensions;
using CasCap.Services;
using McMaster.Extensions.CommandLineUtils;
namespace CasCap.Commands;

[Command("mediaitems", Description = "Manage your media items i.e. photos & videos")]
[Subcommand(typeof(Upload))]
[Subcommand(typeof(List))]
[Subcommand(typeof(Duplicates))]
internal class MediaItems : CommandBase
{
    public MediaItems(IConsole console, DiskCacheService diskCacheSvc, GooglePhotosService googlePhotosSvc) : base(console, diskCacheSvc, googlePhotosSvc) { }

    public async override Task<int> OnExecuteAsync(CommandLineApplication app)
    {
        await base.OnExecuteAsync(app);
        _console.Error.WriteLine("You must specify an action. See --help for more details.");
        return 1;
    }

    [Command(Description = "List media items")]
    class List : CommandBase
    {
        public List(IConsole console, DiskCacheService diskCacheSvc, GooglePhotosService googlePhotosSvc) : base(console, diskCacheSvc, googlePhotosSvc) { }

        public async override Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            await base.OnExecuteAsync(app);
            var mediaitems = await _googlePhotosSvc.GetMediaItemsAsync().ToListAsync();
            if (mediaitems.IsNullOrEmpty())
            {
                _console.WriteLine("Sorry, no media items available...");
                return 0;
            }
            var table = new Table(string.Empty, "File Name", "Mime Type", "Id") { Config = TableConfiguration.Markdown() };
            var i = 1;
            foreach (var mediaitem in mediaitems)
            {
                table.AddRow(i, mediaitem.filename, mediaitem.mimeType, mediaitem.id);
                i++;
            }
            return 0;
        }
    }
}