namespace CasCap.GooglePhotosCli.Tests;

public abstract class TestBase
{
    protected ILogger _logger;

    protected GooglePhotosService _googlePhotosSvc;
    protected ILocalCache _localCache;

    protected TestBase(ITestOutputHelper output)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile($"appsettings.Test.json", optional: false, reloadOnChange: true)
            .AddUserSecrets<TestBase>()//for local testing
            .AddEnvironmentVariables()//for CI testing
            .Build();

        //initiate ServiceCollection w/logging
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .AddXUnitLogging(output);

        _logger = ApplicationLogging.LoggerFactory.CreateLogger<TestBase>();

        //add services
        services.AddGooglePhotos(configuration);
        services.AddCasCapCaching(LocalCacheType: CacheType.Disk);

        //retrieve services
        var serviceProvider = services.BuildServiceProvider();
        _googlePhotosSvc = serviceProvider.GetRequiredService<GooglePhotosService>();
        _localCache = serviceProvider.GetRequiredService<ILocalCache>();
    }
}
