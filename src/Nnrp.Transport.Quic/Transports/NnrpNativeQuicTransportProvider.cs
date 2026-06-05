using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Transport.Quic
{
    public sealed class NnrpNativeQuicTransportProvider : INnrpNativeTransportProvider
    {
        public static NnrpNativeQuicTransportProvider Instance { get; } =
            new NnrpNativeQuicTransportProvider();

        public TransportId TransportId => NnrpNativeQuicRuntime.TransportId;

        public string BindingName => NnrpNativeQuicRuntime.BindingName;

        public uint NativeTransportSlot => NnrpNativeQuicRuntime.NativeTransportSlot;

        public int ProbePriority => 200;
    }
}
