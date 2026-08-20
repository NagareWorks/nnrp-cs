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
        self.assertIn('require-complete-capability-coverage: "true"', workflow)
        self.assertIn("- conformance", workflow)
        self.assertIn(
            "NNRP_CONFORMANCE_SOURCE_COMMIT: d1c2bc6aee489e271a75567c45f56bd966fb90cb",
            workflow,
        )
        self.assertIn(
            "NNRP_DOC_SOURCE_COMMIT: 4319692b4c0a697fe5d360e55bafa2b83f5bbb3d",
            workflow,
        )
        self.assertEqual(1, workflow.count("ref: ${{ env.NNRP_DOC_SOURCE_COMMIT }}"))
        self.assertEqual(4, workflow.count("ref: ${{ env.NNRP_CONFORMANCE_SOURCE_COMMIT }}"))

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
        self.assertIn("--version 1.0.0-preview.4.25", workflow)
        self.assertIn("--require-abi-version 4.4.0", workflow)
        self.assertIn("--workflow-run-id 32365855992", workflow)
        self.assertIn("--workflow-head-sha 35b4ed1e0764623d278035ca1449daeab4192c5c", workflow)
        self.assertIn("NNRP_RS_SOURCE_COMMIT: 35b4ed1e0764623d278035ca1449daeab4192c5c", workflow)
        self.assertIn("NNRP_RS_ARTIFACT_RUN_ID: '32365688997'", workflow)
        self.assertEqual(5, workflow.count("Download final nnrp-rs CI artifacts"))
        self.assertEqual(5, workflow.count("scripts/stage_nnrp_rs_ci_artifacts.py"))
        self.assertEqual(5, workflow.count("Verify final nnrp-rs CI source"))
        self.assertEqual(
            5,
            workflow.count("nnrp-rs CI run does not match the frozen successful source commit."),
        )
        self.assertIn("coordinated-native-artifacts", workflow)
        self.assertNotIn("scripts/package_native_artifacts.py", workflow)
        self.assertIn("native-e2e:", workflow)
        self.assertIn("os: [ubuntu-latest, macos-latest, windows-latest]", workflow)
        self.assertIn("native-e2e-windows-x86:", workflow)
        self.assertIn("architecture: x86", workflow)
        self.assertEqual(5, workflow.count("Resolve final nnrp-rs CI artifact name"))
        self.assertEqual(
            5,
            workflow.count("name: ${{ steps.nnrp-rs-artifact.outputs.name }}"),
        )
        self.assertEqual(
            5,
            workflow.count("python scripts/resolve_nnrp_rs_ci_artifact.py"),
        )
        self.assertIn("--runner-arch X86", workflow)
        self.assertNotIn("nnrp-ffi-native-${{ runner.os }}-${{ runner.arch }}", workflow)
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

    def test_host_route_wire_e2e_is_cross_platform_and_required(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")
        harness = (ROOT / "scripts" / "run_wire_host_route_e2e.ps1").read_text(encoding="utf-8")

        self.assertIn("wire-host-route-e2e:", workflow)
        self.assertIn("os: [ubuntu-latest, macos-latest, windows-latest]", workflow)
        self.assertIn("Checkout nnrp-conformance wire suite", workflow)
        self.assertIn("scripts/run_wire_host_route_e2e.ps1", workflow)
        self.assertIn("Upload host-route wire evidence", workflow)
        self.assertIn("wire-host-route-e2e-${{ runner.os }}", workflow)
        self.assertIn("- wire-host-route-e2e", workflow)
        self.assertIn("Cross-platform host-route wire E2E validation failed.", workflow)
        self.assertIn("$repositoryPrefix", harness)
        self.assertIn("[System.IO.Path]::DirectorySeparatorChar", harness)
        self.assertIn(
            '(ProviderJson "websocket" "nnrp.transport.websocket.native" $true @("plain", "wss"))',
            harness,
        )
        self.assertIn("Assert-AllCasesPassed $installedPlan $installedResults 10", harness)
        self.assertIn("10 installed scenarios and 1 known-uninstalled scenario", harness)

    def test_runtime_frame_wire_e2e_is_cross_platform_and_required(self) -> None:
        workflow = CI_WORKFLOW.read_text(encoding="utf-8")
        harness = (ROOT / "scripts" / "run_wire_runtime_e2e.ps1").read_text(encoding="utf-8")

        self.assertIn("wire-runtime-e2e:", workflow)
        self.assertIn("os: [ubuntu-latest, macos-latest, windows-latest]", workflow)
        self.assertIn("- wire-runtime-e2e", workflow)
        self.assertIn("scripts/run_wire_runtime_e2e.ps1", workflow)
        self.assertIn("wire-plan", harness)
        self.assertIn("wire-run", harness)
        self.assertIn("validate-wire-results", harness)
        self.assertIn("Assert-CompleteWireReport", harness)
        self.assertIn('$requiredScenarioIds = @("wire.control.cancel-abort.client")', harness)
        self.assertIn("timestamp_us", harness)
        self.assertIn("evidence_paths", harness)
        self.assertIn("EvidenceRoot", harness)
        self.assertIn("Assert-NoLinkTraversal", harness)
        self.assertIn("FileAttributes]::ReparsePoint", harness)
        self.assertIn('Properties.Name -contains "LinkType"', harness)
        self.assertIn("Invoke-ExpectedCommandFailure", harness)
        self.assertIn("WaitForExit(30000)", harness)
        self.assertIn("HasExited", harness)
        self.assertIn("System.InvalidOperationException", harness)
        self.assertIn("Kill($true)", harness)
        self.assertIn('"missing-frames.json"', harness)
        self.assertIn('"unexpected-frame.json"', harness)
        self.assertIn('"reordered-frames.json"', harness)
        self.assertIn('"terminal-mismatch.json"', harness)
        self.assertIn('"duplicate-scenario.json"', harness)
        self.assertIn('"missing-evidence.json"', harness)
        self.assertIn('"missing-timing.json"', harness)
        self.assertIn(
            'ExpectedText "missing or reordered expected frame TRACE_CONTEXT"', harness
        )
        self.assertIn('ExpectedText "unexpected frame UNDECLARED_FRAME"', harness)
        self.assertIn(
            'ExpectedText "missing or reordered expected frame RESULT_DROP_REASON"', harness
        )
        self.assertIn('ExpectedText "terminal mismatch"', harness)
        self.assertIn('ExpectedText "duplicate scenario id"', harness)
        self.assertIn('ExpectedText "exactly one suite-owned evidence path"', harness)
        self.assertIn('ExpectedText "frame without timing evidence"', harness)
        self.assertIn("$evidencePrefix", harness)
        self.assertIn("$pathComparison", harness)
        self.assertIn("StringComparison]::Ordinal", harness)
        self.assertIn("StandardOutput.ReadToEndAsync", harness)
        self.assertIn("StandardError.ReadToEndAsync", harness)
        self.assertIn("$targetProcessStarted", harness)
        self.assertIn("$null -eq $targetStdoutTask", harness)
        self.assertIn("$null -eq $targetStderrTask", harness)
        self.assertIn("Cross-platform runtime-frame wire E2E validation failed.", workflow)
        self.assertIn("$repositoryPrefix", harness)
        self.assertIn("[System.IO.Path]::DirectorySeparatorChar", harness)
        self.assertIn("$isRepositoryRoot", harness)


if __name__ == "__main__":
    unittest.main()
