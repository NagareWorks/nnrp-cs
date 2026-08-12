from __future__ import annotations

import argparse
import json
import re
from pathlib import Path
from typing import Any


EXPECTED_CONTRACT_VERSION = 15
EXPECTED_ROLE_METHODS = {
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
EXPECTED_CLIENT_SUBMIT_WAIT = {
    "scopeRule": (
        "These rules apply when an SDK exposes a cancellable or time-bounded "
        "submit-and-wait convenience."
    ),
    "preDispatchCancellationRule": (
        "Cancellation before FRAME_SUBMIT dispatch fails the local wait and emits no submit "
        "or cancellation frame."
    ),
    "postDispatchCancellationRule": (
        "Cancellation after FRAME_SUBMIT dispatch fails the local wait with the language-native "
        "cancellation error and sends CANCEL for the submitted operation."
    ),
    "timeoutRule": (
        "A time-bounded submit wait sends DEADLINE before dispatch; expiry fails the local wait "
        "with the language-native timeout error and sends CANCEL for the submitted operation."
    ),
    "lifecycleRule": (
        "The local lifecycle event produced by caller cancellation or wait expiry remains "
        "observable through the client event pump and must not race the same submit wait into a "
        "successful NnrpResult return. A terminal lifecycle initiated independently by the peer "
        "may complete the submit wait as NnrpResult evidence."
    ),
}
EXPECTED_SERVER_EVENT_PUMP = {
    "canonicalOperation": "server_session.next_event",
    "submitConvenience": "server_session.receive_submit",
    "orderingRule": "next_event delivers every server event in per-session wire order without filtering",
    "submitRule": (
        "receive_submit is a selective convenience that may skip non-submit events only by retaining "
        "them in the same session queue; it must never discard, decode-and-forget, or acknowledge them"
    ),
    "ownershipRule": (
        "a FRAME_SUBMIT event becomes one ServerOperation before it is exposed to the application, "
        "so consuming the canonical event pump never loses the reply capability"
    ),
    "concurrencyRule": (
        "one session has one serialized receive source; concurrent receive calls are rejected or "
        "serialized and never race the native event queue"
    ),
}
EXPECTED_SERVER_OPERATION_INVARIANTS = [
    "submit.header.message_type is frame_submit",
    "submit.metadata is the frame_submit metadata variant",
    "operation_id equals submit.metadata.operation_id",
    "frame_id equals submit.header.frame_id",
    "the reply capability remains valid until exactly one terminal outcome is sent or the session closes",
]
EXPECTED_RESULT_SUCCESS_RULE = (
    "A successful result has terminal_state success and an event whose message type is result_push "
    "and whose metadata variant is result_push."
)
EXPECTED_RESULT_NON_SUCCESS_RULE = (
    "Cancelled, dropped, and error results preserve the terminal protocol or lifecycle event that "
    "established the state; SDKs do not synthesize RESULT_PUSH metadata for them."
)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise SystemExit(message)


def read_source(source_root: Path, relative_path: str) -> str:
    path = source_root / relative_path
    require(path.is_file(), f"C# SDK source is missing {relative_path}")
    return path.read_text(encoding="utf-8")


def require_tokens(source: str, tokens: list[str], subject: str) -> None:
    for token in tokens:
        require(token in source, f"{subject} is missing frozen API token: {token}")


def require_mapping(value: Any, subject: str) -> dict[str, Any]:
    require(isinstance(value, dict), f"{subject} is missing or invalid")
    return value


def type_declaration(source: str, declaration: str, subject: str) -> str:
    start = source.find(declaration)
    require(start >= 0, f"{subject} is missing frozen API token: {declaration}")
    next_type = re.search(
        r"\n    public (?:sealed |static |abstract )?(?:class|record|interface|struct|enum) ",
        source[start + len(declaration) :],
    )
    if next_type is None:
        return source[start:]
    end = start + len(declaration) + next_type.start()
    return source[start:end]


def require_no_public_method(
    source: str,
    declaration: str,
    subject: str,
    return_type: str,
    method_name: str,
    first_parameter_type: str,
) -> None:
    declaration_source = type_declaration(source, declaration, subject)
    modifiers = r"(?:(?:async|static|virtual|override|sealed|new)\s+)*"
    pattern = re.compile(
        rf"\bpublic\s+{modifiers}{re.escape(return_type)}\s+"
        rf"{re.escape(method_name)}\s*\(\s*"
        rf"{re.escape(first_parameter_type)}\s+[_A-Za-z][_A-Za-z0-9]*\b",
        re.MULTILINE,
    )
    require(
        pattern.search(declaration_source) is None,
        f"{subject} exposes an operation-owned reply: {method_name}",
    )


def check_contract(contract_path: Path, source_root: Path) -> None:
    contract: dict[str, Any] = json.loads(contract_path.read_text(encoding="utf-8"))
    require(
        contract.get("contractVersion") == EXPECTED_CONTRACT_VERSION,
        f"expected SDK contract version {EXPECTED_CONTRACT_VERSION}",
    )

    language_projections = require_mapping(contract.get("languageProjections"), "languageProjections")
    projection = require_mapping(language_projections.get("csharp"), "C# language projection")
    require(projection.get("clientEvent") == "Nnrp.Runtime.NnrpClientEvent", "C# client event projection drifted")
    require(projection.get("serverEvent") == "Nnrp.Server.NnrpServerEvent", "C# server event projection drifted")
    require(
        projection.get("serverOperation") == "Nnrp.Server.NnrpServerOperation",
        "C# server operation projection drifted",
    )
    require(projection.get("roleMethods") == EXPECTED_ROLE_METHODS, "C# role method projections drifted")
    role_surfaces = require_mapping(contract.get("roleSurfaces"), "roleSurfaces")
    require(
        role_surfaces.get("clientSubmitWait") == EXPECTED_CLIENT_SUBMIT_WAIT,
        "client submit-wait semantics drifted",
    )
    require(
        role_surfaces.get("serverEventPump") == EXPECTED_SERVER_EVENT_PUMP,
        "server event-pump semantics drifted",
    )
    types = require_mapping(contract.get("types"), "types")
    result = require_mapping(types.get("NnrpResult"), "NnrpResult")
    require(
        result.get("successRule") == EXPECTED_RESULT_SUCCESS_RULE
        and result.get("nonSuccessRule") == EXPECTED_RESULT_NON_SUCCESS_RULE,
        "NnrpResult terminal evidence rules drifted",
    )
    server_operation = require_mapping(types.get("ServerOperation"), "ServerOperation")
    require(
        server_operation.get("invariants") == EXPECTED_SERVER_OPERATION_INVARIANTS,
        "ServerOperation invariants drifted",
    )

    client_event = read_source(source_root, "src/Nnrp.Core/Runtime/NnrpClientEvent.cs")
    require_tokens(
        client_event,
        [
            "public sealed class NnrpClientEvent",
            "NnrpClientEventKind.Runtime",
            "NnrpClientEventKind.Lifecycle",
            "Func<NnrpRuntimeEvent, TResult> runtime",
            "Func<NnrpOperationLifecycleEvent, TResult> lifecycle",
        ],
        "NnrpClientEvent",
    )

    server_event = read_source(source_root, "src/Nnrp.Server/Sessions/NnrpServerEvent.cs")
    require_tokens(
        server_event,
        [
            "public sealed class NnrpServerEvent",
            "NnrpServerEventKind.Submit",
            "NnrpServerEventKind.Runtime",
            "NnrpServerEventKind.Lifecycle",
            "Func<NnrpServerOperation, TResult> submit",
        ],
        "NnrpServerEvent",
    )

    client_session = read_source(source_root, "src/Nnrp.Client/Sessions/NnrpRuntimeClientSession.cs")
    require_tokens(
        client_session,
        [
            "ValueTask<NnrpClientEvent> NextEventAsync",
            "NnrpClientEvent.FromRuntime",
            "NnrpClientEvent.FromLifecycle",
        ],
        "NnrpClientSession",
    )

    server_session = read_source(source_root, "src/Nnrp.Server/Sessions/NnrpRuntimeServerSession.cs")
    require_tokens(
        server_session,
        [
            "ValueTask<NnrpServerEvent> NextEventAsync",
            "ValueTask<NnrpServerOperation> ReceiveSubmitAsync",
            "public ValueTask SendResultAsync",
            "public ValueTask SendResultDropAsync",
            "public ValueTask SendProgressAsync",
            "public ValueTask SendPartialResultAsync",
        ],
        "C# server role surface",
    )
    for return_type, method_name, first_parameter_type in (
        ("ValueTask", "SendProgressAsync", "ProgressMetadata"),
        ("ValueTask", "SendPartialResultAsync", "PartialResultMetadata"),
        ("ValueTask", "SendResultDropAsync", "ResultDropReasonMetadata"),
    ):
        require_no_public_method(
            server_session,
            "public sealed class NnrpServerSession",
            "NnrpServerSession",
            return_type,
            method_name,
            first_parameter_type,
        )

    native_bridge = read_source(source_root, "src/Nnrp.NativeBridge/RuntimeArtifacts/NnrpNativeArtifact.cs")
    require_tokens(
        native_bridge,
        [
            "public void SendProgress(\n            NnrpNativeRuntimeOperation operation",
            "public void SendPartialResult(\n            NnrpNativeRuntimeOperation operation",
            "public void DropResult(\n            NnrpNativeRuntimeOperation operation",
        ],
        "C# native server operation surface",
    )
    for return_type, method_name, first_parameter_type in (
        ("void", "SendProgress", "ProgressMetadata"),
        ("void", "SendPartialResult", "PartialResultMetadata"),
        ("void", "SendResultDropReason", "ResultDropReasonMetadata"),
    ):
        require_no_public_method(
            native_bridge,
            "public sealed class NnrpNativeRuntimeServerSession",
            "C# native server session",
            return_type,
            method_name,
            first_parameter_type,
        )


def main() -> None:
    parser = argparse.ArgumentParser(description="Validate the C# SDK against the frozen NNRP SDK API contract.")
    parser.add_argument("--contract", type=Path, required=True)
    parser.add_argument("--source-root", type=Path, default=Path("."))
    args = parser.parse_args()
    check_contract(args.contract, args.source_root)
    print(f"C# SDK API contract v{EXPECTED_CONTRACT_VERSION} is aligned.")


if __name__ == "__main__":
    main()
