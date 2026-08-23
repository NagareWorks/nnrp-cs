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
EXPECTED_TRACE_CONTEXT_CORRELATION = {
    "sessionFrameId": 0,
    "operationFrameRule": (
        "A non-zero TRACE_CONTEXT frame_id is the FRAME_SUBMIT frame_id of an active operation "
        "and must be rejected when unknown or mismatched."
    ),
    "metadataOperationId": "forbidden",
    "headerTraceIdRule": (
        "A non-zero common-header trace_id equals TraceContextMetadata.trace_id."
    ),
    "sendMethodShapes": {
        "rust": "send_trace_context(frame_id, metadata, body)",
        "python": 'send_trace_context(metadata, body=b"", *, operation_id=None)',
        "javascript": "sendTraceContext(metadata, body?, operationId?)",
        "csharp": (
            "SendTraceContextAsync(TraceContextMetadata, ReadOnlyMemory<byte>, ulong?, "
            "CancellationToken)"
        ),
    },
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
EXPECTED_CSHARP_PROJECTIONS = {
    "submitRequest": "Nnrp.Client.NnrpSubmitRequest",
    "submitHeaderContext": "Nnrp.Client.NnrpSubmitHeaderContext",
    "submitBuilders": [
        "NnrpSubmitRequest.CreateTensor",
        "NnrpSubmitRequest.CreateToken",
        "NnrpSubmitRequest.CreateTypedPayload",
    ],
    "runtimeFrameHeader": "Nnrp.Runtime.RuntimeFrameHeader",
    "runtimeEvent": "Nnrp.Runtime.NnrpRuntimeEvent",
    "clientEvent": "Nnrp.Runtime.NnrpClientEvent",
    "serverEvent": "Nnrp.Server.NnrpServerEvent",
    "serverOperation": "Nnrp.Server.NnrpServerOperation",
    "roleMethods": EXPECTED_ROLE_METHODS,
    "serverCapabilityMethods": {
        "negotiate_capabilities": "NegotiateCapabilitiesAsync",
        "degrade_profile": "DegradeProfileAsync",
    },
    "operationLifecycleEvent": "Nnrp.Runtime.NnrpOperationLifecycleEvent",
    "terminalEvent": "Nnrp.Runtime.NnrpTerminalEvent",
    "result": "Nnrp.Client.NnrpResult",
    "clientRoles": ["Nnrp.Client.NnrpClient", "Nnrp.Client.NnrpClientSession"],
    "serverRoles": ["Nnrp.Server.NnrpServer", "Nnrp.Server.NnrpServerSession"],
    "runtimeMetadataNamespace": "Nnrp.Runtime",
    "capabilityMetadata": "Nnrp.Runtime.CapabilityMetadata",
    "connectionLifecycle": "Nnrp.Core.NnrpConnectionLifecycle",
    "sessionLifecycle": "Nnrp.Core.NnrpSessionLifecycle",
    "typedPayloadDescriptor": "Nnrp.Core.TypedPayloadDescriptor",
    "typedPayloadFrame": "Nnrp.Core.TypedPayloadFrameView",
    "cacheObjectId": "Nnrp.Core.NnrpCacheObjectId",
    "cacheLease": "Nnrp.Core.NnrpCacheLease",
    "cacheLeaseResult": "Nnrp.Core.NnrpCacheLeaseResult",
    "cachePolicyOptions": "Nnrp.Core.CachePolicyOptions",
    "transportProviderMetadata": "Nnrp.Core.NnrpTransportProviderMetadata",
    "transportProviderDescriptor": "Nnrp.Core.NnrpTransportProviderDescriptor",
    "transportSelectionOptions": "Nnrp.Core.NnrpTransportSelectionOptions",
    "transportSelection": "Nnrp.Core.NnrpTransportSelection",
    "transportSelectionFailure": "Nnrp.Core.NnrpTransportSelectionException",
    "applicationEndpoint": "Nnrp.Core.NnrpEndpoint",
    "providerEndpoint": "Nnrp.Core.NnrpProviderEndpoint",
    "clientTransportSecurity": "Nnrp.Core.NnrpTransportClientSecurity",
    "serverTransportSecurity": "Nnrp.Core.NnrpTransportServerSecurity",
    "clientProviderRoute": "Nnrp.Core.NnrpClientProviderRoute",
    "serverProviderRoute": "Nnrp.Core.NnrpServerProviderRoute",
    "schemaDescriptor": "Nnrp.Core.NnrpSchemaDescriptorHeader",
    "schemaRegistry": "Nnrp.Core.NnrpSchemaRegistry",
    "clientBootstrapOptions": "Nnrp.Client.NnrpClientOptions",
    "clientSessionOptions": "Nnrp.Client.NnrpClientSessionOptions",
    "sessionRecoveryTicket": "Nnrp.Core.NnrpSessionRecoveryTicket",
    "sessionRecoveryTicketEncode": "NnrpSessionRecoveryTicket.ToBytes",
    "sessionRecoveryTicketDecode": "NnrpSessionRecoveryTicket.FromBytes",
    "serverBootstrapOptions": "Nnrp.Server.NnrpServerOptions",
    "serverSessionOptions": "Nnrp.Server.NnrpServerSessionOptions",
    "serverAcceptOptions": "Nnrp.Server.NnrpServerAcceptOptions",
    "serverSessionPolicy": "Nnrp.Server.INnrpServerSessionPolicy",
    "baselineMetadataCodecs": {
        "ClientHelloMetadata": ["ClientHelloMetadata.ToArray", "ClientHelloMetadata.TryParse"],
        "SessionPatchAckMetadata": ["SessionPatchAckMetadata.ToArray", "SessionPatchAckMetadata.TryParse"],
        "FlowUpdateMetadata": ["FlowUpdateMetadata.ToArray", "FlowUpdateMetadata.TryParse"],
        "ResultHintMetadata": ["ResultHintMetadata.ToArray", "ResultHintMetadata.TryParse"],
        "FrameSubmitMetadata": ["FrameSubmitMetadata.ToArray", "FrameSubmitMetadata.TryParse"],
        "ResultPushMetadata": ["ResultPushMetadata.ToArray", "ResultPushMetadata.TryParse"],
        "CachePutMetadata": ["CachePutMetadata.ToArray", "CachePutMetadata.TryParse"],
        "CacheAckMetadata": ["CacheAckMetadata.ToArray", "CacheAckMetadata.TryParse"],
        "CacheInvalidateMetadata": ["CacheInvalidateMetadata.ToArray", "CacheInvalidateMetadata.TryParse"],
        "TransportProbeMetadata": ["TransportProbeMetadata.ToArray", "TransportProbeMetadata.TryParse"],
        "TransportProbeAckMetadata": ["TransportProbeAckMetadata.ToArray", "TransportProbeAckMetadata.TryParse"],
        "ObjectReferenceBlock": ["ObjectReferenceBlock.ToArray", "ObjectReferenceBlock.TryParse"],
    },
}

CSHARP_BASELINE_METADATA_CODEC_SOURCES = {
    "ClientHelloMetadata": "src/Nnrp.Core/Messages/Control/ClientHelloMetadata.cs",
    "SessionPatchAckMetadata": "src/Nnrp.Core/Messages/Session/SessionPatchAckMetadata.cs",
    "FlowUpdateMetadata": "src/Nnrp.Core/Messages/Flow/FlowUpdateMetadata.cs",
    "ResultHintMetadata": "src/Nnrp.Core/Messages/Data/ResultHintMetadata.cs",
    "FrameSubmitMetadata": "src/Nnrp.Core/Messages/Data/FrameSubmitMetadata.cs",
    "ResultPushMetadata": "src/Nnrp.Core/Messages/Data/ResultPushMetadata.cs",
    "CachePutMetadata": "src/Nnrp.Core/Messages/Cache/CachePutMetadata.cs",
    "CacheAckMetadata": "src/Nnrp.Core/Messages/Cache/CacheAckMetadata.cs",
    "CacheInvalidateMetadata": "src/Nnrp.Core/Messages/Cache/CacheInvalidateMetadata.cs",
    "TransportProbeMetadata": "src/Nnrp.Core/Messages/Transport/TransportProbeMetadata.cs",
    "TransportProbeAckMetadata": "src/Nnrp.Core/Messages/Transport/TransportProbeAckMetadata.cs",
    "ObjectReferenceBlock": "src/Nnrp.Core/Payloads/ObjectReferences/ObjectReferenceBlock.cs",
}


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


def require_csharp_baseline_metadata_codec(source: str, type_name: str) -> None:
    require(
        re.search(rf"\bpublic\s+readonly\s+struct\s+{re.escape(type_name)}\b", source)
        is not None,
        f"C# baseline metadata codec {type_name} public type is missing",
    )
    declaration = type_declaration(
        source,
        f"public readonly struct {type_name}",
        type_name,
    )
    require(
        re.search(r"\bpublic\s+byte\[\]\s+ToArray\s*\(\s*\)", declaration)
        is not None,
        f"C# baseline metadata codec {type_name}.ToArray is missing",
    )
    require(
        re.search(r"\bpublic\s+static\s+bool\s+TryParse\s*\(", declaration)
        is not None,
        f"C# baseline metadata codec {type_name}.TryParse is missing",
    )


def require_mapping(value: Any, subject: str) -> dict[str, Any]:
    require(isinstance(value, dict), f"{subject} is missing or invalid")
    return value


def braced_declaration(source: str, marker: str, subject: str) -> str:
    header = re.search(rf"{re.escape(marker)}\b[^{{]*{{", source)
    require(header is not None, f"{subject} is missing frozen API token: {marker}")
    start = header.start()
    opening = source.find("{", start + len(marker))
    require(opening >= 0, f"{subject} declaration is missing an opening brace")
    depth = 0
    for index in range(opening, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[opening + 1 : index]
    raise SystemExit(f"{subject} declaration is unterminated")


def require_exact_public_properties(
    source: str,
    marker: str,
    subject: str,
    expected: list[str],
) -> None:
    declaration = braced_declaration(source, marker, subject)
    actual = re.findall(
        r"\bpublic\s+[A-Za-z_][A-Za-z0-9_?.<>,\[\]\s]*\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{\s*get;\s*\}",
        declaration,
    )
    require(actual == expected, f"{subject} public fields drifted: expected {expected}, received {actual}")


def require_exact_public_property_types(
    source: str,
    marker: str,
    subject: str,
    expected: list[tuple[str, str]],
) -> None:
    declaration = braced_declaration(source, marker, subject)
    actual = [
        (name, re.sub(r"\s+", "", property_type))
        for property_type, name in re.findall(
            r"\bpublic\s+([A-Za-z_][A-Za-z0-9_?.<>,\[\]\s]*)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\{\s*get;\s*\}",
            declaration,
        )
    ]
    require(actual == expected, f"{subject} public field types drifted: expected {expected}, received {actual}")


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


def require_no_public_method_name(
    source: str,
    declaration: str,
    subject: str,
    method_name: str,
) -> None:
    declaration_source = type_declaration(source, declaration, subject)
    pattern = re.compile(
        rf"\bpublic\s+(?:(?:async|static|virtual|override|sealed|new)\s+)*"
        rf"[^\s(]+\s+{re.escape(method_name)}\s*\(",
        re.MULTILINE,
    )
    require(
        pattern.search(declaration_source) is None,
        f"{subject} exposes the removed compatibility method: {method_name}",
    )


def check_contract(contract_path: Path, source_root: Path) -> None:
    contract: dict[str, Any] = json.loads(contract_path.read_text(encoding="utf-8"))
    require(
        contract.get("contractVersion") == EXPECTED_CONTRACT_VERSION,
        f"expected SDK contract version {EXPECTED_CONTRACT_VERSION}",
    )

    language_projections = require_mapping(contract.get("languageProjections"), "languageProjections")
    projection = require_mapping(language_projections.get("csharp"), "C# language projection")
    require(
        projection == EXPECTED_CSHARP_PROJECTIONS,
        "C# SDK projection map drifted; update the implementation contract test with the frozen API",
    )
    for type_name, relative_path in CSHARP_BASELINE_METADATA_CODEC_SOURCES.items():
        require_csharp_baseline_metadata_codec(
            read_source(source_root, relative_path),
            type_name,
        )
    role_surfaces = require_mapping(contract.get("roleSurfaces"), "roleSurfaces")
    require(
        role_surfaces.get("clientSubmitWait") == EXPECTED_CLIENT_SUBMIT_WAIT,
        "client submit-wait semantics drifted",
    )
    require(
        role_surfaces.get("serverEventPump") == EXPECTED_SERVER_EVENT_PUMP,
        "server event-pump semantics drifted",
    )
    require(
        role_surfaces.get("traceContextCorrelation") == EXPECTED_TRACE_CONTEXT_CORRELATION,
        "trace-context correlation semantics drifted",
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
    probe_observation = require_mapping(
        types.get("TransportProbeObservation"),
        "TransportProbeObservation",
    )
    require(
        probe_observation.get("stateConstraint") == ["succeeded", "failed"],
        "TransportProbeObservation state constraint drifted",
    )
    selection_options = require_mapping(
        types.get("TransportSelectionOptions"),
        "TransportSelectionOptions",
    )
    provider_descriptor = require_mapping(
        types.get("TransportProviderDescriptor"),
        "TransportProviderDescriptor",
    )
    require(
        provider_descriptor.get("nameSemantics")
        == (
            "provider-owned package or display name; protocol transport identity is "
            "transport_id and selection must not derive it from name"
        ),
        "TransportProviderDescriptor name semantics drifted",
    )
    require(
        selection_options.get("peerSupportedTransportsSemantics")
        == "set; duplicates have no effect and input order is not semantically significant",
        "TransportSelectionOptions peer transport semantics drifted",
    )
    require(
        selection_options.get("requestedMaxFrameBytesZeroRule")
        == "zero is a valid requested size and must not be rejected or treated as absent",
        "TransportSelectionOptions zero frame-size rule drifted",
    )

    transport_contracts = read_source(
        source_root,
        "src/Nnrp.Core/Transport/NnrpTransportProviderContracts.cs",
    )
    require_tokens(
        transport_contracts,
        [
            "state != NnrpTransportProbeState.Succeeded && state != NnrpTransportProbeState.Failed",
            "Succeeded probe observations require metrics and failed observations forbid them.",
        ],
        "NnrpTransportProbeObservation",
    )
    for marker, subject, expected in [
        (
            "public readonly record struct NnrpTransportProviderCost",
            "NnrpTransportProviderCost",
            [("ModelId", "ushort"), ("Units", "ulong")],
        ),
        (
            "public readonly record struct NnrpTransportProviderLimits",
            "NnrpTransportProviderLimits",
            [("MaxFrameBytes", "ulong")],
        ),
        (
            "public sealed class NnrpTransportProviderMetadata",
            "NnrpTransportProviderMetadata",
            [
                ("Id", "string"),
                ("Cost", "NnrpTransportProviderCost"),
                ("PreferenceRank", "ushort"),
                ("Limits", "NnrpTransportProviderLimits"),
                ("Limitations", "IReadOnlyList<NnrpTransportProviderLimitation>"),
            ],
        ),
        (
            "public sealed class NnrpTransportProviderDescriptor",
            "NnrpTransportProviderDescriptor",
            [
                ("Name", "string"),
                ("Version", "string"),
                ("TransportId", "TransportId"),
                ("Kind", "NnrpTransportProviderKind"),
                ("Available", "bool"),
                ("LibraryPath", "string?"),
                ("Metadata", "NnrpTransportProviderMetadata"),
                ("Diagnostic", "string?"),
            ],
        ),
        (
            "public sealed class NnrpTransportCandidateReadiness",
            "NnrpTransportCandidateReadiness",
            [
                ("TransportId", "TransportId"),
                ("ProviderId", "string"),
                ("RouteResolved", "bool"),
                ("SecuritySatisfied", "bool"),
                ("Diagnostic", "string?"),
            ],
        ),
        (
            "public readonly record struct NnrpTransportProbeMetrics",
            "NnrpTransportProbeMetrics",
            [
                ("SampleCount", "uint"),
                ("SuccessCount", "uint"),
                ("MedianThroughputBytesPerSecond", "ulong"),
                ("MedianRttMicroseconds", "ulong"),
            ],
        ),
        (
            "public sealed class NnrpTransportProbeObservation",
            "NnrpTransportProbeObservation",
            [
                ("TransportId", "TransportId"),
                ("ProviderId", "string"),
                ("State", "NnrpTransportProbeState"),
                ("Metrics", "NnrpTransportProbeMetrics?"),
                ("Diagnostic", "string?"),
            ],
        ),
        (
            "public sealed class NnrpTransportCandidate",
            "NnrpTransportCandidate",
            [
                ("TransportId", "TransportId"),
                ("Provider", "NnrpTransportProviderMetadata"),
                ("LocalAvailable", "bool"),
                ("PeerSupported", "bool"),
                ("WithinLimits", "bool"),
                ("ProbeState", "NnrpTransportProbeState"),
                ("Probe", "NnrpTransportProbeMetrics?"),
                ("SelectionRank", "uint?"),
                ("RejectionReason", "NnrpTransportRejectionReason?"),
                ("Diagnostic", "string?"),
            ],
        ),
        (
            "public sealed class NnrpTransportSelection",
            "NnrpTransportSelection",
            [
                ("SelectedProvider", "NnrpTransportProviderDescriptor"),
                ("Candidates", "IReadOnlyList<NnrpTransportCandidate>"),
                ("Policy", "TransportPolicy"),
                ("Diagnostic", "string?"),
            ],
        ),
        (
            "public sealed class NnrpTransportSelectionException",
            "NnrpTransportSelectionException",
            [
                ("Code", "NnrpTransportSelectionErrorCode"),
                ("Policy", "TransportPolicy?"),
                ("TransportId", "TransportId?"),
                ("Candidates", "IReadOnlyList<NnrpTransportCandidate>"),
                ("Diagnostic", "string"),
            ],
        ),
        (
            "public sealed class NnrpTransportSelectionOptions",
            "NnrpTransportSelectionOptions",
            [
                ("PeerSupportedTransports", "IReadOnlyCollection<TransportId>"),
                ("Policy", "TransportPolicy"),
                ("RequestedMaxFrameBytes", "ulong?"),
                ("CandidateReadiness", "IReadOnlyCollection<NnrpTransportCandidateReadiness>"),
                ("ProbeObservations", "IReadOnlyCollection<NnrpTransportProbeObservation>"),
            ],
        ),
    ]:
        require_exact_public_property_types(transport_contracts, marker, subject, expected)

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
            "public ValueTask SendTraceContextAsync(\n            TraceContextMetadata metadata,",
            "ulong? operationId = null,",
            "session.SendTraceContext(ResolveTraceFrameId(operationId), metadata, body)",
            "activeOperationFrames",
            "ObserveTerminal(nativeEvent)",
        ],
        "NnrpClientSession",
    )

    client = read_source(source_root, "src/Nnrp.Client/Bootstrap/NnrpRuntimeClient.cs")
    require_tokens(
        client,
        [
            "ValueTask<NnrpClientSession> OpenSessionAsync(",
            "ValueTask<NnrpClientSession> ResumeSessionAsync(",
            "CancellationToken cancellationToken = default",
        ],
        "NnrpClient",
    )
    for method_name in ("OpenSession", "ResumeSession"):
        require_no_public_method_name(
            client,
            "public sealed class NnrpClient",
            "NnrpClient",
            method_name,
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
            "public ValueTask NegotiateCapabilitiesAsync(",
            "public ValueTask DegradeProfileAsync(",
            "public ValueTask SendTraceContextAsync(\n            TraceContextMetadata metadata,",
            "ulong? operationId = null,",
            "session.SendTraceContext(ResolveTraceFrameId(operationId), metadata, body)",
            "RegisterActiveOperation(metadata.OperationId, submit.Header.FrameId)",
            "completeOperation(OperationId, FrameId)",
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
            "public void NegotiateCapabilities(\n            CapabilityMetadata metadata,",
            "public void DegradeProfile(\n            CapabilityMetadata metadata,",
            "public void SendProgress(\n            NnrpNativeRuntimeOperation operation",
            "public void SendPartialResult(\n            NnrpNativeRuntimeOperation operation",
            "public void DropResult(\n            NnrpNativeRuntimeOperation operation",
            "public void SendTraceContext(\n            uint frameId,",
            "NnrpRuntimeControl.Encode(MessageType.TraceContext, metadata, body.Span)",
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
