from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "download_nnrp_rs_artifacts.py"


def load_downloader():
    spec = importlib.util.spec_from_file_location("download_nnrp_rs_artifacts", SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"unable to load {SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class DownloadNnrpRsArtifactsTests(unittest.TestCase):
    def setUp(self):
        self.downloader = load_downloader()
        self.artifact = self.downloader.NativeArtifact(
            "tcp",
            "windows-x86_64",
            "win-x64",
            "nnrp_ffi.dll",
        )

    def write_archive(self, path: Path, *, abi_version: str = "4.1.1") -> None:
        manifest = {
            "transport_scope": "tcp",
            "abi_version": abi_version,
        }
        with zipfile.ZipFile(path, "w") as archive:
            archive.writestr("nnrp_ffi.dll", b"native")
            archive.writestr("manifest.json", json.dumps(manifest))

    def test_extracts_only_a_matching_transport_abi(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            archive = root / "artifact.zip"
            output = root / "output"
            self.write_archive(archive)

            library = self.downloader.extract_library(
                archive,
                self.artifact,
                output,
                False,
                "4.1.1",
            )

            self.assertEqual(library.read_bytes(), b"native")
            manifest = json.loads((library.parent / "manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(manifest["transport_scope"], "tcp")
            self.assertEqual(manifest["abi_version"], "4.1.1")

    def test_rejects_a_release_asset_with_the_wrong_abi(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            archive = root / "artifact.zip"
            self.write_archive(archive, abi_version="4.1.0")

            with self.assertRaisesRegex(ValueError, "expected ABI 4.1.1"):
                self.downloader.extract_library(
                    archive,
                    self.artifact,
                    root / "output",
                    False,
                    "4.1.1",
                )


if __name__ == "__main__":
    unittest.main()
