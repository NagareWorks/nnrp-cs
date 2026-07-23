using System;
using System.Collections.Generic;

namespace Nnrp.Runtime
{
    public static class NnrpPreview4CapabilityTokens
    {
        public const string ControlCancelAbort = "control.cancel_abort";
        public const string ControlSupersede = "control.supersede";
        public const string ControlPriorityUpdate = "control.priority_update";
        public const string ControlDeadlineExpire = "control.deadline_expire";
        public const string ControlProgressPartial = "control.progress_partial";
        public const string ControlCreditBackpressure = "control.credit_backpressure";
        public const string ControlCapabilityCosts = "control.capability_costs";
        public const string ControlRouteExecutionHint = "control.route_execution_hint";
        public const string ControlTraceContext = "control.trace_context";
        public const string ControlResultDropReason = "control.result_drop_reason";
        public const string ControlDegradeProfile = "control.degrade_profile";
        public const string ControlBudgetUpdate = "control.budget_update";
        public const string ControlRecoverableError = "control.recoverable_error";

        public const string ObjectLifecycle = "object.lifecycle";
        public const string ObjectDelta = "object.delta";
        public const string ObjectCost = "object.cost";
        public const string ObjectOwnership = "object.ownership";
        public const string CacheReference = "cache.reference";

        public const string TransportTcp = "tcp";
        public const string TransportQuic = "quic";
        public const string TransportIpc = "ipc";
        public const string TransportWebSocket = "websocket";

        private static readonly IReadOnlyList<string> ControlTokens = Array.AsReadOnly(new[]
        {
            ControlCancelAbort,
            ControlSupersede,
            ControlPriorityUpdate,
            ControlDeadlineExpire,
            ControlProgressPartial,
            ControlCreditBackpressure,
            ControlCapabilityCosts,
            ControlRouteExecutionHint,
            ControlTraceContext,
            ControlResultDropReason,
            ControlDegradeProfile,
            ControlBudgetUpdate,
            ControlRecoverableError,
        });

        private static readonly IReadOnlyList<string> RuntimeObjectAndCacheTokens = Array.AsReadOnly(new[]
        {
            ObjectLifecycle,
            ObjectDelta,
            ObjectCost,
            ObjectOwnership,
            CacheReference,
        });

        private static readonly IReadOnlyList<string> TransportTokens = Array.AsReadOnly(new[]
        {
            TransportTcp,
            TransportQuic,
            TransportIpc,
            TransportWebSocket,
        });

        private static readonly IReadOnlyList<string> CapabilityTokens = Array.AsReadOnly(new[]
        {
            ControlCancelAbort,
            ControlSupersede,
            ControlPriorityUpdate,
            ControlDeadlineExpire,
            ControlProgressPartial,
            ControlCreditBackpressure,
            ControlCapabilityCosts,
            ControlRouteExecutionHint,
            ControlTraceContext,
            ControlResultDropReason,
            ControlDegradeProfile,
            ControlBudgetUpdate,
            ControlRecoverableError,
            ObjectLifecycle,
            ObjectDelta,
            ObjectCost,
            ObjectOwnership,
            CacheReference,
        });

        public static IReadOnlyList<string> Control => ControlTokens;

        public static IReadOnlyList<string> RuntimeObjectAndCache => RuntimeObjectAndCacheTokens;

        public static IReadOnlyList<string> Transports => TransportTokens;

        public static IReadOnlyList<string> AllCapabilities => CapabilityTokens;
    }
}
