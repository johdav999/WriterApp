"""Build Prosa on a hosted Mac. Never print private material or command arguments."""

import base64
import binascii
import datetime as dt
import fnmatch
import hashlib
import json
import os
from pathlib import Path
import plistlib
import re
import secrets
import shutil
import subprocess
import sys
import uuid
import zipfile
from urllib.parse import urlsplit

BUNDLE_ID = "com.prosa.writer.ios"
CALLBACK = f"msauth.{BUNDLE_ID}://auth"
ROOT = Path(__file__).resolve().parents[2]


def require(condition, message):
    if not condition:
        raise ValueError(message)


def build_number(run_number, offset="0"):
    require(re.fullmatch(r"[1-9][0-9]*", run_number or ""), "Missing GitHub run number.")
    require(re.fullmatch(r"[0-9]+", offset), "IOS_BUILD_NUMBER_OFFSET must be a nonnegative integer.")
    number = int(run_number) + int(offset)
    require(0 < number <= 9999, "Build number exceeds the four-digit iOS limit; plan a new version/counter before uploading.")
    return str(number)


def https_url(value, name):
    # These values also become MSBuild properties: reject property/list separators.
    require(isinstance(value, str) and not any(c.isspace() or c in ";,%" for c in value), f"Invalid {name}.")
    uri = urlsplit(value)
    require(uri.scheme == "https" and uri.hostname and not uri.username and not uri.password
            and not uri.query and not uri.fragment, f"{name} must be an HTTPS URL without credentials, query or fragment.")
    require(uri.hostname.lower() not in {"localhost", "127.0.0.1", "::1"}, f"{name} must be reachable from the iPhone.")
    return uri


def validate_auth(raw):
    try:
        auth = json.loads(raw)
        require(isinstance(auth, dict), "IOS_NATIVE_AUTH_JSON must be a JSON object.")
        require(set(auth) == {"TenantId", "Authority", "ClientId", "RedirectUri", "Scopes"},
                "IOS_NATIVE_AUTH_JSON must contain only the five public client settings listed in the guide.")
        require(uuid.UUID(auth["TenantId"]).int and uuid.UUID(auth["ClientId"]).int,
                "Configure nonempty native TenantId and ClientId values.")
        authority = https_url(auth["Authority"], "authentication authority")
        require(authority.hostname.lower().endswith(".ciamlogin.com")
                or (authority.hostname.lower() == "login.microsoftonline.com"
                    and authority.path.strip("/") == auth["TenantId"]), "Use Prosa's approved identity authority.")
        require(auth["RedirectUri"] == CALLBACK, "The iOS sign-in callback must match the existing bundle ID.")
        require(isinstance(auth["Scopes"], list) and auth["Scopes"], "Configure delegated API scopes.")
        require(all(isinstance(s, str) and urlsplit(s).scheme and not s.endswith("/.default")
                    and not any(c.isspace() for c in s) for s in auth["Scopes"]), "Invalid delegated API scopes.")
    except (KeyError, TypeError, AttributeError, json.JSONDecodeError):
        raise ValueError("Invalid IOS_NATIVE_AUTH_JSON; see docs/ios-testflight.md.") from None
    return auth


def configuration(env):
    require(env.get("PROSA_IOS_ENVIRONMENT") in {"Development", "Staging", "Production"}, "Choose a Prosa backend environment.")
    https_url(env.get("PROSA_IOS_API_BASE_URL", ""), "IOS_API_BASE_URL")
    require(env.get("PROSA_IOS_UPLOAD") in {"true", "false"}, "Choose whether to upload to TestFlight.")
    require(re.fullmatch(r"/Applications/Xcode(?:_[0-9.]+)?\.app", env.get("PROSA_IOS_XCODE_APP", "")),
            "Set IOS_XCODE_APP_PATH to the compatible Xcode app installed on the runner.")
    workload = env.get("PROSA_IOS_WORKLOAD_VERSION", "")
    require(not workload or re.fullmatch(r"[0-9]+(?:\.[0-9]+){2}(?:[-.][A-Za-z0-9.]+)?", workload), "Invalid IOS_WORKLOAD_VERSION.")
    return {
        "environment": env["PROSA_IOS_ENVIRONMENT"],
        "apiBaseUrl": env["PROSA_IOS_API_BASE_URL"],
        "buildNumber": build_number(env.get("GITHUB_RUN_NUMBER", ""), env.get("PROSA_IOS_BUILD_OFFSET", "0")),
        "auth": validate_auth(env.get("PROSA_IOS_AUTH_JSON", "")),
        "upload": env["PROSA_IOS_UPLOAD"] == "true",
    }


def validate_profile(profile, now=None):
    now = now or dt.datetime.now(dt.timezone.utc)
    expires = profile.get("ExpirationDate")
    require(isinstance(expires, dt.datetime) and expires.replace(tzinfo=dt.timezone.utc) > now,
            "The App Store provisioning profile is missing or expired.")
    require(not profile.get("ProvisionedDevices") and not profile.get("ProvisionsAllDevices"),
            "Use an App Store distribution profile, not an ad hoc/development/enterprise profile.")
    entitlements = profile.get("Entitlements", {})
    require(entitlements.get("get-task-allow") is False, "The profile must disable development debugging.")
    require(entitlements.get("beta-reports-active") is True, "The profile must support TestFlight distribution.")
    prefixes = profile.get("ApplicationIdentifierPrefix", [])
    teams = profile.get("TeamIdentifier", [])
    require(len(prefixes) == 1 and len(teams) == 1, "The profile must identify one Apple app prefix and team.")
    require(entitlements.get("application-identifier") == f"{prefixes[0]}.{BUNDLE_ID}", "Provisioning profile bundle ID does not match Prosa.")
    require(entitlements.get("com.apple.developer.team-identifier") == teams[0], "Provisioning profile team mismatch.")
    groups = entitlements.get("keychain-access-groups", [])
    for suffix in (BUNDLE_ID, BUNDLE_ID + ".msal"):
        require(any(fnmatch.fnmatchcase(f"{prefixes[0]}.{suffix}", group) for group in groups),
                "The profile must allow both Prosa authentication Keychain groups.")
    require(profile.get("DeveloperCertificates"), "The profile has no allowed distribution certificates.")
    return str(uuid.UUID(profile.get("UUID", "")))


def decode_secret(env, name):
    try:
        value = base64.b64decode("".join(env.get(name, "").split()), validate=True)
    except (ValueError, binascii.Error):
        raise ValueError(f"{name} is not valid base64.") from None
    require(value, f"Set the GitHub environment secret {name}.")
    return value


def run(args, capture=False):
    # No shell interpolation; suppress security output that can echo imported material.
    result = subprocess.run(args, check=False, stdout=subprocess.PIPE if capture else None,
                            stderr=subprocess.PIPE if capture else None)
    if result.returncode:
        raise RuntimeError(f"{Path(args[0]).name} failed (exit {result.returncode}); inspect the toolchain/signing setup.")
    return result.stdout if capture else b""


def private_file(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("xb") as stream:
        if hasattr(os, "fchmod"):
            os.fchmod(stream.fileno(), 0o600)
        stream.write(data)


def temp_dir():
    require(os.environ.get("RUNNER_TEMP"), "This command requires a GitHub-hosted runner.")
    return Path(os.environ["RUNNER_TEMP"]).resolve() / "prosa-ios-ci"


def cleanup():
    task_dir = temp_dir()
    keychain = task_dir / "signing.keychain-db"
    if keychain.exists():
        run(["security", "delete-keychain", str(keychain)], capture=True)
    marker = task_dir / "profile-uuid.txt"
    if marker.exists():
        profile_id = str(uuid.UUID(marker.read_text().strip()))
        for directory in (Path.home() / "Library/MobileDevice/Provisioning Profiles",
                          Path.home() / "Library/Developer/Xcode/UserData/Provisioning Profiles"):
            (directory / (profile_id + ".mobileprovision")).unlink(missing_ok=True)
    if task_dir.exists():
        shutil.rmtree(task_dir)


def preflight(env):
    config = configuration(env)
    required = ["HAS_IOS_DISTRIBUTION_P12", "HAS_IOS_DISTRIBUTION_PASSWORD", "HAS_IOS_APPSTORE_PROFILE"]
    if config["upload"]:
        required += ["HAS_ASC_KEY", "HAS_ASC_KEY_ID", "HAS_ASC_ISSUER_ID"]
    missing = [name for name in required if env.get(name) != "true"]
    require(not missing, "Missing signing/upload secrets: " + ", ".join(name.removeprefix("HAS_") for name in missing))
    require(Path(env["PROSA_IOS_XCODE_APP"]).is_dir(), "Configured Xcode is not installed on this runner; select a compatible image/version.")
    print("Configuration present. Apple signing, native execution and account permissions still require a real Mac build.")


def build(env):
    require(sys.platform == "darwin", "Signed iPhone builds require macOS/Xcode.")
    config = configuration(env)
    task_dir = temp_dir()
    task_dir.mkdir(mode=0o700, exist_ok=False)
    output = ROOT / "artifacts/ios-testflight"
    output.mkdir(parents=True, exist_ok=True)
    require(not list(output.glob("*.ipa")), "Use a fresh checkout; the package directory already contains an IPA.")
    cert_file, profile_file = task_dir / "distribution.p12", task_dir / "appstore.mobileprovision"
    private_file(cert_file, decode_secret(env, "IOS_DISTRIBUTION_P12_BASE64"))
    private_file(profile_file, decode_secret(env, "IOS_APPSTORE_PROFILE_BASE64"))
    profile = plistlib.loads(run(["security", "cms", "-D", "-i", str(profile_file)], capture=True))
    profile_id = validate_profile(profile)
    # Refuse existing profile paths; cleanup must only remove this job's own files.
    profile_paths = [Path.home() / directory / (profile_id + ".mobileprovision") for directory in
                     ("Library/MobileDevice/Provisioning Profiles", "Library/Developer/Xcode/UserData/Provisioning Profiles")]
    require(not any(path.exists() for path in profile_paths), "This runner already contains that provisioning profile; use a fresh hosted runner.")
    private_file(task_dir / "profile-uuid.txt", profile_id.encode())
    for path in profile_paths:
        private_file(path, profile_file.read_bytes())
    keychain = task_dir / "signing.keychain-db"
    password = secrets.token_urlsafe(32)
    run(["security", "create-keychain", "-p", password, str(keychain)], capture=True)
    run(["security", "set-keychain-settings", "-lut", "3600", str(keychain)], capture=True)
    run(["security", "unlock-keychain", "-p", password, str(keychain)], capture=True)
    run(["security", "import", str(cert_file), "-P", env.get("IOS_DISTRIBUTION_P12_PASSWORD", ""),
         "-k", str(keychain), "-T", "/usr/bin/codesign", "-T", "/usr/bin/security"], capture=True)
    run(["security", "set-key-partition-list", "-S", "apple-tool:,apple:,codesign:", "-s", "-k", password, str(keychain)], capture=True)
    identities = run(["security", "find-identity", "-v", "-p", "codesigning", str(keychain)], capture=True).decode()
    permitted = {hashlib.sha1(cert).hexdigest().upper() for cert in profile["DeveloperCertificates"]}
    identity = next((digest for digest, name in re.findall(r'([A-F0-9]{40}) "([^"]+)"', identities)
                     if digest in permitted and name.startswith("Apple Distribution:")), None)
    require(identity, "The imported private key must match an Apple Distribution certificate allowed by the profile.")
    auth_file = task_dir / "native-auth.json"
    private_file(auth_file, json.dumps(config["auth"]).encode())
    run(["dotnet", "publish", str(ROOT / "WriterApp.iOS/WriterApp.iOS.csproj"), "-c", "Release", "-f", "net10.0-ios",
         "-p:RuntimeIdentifier=ios-arm64", "-p:ArchiveOnBuild=true", "-p:BuildIpa=true", "-p:IpaPackageDir=" + str(output),
         "-p:CodesignKey=" + identity, "-p:CodesignProvision=" + profile_id, "-p:CodesignKeychain=" + str(keychain),
         "-p:ApplicationVersion=" + config["buildNumber"], "-p:ProsaEnvironment=" + config["environment"],
         "-p:ProsaApiBaseUrl=" + config["apiBaseUrl"], "-p:ProsaIosAuthSettingsFile=" + str(auth_file)])
    packages = list(output.glob("*.ipa"))
    require(len(packages) == 1, "Expected exactly one signed IPA in artifacts/ios-testflight.")
    package = packages[0]
    with zipfile.ZipFile(package) as archive:
        manifests = [name for name in archive.namelist() if re.fullmatch(r"Payload/[^/]+\.app/Info\.plist", name)]
        require(len(manifests) == 1, "Expected one Prosa app manifest in the IPA.")
        manifest = plistlib.loads(archive.read(manifests[0]))
    require(manifest.get("CFBundleIdentifier") == BUNDLE_ID
            and str(manifest.get("CFBundleVersion")) == config["buildNumber"], "Packaged bundle ID/build number mismatch.")
    with package.open("rb") as stream:
        package_hash = hashlib.file_digest(stream, "sha256").hexdigest()
    receipt = {"bundleId": BUNDLE_ID, "version": manifest.get("CFBundleShortVersionString"),
               "buildNumber": config["buildNumber"], "environment": config["environment"],
               "commit": env.get("GITHUB_SHA"), "runId": env.get("GITHUB_RUN_ID"),
               "sha256": package_hash,
               "uploadAccepted": False, "appleProcessingVerified": False, "iphoneAcceptanceVerified": False}
    receipt_path = output / "build-receipt.json"
    receipt_path.write_text(json.dumps(receipt, indent=2) + "\n")
    if config["upload"]:
        key_id = env.get("ASC_KEY_ID", "")
        require(re.fullmatch(r"[A-Z0-9]{10}", key_id), "ASC_KEY_ID must identify an App Store Connect team API key.")
        require(uuid.UUID(env.get("ASC_ISSUER_ID", "")).int, "Configure ASC_ISSUER_ID.")
        keys = task_dir / "private_keys"
        private_file(keys / f"AuthKey_{key_id}.p8", decode_secret(env, "ASC_PRIVATE_KEY_BASE64"))
        os.environ["API_PRIVATE_KEYS_DIR"] = str(keys)
        for operation in ("--validate-app", "--upload-app"):
            run(["xcrun", "altool", operation, "--file", str(package), "--type", "ios",
                 "--apiKey", key_id, "--apiIssuer", env["ASC_ISSUER_ID"]])
        receipt["uploadAccepted"] = True
        receipt_path.write_text(json.dumps(receipt, indent=2) + "\n")
    summary = env.get("GITHUB_STEP_SUMMARY")
    if summary:
        with open(summary, "a", encoding="utf-8") as stream:
            stream.write(f"Prosa iOS build **{config['buildNumber']}** packaged from `{env.get('GITHUB_SHA', '')}`.\n\n")
            stream.write("Upload accepted; Apple processing and iPhone acceptance remain pending.\n" if config["upload"]
                         else "Build-only run. This App Store IPA is for review/upload and cannot be installed directly on your phone.\n")


if __name__ == "__main__":
    try:
        command = sys.argv[1] if len(sys.argv) == 2 else ""
        require(command in {"preflight", "build", "cleanup"}, "Usage: testflight.py preflight|build|cleanup")
        if command == "cleanup":
            cleanup()
        elif command == "preflight":
            preflight(os.environ)
        else:
            try:
                build(os.environ)
            finally:
                cleanup()
    except (ValueError, RuntimeError, OSError, plistlib.InvalidFileException, zipfile.BadZipFile):
        # Avoid traceback/local variable dumps or tool arguments containing passwords.
        error = sys.exc_info()[1]
        print(f"::error::{type(error).__name__}: {error}", file=sys.stderr)
        sys.exit(1)
