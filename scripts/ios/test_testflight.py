"""Credential-free checks; native commands are simulated, not Apple acceptance."""

import base64
import copy
import datetime as dt
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import plistlib
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location("testflight", Path(__file__).with_name("testflight.py"))
ci = importlib.util.module_from_spec(spec)
spec.loader.exec_module(ci)


def config_env():
    tenant = "11111111-1111-1111-1111-111111111111"
    return {
        "PROSA_IOS_ENVIRONMENT": "Staging", "PROSA_IOS_API_BASE_URL": "https://staging.example.com/",
        "PROSA_IOS_XCODE_APP": "/Applications/Xcode_26.5.app", "PROSA_IOS_UPLOAD": "false",
        "GITHUB_RUN_NUMBER": "15", "GITHUB_SHA": "a" * 40, "GITHUB_RUN_ID": "123",
        "PROSA_IOS_AUTH_JSON": json.dumps({"TenantId": tenant, "ClientId": "22222222-2222-2222-2222-222222222222",
            "Authority": "https://login.microsoftonline.com/" + tenant, "RedirectUri": ci.CALLBACK,
            "Scopes": ["api://33333333-3333-3333-3333-333333333333/access_as_user"]}),
    }


def profile():
    return {"UUID": "44444444-4444-4444-4444-444444444444", "TeamIdentifier": ["TESTTEAM01"],
        "ApplicationIdentifierPrefix": ["OLDPREFIX1"], "DeveloperCertificates": [b"test-certificate"],
        "ExpirationDate": dt.datetime.now(dt.timezone.utc).replace(tzinfo=None) + dt.timedelta(days=30),
        "Entitlements": {"application-identifier": "OLDPREFIX1." + ci.BUNDLE_ID,
            "com.apple.developer.team-identifier": "TESTTEAM01", "get-task-allow": False,
            "beta-reports-active": True, "keychain-access-groups": ["OLDPREFIX1.*"]}}


class ConfigurationTests(unittest.TestCase):
    def test_explicit_environment_and_offset(self):
        env = config_env()
        env["PROSA_IOS_BUILD_OFFSET"] = "100"
        result = ci.configuration(env)
        self.assertEqual(result["buildNumber"], "115")
        self.assertFalse(result["upload"])

    def test_new_runs_increment_without_modifying_project(self):
        self.assertGreater(int(ci.build_number("16")), int(ci.build_number("15")))
        with self.assertRaises(ValueError):
            ci.build_number("10000")

    def test_backend_cannot_be_local_or_inject_msbuild_properties(self):
        for value in ("http://staging.example.com/", "https://localhost/", "https://127.0.0.1/",
                      "https://[::1]/", "https://user:password@example.com/", "https://example.com/?token=secret",
                      "https://example.com/;CodesignKey=other", "https://example.com/%3Bbad"):
            with self.subTest(value=value), self.assertRaises(ValueError):
                ci.https_url(value, "backend")

    def test_missing_or_secret_bearing_auth_is_rejected(self):
        auth = json.loads(config_env()["PROSA_IOS_AUTH_JSON"])
        for mutation in ({"ClientId": "00000000-0000-0000-0000-000000000000"},
                         {"RedirectUri": "http://localhost"}, {"Scopes": ["api://example/.default"]},
                         {"Authority": "https://evil.example/"}, {"ClientSecret": "must-not-be-packaged"}):
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                ci.validate_auth(json.dumps(auth | mutation))

    def test_preflight_names_missing_secrets(self):
        with self.assertRaisesRegex(ValueError, "IOS_DISTRIBUTION_P12"):
            ci.preflight(config_env())


class ProfileTests(unittest.TestCase):
    def test_legacy_app_prefix_is_distinct_from_team_and_wildcard_groups_work(self):
        self.assertEqual(ci.validate_profile(profile()), profile()["UUID"])

    def test_reject_wrong_bundle_expired_or_non_appstore_profiles(self):
        cases = [{"ProvisionedDevices": ["phone"]}, {"ProvisionsAllDevices": True},
                 {"ExpirationDate": dt.datetime(2000, 1, 1)}, {"DeveloperCertificates": []}]
        for mutation in cases:
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                ci.validate_profile(profile() | mutation)
        for mutation in ({"application-identifier": "OLDPREFIX1.other.app"}, {"get-task-allow": True},
                         {"beta-reports-active": False}, {"com.apple.developer.team-identifier": "OTHERTEAM1"},
                         {"keychain-access-groups": ["OLDPREFIX1." + ci.BUNDLE_ID]}):
            invalid = copy.deepcopy(profile())
            invalid["Entitlements"].update(mutation)
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                ci.validate_profile(invalid)

    def test_bad_base64_has_no_decoded_material_in_error(self):
        with self.assertRaisesRegex(ValueError, "not valid base64"):
            ci.decode_secret({"CERT": "not-base64!"}, "CERT")


class PipelineTests(unittest.TestCase):
    def exercise(self, upload=False, upload_failure=False):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            env = config_env() | {"RUNNER_TEMP": str(root / "temp"),
                "PROSA_IOS_UPLOAD": str(upload).lower(), "IOS_DISTRIBUTION_P12_PASSWORD": "private-password",
                "IOS_DISTRIBUTION_P12_BASE64": base64.b64encode(b"test-p12").decode(),
                "IOS_APPSTORE_PROFILE_BASE64": base64.b64encode(b"test-profile").decode(),
                "ASC_KEY_ID": "KEY1234567", "ASC_ISSUER_ID": "55555555-5555-5555-5555-555555555555",
                "ASC_PRIVATE_KEY_BASE64": base64.b64encode(b"test-p8").decode()}
            (root / "temp").mkdir()
            commands = []

            def native(args, capture=False):
                commands.append(args)
                if args[:3] == ["security", "cms", "-D"]:
                    return plistlib.dumps(profile())
                if args[:2] == ["security", "create-keychain"]:
                    Path(args[-1]).touch()
                if args[:2] == ["security", "find-identity"]:
                    digest = hashlib.sha1(b"test-certificate").hexdigest().upper()
                    return f'1) {digest} "Apple Distribution: Test (TESTTEAM01)"'.encode()
                if args[:2] == ["dotnet", "publish"]:
                    manifest = {"CFBundleIdentifier": ci.BUNDLE_ID, "CFBundleVersion": "15", "CFBundleShortVersionString": "0.1.0"}
                    with zipfile.ZipFile(root / "artifacts/ios-testflight/Prosa.ipa", "w") as archive:
                        archive.writestr("Payload/Prosa.app/Info.plist", plistlib.dumps(manifest))
                if upload_failure and "--upload-app" in args:
                    raise RuntimeError("simulated Apple upload failure")
                return b""

            with patch.dict(os.environ, env, clear=True), patch.object(ci, "ROOT", root), \
                 patch.object(ci.sys, "platform", "darwin"), patch.object(ci.Path, "home", return_value=root / "home"), \
                 patch.object(ci, "run", side_effect=native):
                try:
                    if upload_failure:
                        with self.assertRaisesRegex(RuntimeError, "simulated Apple"):
                            ci.build(env)
                    else:
                        ci.build(env)
                finally:
                    ci.cleanup()
                self.assertFalse((root / "temp/prosa-ios-ci").exists())
                self.assertFalse(list((root / "home").rglob("*.mobileprovision")))
                receipt = json.loads((root / "artifacts/ios-testflight/build-receipt.json").read_text())
                self.assertEqual(receipt["uploadAccepted"], upload and not upload_failure)
                self.assertFalse(receipt["appleProcessingVerified"])
                self.assertFalse(receipt["iphoneAcceptanceVerified"])
                self.assertNotIn("private-password", json.dumps(receipt))
                self.assertEqual(any("--upload-app" in args for args in commands), upload)
                ci.cleanup()  # Safe when workflow's always() step repeats cleanup.

    def test_build_only_never_contacts_apple_upload(self):
        self.exercise()

    def test_upload_and_cleanup_with_pending_device_acceptance(self):
        self.exercise(upload=True)

    def test_failed_upload_preserves_package_receipt_but_removes_credentials(self):
        self.exercise(upload=True, upload_failure=True)


if __name__ == "__main__":
    unittest.main()
