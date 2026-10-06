# Windows desktop beta packaging

The Windows host uses a signed MSIX for private beta distribution. The normal Debug build stays unpackaged. A Release build defaults to the production API at `https://app.prosa-app.com/`; changing the backend for a release requires an explicit build property. No endpoint configuration contains credentials.

## Build and identity

On a Windows machine with the .NET 10 SDK and `maui-windows` workload:

```powershell
dotnet restore WriterApp.Desktop/WriterApp.Desktop.csproj
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --configuration Release --framework net10.0-windows10.0.19041.0 -warnaserror
./scripts/windows/Build-ProsaTestMsix.ps1
```

The script creates a temporary `CN=Prosa Development` test certificate and a signed x64 MSIX under `artifacts/windows-msix`. It temporarily imports the generated signing key into `Cert:\CurrentUser\My`, signs by thumbprint, then removes that store entry in a `finally` block. It does not establish trust or install the package. The PFX is private, is ignored by Git, and must never be shared. The current SDK uses `WindowsPackageType=MSIX`; `Package` is rejected by its targets. Publishing treats warnings as errors and discovers the installed VS x64 `mspdbcmf.exe` through `vswhere` for package symbol generation.

Installation needs an approved trusted signing identity. On this test machine, importing the public `.cer` into CurrentUser/TrustedPeople was insufficient (`0x800B0109`) and that entry was removed. Importing a test certificate into **LocalMachine/TrustedPeople** changes persistent machine-wide trust and must be explicitly approved by the machine owner, with the exact certificate verified and a cleanup plan. Once trust is configured, install the app MSIX and its matching architecture dependencies from the generated `Dependencies` folder, using Windows App Installer or `Add-AppxPackage -Path <app.msix> -DependencyPath <dependency.msix>`. Do not assume dependency packages already exist on a clean test machine.

These temporary certificates are for local review only. The [Windows CI workflow](../.github/workflows/desktop-windows.yml) generates a new certificate per run, uploads the MSIX/appx packages (including framework dependencies) and public `.cer`, and does not publish an installer to customers. The [Prompt 12 report](release-1-uat.md) records the successfully built package and its hash, the blocked installation, and the remaining release gates.

The package name is `Prosa.WriterApp.Desktop`; the beta publisher placeholder is `CN=Prosa Development`. The app's display version is `0.1.0` and the MSIX identity version is `0.1.0.1`. Before distributing a long-lived beta or production build, choose the final publisher subject and a trusted signing certificate, keep the signing key outside Git, and build with that stable identity. A different package name or publisher creates a different package family and **will not upgrade this test package or reuse its local data**. Re-signing the same package family with an unrelated temporary certificate is also unsuitable for an upgrade test. Keep the approved signing identity for the whole release channel and increase the MSIX version for each update. Update `ApplicationDisplayVersion`, `ApplicationVersion`, and `Package.appxmanifest` together; the manifest's four-part version must increase. The MAUI icons live in `WriterApp.Desktop/Resources/AppIcon/` and are already included in the package.

For staging, pass the backend URL at build time and keep the update feed unset unless a staging feed is available:

```powershell
dotnet publish WriterApp.Desktop/WriterApp.Desktop.csproj --configuration Release --framework net10.0-windows10.0.19041.0 `
  -p:RuntimeIdentifierOverride=win-x64 -p:WindowsPackageType=MSIX `
  -p:ProsaEnvironment=Staging -p:ProsaApiBaseUrl=https://your-staging-host.example/
```

That command illustrates environment selection; a distributable package also needs an appropriate trusted signing certificate. The test-signing script defaults to **Production** and accepts explicit `-Environment`, `-ApiBaseUrl`, `-NoRestore` and Development-only `-ValidationDataDirectory` parameters. It isolates build output beneath the requested output directory. Current Debug defaults are `Development` at `http://localhost:5387/`; `WRITERAPP_API_BASE_URL` remains a Debug-only override. Release uses embedded build properties. A staging build without `ProsaApiBaseUrl` fails, and non-local HTTP backends are rejected.

## Updates and local data

The app's **Updates and diagnostics** panel checks the configured HTTPS manifest only when the user clicks **Check for updates**. It opens a same-host HTTPS installer URL only after the user clicks **Open installer page**. It does not auto-download or silently install. Production expects `https://app.prosa-app.com/desktop/updates/stable.json`, which must be published separately before update checks can succeed. Its JSON shape is:

```json
{ "version": "0.2.0", "downloadUrl": "https://app.prosa-app.com/desktop/downloads/Prosa-0.2.0.msix" }
```

Host the signed installer on that host and use a higher semantic version. The manifest and download must remain HTTPS on the configured backend host. A missing or malformed feed shows a safe unavailable message. Installation and certificate trust remain Windows decisions.

The document store, recovery journals, sync queue, and diagnostics use `FileSystem.AppDataDirectory`, with writing in its `documents` subdirectory. For a packaged Windows app this maps to its per-user package data (typically `%LocalAppData%\Packages\<package-family-name>\LocalState`). **An in-place MSIX upgrade of the same package family is intended to preserve this data.** A package uninstall or app reset can remove the package's local data. Before either action, export important writing and back up the entire app data directory while Prosa is closed, including recovery, backup, and sync journal files. An exported document is a portable writing copy, not a full sync-state backup. Paid cloud copies are useful but may lag unsynced edits; check sync status first. Preserve the original data directory when diagnosing an upgrade problem.

## Diagnostics

The panel exports JSON-lines logs via a native Save dialog. They are stored under `FileSystem.AppDataDirectory/diagnostics`, capped at 512 KiB for the active file plus three rotated archives. Entries contain timestamp, event name, app/runtime version, environment, coarse error class, and sync counts/status. They have no free-text message field, request headers, tokens, or document content. The exported file is still user data; inspect it before sharing. Update checks use an unauthenticated client with redirects and cookies disabled.

## Release validation on a Windows test machine

1. Install a package whose certificate is trusted, launch it, create writing, and close/reopen.
2. Build a **higher** MSIX version with the **same package name and publisher/signing identity**. Install over the previous build and confirm writing, recovery files, and diagnostics remain accessible.
3. Confirm the installed Production build contacts the production backend only. Exercise staging with a separately configured test build.
4. Check update behavior with an available, missing, and malformed manifest. Confirm no installer opens without a click.
5. Export diagnostics after a save/sync error and inspect them for secrets and writing content.
6. Export/back up writing, uninstall, and confirm the documented local-data behavior on the target Windows version.

These installation, upgrade, and uninstall checks require a real signed package and a Windows test machine. They remain open in the [Prompt 12 UAT checklist](release-1-uat.md); a successful package build is not an installation pass. Microsoft documents the [MAUI MSIX CLI process](https://learn.microsoft.com/en-us/dotnet/maui/windows/deployment/publish-cli?view=net-maui-10.0), [package identity and update constraints](https://learn.microsoft.com/en-us/windows/msix/app-package-updates), and [MSIX local data behavior](https://learn.microsoft.com/en-us/windows/msix/msix-containerization-overview).

## Desktop AI prompt 23 package verification (2026-10-04)

The current local review package is `artifacts/desktopai-p23/msix/WriterApp.Desktop_0.1.0.1_x64_Test/WriterApp.Desktop_0.1.0.1_x64.msix`, SHA256 `D4058CC79528376152CF80090CD941D9B0A7AC2E9A232BC8954BFDBD0B9E1A14`. Its actual manifest identifies `Prosa.WriterApp.Desktop`, publisher `CN=Prosa Development`, x64, version `0.1.0.1`, executable `WriterApp.Desktop.exe`, and Windows App Runtime 1.7 dependency. Matching architecture dependency packages are in its `Dependencies` directories. This is an isolated **Development** review artifact, not a Production release.

`Inspect-ProsaMsix.ps1` opens the real package, compares all 247 file block-map hashes, verifies the CMS signature without trusting its certificate, checks publisher/signing subject agreement and reads assembly metadata directly from the packaged DLL. Embedded metadata confirms Development, `http://127.0.0.1:5390/`, an absolute isolated `artifacts/desktopai-p22/acceptance-data-…/native` directory, and no update feed. The report records full paths, hashes, identity and the exact data directory. The packaged DLL SHA256 is `BCB9376FA5AC189318423876BC477AA1D330EFCB310CB5E8663DFAA5266BD066`.

Reproduce after preparing the prompt-22 normal fixture:

```powershell
$fixture = Get-Content artifacts/desktopai-p22/acceptance-fixture.json -Raw | ConvertFrom-Json
./scripts/windows/Build-ProsaTestMsix.ps1 -NoRestore -OutputDirectory artifacts/desktopai-p23/msix `
  -Environment Development -ApiBaseUrl http://127.0.0.1:5390/ -ValidationDataDirectory (Join-Path $fixture.root 'native')
./scripts/windows/Inspect-ProsaMsix.ps1 `
  -PackagePath artifacts/desktopai-p23/msix/WriterApp.Desktop_0.1.0.1_x64_Test/WriterApp.Desktop_0.1.0.1_x64.msix `
  -ReportPath artifacts/desktopai-p23/msix-inspection.json
```

Windows `signtool verify /pa /all /v` exits 1 because the self-signed root is untrusted; the review artifact has no timestamp. No Prosa package is installed and no desktop process was launched. The exact temporary signing key was removed from CurrentUser/My; its certificate is absent from both checked TrustedPeople stores. No machine trust was changed. Package integrity/CMS checks therefore pass independently of **trusted installation, actual native launch, clean install, same-family higher-version upgrade and distribution**, which remain open. Use an approved stable signing identity/test channel before those checks; preserve and back up isolated prior-version data, then inspect installed package identity, launched EXE, embedded backend and active storage path. Do not use this fresh temporary signer as evidence of an upgradeable release channel.

Production-store tests now cover document versions 1–3, guide migration, interruption/retry and retained queued/private/recovery data. Those tests do not replace installed upgrade verification. Current native and iOS prerequisites and their independent statuses are in [the Desktop AI release checklist](desktopai-release-checklist.md).
