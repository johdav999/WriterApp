# Development setup

## Prerequisites

- .NET SDK 10.0.4xx. `global.json` selects the latest installed 10.0.4xx patch.
- .NET MAUI Windows and iOS workloads when developing the device hosts. Building and signing iOS still requires a paired Mac with a compatible Xcode installation.
- Node.js 20 or newer.
- npm.

All existing projects target .NET 10 LTS. Keep new shared, Windows, and iOS projects on .NET 10 so application hosts can reference `WriterApp.Shared` without cross-version compatibility work.

## Initial setup

1. Copy `appsettings.Development.example.json` to `appsettings.Development.json`.
2. Keep local secrets out of JSON. Use .NET user secrets or environment variables.
3. Install and build the TipTap assets:

   ```powershell
   npm ci --prefix WriterApp.Client
   npm run build --prefix WriterApp.Client
   ```

4. Restore, build, and test the solution:

   ```powershell
   dotnet tool restore
   dotnet restore BlazorApp.sln
   dotnet build BlazorApp.sln --configuration Release --no-restore
   dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --configuration Release --no-build
   ```

5. Run the server and hosted WebAssembly client:

   ```powershell
   dotnet run --project BlazorApp.csproj
   ```

## Local data

SQLite databases, `App_Data`, logs, deployment archives, publish output, and JavaScript build output are local artifacts and are ignored by Git. Do not place credentials in tracked configuration files.

## CI baseline

`.github/workflows/ci.yml` builds the TipTap bundle, restores and builds the solution, runs the test project, and checks the authentication navigation guardrail on pushes and pull requests to `main`.

The test step is blocking: any test failure fails the workflow.

## Verified baseline

- The solution builds without compiler or analyzer warnings.
- The test suite passes all 382 tests.
- `WriterApp.Client` uses TipTap 3.31.3 and has no production npm advisories.
- `docs-site` uses Docusaurus 3.10.2, including the matching `@docusaurus/faster` package required by its v4 compatibility mode. The site type-checks and builds successfully. Its dependency tree still reports 22 upstream advisories (21 moderate and 1 high); Docusaurus 3.10.2 is the latest published release and does not yet provide compatible patched transitive versions.
- Git history was rewritten on 2026-09-27 to remove the accidentally committed nested repository, publish output, build output, logs, local databases, and deployment archives. Existing clones must fetch the rewritten branches and rebase or clone again before pushing.
