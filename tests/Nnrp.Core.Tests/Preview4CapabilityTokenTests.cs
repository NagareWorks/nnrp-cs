using System.Collections.Generic;
using Nnrp.Runtime;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class Preview4CapabilityTokenTests
    {
        [Fact]
        public void ControlCapabilityTokensMatchFrozenRustCatalog()
        {
            Assert.Equal(new[]
            {
                "control.cancel_abort",
                "control.supersede",
                "control.priority_update",
                "control.deadline_expire",
                "control.progress_partial",
                "control.credit_backpressure",
                "control.capability_costs",
                "control.route_execution_hint",
                "control.trace_context",
                "control.result_drop_reason",
                "control.degrade_profile",
                "control.budget_update",
                "control.recoverable_error",
            }, NnrpPreview4CapabilityTokens.Control);
        }

        [Fact]
        public void ObjectAndTransportTokensRemainSeparateCatalogs()
        {
            Assert.Equal(new[]
            {
                "object.lifecycle",
                "object.delta",
                "object.cost",
                "object.ownership",
                "cache.reference",
            }, NnrpPreview4CapabilityTokens.RuntimeObjectAndCache);
            Assert.Equal(new[] { "tcp", "quic", "ipc", "websocket" }, NnrpPreview4CapabilityTokens.Transports);
            Assert.Equal(18, NnrpPreview4CapabilityTokens.AllCapabilities.Count);
            Assert.DoesNotContain(NnrpPreview4CapabilityTokens.TransportTcp, NnrpPreview4CapabilityTokens.AllCapabilities);
            Assert.IsAssignableFrom<IReadOnlyList<string>>(NnrpPreview4CapabilityTokens.AllCapabilities);
        }
    }
}
