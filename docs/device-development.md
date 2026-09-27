# Device application development

Prosa's Windows and iOS applications use .NET MAUI Blazor Hybrid. Each application is a native host around a shared Razor UI, so ordinary editing and local file persistence can run on the device while authentication, paid-account synchronization, and AI operations use the existing backend.

## Projects

- `WriterApp.Device.Shared` contains reusable Razor UI, the configured backend `HttpClient`, and the device-local document store. It targets plain `net10.0`, so the main Linux CI workflow can compile and test this layer.
- `WriterApp.Desktop` is the Windows MAUI host. Its files stay in the app data directory and its unpackaged development build targets Windows 10 version 1809 or later.
- `WriterApp.iOS` is the iPhone and iPad MAUI host. It targets iOS 15 or later.
- `WriterApp.Shared` remains the home for contracts shared with the existing web client and server.

`BlazorApp.sln` includes the cross-platform shared device project. `WriterApp.Device.sln` groups the shared library and both platform hosts for device development. The platform hosts are kept out of the server solution because GitHub's Linux runner cannot build the Windows and iOS workloads.

## Build and run

Restore and verify the cross-platform layer on any supported development system:

```powershell
dotnet restore BlazorApp.sln
dotnet build BlazorApp.sln --configuration Release --no-restore
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --configuration Release --no-build
```

Build and run the Windows host on Windows:

```powershell
dotnet restore WriterApp.Desktop/WriterApp.Desktop.csproj
dotnet build WriterApp.Desktop/WriterApp.Desktop.csproj --configuration Debug --no-restore
dotnet run --project WriterApp.Desktop/WriterApp.Desktop.csproj --framework net10.0-windows10.0.19041.0
```

Restore the iOS project on Windows or macOS. Compiling, running, signing, and publishing it requires a Mac with a compatible Xcode installation; Visual Studio on Windows can use a paired Mac:

```powershell
dotnet restore WriterApp.iOS/WriterApp.iOS.csproj
dotnet build WriterApp.iOS/WriterApp.iOS.csproj --configuration Debug
```

## Backend and local data

Both hosts use `https://app.prosa-app.com/` by default. Set `WRITERAPP_API_BASE_URL` to an absolute URL before launching a host to use another environment. Keep tokens and secrets out of repository configuration.

The local store writes one JSON file per document beneath `FileSystem.AppDataDirectory/documents`. Writes use a temporary file followed by an atomic replacement. The `ServerVersion` field is reserved for synchronization and conflict detection.

## Remaining implementation

The scaffold establishes native startup, shared routing, backend configuration, and tested local persistence. Product functionality still requires:

1. Extracting the editor surface and reusable components from `WriterApp.Client` into a shared Razor library without depending on browser-only APIs.
2. Packaging the TipTap bundle for each MAUI WebView and adding device-safe import, export, and autosave flows.
3. Implementing an OAuth/OIDC sign-in flow with platform callbacks and secure token storage in MAUI `SecureStorage`.
4. Defining backend synchronization endpoints and entitlements for paying customers, then implementing an offline queue, server version checks, deletion markers, and a visible conflict-resolution flow.
5. Adding Windows packaging/signing and Apple bundle identifiers, provisioning profiles, capabilities, privacy declarations, and App Store metadata.
6. Adding platform CI runners once signing credentials and Apple build infrastructure are available.

Changes limited to these device projects do not match the Azure landing-site workflow's `Prosa.Landing/**` path filter and therefore do not trigger that deployment.
