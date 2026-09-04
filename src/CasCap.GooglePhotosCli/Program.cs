using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CasCap;

/// <summary>Entry point and root command for the unofficial Google Photos command line interface.</summary>
[Command(Name = "googlephotos", Description = "*Unofficial* Google Photos CLI", ExtendedHelpText = @"
Remarks:
  Since Google's API change of 31 March 2025 this tool can only list, download and organise
  albums and media items which it created itself. Existing media in your account is not visible.

  See the project site for further information, https://github.com/f2calv/CasCap.GooglePhotosCli
")]
[VersionOptionFromMember("--version", MemberName = nameof(GetVersion))]
[Subcommand(typeof(Albums))]
[Subcommand(typeof(MediaItems))]
[Subcommand(typeof(Logout))]
internal sealed class Program
{
    private static string DefaultDataStorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "googlephotos", "auth");

    private static async Task<int> Main(string[] args)
    {
        var host = new HostBuilder()
            .ConfigureAppConfiguration((context, builder) =>
            {
                //The tool runs from the global tool store, so the shipped defaults are loaded from the
                //assembly directory while a per-project override is loaded from the working directory.
                builder.SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .AddJsonFile(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"), optional: true, reloadOnChange: false)
                    .AddUserSecrets<Program>(optional: true)
                    .AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton(PhysicalConsole.Singleton);
                services.AddGooglePhotos(context.Configuration);
                //Commands are constructed while parsing, including for --help, so resolving the typed
                //client eagerly would demand valid credentials before the user can read the help text.
                services.AddSingleton(serviceProvider =>
                    new Lazy<GooglePhotosService>(serviceProvider.GetRequiredService<GooglePhotosService>));
                services.PostConfigure<GooglePhotosOptions>(options =>
                {
                    //Own the OAuth cache so 'logout' can clear it without touching other applications' Google credentials.
                    if (string.IsNullOrWhiteSpace(options.FileDataStoreFullPathOverride))
                        options.FileDataStoreFullPathOverride = DefaultDataStorePath;
                });
                //AddGooglePhotos validates on host start, which would make 'googlephotos --help' fail before
                //the user has any credentials. The same validators still run when the options are first read,
                //so an unconfigured tool fails on the command which actually needs Google, not on every command.
                services.RemoveAll<IStartupValidator>();
            });

        try
        {
            return await host.RunCommandLineApplicationAsync<Program>(args);
        }
        catch (OptionsValidationException ex)
        {
            await Console.Error.WriteLineAsync("Google Photos is not configured yet:");
            foreach (var failure in ex.Failures)
                await Console.Error.WriteLineAsync($"    {failure}");
            await Console.Error.WriteLineAsync();
            await Console.Error.WriteLineAsync("Set CasCap:GooglePhotosOptions via user secrets or environment variables, for example:");
            await Console.Error.WriteLineAsync("    CasCap__GooglePhotosOptions__User, CasCap__GooglePhotosOptions__ClientId, CasCap__GooglePhotosOptions__ClientSecret");
            await Console.Error.WriteLineAsync("See https://github.com/f2calv/CasCap.GooglePhotosCli#configuration");
            return 1;
        }
        catch (CommandParsingException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            if (ex is UnrecognizedCommandParsingException uex && uex.NearestMatches.Any())
            {
                await Console.Error.WriteLineAsync();
                await Console.Error.WriteLineAsync("Did you mean this?");
                await Console.Error.WriteLineAsync($"    {uex.NearestMatches.First()}");
            }
            return 1;
        }
    }

    private int OnExecute(CommandLineApplication app, IConsole console)
    {
        console.WriteLine("You must specify a subcommand.");
        app.ShowHelp();
        return 1;
    }

    private static string GetVersion()
        => typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}
