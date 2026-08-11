from __future__ import annotations

import importlib.util
import json
import tempfile
import unittest
from pathlib import Path
from typing import cast


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "check_sdk_api_contract.py"
SPEC = importlib.util.spec_from_file_location("check_sdk_api_contract", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"Unable to load SDK API contract checker from {SCRIPT}")
CHECKER = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CHECKER)


def contract() -> dict[str, object]:
    return {
        "contractVersion": CHECKER.EXPECTED_CONTRACT_VERSION,
        "languageProjections": {
            "csharp": {
                "clientEvent": "Nnrp.Runtime.NnrpClientEvent",
                "serverEvent": "Nnrp.Server.NnrpServerEvent",
                "serverOperation": "Nnrp.Server.NnrpServerOperation",
                "roleMethods": CHECKER.EXPECTED_ROLE_METHODS,
            }
        },
        "roleSurfaces": {
            "clientSubmitWait": CHECKER.EXPECTED_CLIENT_SUBMIT_WAIT,
            "serverEventPump": CHECKER.EXPECTED_SERVER_EVENT_PUMP,
        },
    }


class CheckSdkApiContractTests(unittest.TestCase):
    def check(self, value: dict[str, object]) -> None:
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "contract.json"
            path.write_text(json.dumps(value), encoding="utf-8")
            CHECKER.check_contract(path, ROOT)

    def test_accepts_frozen_v15_role_contract_and_sources(self) -> None:
        self.check(contract())

    def test_rejects_contract_version_drift(self) -> None:
        value = contract()
        value["contractVersion"] = 14
        with self.assertRaisesRegex(SystemExit, "expected SDK contract version 15"):
            self.check(value)

    def test_rejects_server_event_pump_drift(self) -> None:
        value = contract()
        role_surfaces = value["roleSurfaces"]
        self.assertIsInstance(role_surfaces, dict)
        role_surfaces = cast(dict[str, object], role_surfaces)
        server_event_pump = dict(CHECKER.EXPECTED_SERVER_EVENT_PUMP)
        server_event_pump["ownershipRule"] = "submit events may be decoded without an operation owner"
        role_surfaces["serverEventPump"] = server_event_pump
        with self.assertRaisesRegex(SystemExit, "server event-pump semantics drifted"):
            self.check(value)

    def test_rejects_missing_language_projections_with_clean_diagnostic(self) -> None:
        value = contract()
        del value["languageProjections"]
        with self.assertRaisesRegex(SystemExit, "languageProjections is missing or invalid"):
            self.check(value)

    def test_rejects_missing_role_surfaces_with_clean_diagnostic(self) -> None:
        value = contract()
        del value["roleSurfaces"]
        with self.assertRaisesRegex(SystemExit, "roleSurfaces is missing or invalid"):
            self.check(value)

    def test_type_declaration_reports_missing_frozen_type(self) -> None:
        with self.assertRaisesRegex(SystemExit, "NnrpServerSession is missing frozen API token"):
            CHECKER.type_declaration("namespace Nnrp.Server;", "public sealed class NnrpServerSession", "NnrpServerSession")

    def test_type_declaration_does_not_scan_a_later_type(self) -> None:
        source = """
namespace Nnrp.Server
{
    public sealed class NnrpServerSession
    {
        public ValueTask NextEventAsync() => default;
    }

    public sealed class NnrpServerOperation
    {
        public ValueTask SendProgressAsync(
            ProgressMetadata metadata) => default;
    }
}
"""
        declaration = CHECKER.type_declaration(
            source,
            "public sealed class NnrpServerSession",
            "NnrpServerSession",
        )
        self.assertNotIn("SendProgressAsync", declaration)

    def test_forbidden_method_check_ignores_signature_whitespace(self) -> None:
        source = """
namespace Nnrp.Server
{
    public sealed class NnrpServerSession
    {
        public async
            ValueTask
            SendProgressAsync
            (
                ProgressMetadata
                metadata,
                CancellationToken cancellationToken = default)
        {
            await ValueTask.CompletedTask;
        }
    }
}
"""
        with self.assertRaisesRegex(SystemExit, "NnrpServerSession exposes an operation-owned reply"):
            CHECKER.require_no_public_method(
                source,
                "public sealed class NnrpServerSession",
                "NnrpServerSession",
                "ValueTask",
                "SendProgressAsync",
                "ProgressMetadata",
            )

    def test_forbidden_method_check_allows_operation_owned_first_parameter(self) -> None:
        source = """
namespace Nnrp.NativeBridge
{
    public sealed class NnrpNativeRuntimeServerSession
    {
        public void SendProgress(
            NnrpNativeRuntimeOperation operation,
            ProgressMetadata metadata)
        {
        }
    }
}
"""
        CHECKER.require_no_public_method(
            source,
            "public sealed class NnrpNativeRuntimeServerSession",
            "C# native server session",
            "void",
            "SendProgress",
            "ProgressMetadata",
        )

    def test_forbidden_native_method_check_ignores_signature_whitespace(self) -> None:
        source = """
namespace Nnrp.NativeBridge
{
    public sealed class NnrpNativeRuntimeServerSession
    {
        public
            void SendResultDropReason (
                ResultDropReasonMetadata metadata)
        {
        }
    }
}
"""
        with self.assertRaisesRegex(SystemExit, "C# native server session exposes an operation-owned reply"):
            CHECKER.require_no_public_method(
                source,
                "public sealed class NnrpNativeRuntimeServerSession",
                "C# native server session",
                "void",
                "SendResultDropReason",
                "ResultDropReasonMetadata",
            )


if __name__ == "__main__":
    unittest.main()
