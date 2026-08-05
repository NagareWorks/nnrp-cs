using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Nnrp.WireConformance;

internal sealed record WireHostRouteScenario(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("host_route")] WireHostRouteFixture? HostRoute);

internal sealed record WireHostRouteFixture(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("application_endpoint")] string ApplicationEndpoint,
    [property: JsonPropertyName("routes")] IReadOnlyList<WireHostRoute> Routes);

internal sealed record WireHostRoute(
    [property: JsonPropertyName("transport")] string Transport,
    [property: JsonPropertyName("provider_id")] string ProviderId,
    [property: JsonPropertyName("locator")] string Locator,
    [property: JsonPropertyName("security")] WireHostRouteSecurity Security,
    [property: JsonPropertyName("injected_failures")] IReadOnlyList<string>? InjectedFailures = null);

internal sealed record WireHostRouteSecurity(
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("credential_owner")] string CredentialOwner);

internal sealed record WireHostRouteReadyReport(
    [property: JsonPropertyName("$schema")] string Schema,
    [property: JsonPropertyName("protocol_version")] string ProtocolVersion,
    [property: JsonPropertyName("scenario_id")] string ScenarioId,
    [property: JsonPropertyName("listeners")] IReadOnlyList<WireHostRouteReadyListener> Listeners);

internal sealed record WireHostRouteReadyListener(
    [property: JsonPropertyName("transport")] string Transport,
    [property: JsonPropertyName("provider_id")] string ProviderId,
    [property: JsonPropertyName("bound_endpoint")] string BoundEndpoint);

internal sealed record WireHostRouteResultReport(
    [property: JsonPropertyName("$schema")] string Schema,
    [property: JsonPropertyName("protocol_version")] string ProtocolVersion,
    [property: JsonPropertyName("suite_version")] string SuiteVersion,
    [property: JsonPropertyName("target_name")] string TargetName,
    [property: JsonPropertyName("results")] IReadOnlyList<WireHostRouteCaseResult> Results);

internal sealed record WireHostRouteCaseResult(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("outcome")] string Outcome,
    [property: JsonPropertyName("terminal")] string Terminal,
    [property: JsonPropertyName("observed_frames")] IReadOnlyList<object> ObservedFrames,
    [property: JsonPropertyName("route_evidence")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WireHostRouteEvidence? RouteEvidence,
    [property: JsonPropertyName("message")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Message,
    [property: JsonPropertyName("evidence_paths")] IReadOnlyList<string> EvidencePaths);

internal sealed record WireHostRouteEvidence(
    [property: JsonPropertyName("application_endpoint")] string ApplicationEndpoint,
    [property: JsonPropertyName("candidates")] IReadOnlyList<WireHostRouteCandidateEvidence> Candidates,
    [property: JsonPropertyName("listeners")] IReadOnlyList<WireHostRouteListenerEvidence> Listeners,
    [property: JsonPropertyName("accepted_sessions")] IReadOnlyList<WireHostRouteAcceptedSessionEvidence> AcceptedSessions,
    [property: JsonPropertyName("atomic_rollback")] bool AtomicRollback,
    [property: JsonPropertyName("logical_set_closed")] bool LogicalSetClosed,
    [property: JsonPropertyName("terminal_failure")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TerminalFailure = null);

internal sealed record WireHostRouteCandidateEvidence(
    [property: JsonPropertyName("transport")] string Transport,
    [property: JsonPropertyName("provider_id")] string ProviderId,
    [property: JsonPropertyName("requested_locator")] string RequestedLocator,
    [property: JsonPropertyName("locator_resolved")] bool LocatorResolved,
    [property: JsonPropertyName("security_satisfied")] bool SecuritySatisfied,
    [property: JsonPropertyName("selected")] bool Selected,
    [property: JsonPropertyName("rejection_reason")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? RejectionReason = null);

internal sealed record WireHostRouteListenerEvidence(
    [property: JsonPropertyName("transport")] string Transport,
    [property: JsonPropertyName("provider_id")] string ProviderId,
    [property: JsonPropertyName("requested_locator")] string RequestedLocator,
    [property: JsonPropertyName("bound_endpoint")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BoundEndpoint,
    [property: JsonPropertyName("state")] string State);

internal sealed record WireHostRouteAcceptedSessionEvidence(
    [property: JsonPropertyName("transport")] string Transport,
    [property: JsonPropertyName("provider_id")] string ProviderId,
    [property: JsonPropertyName("active_transport")] string ActiveTransport);

[JsonSerializable(typeof(WireHostRouteScenario))]
[JsonSerializable(typeof(WireHostRouteReadyReport))]
[JsonSerializable(typeof(WireHostRouteResultReport))]
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[ExcludeFromCodeCoverage]
internal partial class WireHostRouteJsonContext : JsonSerializerContext;
