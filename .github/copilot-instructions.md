# Copilot Instructions

<!-- Synced section ------------------------------------------------------
	This file plus the shared files under `.github/instructions/` are kept
	aligned across f2calv .NET repositories. The repo-specific
	"Project-Specific Overrides" section below is excluded from sync.
	Edit once, sync everywhere.
	------------------------------------------------------------------- -->

## Instruction Files

Detailed conventions live in scoped instruction files under `.github/instructions/`, auto-applied by file type:

| File | Applies to | Covers |
| --- | --- | --- |
| `csharp.instructions.md` | `**/*.cs` | C# / .NET style, XML docs, logging, performance, Web API |
| `csharp.testing.instructions.md` | `**/*Tests/**/*.cs` | xUnit test structure, naming, theories, assertions |
| `dotnet.instructions.md` | `**/*.csproj`, `*.slnx`, `Directory.*.props` | Central build/package config, solution format, SDK selection |
| `github-actions.instructions.md` | workflows / `action.yml` | GitHub Actions naming, YAML, security, GitVersion |
| `bash.instructions.md` | `**/*.sh` | Bash scripting structure, error handling, logging, testability |
| `documentation.instructions.md` | `**/*.md` | README consistency and Mermaid diagrams |
| `configuration.instructions.md` | `**/appsettings*.json` | Options/appsettings synchronization and secret safety |

The conventions below always apply, regardless of the file being edited.

## Copilot Workflow

- **Test execution**: Never run tests automatically; they may be integration tests requiring Google Photos credentials and interactive authentication. Always prompt (ideally with a visual yes/no button) before running any tests.
- **Preserve git history during renames/moves**: When renaming or relocating files, first perform the rename/move (preferably via `git mv`), then make content edits to the file at its new path. Do not delete and recreate files when a rename or move is intended.
- **Multi-repo commits**: When a single change spans multiple repositories, separate per-repository commit messages are acceptable (but not mandatory). Prefer them where the changes are disconnected, or where one repository should not know about the other.
- **Build after refactoring**: After any refactoring, build the entire solution (not only the affected project) to catch compilation errors in dependent projects. When multiple solutions exist, prefer `CasCap.GooglePhotosCli.Debug.slnx`.

## Public Repository Confidentiality

- Treat every non-public repository's identity and contents as confidential, even when they appear in the local workspace, conversation context, diffs, logs, or tool output.
- Never publish private repository names, URLs, owner/repository coordinates, branches, file paths, architecture, deployment details, or inferred existence in tracked files, commit messages, issues, pull request titles/descriptions/reviews/comments, release notes, workflow annotations, examples, or other public-facing content.
- Describe required relationships generically (for example, "private GitOps repository" or "internal service") and supply private coordinates only through secrets, repository variables, or caller-provided values.
- Before creating or updating public GitHub content, review the proposed text and metadata for private identifiers and implementation details.

## Repository Structure

Every f2calv repository follows a consistent layout, regardless of language:

- Root files include `README.md`, `LICENSE`, `GitVersion.yml`, `.editorconfig`, `.gitattributes`, `.gitignore`, and `.pre-commit-config.yaml`.
- Source code lives under `src/`.
- Tooling lives in dot-prefixed folders such as `.github/`, `.scripts/`, `.devcontainer/`, `.docker/`, `.config/`, and `.vscode/`.
- Additional documentation beyond the root `README.md` lives as Markdown under `docs/`.
- `.editorconfig` is the source of truth for indentation, line endings, and analyzer or formatting rules.
- `GitVersion.yml` in the root drives semantic-versioning rules.

## Miscellaneous

- When detecting new conventions or patterns, add them to the appropriate `.github/instructions/*.instructions.md` file (or this file for cross-cutting workflow rules) and apply them retroactively where applicable.
- Keep this file and the shared `.github/instructions/` files aligned with the common guidelines used by sibling .NET repositories.

---

## Project-Specific Overrides

### Repository Purpose

This repository is a .NET global tool named `googlephotos` which wraps the
[CasCap.Api.GooglePhotos](https://github.com/f2calv/CasCap.Api.GooglePhotos) library in a
command-line interface. It is published to NuGet as the `googlephotos` package.

### Google Photos API Scope Boundary

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

### Credential Handling

- The tool must never prompt for, persist, or log an OAuth client id, client secret, access token,
  or refresh token in its own files. Credentials are supplied through `appsettings.json`, .NET User
  Secrets, or environment variables and bound to `GooglePhotosOptions`, exactly as any other
  library consumer would.
- The Google authentication cache is owned by `Google.Apis.Auth` and lives outside the repository.
- Never log a Google account identifier, a media item `ProductUrl` or `BaseUrl`, a full local file
  path, or a personally identifying filename.

### Console Output

- Console presentation is this tool's user interface and legitimately uses `IConsole`, tables and
  progress bars. This is the single exception to the `csharp.instructions.md` rule against writing
  to the console. It does not license `Console.WriteLine` or `Debug.WriteLine` for **diagnostics**,
  which must still flow through `ILogger<T>`.
- Never call `Debugger.Break()` in shipped code.
