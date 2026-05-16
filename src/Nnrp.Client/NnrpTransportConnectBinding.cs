using System;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.Client
{
    public sealed class NnrpTransportConnectBinding
    {
        private readonly Func<CancellationToken, ValueTask<INnrpMessageTransport>> connectAsync;

        public NnrpTransportConnectBinding(
            NnrpTransportProbeBinding probeBinding,
            Func<CancellationToken, ValueTask<INnrpMessageTransport>> connectAsync)
        {
            ProbeBinding = probeBinding ?? throw new ArgumentNullException(nameof(probeBinding));
            this.connectAsync = connectAsync ?? throw new ArgumentNullException(nameof(connectAsync));
        }

        public NnrpTransportProbeBinding ProbeBinding { get; }

        public TransportId TransportId => ProbeBinding.TransportId;

        public string BindingName => ProbeBinding.BindingName;

        public ValueTask<INnrpMessageTransport> ConnectAsync(CancellationToken cancellationToken)
        {
            return connectAsync(cancellationToken);
        }
    }
}
