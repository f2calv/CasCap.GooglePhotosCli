# CasCap.GooglePhotosCli

An unofficial Google Photos command line interface, distributed as a .NET global tool named `googlephotos`, for uploading media to and organising albums in a Google Photos account.

[cascap.googlephotoscli-badge]: https://img.shields.io/nuget/v/googlephotos?color=blue
[cascap.googlephotoscli-url]: https://nuget.org/packages/googlephotos

![CI](https://github.com/f2calv/CasCap.GooglePhotosCli/actions/workflows/ci.yml/badge.svg) [![Coverage Status](https://coveralls.io/repos/github/f2calv/CasCap.GooglePhotosCli/badge.svg?branch=main)](https://coveralls.io/github/f2calv/CasCap.GooglePhotosCli?branch=main) [![Nuget][cascap.googlephotoscli-badge]][cascap.googlephotoscli-url]

## Important: Google changed the Photos APIs on 31 March 2025

Google fundamentally reduced what any third-party application can do with a user's Google Photos library. This tool, and the [CasCap.Api.GooglePhotos](https://github.com/f2calv/CasCap.Api.GooglePhotos) library beneath it, are limited by that change:

- The Library API can only see **albums and media items created by this tool's own OAuth client**. Everything already in your account is invisible to it.
- Reading an entire library, and therefore whole-library duplicate detection and whole-library backup, is no longer possible.
- Library API sharing and shared-album operations were withdrawn entirely.
- Existing user media can only be reached through the Picker API, which requires the user to select items interactively, one session at a time.

Version 1.0 removes the commands that depended on whole-library access rather than leaving them to return misleading partial results. See [Google's API update](https://developers.google.com/photos/support/updates).

### Removed in version 1.0

| Command | Reason |
| --- | --- |
| `mediaitems duplicates` | Duplicate detection needs to read the whole library. |
| `sync`, `albums sync` | There is no library-wide metadata left to cache. |
| Interactive credential prompt | Credentials now come from configuration; see [Configuration](#configuration). |

## Installation

The tool is distributed as a [.NET global tool](https://learn.microsoft.com/dotnet/core/tools/global-tools):

```powershell
dotnet tool update --global googlephotos
```

It requires the [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0).

## Google Cloud setup

1. Create a project in the [Google Cloud console](https://console.cloud.google.com/).
2. Open **APIs & Services > Library** and enable **Google Photos Library API**.
3. Configure the OAuth consent screen and create an **OAuth client ID** of type **Desktop app**.
4. Note the client ID and client secret; they are supplied through configuration below.

Full setup guidance lives in the [library documentation](https://github.com/f2calv/CasCap.Api.GooglePhotos#google-cloud-setup).

## Configuration

The tool binds the `CasCap:GooglePhotosOptions` section from, in ascending order of precedence, the shipped `appsettings.json`, an `appsettings.json` in the current working directory, .NET User Secrets, and environment variables.

Because a global tool installs into a read-only store, User Secrets or environment variables are the practical choice:

```powershell
$env:CasCap__GooglePhotosOptions__User = "your.email@example.com"
$env:CasCap__GooglePhotosOptions__ClientId = "000000000000-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.apps.googleusercontent.com"
$env:CasCap__GooglePhotosOptions__ClientSecret = "your-client-secret"
```

Or an `appsettings.json` beside where you run the tool:

```json
{
  "CasCap": {
    "GooglePhotosOptions": {
      "User": "your.email@example.com",
      "ClientId": "000000000000-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.apps.googleusercontent.com",
      "ClientSecret": "your-client-secret",
      "Scopes": [
        "AppendOnly",
        "ReadOnlyAppCreatedData",
        "EditAppCreatedData"
      ]
    }
  }
}
```

| Scope | Needed for |
| --- | --- |
| `AppendOnly` | `mediaitems upload`, `albums add` |
| `ReadOnlyAppCreatedData` | `albums list`, `albums download`, `mediaitems list` |
| `EditAppCreatedData` | Adding uploaded media items to albums |

Never commit a client secret. The OAuth grant itself is cached by `Google.Apis.Auth` under `%APPDATA%/googlephotos/auth` (or the platform equivalent) and can be cleared with `googlephotos logout`.

## Usage

Every command has context-sensitive help:

```powershell
googlephotos --help
googlephotos albums --help
```

### Albums

```powershell
# list the albums this tool created
googlephotos albums list

# show only albums which share a title with another album
googlephotos albums list --duplicates

# create an empty album
googlephotos albums add -t "my album title"

# download an album's media items
googlephotos albums download -t "my album title" -o ./download

# download resized, cropped, EXIF-preserving copies
googlephotos albums download -t "my album title" -o ./download --maxheight 100 --crop --exif --overwrite
```

### Media items

```powershell
# list the media items this tool created
googlephotos mediaitems list

# upload a folder tree
googlephotos mediaitems upload -s ./photos

# upload only JPEGs, into a named album, without prompting
googlephotos mediaitems upload -s ./photos --pattern *.jpg -t "holiday 2026" -y

# upload a folder tree, creating one album per sub-folder
googlephotos mediaitems upload -s ./photos --hierarchy
```

### Sign out

```powershell
googlephotos logout
```

## Development

```powershell
dotnet build CasCap.GooglePhotosCli.Debug.slnx
dotnet test src/CasCap.GooglePhotosCli.Tests/CasCap.GooglePhotosCli.Tests.csproj
```

The Debug solution resolves `CasCap.Api.GooglePhotos` through a local project reference, so it expects that repository to be cloned alongside this one. The Release solution uses the published NuGet package.

## Feedback and issues

Please raise anything on the [GitHub issues page](https://github.com/f2calv/CasCap.GooglePhotosCli/issues).

## License

CasCap.GooglePhotosCli is Copyright &copy; 2020 [@f2calv](https://github.com/f2calv) under the [MIT license](LICENSE).
