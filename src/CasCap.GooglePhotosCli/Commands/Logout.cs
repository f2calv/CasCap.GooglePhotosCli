namespace CasCap.Commands;

[Command(Description = "Sign-out and delete all local data.")]
internal class Logout : CommandBase
{
    public Logout(IConsole console, ILocalCache localCache, IOptions<CachingConfig> cachingConfig, GooglePhotosService googlePhotosSvc)
        : base(console, localCache, cachingConfig, googlePhotosSvc) { }

    public async override Task<int> OnExecuteAsync(CommandLineApplication app)
    {
        await base.OnExecuteAsync(app);

        _console.WriteLine($"todo: need to implement this...");

        return 0;
    }
}
