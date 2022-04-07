using CasCap.Commands;
using CasCap.Services;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Reflection;
namespace CasCap;

[Command(Name = "googlephotos", Description = "*Unofficial* Google Photos CLI", ExtendedHelpText = @"
Remarks:
  See the project site for further information, https://github.com/f2calv/CasCap.GooglePhotosCli
")]
[VersionOptionFromMember("--version", MemberName = nameof(GetVersion))]
[Subcommand(typeof(Logout))]
[Subcommand(typeof(Albums)), Subcommand(typeof(MediaItems))]
[Subcommand(typeof(Sync))]
class Program
{
    static async Task<int> Main(string[] args)
    {
        var host = new HostBuilder()
            .ConfigureLogging((context, builder) =>
            {
                    //builder.AddConsole();
                })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton<DiskCacheService>();
                services.AddSingleton(PhysicalConsole.Singleton);
                services.AddGooglePhotos();
            });
        var result = 0;
        try
        {
            result = await host.RunCommandLineApplicationAsync<Program>(args);
        }
        catch (CommandParsingException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            if (ex is UnrecognizedCommandParsingException uex && uex.NearestMatches.Any())
            {
                await Console.Error.WriteLineAsync();
                await Console.Error.WriteLineAsync("Did you mean this?");
                await Console.Error.WriteLineAsync("    " + uex.NearestMatches.First());
            }
            result = -1;
        }
        return result;
    }

    readonly IConsole _console;
    readonly GooglePhotosService _googlePhotosSvc;

    public Program(IConsole console, GooglePhotosService googlePhotosSvc)
    {
        _console = console;
        _googlePhotosSvc = googlePhotosSvc;
    }

    int OnExecute(CommandLineApplication app, IConsole console, CancellationToken cancellationToken = default)
    {
        console.WriteLine("You must specify a subcommand.");
        app.ShowHelp();
        return 1;
    }

    static string GetVersion()
        => typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
}