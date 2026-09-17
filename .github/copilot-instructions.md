# Copilot Instructions

## Shared Instructions

Shared Copilot instruction files are maintained centrally in the [.github](https://github.com/f2calv/.github) repository under `instructions/`, and are applied to every workspace from the VS Code user profile via `~/.copilot/instructions`. They are deliberately not copied into this repository, so a change there takes effect everywhere without a pull request here.

Everything below is specific to this repository.

## Repository Purpose

This repository is a .NET global tool named `googlephotos` which wraps the
[CasCap.Api.GooglePhotos](https://github.com/f2calv/CasCap.Api.GooglePhotos) library in a
command-line interface. It is published to NuGet as the `googlephotos` package.

## Google Photos API Scope Boundary

Google changed the Photos APIs on 31 March 2025. The Library API can only see albums and media
items **created by this tool's own OAuth client**. Existing user media is reachable only through
the Picker API, which requires interactive per-session user selection.

This constrains what the CLI may claim to do:

- Never add, restore, or document a command which implies whole-library access, for example
  library-wide duplicate detection, "backup everything", or listing every media item in an account.
- Commands which enumerate albums or media items must state, in both their help text and the
  README, that results are limited to content this tool created.
- Any future selective-download feature must be built on `GooglePhotosPickerService`, never on
  Library API search or paging.

## Credential Handling

- The tool must never prompt for, persist, or log an OAuth client id, client secret, access token,
  or refresh token in its own files. Credentials are supplied through `appsettings.json`, .NET User
  Secrets, or environment variables and bound to `GooglePhotosOptions`, exactly as any other
  library consumer would.
- The Google authentication cache is owned by `Google.Apis.Auth` and lives outside the repository.
- Never log a Google account identifier, a media item `ProductUrl` or `BaseUrl`, a full local file
  path, or a personally identifying filename.

## Console Output

- Console presentation is this tool's user interface and legitimately uses `IConsole`, tables and
  progress bars. This is the single exception to the `csharp.instructions.md` rule against writing
  to the console. It does not license `Console.WriteLine` or `Debug.WriteLine` for **diagnostics**,
  which must still flow through `ILogger<T>`.
- Never call `Debugger.Break()` in shipped code.
