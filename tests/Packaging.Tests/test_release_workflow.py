from __future__ import annotations

import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
WORKFLOW = ROOT / ".github" / "workflows" / "release.yml"


class ReleaseWorkflowTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.workflow = WORKFLOW.read_text(encoding="utf-8")

    def test_defaults_to_current_rust_artifact_release(self):
        self.assertEqual(self.workflow.count("1.0.0-preview.4.19"), 2)
        self.assertNotIn("1.0.0-preview.4.15", self.workflow)
        self.assertIn("--require-abi-version 4.1.1", self.workflow)

    def test_verifies_packed_nuget_boundaries_before_bundling_or_publishing(self):
        verify_index = self.workflow.index("Verify NuGet package boundaries")
        bundle_index = self.workflow.index("Bundle release artifacts")
        publish_index = self.workflow.index("- name: Publish NuGet packages to GitHub Packages")
        self.assertLess(verify_index, bundle_index)
        self.assertLess(verify_index, publish_index)
        self.assertIn("python scripts/verify_nuget_packages.py", self.workflow)
        self.assertIn("--packages artifacts/packages", self.workflow)

    def test_release_bundles_include_websocket_provider(self):
        self.assertEqual(self.workflow.count("'Nnrp.Transport.WebSocket'"), 2)

    def test_repository_tools_are_not_published_as_nuget_packages(self):
        for project in (
            "tools/Nnrp.BenchmarkAdapter/Nnrp.BenchmarkAdapter.csproj",
            "tools/Nnrp.ConformanceAdapter/Nnrp.ConformanceAdapter.csproj",
            "tools/Nnrp.WireConformance/Nnrp.WireConformance.csproj",
        ):
            with self.subTest(project=project):
                project_text = (ROOT / project).read_text(encoding="utf-8")
                self.assertIn("<IsPackable>false</IsPackable>", project_text)


if __name__ == "__main__":
    unittest.main()
