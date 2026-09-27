# Development setup

## Prerequisites

- .NET SDK 9.0.3xx. `global.json` selects the latest installed 9.0.3xx patch.
- Node.js 20 or newer.
- npm.

The current application targets .NET 9. Upgrade the solution to .NET 10 before starting the MAUI Windows and iOS host so all shared projects use a supported LTS release.

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

The test step currently reports failures without blocking the workflow because the pre-existing suite has 47 failing tests (335 passing as of 2026-09-27). Remove `continue-on-error` after those baseline failures are repaired.
