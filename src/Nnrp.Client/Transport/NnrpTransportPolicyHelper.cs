using System;
using Nnrp.Core;

namespace Nnrp.Client
{
    public static class NnrpTransportPolicyHelper
    {
        public static NnrpTransportSelectionDecision ResolveSelectionDecision(TransportPolicy policy)
        {
            return policy switch
            {
                TransportPolicy.Auto => new NnrpTransportSelectionDecision(true, TransportId.Unspecified, false),
                TransportPolicy.PreferQuic => new NnrpTransportSelectionDecision(true, TransportId.Quic, false),
                TransportPolicy.PreferTcp => new NnrpTransportSelectionDecision(true, TransportId.Tcp, false),
                TransportPolicy.ForceQuic => new NnrpTransportSelectionDecision(false, TransportId.Quic, true),
                TransportPolicy.ForceTcp => new NnrpTransportSelectionDecision(false, TransportId.Tcp, true),
                _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown transport policy."),
            };
        }
    }
}
