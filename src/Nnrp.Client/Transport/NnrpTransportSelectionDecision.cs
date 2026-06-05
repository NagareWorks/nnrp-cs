using Nnrp.Core;

namespace Nnrp.Client
{
    public readonly struct NnrpTransportSelectionDecision
    {
        public NnrpTransportSelectionDecision(bool shouldProbe, TransportId preferredTransportId, bool isForced)
        {
            ShouldProbe = shouldProbe;
            PreferredTransportId = preferredTransportId;
            IsForced = isForced;
        }

        public bool ShouldProbe { get; }

        public TransportId PreferredTransportId { get; }

        public bool IsForced { get; }
    }
}
