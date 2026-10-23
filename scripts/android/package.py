"""Build signed Prosa APK/AAB artifacts without putting signing passwords in command arguments or logs."""
from __future__ import annotations
import argparse
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import urllib.parse
import uuid
import xml.etree.ElementTree as ET

PACKAGE = "com.prosa.writer.android"

def validate_config(environment: str, backend: str, auth: dict, signature_hash: str) -> None:
    if environment not in ("Development", "Staging", "Production"):
        raise ValueError("Choose Development, Staging or Production.")
    uri = urllib.parse.urlsplit(backend)
    if uri.scheme != "https" or not uri.hostname or uri.username or uri.password or uri.query or uri.fragment:
        raise ValueError("Release packages require an HTTPS backend without credentials, query or fragment.")
    if len(base64.b64decode(signature_hash, validate=True)) != 20:
        raise ValueError("Android callback signature hash must identify a SHA-1 certificate digest.")
    for field in ("TenantId", "ClientId"):
        if not uuid.UUID(auth.get(field, "")).int:
            raise ValueError("Supply an approved native TenantId and ClientId.")
    authority = urllib.parse.urlsplit(auth.get("Authority", ""))
    if authority.scheme != "https" or authority.username or authority.password or authority.query or authority.fragment:
        raise ValueError("Invalid identity authority.")
    if not (authority.hostname and authority.hostname.endswith(".ciamlogin.com") or
            authority.hostname == "login.microsoftonline.com" and authority.path.strip("/") == auth["TenantId"]):
        raise ValueError("Use the configured tenant's supported Microsoft authority.")
    expected = f"msauth://{PACKAGE}/{urllib.parse.quote(signature_hash, safe='')}"
    if auth.get("RedirectUri") != expected:
        raise ValueError("RedirectUri must match the Android package and installed signing certificate.")
    scopes = auth.get("Scopes")
    if not isinstance(scopes, list) or not scopes or any(not isinstance(s, str) or not urllib.parse.urlsplit(s).scheme
            or any(c.isspace() for c in s) or s.endswith("/.default") for s in scopes):
        raise ValueError("Provide explicit delegated API scopes.")

def build_number(run_number: str, offset: str) -> int:
    if not re.fullmatch(r"[0-9]+", run_number) or not re.fullmatch(r"[0-9]+", offset):
        raise ValueError("Build number and offset must be nonnegative integers.")
    value = int(run_number) + int(offset)
    if not 1 <= value <= 2_100_000_000:
        raise ValueError("Android versionCode is outside its supported release range.")
    return value

def require(env: dict, key: str) -> str:
    value = env.get(key, "")
    if not value:
        raise ValueError(f"Configure {key} in the protected android-release environment.")
    return value

def run(args: list[str], **kwargs) -> subprocess.CompletedProcess:
    return subprocess.run(args, check=True, **kwargs)

def verify_package(path: Path, signing_hash: str) -> None:
    if path.suffix == ".apk":
        sdk = os.environ.get("ANDROID_HOME") or os.environ.get("ANDROID_SDK_ROOT")
        candidates = sorted((Path(sdk) / "build-tools").glob("*/apksigner.bat" if os.name == "nt" else "*/apksigner")) if sdk else []
        executable = shutil.which("apksigner") or (str(candidates[-1]) if candidates else None)
        if not executable:
            raise ValueError("Android SDK apksigner is required to verify the release APK.")
        verified = run([executable, "verify", "--print-certs", str(path)], capture_output=True, text=True).stdout
        digests = re.findall(r"certificate SHA-1 digest: ([0-9a-fA-F]{40})", verified)
        if len(digests) != 1 or base64.b64encode(bytes.fromhex(digests[0])).decode() != signing_hash:
            raise ValueError("The APK signer differs from the configured release key.")
    else:
        verified = run(["jarsigner", "-J-Duser.language=en", "-J-Duser.country=US", "-verify", str(path)], capture_output=True, text=True)
        if "jar verified." not in verified.stdout:
            raise ValueError("The AAB signature could not be verified.")

def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    env = os.environ
    version = build_number(require(env, "GITHUB_RUN_NUMBER"), env.get("ANDROID_BUILD_NUMBER_OFFSET", "0"))
    alias = require(env, "ANDROID_KEY_ALIAS")
    if not re.fullmatch(r"[A-Za-z0-9_.-]+", alias):
        raise ValueError("Signing alias must contain letters, digits, dots, underscores or hyphens.")
    auth = json.loads(require(env, "ANDROID_NATIVE_AUTH_JSON"))
    callback_hash = require(env, "ANDROID_CALLBACK_SIGNATURE_HASH")
    validate_config(require(env, "PROSA_ANDROID_ENVIRONMENT"), require(env, "ANDROID_API_BASE_URL"), auth, callback_hash)
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    if any(output.iterdir()):
        raise ValueError("Use a fresh output directory to avoid mixing release candidates.")
    root = Path(__file__).resolve().parents[2]
    # All secrets stay outside checkout/output and are removed on every exit path.
    with tempfile.TemporaryDirectory(prefix="prosa-android-") as temporary:
        private = Path(temporary)
        key = private / "release.keystore"
        key.write_bytes(base64.b64decode(require(env, "ANDROID_KEYSTORE_BASE64"), validate=True))
        store_pass = private / "store-pass.txt"
        key_pass = private / "key-pass.txt"
        for path, name in ((store_pass, "ANDROID_STORE_PASSWORD"), (key_pass, "ANDROID_KEY_PASSWORD")):
            password = require(env, name)
            if "\n" in password or "\r" in password:
                raise ValueError("Signing passwords must be single-line values.")
            path.write_text(password, encoding="utf-8")
        settings = private / "native-auth.json"
        settings.write_text(json.dumps(auth), encoding="utf-8")
        for path in private.iterdir():
            path.chmod(0o600)
        certificate = run(["keytool", "-exportcert", "-alias", alias, "-keystore", str(key), "-storepass:file", str(store_pass)], capture_output=True).stdout
        signing_hash = base64.b64encode(hashlib.sha1(certificate).digest()).decode()
        # Play re-signs AAB installations: the callback can use Play's certificate while the upload key differs.
        mode = env.get("ANDROID_SIGNING_MODE", "direct")
        if mode not in ("direct", "play") or mode == "direct" and signing_hash != callback_hash:
            raise ValueError("Direct-install callback must match the APK signer; choose play mode only for Play app signing.")
        props = ET.Element("Project")
        group = ET.SubElement(props, "PropertyGroup")
        properties = {
            "ProsaEnvironment": env["PROSA_ANDROID_ENVIRONMENT"], "ProsaApiBaseUrl": env["ANDROID_API_BASE_URL"],
            "ProsaAndroidAuthSettingsFile": str(settings), "ProsaAndroidSignatureHash": callback_hash,
            "AndroidKeyStore": "true", "AndroidSigningKeyStore": str(key), "AndroidSigningKeyAlias": alias,
            "AndroidSigningStorePass": "file:" + str(store_pass), "AndroidSigningKeyPass": "file:" + str(key_pass),
            "ApplicationVersion": str(version), "AndroidPackageFormats": "aab" if mode == "play" else "apk;aab",
            "EmbedAssembliesIntoApk": "true", "RunAOTCompilation": "false",
        }
        for name, value in properties.items():
            ET.SubElement(group, name).text = value
        properties_file = private / "release.props"
        ET.ElementTree(props).write(properties_file, encoding="utf-8", xml_declaration=True)
        properties_file.chmod(0o600)
        run(["dotnet", "publish", str(root / "WriterApp.Android/WriterApp.Android.csproj"), "-c", "Release",
             "-p:CustomBeforeMicrosoftCommonProps=" + str(properties_file), "-o", str(output), "-v:minimal"])
    packages = sorted(p for p in output.iterdir() if p.suffix in (".apk", ".aab") and "Signed" in p.name)
    expected_extensions = {".aab"} if mode == "play" else {".apk", ".aab"}
    if {p.suffix for p in packages} != expected_extensions:
        raise ValueError("The build did not produce all expected signed packages.")
    for package in packages:
        verify_package(package, signing_hash)
    # Keep only distribution artifacts and a public receipt; never upload an entire build directory.
    receipt = {"package": PACKAGE, "versionCode": version, "environment": env["PROSA_ANDROID_ENVIRONMENT"],
               "signingMode": mode, "callbackSignatureHash": callback_hash, "packageSignerHash": signing_hash,
               "packages": [{"name": p.name, "sha256": hashlib.sha256(p.read_bytes()).hexdigest()} for p in packages],
               "physicalDeviceAcceptance": False, "storeUploadPerformed": False}
    (output / "receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf-8")
    print("Signed Android artifacts prepared. Installation and store acceptance remain separate checks.")

if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, json.JSONDecodeError, subprocess.CalledProcessError) as error:
        # Never print provider/configuration values or captured keytool output.
        raise SystemExit("Android packaging failed: " + (str(error) if isinstance(error, ValueError) and not isinstance(error, json.JSONDecodeError) else type(error).__name__))
