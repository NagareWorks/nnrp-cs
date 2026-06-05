using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Transport.Tcp
{
    public sealed class NnrpNativeTcpTransportProvider : INnrpNativeTransportProvider
    {
        public static NnrpNativeTcpTransportProvider Instance { get; } =
            new NnrpNativeTcpTransportProvider();

        public TransportId TransportId => NnrpNativeTcpRuntime.TransportId;

        public string BindingName => NnrpNativeTcpRuntime.BindingName;

        public uint NativeTransportSlot => NnrpNativeTcpRuntime.NativeTransportSlot;

        public int ProbePriority => 100;
    }
}
