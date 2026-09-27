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

## Known baseline debt

- The solution builds with 59 compiler/analyzer warnings.
- The test suite has 47 failures and 335 passes.
- `WriterApp.Client` has 28 moderate npm advisories in TipTap 2.x. The fix requires migrating the editor and all TipTap extensions to 3.31.3 or newer.
- `docs-site` has 22 npm advisories (21 moderate and 1 high) in the current Docusaurus dependency tree. Docusaurus 3.10.2 is the latest published release and does not yet resolve them.
- The active Git tree is clean of the accidentally committed nested repository and generated artifacts, but those blobs remain in Git history. A coordinated history rewrite and force-push is required to reduce existing clone size.
