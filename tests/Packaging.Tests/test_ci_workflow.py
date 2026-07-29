import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CI_WORKFLOW = ROOT / ".github" / "workflows" / "ci.yml"
CONFORMANCE_ROOT = ROOT / "conformance"


class CiWorkflowTests(unittest.TestCase):
    def test_conformance_job_is_pinned_to_the_only_preview4_capability_manifest(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")
        manifests = list(CONFORMANCE_ROOT.glob("*.capabilities.json"))

        self.assertEqual(["nnrp-1-preview4.capabilities.json"], [path.name for path in manifests])
        manifest = json.loads(manifests[0].read_text(encoding="utf-8"))
        self.assertEqual("nnrp-1-preview4", manifest["protocol_version"])
        self.assertIn("Expected exactly one capability manifest", workflow)
        self.assertIn("Run suite-owned conformance action", workflow)
        self.assertIn("- conformance", workflow)

    def test_package_validation_canonicalizes_archives_before_inspection(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")
        pack_index = workflow.index("Pack release package graph")
        canonical_index = workflow.index("Canonicalize NuGet package archives")
        verify_index = workflow.index("Verify package boundaries and clean installs")
        self.assertLess(pack_index, canonical_index)
        self.assertLess(canonical_index, verify_index)
        self.assertIn("python scripts/canonicalize_nuget_packages.py", workflow)

    def test_native_foreign_abi_jobs_are_required(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")

        self.assertIn("native-artifacts:", workflow)
        self.assertIn("--version 1.0.0-preview.4.19", workflow)
        self.assertIn("--require-abi-version 4.1.1", workflow)
        self.assertIn("coordinated-native-artifacts", workflow)
        self.assertNotIn("scripts/package_native_artifacts.py", workflow)
        self.assertIn("native-e2e:", workflow)
        self.assertIn("os: [ubuntu-latest, macos-latest, windows-latest]", workflow)
        self.assertIn("native-e2e-windows-x86:", workflow)
        self.assertIn("architecture: x86", workflow)
        self.assertNotIn("--rid win-x86", workflow)
        self.assertNotIn("--transport websocket", workflow)
        self.assertIn("transport-tcp/win-x86/nnrp_ffi.dll", workflow)
        self.assertIn("transport-quic/win-x86/nnrp_ffi.dll", workflow)
        self.assertIn("transport-ipc/win-x86/nnrp_ffi.dll", workflow)
        self.assertIn("transport-websocket/win-x86/nnrp_ffi.dll", workflow)
        self.assertIn("Run x86 TCP provider E2E", workflow)
        self.assertIn("NNRP_NATIVE_WEBSOCKET_ARTIFACT_PATH", workflow)
        self.assertIn("Run WebSocket provider E2E", workflow)
        self.assertIn("Run x86 WebSocket provider E2E", workflow)
        self.assertIn("- native-e2e", workflow)
        self.assertIn("- native-e2e-windows-x86", workflow)
        self.assertIn("Windows x86 native artifact E2E validation failed.", workflow)
        self.assertIn("package-validation:", workflow)
        self.assertIn("Verify package boundaries and clean installs", workflow)
        self.assertIn("--smoke-install", workflow)
        self.assertIn("- package-validation", workflow)
        self.assertIn("NuGet package boundary or clean-install validation failed.", workflow)


if __name__ == "__main__":
    unittest.main()
