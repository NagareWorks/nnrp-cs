import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
CI_WORKFLOW = ROOT / ".github" / "workflows" / "ci.yml"


class CiWorkflowTests(unittest.TestCase):
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
        self.assertIn("--rid win-x86", workflow)
        self.assertIn("Run x86 TCP provider E2E", workflow)
        self.assertIn("- native-e2e", workflow)
        self.assertIn("- native-e2e-windows-x86", workflow)
        self.assertIn("Windows x86 native artifact E2E validation failed.", workflow)


if __name__ == "__main__":
    unittest.main()
