---
title: CasCap.GooglePhotosCli Tests
description: Test structure, coverage and execution commands for the Google Photos CLI.
---

# CasCap.GooglePhotosCli Tests

## Purpose

This project pins the command surface the `googlephotos` tool exposes. The tests are credential-free and contact no network; they build the command model through dependency injection with synthetic OAuth values and assert what the parser accepts.

## Tests

| Test method                                    | Method count | Test case count | Category |
| ---------------------------------------------- | ------------ | --------------- | -------- |
| `RootCommand_ExposesExpectedSubcommands`       | 1            | 1               | Parsing  |
| `AlbumsCommand_ExposesExpectedSubcommands`     | 1            | 1               | Parsing  |
| `MediaItemsCommand_ExposesExpectedSubcommands` | 1            | 1               | Parsing  |
| `WholeLibraryCommands_AreNotExposed`           | 1            | 3               | Parsing  |
| `UnknownCommand_Throws`                        | 1            | 1               | Parsing  |
| `Command_ExposesExpectedOptions`               | 1            | 4               | Parsing  |

`WholeLibraryCommands_AreNotExposed` guards a product decision rather than an implementation detail. Google's API change of 31 March 2025 removed whole-library access, so `sync`, `albums sync` and `mediaitems duplicates` must stay withdrawn rather than return misleading partial results.

## Running the tests

```powershell
dotnet test src/CasCap.GooglePhotosCli.Tests/CasCap.GooglePhotosCli.Tests.csproj
```

`global.json` selects Microsoft.Testing.Platform, so a single method can be run directly:

```powershell
dotnet test --project ./src/CasCap.GooglePhotosCli.Tests/CasCap.GooglePhotosCli.Tests.csproj --filter-method CasCap.GooglePhotosCli.Tests.CommandParsingTests.UnknownCommand_Throws --show-live-output on
```

## Dependencies

| NuGet package                               | Purpose                            |
| ------------------------------------------- | ---------------------------------- |
| `xunit.v3`                                  | Test framework and MTP integration |
| `Microsoft.Testing.Extensions.CodeCoverage` | Code coverage collection           |

| Project reference        | Purpose             |
| ------------------------ | ------------------- |
| `CasCap.GooglePhotosCli` | Tool under test     |
