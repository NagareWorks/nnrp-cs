from __future__ import annotations

import importlib.util
import tempfile
import unittest
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "canonicalize_nuget_packages.py"
SPEC = importlib.util.spec_from_file_location("canonicalize_nuget_packages", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
CANONICALIZER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CANONICALIZER)


class CanonicalizeNugetPackagesTests(unittest.TestCase):
    def test_semantically_identical_packages_become_byte_identical(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            first = root / "first.nupkg"
            second = root / "second.nupkg"
            self.write_fixture(first, "a" * 32, "RANDOM1", reversed_order=False)
            self.write_fixture(second, "b" * 32, "RANDOM2", reversed_order=True)

            CANONICALIZER.canonicalize_archive(first)
            CANONICALIZER.canonicalize_archive(second)

            self.assertEqual(first.read_bytes(), second.read_bytes())
            CANONICALIZER.verify_canonical_archive(first)
            CANONICALIZER.verify_canonical_archive(second)

    def test_signed_package_is_rejected(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            package = Path(directory) / "signed.nupkg"
            self.write_fixture(package, "a" * 32, "RANDOM", reversed_order=False)
            with zipfile.ZipFile(package, "a") as archive:
                archive.writestr(".signature.p7s", b"signature")

            with self.assertRaisesRegex(ValueError, "signed package"):
                CANONICALIZER.canonicalize_archive(package)

    @staticmethod
    def write_fixture(
        path: Path,
        core_name: str,
        relationship_id: str,
        *,
        reversed_order: bool,
    ) -> None:
        nuspec = b"""<?xml version="1.0"?>
<package><metadata><id>Nnrp.Core</id><version>1.0.0-preview.4</version></metadata></package>
"""
        relationships = f"""<?xml version="1.0"?>
<Relationships xmlns="{CANONICALIZER.RELATIONSHIP_NAMESPACE}">
  <Relationship Type="{CANONICALIZER.CORE_PROPERTIES_RELATIONSHIP}" Target="/package/services/metadata/core-properties/{core_name}.psmdcp" Id="{relationship_id}" />
  <Relationship Type="http://schemas.microsoft.com/packaging/2010/07/manifest" Target="/Nnrp.Core.nuspec" Id="MANIFEST" />
</Relationships>
""".encode()
        files = [
            ("_rels/.rels", relationships),
            ("Nnrp.Core.nuspec", nuspec),
            (f"package/services/metadata/core-properties/{core_name}.psmdcp", b"metadata"),
            ("lib/netstandard2.1/Nnrp.Core.dll", b"assembly"),
        ]
        if reversed_order:
            files.reverse()
        with zipfile.ZipFile(path, "w") as archive:
            for name, content in files:
                archive.writestr(name, content)


if __name__ == "__main__":
    unittest.main()
