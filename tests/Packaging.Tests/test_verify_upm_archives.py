from __future__ import annotations

import importlib.util
import io
import json
import tarfile
import tempfile
import unittest
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "verify_upm_archives.py"
SPEC = importlib.util.spec_from_file_location("verify_upm_archives", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
VERIFIER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFIER)


class VerifyUpmArchivesTests(unittest.TestCase):
    version = "1.0.0-preview.4"

    def test_accepts_matching_archives_with_complete_role_boundary(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package = self.create_package(root)
            zip_path, tgz_path = self.create_archives(root, package)

            VERIFIER.verify_upm_archives(package, zip_path, tgz_path, self.version)

    def test_rejects_archive_content_drift(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package = self.create_package(root)
            zip_path, tgz_path = self.create_archives(root, package)
            with zipfile.ZipFile(zip_path, "a") as archive:
                archive.writestr("unexpected.txt", "drift")

            with self.assertRaisesRegex(ValueError, "zip file list or content differs"):
                VERIFIER.verify_upm_archives(package, zip_path, tgz_path, self.version)

    def test_rejects_server_assembly(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package = self.create_package(root)
            self.write(package / "Runtime/Managed/Nnrp.Server.dll", b"server")
            zip_path, tgz_path = self.create_archives(root, package)

            with self.assertRaisesRegex(ValueError, "managed assembly boundary mismatch"):
                VERIFIER.verify_upm_archives(package, zip_path, tgz_path, self.version)

    def create_package(self, root: Path) -> Path:
        package = root / "com.nnrp.client"
        self.write(
            package / "package.json",
            json.dumps({"name": "com.nnrp.client", "version": self.version}).encode(),
        )
        for assembly in VERIFIER.build_upm_package.MANAGED_ASSEMBLIES:
            self.write(package / f"Runtime/Managed/{assembly}.dll", assembly.encode())
        for native_path in VERIFIER.expected_native_paths():
            self.write(package / native_path, native_path.encode())
            self.write(package / f"{native_path}.meta", b"meta")
        return package

    @staticmethod
    def create_archives(root: Path, package: Path) -> tuple[Path, Path]:
        zip_path = root / "package.zip"
        tgz_path = root / "package.tgz"
        files = [path for path in sorted(package.rglob("*")) if path.is_file()]

        with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as archive:
            for path in files:
                archive.write(path, path.relative_to(package).as_posix())

        with tarfile.open(tgz_path, "w:gz") as archive:
            for path in files:
                content = path.read_bytes()
                info = tarfile.TarInfo(f"package/{path.relative_to(package).as_posix()}")
                info.size = len(content)
                archive.addfile(info, io.BytesIO(content))

        return zip_path, tgz_path

    @staticmethod
    def write(path: Path, content: bytes) -> None:
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(content)


if __name__ == "__main__":
    unittest.main()
