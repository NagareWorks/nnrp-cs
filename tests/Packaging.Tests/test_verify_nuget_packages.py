from __future__ import annotations

import importlib.util
import tempfile
import unittest
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "verify_nuget_packages.py"


def load_verifier():
    spec = importlib.util.spec_from_file_location("verify_nuget_packages", SCRIPT)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"unable to load {SCRIPT}")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def write_package(path: Path, package_id: str, version: str, files: set[str]) -> None:
    nuspec = f"""<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata><id>{package_id}</id><version>{version}</version></metadata>
</package>
"""
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(f"{package_id}.nuspec", nuspec)
        for name in files:
            archive.writestr(name, b"fixture")


class VerifyNugetPackagesTests(unittest.TestCase):
    def setUp(self):
        self.verifier = load_verifier()
        self.version = "1.0.0-preview.4"

    def write_complete_set(self, root: Path) -> None:
        for package_id in self.verifier.ROLE_PACKAGE_IDS:
            write_package(root / f"{package_id}.{self.version}.nupkg", package_id, self.version, set())
        for package_id, transport in self.verifier.TRANSPORT_PACKAGES.items():
            write_package(
                root / f"{package_id}.{self.version}.nupkg",
                package_id,
                self.version,
                self.verifier.expected_transport_paths(transport),
            )

    def test_complete_transport_scoped_package_set_passes(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            self.write_complete_set(root)
            self.verifier.verify_packages(root, self.version)

    def test_missing_transport_package_fails(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            self.write_complete_set(root)
            (root / f"Nnrp.Transport.Ipc.{self.version}.nupkg").unlink()
            with self.assertRaisesRegex(ValueError, "Nnrp.Transport.Ipc"):
                self.verifier.verify_packages(root, self.version)

    def test_role_package_cannot_hide_native_artifact(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            self.write_complete_set(root)
            client = root / f"Nnrp.Client.{self.version}.nupkg"
            write_package(
                client,
                "Nnrp.Client",
                self.version,
                {"runtimes/win-x64/native/nnrp_ffi.dll"},
            )
            with self.assertRaisesRegex(ValueError, "contains native artifacts"):
                self.verifier.verify_packages(root, self.version)

    def test_transport_package_cannot_contain_another_transport(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            self.write_complete_set(root)
            tcp = root / f"Nnrp.Transport.Tcp.{self.version}.nupkg"
            paths = self.verifier.expected_transport_paths("tcp")
            paths.add(
                "runtimes/win-x64/native/nnrp/transport/quic/nnrp_ffi_quic.dll"
            )
            write_package(tcp, "Nnrp.Transport.Tcp", self.version, paths)
            with self.assertRaisesRegex(ValueError, "unexpected="):
                self.verifier.verify_packages(root, self.version)


if __name__ == "__main__":
    unittest.main()
