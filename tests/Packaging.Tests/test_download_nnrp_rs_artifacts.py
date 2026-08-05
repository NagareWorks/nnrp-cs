from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch
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

    def write_archive(self, path: Path, *, abi_version: str = "4.4.0") -> None:
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
                "4.4.0",
            )

            self.assertEqual(library.read_bytes(), b"native")
            manifest = json.loads((library.parent / "manifest.json").read_text(encoding="utf-8"))
            self.assertEqual(manifest["transport_scope"], "tcp")
            self.assertEqual(manifest["abi_version"], "4.4.0")

    def test_rejects_a_release_asset_with_the_wrong_abi(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            archive = root / "artifact.zip"
            self.write_archive(archive, abi_version="4.1.0")

            with self.assertRaisesRegex(ValueError, "expected ABI 4.4.0"):
                self.downloader.extract_library(
                    archive,
                    self.artifact,
                    root / "output",
                    False,
                    "4.4.0",
                )

    def test_workflow_artifact_requires_exact_successful_commit(self):
        completed = type(
            "Completed",
            (),
            {
                "stdout": json.dumps(
                    {
                        "headSha": "784a4a354f4e6a73798248f93cf574bd7a5af829",
                        "status": "completed",
                        "conclusion": "success",
                    }
                )
            },
        )()
        with tempfile.TemporaryDirectory() as temp_dir, patch.object(
            self.downloader.subprocess,
            "run",
            side_effect=[completed, type("Downloaded", (), {})()],
        ) as run:
            self.downloader.download_workflow_artifact(
                "NagareWorks/nnrp-rs",
                "1.0.0-preview.4.22",
                "30862254352",
                "784a4a354f4e6a73798248f93cf574bd7a5af829",
                Path(temp_dir),
            )

        self.assertEqual(2, run.call_count)

    def test_checksum_evidence_is_mandatory(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            archive = root / "artifact.zip"
            archive.write_bytes(b"native")
            digest = self.downloader.hashlib.sha256(b"native").hexdigest()
            checksums = self.downloader.read_checksums(
                self.write_text(root / "SHA256SUMS", f"{digest}  artifact.zip\n")
            )
            self.downloader.verify_checksum(archive, checksums)
            with self.assertRaisesRegex(ValueError, "does not contain missing.zip"):
                self.downloader.verify_checksum(root / "missing.zip", checksums)

    @staticmethod
    def write_text(path: Path, content: str) -> Path:
        path.write_text(content, encoding="utf-8")
        return path


if __name__ == "__main__":
    unittest.main()
