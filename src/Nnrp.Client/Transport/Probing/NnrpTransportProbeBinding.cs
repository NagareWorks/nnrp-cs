using System;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.Client
{
    public sealed class NnrpTransportProbeBinding
    {
        private readonly Func<NnrpTransportProbeRequest, CancellationToken, ValueTask<NnrpTransportProbeSampleResult>> probeAsync;

        public NnrpTransportProbeBinding(
            TransportId transportId,
            string bindingName,
            Func<NnrpTransportProbeRequest, CancellationToken, ValueTask<NnrpTransportProbeSampleResult>> probeAsync)
        {
            if (transportId == TransportId.Unspecified)
            {
                throw new ArgumentOutOfRangeException(nameof(transportId));
            }

            if (string.IsNullOrWhiteSpace(bindingName))
            {
                throw new ArgumentException("Binding name must not be null or empty.", nameof(bindingName));
            }

            this.probeAsync = probeAsync ?? throw new ArgumentNullException(nameof(probeAsync));
            TransportId = transportId;
            BindingName = bindingName;
        }

        public TransportId TransportId { get; }

        public string BindingName { get; }

        public ValueTask<NnrpTransportProbeSampleResult> ProbeAsync(
            NnrpTransportProbeRequest request,
            CancellationToken cancellationToken)
        {
            return probeAsync(request, cancellationToken);
        }
    }
}
