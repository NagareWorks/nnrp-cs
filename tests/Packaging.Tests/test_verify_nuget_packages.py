from __future__ import annotations

import importlib.util
import tempfile
import unittest
import xml.etree.ElementTree as ET
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


def write_package(
    path: Path,
    package_id: str,
    version: str,
    files: set[str],
    dependencies: set[str],
) -> None:
    dependency_xml = "".join(
        f'<dependency id="{dependency}" version="[{version}]" />'
        for dependency in sorted(dependencies)
    )
    nuspec = f"""<?xml version="1.0"?>
<package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
  <metadata>
    <id>{package_id}</id><version>{version}</version>
    <authors>NNRP Contributors</authors><description>Package fixture.</description>
    <tags>nnrp;protocol</tags><readme>README.md</readme>
    <license type="file">LICENSE</license>
    <repository type="git" url="https://github.com/NagareWorks/nnrp-cs" />
    <dependencies><group targetFramework=".NETStandard2.1">{dependency_xml}</group></dependencies>
  </metadata>
</package>
"""
    with zipfile.ZipFile(path, "w") as archive:
        archive.writestr(f"{package_id}.nuspec", nuspec)
        for name in files | {"README.md", "LICENSE", f"lib/netstandard2.1/{package_id}.dll"}:
            archive.writestr(name, b"fixture")
    with zipfile.ZipFile(path.with_suffix(".snupkg"), "w") as archive:
        archive.writestr(f"lib/netstandard2.1/{package_id}.pdb", b"symbols")


class VerifyNugetPackagesTests(unittest.TestCase):
    def setUp(self):
        self.verifier = load_verifier()
        self.version = "1.0.0-preview.4"

    def write_complete_set(self, root: Path) -> None:
        for package_id in self.verifier.ROLE_PACKAGE_IDS:
            write_package(
                root / f"{package_id}.{self.version}.nupkg",
                package_id,
                self.version,
                set(),
                self.verifier.EXPECTED_DEPENDENCIES[package_id],
            )
        for package_id, transport in self.verifier.TRANSPORT_PACKAGES.items():
            write_package(
                root / f"{package_id}.{self.version}.nupkg",
                package_id,
                self.version,
                self.verifier.expected_transport_paths(transport),
                self.verifier.EXPECTED_DEPENDENCIES[package_id],
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
                self.verifier.EXPECTED_DEPENDENCIES["Nnrp.Client"],
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
            write_package(
                tcp,
                "Nnrp.Transport.Tcp",
                self.version,
                paths,
                self.verifier.EXPECTED_DEPENDENCIES["Nnrp.Transport.Tcp"],
            )
            with self.assertRaisesRegex(ValueError, "unexpected="):
                self.verifier.verify_packages(root, self.version)

    def test_role_package_dependency_direction_is_exact(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            self.write_complete_set(root)
            client = root / f"Nnrp.Client.{self.version}.nupkg"
            write_package(
                client,
                "Nnrp.Client",
                self.version,
                set(),
                {"Nnrp.Core", "Nnrp.NativeBridge", "Nnrp.Server"},
            )
            with self.assertRaisesRegex(ValueError, "dependency boundary mismatch"):
                self.verifier.verify_packages(root, self.version)

    def test_missing_symbol_package_fails(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            self.write_complete_set(root)
            (root / f"Nnrp.Core.{self.version}.snupkg").unlink()
            with self.assertRaisesRegex(ValueError, "missing symbol package"):
                self.verifier.verify_packages(root, self.version)

    def test_smoke_nuget_config_pins_nnrp_packages_to_local_source(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            root = Path(temp_dir)
            config = root / "NuGet.Config"
            packages = root / "packages"
            self.verifier.write_smoke_nuget_config(config, packages)

            document = ET.parse(config)
            source_mappings = {
                source.attrib["key"]: [package.attrib["pattern"] for package in source]
                for source in document.findall("./packageSourceMapping/packageSource")
            }
            self.assertEqual(["Nnrp.*"], source_mappings["NNRP Local"])
            self.assertEqual(["Microsoft.*"], source_mappings["NuGet.org"])


if __name__ == "__main__":
    unittest.main()
