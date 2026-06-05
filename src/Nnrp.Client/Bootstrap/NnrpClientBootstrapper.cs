using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;

namespace Nnrp.Client
{
    public static class NnrpClientBootstrapper
    {
        public static async ValueTask<NnrpClientAutoProbeConnectResult> ConnectWithAutoProbeAsync(
            ClientProfile profile,
            IEnumerable<NnrpTransportConnectBinding> bindings,
            NnrpTransportProbeOptions probeOptions,
            uint requestedSessionId = 0,
            ulong traceId = 0,
            CancellationToken cancellationToken = default)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            var bindingArray = bindings.ToArray();
            if (bindingArray.Length == 0)
            {
                throw new ArgumentException("At least one transport binding must be supplied.", nameof(bindings));
            }

            var decision = NnrpTransportPolicyHelper.ResolveSelectionDecision(profile.TransportPolicy);
            INnrpMessageTransport transport;
            NnrpTransportProbeSelectionResult? probeSelection = null;

            if (decision.ShouldProbe)
            {
                probeSelection = await NnrpTransportProbeOrchestrator.ProbeAsync(
                    bindingArray.Select(binding => binding.ProbeBinding),
                    probeOptions,
                    cancellationToken).ConfigureAwait(false);

                var selectedBinding = bindingArray.FirstOrDefault(binding => binding.TransportId == probeSelection.Value.SelectedTransportId);
                if (selectedBinding == null)
                {
                    throw new InvalidOperationException("Probe selected a transport binding that is not available for connect.");
                }

                transport = await selectedBinding.ConnectAsync(cancellationToken).ConfigureAwait(false);
                var hello = profile.CreateClientHello(
                    requestedSessionId,
                    traceId,
                    profile.TransportPolicy,
                    probeSelection.Value.SelectedTransportId);
                var client = new NnrpClient(profile, transport);
                try
                {
                    var connectResult = await client.ConnectAsync(
                        hello,
                        expectedActiveTransportId: probeSelection.Value.SelectedTransportId,
                        cancellationToken).ConfigureAwait(false);
                    return new NnrpClientAutoProbeConnectResult(
                        client,
                        connectResult,
                        probeSelection,
                        wasProbed: true);
                }
                catch
                {
                    await NnrpTransportProbeExchange.DisposeTransportAsync(transport).ConfigureAwait(false);
                    throw;
                }
            }
            else
            {
                var forcedBinding = bindingArray.FirstOrDefault(binding => binding.TransportId == decision.PreferredTransportId);
                if (forcedBinding == null)
                {
                    throw new InvalidOperationException($"No transport binding is available for forced selection {decision.PreferredTransportId}.");
                }

                transport = await forcedBinding.ConnectAsync(cancellationToken).ConfigureAwait(false);
                var hello = profile.CreateClientHello(
                    requestedSessionId,
                    traceId,
                    profile.TransportPolicy,
                    decision.PreferredTransportId);
                var client = new NnrpClient(profile, transport);
                try
                {
                    var connectResult = await client.ConnectAsync(
                        hello,
                        expectedActiveTransportId: decision.PreferredTransportId,
                        cancellationToken).ConfigureAwait(false);
                    return new NnrpClientAutoProbeConnectResult(
                        client,
                        connectResult,
                        probeSelection: null,
                        wasProbed: false);
                }
                catch
                {
                    await NnrpTransportProbeExchange.DisposeTransportAsync(transport).ConfigureAwait(false);
                    throw;
                }
            }
        }
    }
}
