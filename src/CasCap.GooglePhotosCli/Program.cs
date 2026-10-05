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
    private static Task<int> Main(string[] args) => AppHost.RunAsync(args);

    public int OnExecute(CommandLineApplication app, IConsole console)
    {
        console.WriteLine("You must specify a subcommand.");
        app.ShowHelp();
        return 1;
    }

    private static string GetVersion()
        => typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}
