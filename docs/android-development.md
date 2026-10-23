# Prosa for Android

The Android host in `WriterApp.Android` targets .NET 10 MAUI Blazor Hybrid and Android API 26 or later. Its package is `com.prosa.writer.android`. Documents, projects, editor, planning, AI review, Brainstorm, recovery and synchronization use the existing `WriterApp.Device.Shared`, `WriterApp.UI.Shared`, `WriterApp.Shared` and publishing projects.

## Build and run

From the repository root, run `.\android.ps1` to start the installed emulator and wait for Android to boot. It reuses an already running instance and preserves device data. The launcher restores `adb reverse tcp:5387 tcp:5387`, so Android's localhost reaches the PC development backend, and checks the server's health endpoint. Run `.\android.ps1 -OpenApp` to also open an installed Prosa app. Use `-Avd <name>` to select another virtual device, `-SdkPath <path>` for a custom Android SDK installation, `-BackendPort <port>` for a different local backend port, or `-SkipBackendForwarding` for a build using a remote HTTPS backend. The launcher does not start the backend server.

Install .NET SDK specified by `global.json`, the `maui-android` workload, JDK 21, Android SDK platform 36 and compatible build tools. Android is included in `WriterApp.Device.sln`. Build the Android project directly when other solution hosts are unavailable.

```powershell
dotnet build WriterApp.Android/WriterApp.Android.csproj -c Debug -p:EmbedAssembliesIntoApk=true
adb devices -l
adb -s emulator-5554 install --no-incremental -r WriterApp.Android/bin/Debug/net10.0-android/com.prosa.writer.android-Signed.apk
adb -s emulator-5554 shell monkey -p com.prosa.writer.android -c android.intent.category.LAUNCHER 1
```

Pass `-p:AndroidSdkDirectory=...` and `-p:JavaSdkDirectory=...` when discovery differs from your installation. For an x64 emulator, `-p:RuntimeIdentifier=android-x64` creates an APK under the additional `android-x64` directory. ARM phones require an ARM-capable build; the default Debug package includes supported architectures.

Debug defaults to Development and `http://localhost:5387/`. Use `adb reverse tcp:5387 tcp:5387` for a backend on the development PC. This also works on a connected phone: the Android app's localhost is otherwise the device. Production defaults to `https://app.prosa-app.com/`; Staging requires an explicit `ProsaApiBaseUrl`. The shared environment validator permits HTTP only for Development loopback. Android network policy blocks arbitrary cleartext and permits loopback; user CAs are trusted only in debuggable builds. Offline writing works without an auth registration or backend.

## Native services

- File import uses Android's document provider for TXT, HTML and DOCX; PNG covers use the same provider. Streams are bounded to 5 MB and existing import/cover validation remains authoritative. No broad storage permission is requested.
- Save uses `ACTION_CREATE_DOCUMENT`; success is reported after the provider stream has been written and flushed. Provider errors/cancellation retain local writing. Provider-side atomicity is outside the app's control.
- Share export creates an immutable app-cache file and opens Android's share sheet. It does not report a recipient send as completed. Old export cache directories are reclaimed after seven days on the next share. PDF uses the system print path; its saved file can subsequently be shared through Android Files.
- PDF opens the Android print dialog with an immutable, script-disabled offline HTML snapshot. Choose **Save as PDF** or a printer explicitly. The platform print renderer preserves searchable text and print layout; remote resources are blocked. The app retains the dedicated WebView while the job is active and reports completed/cancelled/failed jobs separately.
- Browser links use the system browser and HTTPS. Native tokens are not passed to billing/help URLs; a browser may require its own sign-in.
- Microphone capture requests permission only on Start and produces bounded mono 16 kHz PCM16 WAV for the existing transcription contract. Capture ends on cancellation/background and audio is transient. Speech output selects installed offline English/Swedish voices. Missing voices/permissions have actionable text fallbacks.
- Back first closes the soft keyboard. Subsequent Back awaits the current save before returning to the library; from the library it backgrounds the task. Backgrounding flushes writing and stops voice/capture. Per-environment route preferences restore an existing, active local document after process recreation. Existing atomic local saves and per-edit recovery journals protect committed/recoverable writing. Abrupt termination cannot guarantee preservation of a keystroke that has not yet reached the native app.
- Android cloud/AI features use the same entitlement, ownership and checked-approval contracts as the other device hosts. Unconfigured identity disables online actions while retaining local writing.

## Authentication

For local Debug testing, the existing Prosa native public client can also serve Android
after its exact installed certificate callback is registered. The setup helper verifies
the APK signer, existing delegated permission, API preauthorization and user-flow link:

```powershell
# Reads the tenant and prepares local public settings; does not change registration.
pwsh -File scripts/android/Configure-ProsaAndroidIdentity.ps1
# Explicit administrator action: add the exact callback while preserving existing redirects.
pwsh -File scripts/android/Configure-ProsaAndroidIdentity.ps1 -RegisterCallback
```

Run this with the existing customer-tenant administrator Azure session. It writes
ignored `WriterApp.Android/native-auth.local.json` and `native-auth.local.props`.
Normal Debug builds import those settings automatically; rebuild and reinstall with
`adb install -r` to preserve local writing. Release builds do not import these
machine-specific Debug settings. Reconfigure after changing the Debug signer.
Without the tenant callback, local settings enable the button but the identity provider
will reject the return URI. The existing public client's native API allowlist and user
flow remain applicable; a separate production Android client needs its own approved
permission, flow association and backend allowlist entry.

Use an approved Android public-client registration with delegated API permission. Supply public settings via `ProsaAndroidAuthSettingsFile`, and the **installed app signing certificate's** Base64 SHA-1 digest via `ProsaAndroidSignatureHash`.

```json
{
  "TenantId": "approved-tenant-guid",
  "Authority": "https://your-tenant.ciamlogin.com/",
  "ClientId": "approved-android-client-guid",
  "RedirectUri": "msauth://com.prosa.writer.android/URL-ENCODED-BASE64-SHA1",
  "Scopes": ["api://approved-api/access_as_user"]
}
```

These example placeholders are intentionally invalid. The backend must allow the approved native client ID and API scope/audience. Configure external registrations through their approved management process; adding the host does not change them automatically.

The manifest callback path and embedded identity configuration must contain the same digest. The host checks the installed certificate before interactive sign-in. MSAL uses the system browser with PKCE and its native encrypted cache; selected account identity uses MAUI SecureStorage. Silent restoration requires the exact selected account for this backend/tenant/client/scope combination. Cancellation, wrong tenant/account, expiry and sign-out cannot select an unrelated cached identity. Android backups are disabled to avoid restoring writing/credential metadata under incompatible secure-storage keys. Credentials never go into document storage or diagnostics.

Debug, direct release and Google Play signing certificates can differ. For Play app signing use the **Play app signing certificate** for the callback, and the **upload key** for AAB signing. Local sideloads signed with the upload key cannot use that Play-bound callback. Certificate rotation needs matching registration/configuration review.

## Build and distribution automation

`android-build.yml` builds Debug APKs on pull requests affecting the shared/device implementation and on manual dispatch. Download `prosa-android-debug` from the workflow artifacts for emulator/internal review.

`android-release.yml` is manually dispatched against the protected `android-release` environment. It creates downloadable signed packages and a hash receipt; it does not upload to Google Play or authorize a public release.

Environment variables:

| Variable | Meaning |
|---|---|
| `ANDROID_API_BASE_URL` | Approved reachable HTTPS backend |
| `ANDROID_NATIVE_AUTH_JSON` | Public native auth settings above |
| `ANDROID_CALLBACK_SIGNATURE_HASH` | Installed certificate's Base64 SHA-1 |
| `ANDROID_KEY_ALIAS` | Keystore alias |
| `ANDROID_BUILD_NUMBER_OFFSET` | Optional offset above prior uploaded version codes |

Secrets: `ANDROID_KEYSTORE_BASE64`, `ANDROID_STORE_PASSWORD`, `ANDROID_KEY_PASSWORD`. Do not commit signing keys or passwords. The packaging helper uses private temporary files and password-file references, verifies callback configuration and cleans temporary secrets on every exit. Direct mode requires callback/signing certificate equality and produces signed APK/AAB. Play mode produces AAB only, allowing an approved Play-signing callback distinct from the upload signer. Version code is run number plus offset; start a fresh workflow run after a successful upload rather than reusing a previously uploaded version code. Keep the same package/signer for an in-place upgrade and preserve app data. Debug-to-release signer changes require a separate test installation strategy.

After acceptance, download the signed AAB and upload it to the appropriate Play Console internal test track, or distribute the signed direct APK to approved testers. Store enrollment, publication, external identity configuration and a stable signing key remain owner configuration tasks.

## Acceptance

See [Android UAT](android-uat.md). Execute on an emulator first, then representative physical phones/tablets.

```powershell
python scripts/android/test_package.py
# Use isolated BaseOutputPath if a running backend locks its normal output.
dotnet test Tests/WriterApp.Tests/WriterApp.Tests.csproj --filter FullyQualifiedName~AndroidHostTests
$env:PROSA_ADB = 'path/to/adb.exe'
$env:PROSA_ANDROID_SERIAL = 'emulator-5554'
node Tests/android-native-smoke.mjs inspect
node Tests/android-native-smoke.mjs create
node Tests/android-native-smoke.mjs write
```

The smoke script operates only a synthetic document created in the emulator and captures evidence under `artifacts/android-uat`. Do not run destructive lifecycle checks on an author's active writing. `write` replaces the selected test page's contents and force-stops the test app after background saving. The script does not clear data or send provider requests.

Debug builds also include `com.prosa.writer.android.NativeAcceptance` instrumentation. With microphone permission granted on the test emulator, run `adb shell am instrument -w com.prosa.writer.android/com.prosa.writer.android.NativeAcceptance`. It checks bounded native audio capture and installed voice availability without transcription/provider requests. It is excluded from Release. Emulator audio is synthetic and does not establish physical microphone/playback quality.

Architecture/API references: [MSAL Android configuration](https://learn.microsoft.com/en-us/entra/identity-platform/tutorial-mobile-app-android-prepare-app), [MAUI authentication](https://learn.microsoft.com/en-us/dotnet/maui/data-cloud/authentication?view=net-maui-10.0), [Android printing](https://developer.android.com/reference/android/print/PrintDocumentAdapter), [AudioRecord](https://developer.android.com/reference/android/media/AudioRecord), [TextToSpeech](https://developer.android.com/reference/android/speech/tts/TextToSpeech), [Android packaging](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli?view=net-maui-10.0).
