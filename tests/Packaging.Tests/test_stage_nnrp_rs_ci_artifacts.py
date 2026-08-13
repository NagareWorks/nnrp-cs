import json
import io
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

from scripts.stage_nnrp_rs_ci_artifacts import (
    TRANSPORTS,
    parse_args,
    prepare_output_root,
    stage_artifacts,
)


class StageNnrpRsCiArtifactsTests(unittest.TestCase):
    def test_help_describes_the_transport_artifact_set(self) -> None:
        output = io.StringIO()
        with mock.patch.object(sys, "argv", ["stage", "--help"]):
            with redirect_stdout(output):
                with self.assertRaisesRegex(SystemExit, "0"):
                    parse_args()

        self.assertIn("native transport artifacts", output.getvalue())

    def test_stages_complete_transport_set_from_manifest_platform(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source = root / "source"
            output = root / "output"
            for transport in TRANSPORTS:
                artifact = source / f"{transport}-windows-x86_64"
                artifact.mkdir(parents=True)
                (artifact / "nnrp_ffi.dll").write_bytes(transport.encode())
                (artifact / "manifest.json").write_text(
                    json.dumps(
                        {
                            "transport_scope": transport,
                            "abi_version": "4.4.0",
                            "os": "windows",
                            "arch": "x86_64",
                            "library": "nnrp_ffi.dll",
                        }
                    ),
                    encoding="utf-8",
                )

            staged = stage_artifacts(source, output, "4.4.0")

            self.assertEqual(4, len(staged))
            for transport in TRANSPORTS:
                self.assertEqual(
                    transport.encode(),
                    (output / f"transport-{transport}" / "win-x64" / "nnrp_ffi.dll").read_bytes(),
                )

    def test_rejects_missing_transport_or_abi_drift(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            artifact = root / "tcp-linux-x86_64"
            artifact.mkdir()
            (artifact / "libnnrp_ffi.so").write_bytes(b"tcp")
            manifest = {
                "transport_scope": "tcp",
                "abi_version": "4.3.0",
                "os": "linux",
                "arch": "x86_64",
                "library": "libnnrp_ffi.so",
            }
            (artifact / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")

            with self.assertRaisesRegex(ValueError, "expected ABI 4.4.0"):
                stage_artifacts(root, root / "output", "4.4.0")

            manifest["abi_version"] = "4.4.0"
            (artifact / "manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "missing transports"):
                stage_artifacts(root, root / "output", "4.4.0")

    def test_refuses_to_delete_a_filesystem_root(self) -> None:
        root = Path(Path.cwd().anchor)

        with self.assertRaisesRegex(ValueError, "refusing to delete filesystem root"):
            prepare_output_root(root)

    def test_removes_an_existing_non_root_output_directory(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory) / "output"
            output.mkdir()
            (output / "stale.txt").write_text("stale", encoding="utf-8")

            prepare_output_root(output)

            self.assertFalse(output.exists())


if __name__ == "__main__":
    unittest.main()
