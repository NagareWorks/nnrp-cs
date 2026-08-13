from __future__ import annotations

import importlib.util
import copy
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

FROZEN_ROLE_METHODS = {
    "client.open_session": "OpenSessionAsync",
    "client.resume_session": "ResumeSessionAsync",
    "client_session.recovery_ticket": "GetRecoveryTicket",
    "client_session.next_event": "NextEventAsync",
    "server.accept": "AcceptAsync",
    "server_session.next_event": "NextEventAsync",
    "server_session.receive_submit": "ReceiveSubmitAsync",
    "server_operation.send_result": "SendResultAsync",
    "server_operation.send_result_drop": "SendResultDropAsync",
    "server_operation.send_progress": "SendProgressAsync",
    "server_operation.send_partial_result": "SendPartialResultAsync",
}
FROZEN_CLIENT_SUBMIT_WAIT = {
    "scopeRule": "These rules apply when an SDK exposes a cancellable or time-bounded submit-and-wait convenience.",
    "preDispatchCancellationRule": (
        "Cancellation before FRAME_SUBMIT dispatch fails the local wait and emits no submit or cancellation frame."
    ),
    "postDispatchCancellationRule": (
        "Cancellation after FRAME_SUBMIT dispatch fails the local wait with the language-native cancellation error "
        "and sends CANCEL for the submitted operation."
    ),
    "timeoutRule": (
        "A time-bounded submit wait sends DEADLINE before dispatch; expiry fails the local wait with the "
        "language-native timeout error and sends CANCEL for the submitted operation."
    ),
    "lifecycleRule": (
        "The local lifecycle event produced by caller cancellation or wait expiry remains observable through the "
        "client event pump and must not race the same submit wait into a successful NnrpResult return. A terminal "
        "lifecycle initiated independently by the peer may complete the submit wait as NnrpResult evidence."
    ),
}
FROZEN_SERVER_EVENT_PUMP = {
    "canonicalOperation": "server_session.next_event",
    "submitConvenience": "server_session.receive_submit",
    "orderingRule": "next_event delivers every server event in per-session wire order without filtering",
    "submitRule": (
        "receive_submit is a selective convenience that may skip non-submit events only by retaining them in the "
        "same session queue; it must never discard, decode-and-forget, or acknowledge them"
    ),
    "ownershipRule": (
        "a FRAME_SUBMIT event becomes one ServerOperation before it is exposed to the application, so consuming the "
        "canonical event pump never loses the reply capability"
    ),
    "concurrencyRule": (
        "one session has one serialized receive source; concurrent receive calls are rejected or serialized and "
        "never race the native event queue"
    ),
}
FROZEN_SERVER_OPERATION_INVARIANTS = [
    "submit.header.message_type is frame_submit",
    "submit.metadata is the frame_submit metadata variant",
    "operation_id equals submit.metadata.operation_id",
    "frame_id equals submit.header.frame_id",
    "the reply capability remains valid until exactly one terminal outcome is sent or the session closes",
]
FROZEN_RESULT_SUCCESS_RULE = (
    "A successful result has terminal_state success and an event whose message type is result_push "
    "and whose metadata variant is result_push."
)
FROZEN_RESULT_NON_SUCCESS_RULE = (
    "Cancelled, dropped, and error results preserve the terminal protocol or lifecycle event that "
    "established the state; SDKs do not synthesize RESULT_PUSH metadata for them."
)


def contract() -> dict[str, object]:
    return {
        "contractVersion": 15,
        "languageProjections": {
            "csharp": copy.deepcopy(CHECKER.EXPECTED_CSHARP_PROJECTIONS),
        },
        "roleSurfaces": {
            "clientSubmitWait": copy.deepcopy(FROZEN_CLIENT_SUBMIT_WAIT),
            "serverEventPump": copy.deepcopy(FROZEN_SERVER_EVENT_PUMP),
        },
        "types": {
            "NnrpResult": {
                "successRule": FROZEN_RESULT_SUCCESS_RULE,
                "nonSuccessRule": FROZEN_RESULT_NON_SUCCESS_RULE,
            },
            "ServerOperation": {
                "invariants": copy.deepcopy(FROZEN_SERVER_OPERATION_INVARIANTS),
            },
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

    def test_contract_fixture_does_not_share_nested_mutable_state(self) -> None:
        first = contract()
        first_role_surfaces = cast(dict[str, object], first["roleSurfaces"])
        first_submit_wait = cast(dict[str, object], first_role_surfaces["clientSubmitWait"])
        first_submit_wait["timeoutRule"] = "mutated"
        first_types = cast(dict[str, object], first["types"])
        first_server_operation = cast(dict[str, object], first_types["ServerOperation"])
        first_invariants = cast(list[str], first_server_operation["invariants"])
        first_invariants.clear()

        second = contract()
        second_role_surfaces = cast(dict[str, object], second["roleSurfaces"])
        second_submit_wait = cast(dict[str, object], second_role_surfaces["clientSubmitWait"])
        self.assertEqual(FROZEN_CLIENT_SUBMIT_WAIT["timeoutRule"], second_submit_wait["timeoutRule"])
        second_types = cast(dict[str, object], second["types"])
        second_server_operation = cast(dict[str, object], second_types["ServerOperation"])
        self.assertEqual(
            FROZEN_SERVER_OPERATION_INVARIANTS,
            second_server_operation["invariants"],
        )

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
        server_event_pump = dict(FROZEN_SERVER_EVENT_PUMP)
        server_event_pump["ownershipRule"] = "submit events may be decoded without an operation owner"
        role_surfaces["serverEventPump"] = server_event_pump
        with self.assertRaisesRegex(SystemExit, "server event-pump semantics drifted"):
            self.check(value)

    def test_rejects_client_submit_wait_drift(self) -> None:
        value = contract()
        role_surfaces = cast(dict[str, object], value["roleSurfaces"])
        client_submit_wait = dict(FROZEN_CLIENT_SUBMIT_WAIT)
        client_submit_wait["timeoutRule"] = "expiry returns success"
        role_surfaces["clientSubmitWait"] = client_submit_wait
        with self.assertRaisesRegex(SystemExit, "client submit-wait semantics drifted"):
            self.check(value)

    def test_rejects_server_operation_invariant_drift(self) -> None:
        value = contract()
        types = cast(dict[str, object], value["types"])
        operation = cast(dict[str, object], types["ServerOperation"])
        operation["invariants"] = FROZEN_SERVER_OPERATION_INVARIANTS[:-1]
        with self.assertRaisesRegex(SystemExit, "ServerOperation invariants drifted"):
            self.check(value)

    def test_rejects_result_terminal_evidence_drift(self) -> None:
        value = contract()
        types = cast(dict[str, object], value["types"])
        result = cast(dict[str, object], types["NnrpResult"])
        result["nonSuccessRule"] = "synthesize result_push metadata"
        with self.assertRaisesRegex(SystemExit, "NnrpResult terminal evidence rules drifted"):
            self.check(value)

    def test_rejects_missing_language_projections_with_clean_diagnostic(self) -> None:
        value = contract()
        del value["languageProjections"]
        with self.assertRaisesRegex(SystemExit, "languageProjections is missing or invalid"):
            self.check(value)

    def test_rejects_complete_csharp_projection_drift(self) -> None:
        value = contract()
        projections = cast(dict[str, object], value["languageProjections"])
        csharp = cast(dict[str, object], projections["csharp"])
        csharp["typedPayloadFrame"] = "Nnrp.Core.LegacyTypedPayloadFrame"
        with self.assertRaisesRegex(SystemExit, "C# SDK projection map drifted"):
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
