import base64
import unittest
from unittest.mock import patch
from pathlib import Path
import package

class PackagingTests(unittest.TestCase):
    def setUp(self):
        self.hash = base64.b64encode(bytes(range(20))).decode()
        self.auth = {"TenantId": "11111111-1111-1111-1111-111111111111", "ClientId": "22222222-2222-2222-2222-222222222222",
                     "Authority": "https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111",
                     "RedirectUri": "msauth://com.prosa.writer.android/" + package.urllib.parse.quote(self.hash, safe=""), "Scopes": ["api://api/access_as_user"]}
    def test_valid_certificate_bound_release(self):
        package.validate_config("Staging", "https://api.example/", self.auth, self.hash)
    def test_reject_backend_credentials_http_and_fragments(self):
        for backend in ["http://localhost/", "https://user:password@api.example/", "https://api.example/#fragment", "https://api.example/?query=1"]:
            with self.subTest(backend=backend), self.assertRaises(ValueError):
                package.validate_config("Production", backend, self.auth, self.hash)
    def test_callback_cannot_reuse_ios_or_foreign_signer(self):
        for redirect in ["msauth.com.prosa.writer.ios://auth", "msauth://foreign/hash", "http://localhost"]:
            with self.subTest(redirect=redirect), self.assertRaises(ValueError):
                package.validate_config("Production", "https://api.example/", dict(self.auth, RedirectUri=redirect), self.hash)
    def test_no_implicit_scopes(self):
        for scopes in [[], ["api://api/.default"], ["relative"], ["api://api/a b"]]:
            with self.subTest(scopes=scopes), self.assertRaises(ValueError):
                package.validate_config("Production", "https://api.example/", dict(self.auth, Scopes=scopes), self.hash)
    def test_monotonic_version_offset(self):
        self.assertEqual(110, package.build_number("10", "100"))
        for run, offset in [("0", "0"), ("1", "-1"), ("$(command)", "0"), ("2100000001", "0")]:
            with self.subTest(run=run), self.assertRaises(ValueError): package.build_number(run, offset)
    def test_unsigned_bundle_cannot_pass_verification(self):
        with patch.object(package, "run", return_value=package.subprocess.CompletedProcess([], 0, "jar is unsigned.")):
            with self.assertRaises(ValueError): package.verify_package(Path("test.aab"), self.hash)
    def test_apk_verification_requires_the_expected_signer(self):
        response = package.subprocess.CompletedProcess([], 0, "Signer #1 certificate SHA-1 digest: " + bytes(range(20)).hex())
        with patch.object(package.shutil, "which", return_value="apksigner"), patch.object(package, "run", return_value=response):
            package.verify_package(Path("test.apk"), self.hash)
            with self.assertRaises(ValueError): package.verify_package(Path("test.apk"), base64.b64encode(bytes(20)).decode())

if __name__ == "__main__": unittest.main()
