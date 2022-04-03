# Google Photos CLI (a work in progress)

## _Unofficial_ Google Photos Command Line Interface

[azdo-badge]: https://dev.azure.com/f2calv/github/_apis/build/status/f2calv.CasCap.GooglePhotosCli?branchName=master
[azdo-url]: https://dev.azure.com/f2calv/github/_build/latest?definitionId=11&branchName=master
[azdo-coverage-url]: https://img.shields.io/azure-devops/coverage/f2calv/github/11
[cascap.apis.googlephotoscli-badge]: https://img.shields.io/nuget/v/googlephotos?color=blue
[cascap.apis.googlephotoscli-url]: https://nuget.org/packages/googlephotos

![CI](https://github.com/f2calv/CasCap.GooglePhotosCli/actions/workflows/ci.yml/badge.svg) [![Coverage Status](https://coveralls.io/repos/github/f2calv/CasCap.GooglePhotosCli/badge.svg?branch=main)](https://coveralls.io/github/f2calv/CasCap.GooglePhotosCli?branch=main) [![SonarCloud Coverage](https://sonarcloud.io/api/project_badges/measure?project=f2calv_CasCap.GooglePhotosCli&metric=code_smells)](https://sonarcloud.io/component_measures/metric/code_smells/list?id=f2calv_CasCap.GooglePhotosCli) [![Nuget][cascap.apis.googlephotoscli-badge]][cascap.apis.googlephotoscli-url]

This is an _unofficial_ Google Photos CLI which can be installed as a .NET Global Tool

Google Photos CLI is an _unofficial_ utility which leverages the [CasCap.Apis.GooglePhotos](https://github.com/f2calv/CasCap.Apis.GooglePhotos) library to perform common and helpful operations against the media items held in your Google Photos account.

Key functionality;

- Media item upload
- Media item download/backup
- Media item duplicate detection

## Installation/Set-up

The Google Photos CLI is distributed as a [.NET Core Global Tool](https://docs.microsoft.com/en-us/dotnet/core/tools/global-tools), to install the tool follow these steps;

- Follow [these instructions](https://github.com/f2calv/CasCap.Apis.GooglePhotos#google-photos-api-set-up) to set-up OAuth login details.
- Download and install either the [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1) or [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0).
- From a command line shell install the tool `dotnet tool update --global googlephotos`

Now check the tool is installed by entering `googlephotos` at a shell.

## Usage

Use the context-sensitive help command to discover additional functionality/arguments;

- `googlephotos --help`

The tool will download and cache album and media item _metadata_ locally for speed during duplicate detection. This local cache data is stored in your user profile. If you use the `logout` command this local cache will be deleted;

- `googlephotos logout`

### Albums

Show context-senstive help for the albums sub-command;

- `googlephotos albums --help`

List all albums;

- `googlephotos albums list`

Show albums with duplicate names;

- `googlephotos albums list --duplicates`

Add/create a new empty album with a title of 'my album title';

- `googlephotos albums add -t "my album title"`

Download media items from specified album title into a folder;

- `googlephotos albums download -t "my album title" -o c:/temp/download`

Download media items from specified album title into a folder, thumbnails, cropped, with EXIF information (except location);

- `googlephotos albums download -t "my album title" -o c:/temp/download --maxwidth 100`
- `googlephotos albums download -t "my album title" -o c:/temp/download --maxheight 100`
- `googlephotos albums download -t "my album title" -o c:/temp/download --maxheight 100 --crop`
- `googlephotos albums download -t "my album title" -o c:/temp/download --maxheight 100 --crop --exif`
- `googlephotos albums download -t "my album title" -o c:/temp/download --maxheight 100 --crop --exif --overwrite`

Re-sync local album data with the latest data from the API;

- `googlephotos albums sync`

### MediaItems

Show context-senstive help for the media items sub-command;

- `googlephotos mediaitems --help`

List all media items (this could be a very long list!);

- `googlephotos mediaitems list`

Anaylse all meta data and search for possible duplicates;

- `googlephotos mediaitems duplicates`

Upload media items into your google photos account, with optional pattern;

- `googlephotos mediaitems upload -s C:/temp/fotos`
- `googlephotos mediaitems upload -s C:/temp/fotos --pattern *.jpg`

### Feedback/Issues

This CLI is a work in progress, I have started lots of features or left stubs in the code which I hope to eventually complete...
Please post any issues or feedback [here](https://github.com/f2calv/CasCap.GooglePhotosCli/issues).

### License

GooglePhotosCli is Copyright &copy; 2020 [@f2calv](https://github.com/f2calv) under the [MIT license](LICENSE).
