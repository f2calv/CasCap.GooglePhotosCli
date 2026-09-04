using Microsoft.Extensions.Options;

namespace CasCap.Commands;

/// <summary>Clears the locally cached Google OAuth grant.</summary>
[Command(Description = "Sign out by deleting the locally cached OAuth credentials.")]
internal sealed class Logout(
    ILogger<Logout> logger,
    IOptions<GooglePhotosOptions> googlePhotosOptions,
    IConsole console,
    Lazy<GooglePhotosService> googlePhotosSvc)
    : CommandBase(logger, console, googlePhotosSvc)
{
    /// <summary>Deletes the cached credentials without asking for confirmation.</summary>
    [Option("-y|--yes", Description = "Delete the cached credentials without prompting.")]
    public bool AutoConfirm { get; }

    /// <inheritdoc/>
    public override Task<int> OnExecuteAsync(CommandLineApplication app, CancellationToken cancellationToken)
    {
        //Deliberately does not call the base implementation, which would authenticate before signing out.
        var dataStorePath = googlePhotosOptions.Value.FileDataStoreFullPathOverride;
        if (string.IsNullOrWhiteSpace(dataStorePath))
        {
            _console.Error.WriteLine("The OAuth cache is shared with other Google applications and will not be deleted.");
            _console.Error.WriteLine($"Set CasCap:GooglePhotosOptions:FileDataStoreFullPathOverride to a tool-owned folder first.");
            return Task.FromResult(1);
        }

        if (!Directory.Exists(dataStorePath))
        {
            _console.WriteLine("Already signed out, no cached credentials found.");
            return Task.FromResult(0);
        }

        if (!AutoConfirm && !Prompt.GetYesNo("Delete the cached Google credentials for this tool?", true))
            return Task.FromResult(0);

        Directory.Delete(dataStorePath, recursive: true);
        _console.WriteLine("Signed out, cached credentials deleted.");
        return Task.FromResult(0);
    }
}
