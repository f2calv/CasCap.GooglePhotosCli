# Copilot Instructions

## Shared Instructions

Shared Copilot instructions, skills and prompts are maintained centrally in the [.github](https://github.com/f2calv/.github) repository, under `.github/instructions/`, `.github/skills/` and `.github/prompts/`. They are deliberately not copied into this repository, so a change there takes effect everywhere without a pull request here.

To load them, clone that repository and either add it to this VS Code workspace, or link its folders into `~/.copilot/`. Its README explains both.

If those shared files are not visible, stop and tell the user rather than guessing the conventions — this repository depends on them.

Everything below is specific to this repository.

## Google Photos API Scope Boundary

The Library API can only see albums and media items created by this tool's own OAuth client, and
existing user media is reachable only through the Picker API. That constrains what the CLI may
claim to do:

- Never add, restore, or document a command which implies whole-library access, for example
  library-wide duplicate detection, "backup everything", or listing every media item in an account.
- Commands which enumerate albums or media items must state, in both their help text and the
  README, that results are limited to content this tool created.
- Any future selective-download feature must be built on `GooglePhotosPickerService`, never on
  Library API search or paging.

## Credential Handling

- Credentials are supplied through `appsettings.json`, .NET User Secrets, or environment variables
  and bound to `GooglePhotosOptions`, exactly as any other library consumer would.
- The Google authentication cache is owned by `Google.Apis.Auth` and lives outside the repository.
