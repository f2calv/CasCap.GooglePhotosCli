using CasCap.Services;
using McMaster.Extensions.CommandLineUtils;
using System.Threading.Tasks;
namespace CasCap.Commands
{
    [Command(Description = "Sign-out and delete all local data.")]
    internal class Logout : CommandBase
    {
        public Logout(IConsole console, DiskCacheService diskCacheSvc, GooglePhotosService googlePhotosSvc) : base(console, diskCacheSvc, googlePhotosSvc) { }

        public async override Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            await base.OnExecuteAsync(app);

            _console.WriteLine($"todo: need to implement this...");

            return 0;
        }
    }
}