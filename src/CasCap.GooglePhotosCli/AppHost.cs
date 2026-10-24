using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CasCap;

/// <summary>Builds and runs the unofficial Google Photos command-line host.</summary>
internal static class AppHost
{
    private static string DefaultDataStorePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "googlephotos",
        "auth");

    /// <summary>Configures the generic host and dispatches the requested command.</summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    internal static async Task<int> RunAsync(string[] args)
    {
        // TODO: Register console logging so injected ILogger diagnostics are observable after output behavior is reviewed.
        var host = new HostBuilder()
            .ConfigureAppConfiguration((_, builder) =>
            {
                // The tool runs from the global tool store, so shipped defaults load from the assembly
                // directory while a per-project override loads from the working directory.
                builder.SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                    .AddJsonFile(
                        Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json"),
                        optional: true,
                        reloadOnChange: false)
                    .AddUserSecrets<Program>(optional: true)
                    .AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddSingleton(PhysicalConsole.Singleton);
                services.AddGooglePhotos(context.Configuration);
                // Commands are constructed while parsing, including for --help, so resolving the typed
                // client eagerly would demand valid credentials before the user can read the help text.
                services.AddSingleton(serviceProvider =>
                    new Lazy<GooglePhotosService>(serviceProvider.GetRequiredService<GooglePhotosService>));
                services.PostConfigure<GooglePhotosOptions>(options =>
                {
                    // Own the OAuth cache so logout can clear it without touching other applications' credentials.
                    if (string.IsNullOrWhiteSpace(options.FileDataStoreFullPathOverride))
                        options.FileDataStoreFullPathOverride = DefaultDataStorePath;
                });
                // AddGooglePhotos validates on host start, which would make --help fail before credentials exist.
                // The validators still run when options are first read by a command that needs Google.
                services.RemoveAll<IStartupValidator>();
            });

        try
        {
            return await host.RunCommandLineApplicationAsync<Program>(args);
        }
        catch (OptionsValidationException exception)
        {
            await Console.Error.WriteLineAsync("Google Photos is not configured yet:");
            foreach (var failure in exception.Failures)
                await Console.Error.WriteLineAsync($"    {failure}");
            await Console.Error.WriteLineAsync();
            await Console.Error.WriteLineAsync(
                "Set CasCap:GooglePhotosOptions via user secrets or environment variables, for example:");
            await Console.Error.WriteLineAsync(
                "    CasCap__GooglePhotosOptions__User, CasCap__GooglePhotosOptions__ClientId, CasCap__GooglePhotosOptions__ClientSecret");
            await Console.Error.WriteLineAsync(
                "See https://github.com/f2calv/CasCap.GooglePhotosCli#configuration");
            return 1;
        }
        catch (CommandParsingException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            if (exception is UnrecognizedCommandParsingException unrecognizedException
                && unrecognizedException.NearestMatches.Any())
            {
                await Console.Error.WriteLineAsync();
                await Console.Error.WriteLineAsync("Did you mean this?");
                await Console.Error.WriteLineAsync($"    {unrecognizedException.NearestMatches.First()}");
            }
            return 1;
        }
    }
}
