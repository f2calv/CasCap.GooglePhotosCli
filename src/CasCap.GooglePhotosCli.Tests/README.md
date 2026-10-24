# CasCap.GooglePhotosCli Tests

This .NET 10 test project checks the command surface of `googlephotos` and the console rendering of API responses with optional fields. Its source-declared cases are credential-free and contact no network; they do not verify live OAuth, transfers or the full application host.

## Tests

| Test method | Method count | Test case count | Category |
| --- | --- | --- | --- |
| `RootCommand_ExposesExpectedSubcommands` | 1 | 1 | Parsing |
| `AlbumsCommand_ExposesExpectedSubcommands` | 1 | 1 | Parsing |
| `MediaItemsCommand_ExposesExpectedSubcommands` | 1 | 1 | Parsing |
| `WholeLibraryCommands_AreNotExposed` | 1 | 3 | Parsing |
| `UnknownCommand_Throws` | 1 | 1 | Parsing |
| `Command_ExposesExpectedOptions` | 1 | 4 | Parsing |
| `AlbumTable_RendersAlbumWithoutMediaItemCount` | 1 | 1 | Rendering |
| `AlbumTable_RendersAlbumWithoutTitle` | 1 | 1 | Rendering |
| `AlbumTable_RendersHeadersWhenEmpty` | 1 | 1 | Rendering |
| **Total** | **9** | **14** | **Parsing, Rendering** |

`WholeLibraryCommands_AreNotExposed` guards a product decision rather than an implementation detail. Google's API change of 31 March 2025 removed whole-library access, so `sync`, `albums sync` and `mediaitems duplicates` must stay withdrawn rather than return misleading partial results.

The definitions are [CommandParsingTests](Tests/Unit/CommandParsingTests.cs) (6 methods, 11 cases, `Parsing`) and [AlbumTableTests](Tests/Unit/AlbumTableTests.cs) (3 methods, 3 cases, `Rendering`). Parsing uses dummy options and a null console, not browser consent or a live Google client. Rendering uses in-memory album objects. These are declared counts, not results from a test run.

## Skipped Tests

There are no declared skipped tests, dynamic skip branches or credential-based skips: **0 skipped cases**. All declared categories are `Parsing` and `Rendering`; none is an integration category.

## Test Layout

```text
Tests/
└── Unit/
    ├── AlbumTableTests.cs
    └── CommandParsingTests.cs
```

No `Integration/` folder exists because the project contains only offline checks.

## Prerequisites and Execution

Use the .NET 10 SDK and Microsoft.Testing.Platform selected by [global.json](../../global.json). Run from the repository root after choosing the configuration; Release uses the published dependency, while Debug requires the sibling library checkout described in the [source README](../CasCap.GooglePhotosCli/README.md).

```powershell
dotnet test --project src/CasCap.GooglePhotosCli.Tests/CasCap.GooglePhotosCli.Tests.csproj -c Release
```

`global.json` selects Microsoft.Testing.Platform, so a single method can be run directly:

```powershell
dotnet test --project ./src/CasCap.GooglePhotosCli.Tests/CasCap.GooglePhotosCli.Tests.csproj -c Release --filter-method CasCap.GooglePhotosCli.Tests.CommandParsingTests.UnknownCommand_Throws --show-live-output on
```

## Dependencies

| NuGet package                               | Purpose                            |
| ------------------------------------------- | ---------------------------------- |
| `xunit.v3`                                  | Test framework and MTP integration |
| `Microsoft.Testing.Extensions.CodeCoverage` | Code coverage collection           |

| Project reference        | Purpose             |
| ------------------------ | ------------------- |
| `CasCap.GooglePhotosCli` | Tool under test     |

See the [consumer README](../../README.md) for supported commands and API restrictions.
