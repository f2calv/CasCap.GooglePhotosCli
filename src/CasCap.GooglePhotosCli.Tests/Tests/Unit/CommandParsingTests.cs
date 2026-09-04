using Microsoft.Extensions.Logging;

namespace CasCap.GooglePhotosCli.Tests;

/// <summary>
/// Verifies the command surface the tool exposes, including the commands deliberately withdrawn
/// after Google's API change of 31 March 2025.
/// </summary>
[Trait("Category", "Parsing")]
public sealed class CommandParsingTests
{
    [Fact]
    public void RootCommand_ExposesExpectedSubcommands()
    {
        using var app = CreateApplication();

        var commands = GetCommandNames(app);

        Assert.Equal(["albums", "logout", "mediaitems"], commands);
    }

    [Fact]
    public void AlbumsCommand_ExposesExpectedSubcommands()
    {
        using var app = CreateApplication();

        var commands = GetCommandNames(GetCommand(app, "albums"));

        Assert.Equal(["add", "download", "list"], commands);
    }

    [Fact]
    public void MediaItemsCommand_ExposesExpectedSubcommands()
    {
        using var app = CreateApplication();

        var commands = GetCommandNames(GetCommand(app, "mediaitems"));

        Assert.Equal(["list", "upload"], commands);
    }

    /// <summary>
    /// The Library API can no longer see media the tool did not create, so whole-library commands
    /// must stay withdrawn rather than silently returning partial results.
    /// </summary>
    [Theory]
    [InlineData("sync")]
    [InlineData("albums sync")]
    [InlineData("mediaitems duplicates")]
    public void WholeLibraryCommands_AreNotExposed(string commandLine)
    {
        using var app = CreateApplication();

        Assert.Throws<UnrecognizedCommandParsingException>(() => app.Parse(commandLine.Split(' ')));
    }

    [Fact]
    public void UnknownCommand_Throws()
    {
        using var app = CreateApplication();

        Assert.Throws<UnrecognizedCommandParsingException>(() => app.Parse("nonsense"));
    }

    [Theory]
    [InlineData("albums", "download", "title", "output", "yes", "maxwidth", "maxheight", "crop", "exif", "overwrite")]
    [InlineData("albums", "add", "title")]
    [InlineData("albums", "list", "duplicates")]
    [InlineData("mediaitems", "upload", "source", "pattern", "delete", "title", "hierarchy", "yes")]
    public void Command_ExposesExpectedOptions(string parent, string command, params string[] expectedOptions)
    {
        using var app = CreateApplication();

        var options = GetCommand(GetCommand(app, parent), command).Options
            .Select(p => p.LongName)
            .Where(p => p is not null && p != "help")
            .Order(StringComparer.Ordinal);

        Assert.Equal([.. expectedOptions.Order(StringComparer.Ordinal)], options);
    }

    private static CommandLineApplication GetCommand(CommandLineApplication app, string name)
        => app.Commands.Single(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    private static string[] GetCommandNames(CommandLineApplication app)
        => [.. app.Commands.Select(p => p.Name!).Order(StringComparer.Ordinal)];

    private static CommandLineApplication CreateApplication()
    {
        //Dummy credentials keep the model activatable without contacting Google or reading a real OAuth cache.
        var services = new ServiceCollection()
            .AddSingleton<IConsole>(NullConsole.Singleton)
            .AddLogging();
        services.AddGooglePhotos(new GooglePhotosOptions
        {
            User = "test@example.com",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            Scopes = [GooglePhotosScope.ReadOnlyAppCreatedData],
            FileDataStoreFullPathOverride = Path.Combine(Path.GetTempPath(), "googlephotos-tests")
        });
        services.AddSingleton(serviceProvider =>
            new Lazy<GooglePhotosService>(serviceProvider.GetRequiredService<GooglePhotosService>));
        var serviceProvider = services.BuildServiceProvider();

        var app = new CommandLineApplication<Program>();
        app.Conventions.UseDefaultConventions().UseConstructorInjection(serviceProvider);
        return app;
    }
}
