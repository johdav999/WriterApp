# Prosa iPhone cloud build and TestFlight setup

Status: workflow prepared locally; no signed Mac build, Apple upload, TestFlight processing, or physical iPhone acceptance has been verified. Apple enrollment and an App Store Connect app record are not yet available (2026-10-09). Existing iOS managed compilation on Windows is not native distribution evidence.

Use `.github/workflows/ios-testflight.yml` to build the existing `WriterApp.iOS` host on a disposable GitHub-hosted macOS runner. The workflow is manually triggered and can either retain a signed package for review or validate/upload it to App Store Connect. It does not publish an App Store release or configure tester groups.

## What you need to do first

1. Enroll in the [Apple Developer Program](https://developer.apple.com/programs/enroll/) using the account or organization that will own Prosa. Complete payment, identity checks, and Apple's agreements yourself.
2. Register the explicit app identifier `com.prosa.writer.ios` with that Apple team. Keep this identifier: it is coupled to the iOS sign-in callback and Keychain groups already in the code. Configure the appropriate Keychain sharing capabilities, then create an App Store distribution provisioning profile for this identifier. The profile must permit `<AppIdentifierPrefix>.com.prosa.writer.ios` and `<AppIdentifierPrefix>.com.prosa.writer.ios.msal` (an authorized wildcard also covers them). App identifier prefix and team ID can differ on older accounts.
3. Create the iOS app record in App Store Connect with the same identifier. Add yourself to an internal TestFlight group. Enable automatic distribution to that group if wanted; an upload alone does not establish that a build is processed or available to install.
4. Create an Apple Distribution certificate with its private key and export a password-protected `.p12`. Download the App Store provisioning profile. The private key is essential: a downloaded `.cer` alone cannot sign an app.
5. Create a **team** App Store Connect API key with a role allowed to upload builds (Developer or an appropriate higher role) and access to the Prosa app. Download its `.p8` once; record its key ID and issuer ID.
6. Confirm the approved native identity registration/API scopes for Prosa's backend. The existing iOS host needs public-client settings with callback `msauth.com.prosa.writer.ios://auth`; a web-only registration or empty client ID does not prove iPhone sign-in.

Store private keys and certificates outside the repository and chat. Apple agreements, payment, and approval of the native identity registration remain account-owner tasks.

## Create the distribution key from Windows

You do not need your own Mac to create the signing key. Use a trusted OpenSSL installation locally; Git for Windows normally includes `C:\Program Files\Git\usr\bin\openssl.exe`. Verify that executable exists, or install OpenSSL through your normal development-tool setup. Work in a private local credential directory outside the checkout:

```powershell
$credentialDirectory = Join-Path $env:LOCALAPPDATA 'ProsaAppleSigning'
New-Item -ItemType Directory -Path $credentialDirectory -Force | Out-Null
Set-Location -LiteralPath $credentialDirectory
$opensslPath = 'C:\Program Files\Git\usr\bin\openssl.exe'
& $opensslPath genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -aes-256-cbc -out prosa-ios-private.pem
& $opensslPath req -new -key prosa-ios-private.pem -out prosa-ios.csr
```

OpenSSL prompts for your key passphrase and CSR identity details. Upload the `.csr` in Apple's Certificates portal when creating the **Apple Distribution** certificate. Download its certificate into this directory (named `distribution.cer` below), then convert and export:

```powershell
& $opensslPath x509 -inform DER -in distribution.cer -out distribution.pem
& $opensslPath pkcs12 -export -inkey prosa-ios-private.pem -in distribution.pem -out distribution.p12
```

Record the `.p12` export password securely. Preserve an encrypted backup of the original private key. These commands are setup instructions, not commands executed by the workflow. Do not revoke an existing production certificate merely to set up CI.

## GitHub configuration

Create the repository environment **`ios-testflight`** in Settings → Environments. Limit its deployment branches to branches you trust to run signing code. Use any environment protection rules appropriate to the repository. The workflow never receives credentials from pull requests and does not trigger on a push.

Add these **environment secrets**:

| Secret | Value |
| --- | --- |
| `IOS_DISTRIBUTION_P12_BASE64` | Base64 of the password-protected `.p12`, including its private key |
| `IOS_DISTRIBUTION_P12_PASSWORD` | Nonempty `.p12` export password |
| `IOS_APPSTORE_PROFILE_BASE64` | Base64 of the App Store `.mobileprovision` profile |
| `ASC_PRIVATE_KEY_BASE64` | Base64 of the team API key `.p8`; needed for upload |
| `ASC_KEY_ID` | The API key's 10-character ID; needed for upload |
| `ASC_ISSUER_ID` | The team's API issuer UUID; needed for upload |

Copy a file's base64 directly to the clipboard in PowerShell, then paste it into the corresponding GitHub secret field:

```powershell
$signingFile = 'C:\path\outside-repository\distribution.p12'
[Convert]::ToBase64String([IO.File]::ReadAllBytes($signingFile)) | Set-Clipboard
```

Repeat for the profile and `.p8`, then clear the clipboard after adding the secrets. Do not print the encoded files in logs or commit them.

Add these **environment variables** (public build configuration):

| Variable | Value |
| --- | --- |
| `IOS_API_BASE_URL` | Authorized, iPhone-reachable HTTPS backend base URL for the environment selected at dispatch; required |
| `IOS_NATIVE_AUTH_JSON` | The five public native client settings below; required |
| `IOS_XCODE_APP_PATH` | Compatible Xcode application installed on `macos-26`, e.g. `/Applications/Xcode_26.5.app` **only if available and compatible**; required |
| `IOS_WORKLOAD_VERSION` | Optional tested .NET workload-set version; pin it once the first cloud build establishes the supported combination |
| `IOS_BUILD_NUMBER_OFFSET` | Optional integer, default `0`; configure before upload if builds for this version already exist |

`global.json` selects .NET SDK 10.0.400 with latest-patch roll-forward. The Mac workflow selects Python 3.12 for its helper, installs `maui-ios` and logs the SDK, workloads and Xcode version. Verify the runner's installed Xcode apps and the selected workload's required Xcode version before the first run. Pin a compatible combination rather than assuming the runner's default Xcode matches the workload. See the [runner image inventories](https://github.com/actions/runner-images/tree/main/images/macos) and [.NET iOS release notes](https://github.com/dotnet/macios/releases).

The existing public-auth JSON has this shape; replace every placeholder with approved values:

```json
{
  "TenantId": "<tenant UUID>",
  "Authority": "https://login.microsoftonline.com/<tenant UUID>",
  "ClientId": "<native public-client UUID>",
  "RedirectUri": "msauth.com.prosa.writer.ios://auth",
  "Scopes": ["api://<API application UUID>/<delegated scope>"]
}
```

Use the approved CIAM authority instead if that is how Prosa's identity provider is configured. This JSON contains **no client secret, access token, or signing credentials**. The pipeline checks configuration presence and shape; provider/backend authorization and real sign-in still need iPhone acceptance. The workflow embeds this configuration from a temporary file without modifying the tracked `native-auth.json`.

Do not use `localhost`: on an iPhone it points to the phone. This workflow requires HTTPS and rejects URLs with credentials, query strings, fragments or MSBuild property separators. Set the backend variable to match the selected Development/Staging/Production label; CI cannot infer which tenant/environment an arbitrary host serves.

## Run and install

1. Commit and push the intended app changes and these CI files together to an appropriate trusted branch. GitHub only builds committed remote files; this checkout currently contains other unfinished work that must be reviewed before selecting what to push. To expose a new manual workflow in the Actions UI, its definition must first be on the repository's default branch; afterward choose the intended branch in the dispatch selector.
2. Open Actions → **iOS TestFlight package** → Run workflow. Choose the backend environment. Leave **Upload the signed package** off for the first signing check.
3. Review the workflow outcome, selected toolchain and `Prosa-iOS-<run number>` artifact. It contains the signed `.ipa` and a non-secret receipt with commit, version, build number, hash and separate upload/acceptance states. An App Store-signed IPA cannot be directly installed from this artifact onto your phone.
4. Start a **new workflow run** with upload enabled when ready. The pipeline validates the IPA with Apple's tool before uploading it using the API key. Upload errors retain the package receipt for diagnosis, with upload acceptance false.
5. Wait for Apple processing, complete any required export-compliance/test information in App Store Connect, and make the build available to your internal tester group. Install or update it through TestFlight on your iPhone.
6. Test sign-in/callback, document creation/opening, typing/selection/keyboard, offline save/reopen, sync after reconnect, and upgrade with existing local data.

Build number = `IOS_BUILD_NUMBER_OFFSET + github.run_number`; it is injected only for this build, without committing version bumps. New manual runs increment it. Re-running the same workflow run retains its number, so after an accepted upload start a new run rather than retrying the uploaded build. An old run may also be lower than a newer accepted build. Keep numbers at or below 9999 for the single-component iOS build string; plan the next numbering scheme/version before this bound. Deleting/recreating the workflow can reset its counter: establish an offset above the last uploaded build before doing so.

Only the IPA and receipt are uploaded as GitHub artifacts. P12/profile/auth/API-key files remain in runner-private temporary storage; the temporary Keychain and installed profile are removed after success or failure, with a second `always()` cleanup step. The workflow is intended for fresh GitHub-hosted runners, not a personal Mac or a shared self-hosted runner.

## Validation and remaining gates

Run the credential-free checks on Windows or macOS:

```powershell
python scripts/ios/test_testflight.py
```

They exercise configuration/profile rejection and a simulated signed-package/upload lifecycle, including cleanup on upload failure. They do not execute Apple's signing or upload tools. YAML validation likewise does not prove native bundling or App Store acceptance.

Local preparation checks on 2026-10-09: 11 Python tests passed (including invalid configuration/profile subcases, build-only, accepted upload, failed upload and repeat cleanup); actionlint 1.7.12 passed; workflow YAML gates/credential scope and Python syntax passed. Shellcheck and pyflakes were disabled in actionlint because they are not installed; embedded shell snippets are separately syntax-checked with Git Bash. No repository-wide application build/test claims follow from this CI-only change.

The first real hosted Mac run must establish: compatible toolchains; successful editor bundling/native archive/signing; correct profile/certificate and embedded identity; Apple upload validation; processing/distribution; and physical iPhone behavior. No account credentials were created, installed, or uploaded while preparing this workflow.

Reference implementation guidance: [MAUI iOS publishing](https://learn.microsoft.com/en-us/dotnet/maui/ios/deployment/publish-cli?view=net-maui-10.0), [GitHub Apple signing secrets and Keychain setup](https://docs.github.com/en/actions/how-tos/deploy/deploy-to-third-party-platforms/sign-xcode-applications), [Apple upload tools](https://developer.apple.com/help/app-store-connect/manage-builds/upload-builds/), [TestFlight internal testing](https://developer.apple.com/help/app-store-connect/test-a-beta-version/add-internal-testers/).
