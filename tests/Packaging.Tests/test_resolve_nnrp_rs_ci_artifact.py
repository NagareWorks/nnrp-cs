import importlib.util
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "resolve_nnrp_rs_ci_artifact.py"
SPEC = importlib.util.spec_from_file_location("resolve_nnrp_rs_ci_artifact", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class ResolveNnrpRsCiArtifactTests(unittest.TestCase):
    def test_maps_github_runner_identity_to_release_artifact_identity(self) -> None:
        cases = {
            ("Linux", "X64"): "nnrp-ffi-native-linux-x86_64",
            ("macOS", "ARM64"): "nnrp-ffi-native-macos-aarch64",
            ("macOS", "X64"): "nnrp-ffi-native-macos-x86_64",
            ("Windows", "X64"): "nnrp-ffi-native-windows-x86_64",
            ("Windows", "X86"): "nnrp-ffi-native-windows-x86",
        }

        for identity, expected in cases.items():
            with self.subTest(identity=identity):
                self.assertEqual(expected, MODULE.artifact_name(*identity))

    def test_rejects_unknown_runner_identity(self) -> None:
        with self.assertRaisesRegex(ValueError, "unsupported runner OS"):
            MODULE.artifact_name("Plan9", "X64")
        with self.assertRaisesRegex(ValueError, "unsupported runner architecture"):
            MODULE.artifact_name("Linux", "MIPS")


if __name__ == "__main__":
    unittest.main()
