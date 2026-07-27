using System;
using System.Collections.Generic;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class TransportProviderContractTests
    {
        [Fact]
        public void ProviderMetadataOwnsValidatedLimitations()
        {
            var limitations = new[]
            {
                NnrpTransportProviderLimitation.RequiresTcp,
                NnrpTransportProviderLimitation.NativeHostOnly,
            };
            var metadata = Metadata("nnrp.transport.tcp.native", limitations);

            limitations[0] = NnrpTransportProviderLimitation.RequiresUdp;

            Assert.Equal((ushort)2, metadata.PreferenceRank);
            Assert.Equal((ulong)67_108_864, metadata.Limits.MaxFrameBytes);
            Assert.Equal(NnrpTransportProviderLimitation.RequiresTcp, metadata.Limitations[0]);
            Assert.Equal(default, metadata.Cost);
        }

        [Fact]
        public void ProviderMetadataRejectsInvalidCanonicalValues()
        {
            Assert.Throws<ArgumentException>(() => new NnrpTransportProviderCost(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProviderLimits(0));
            Assert.Throws<ArgumentException>(() => Metadata("bad id", Array.Empty<NnrpTransportProviderLimitation>()));
            Assert.Throws<ArgumentNullException>(() => new NnrpTransportProviderMetadata(
                "nnrp.transport.tcp.native",
                default,
                2,
                new NnrpTransportProviderLimits(1),
                null!));
            Assert.Throws<ArgumentException>(() => Metadata(
                "nnrp.transport.tcp.native",
                new[]
                {
                    NnrpTransportProviderLimitation.RequiresTcp,
                    NnrpTransportProviderLimitation.RequiresTcp,
                }));
            Assert.Throws<ArgumentException>(() => Metadata(
                "nnrp.transport.tcp.native",
                new[] { (NnrpTransportProviderLimitation)999 }));
        }

        [Theory]
        [InlineData(TransportId.Unspecified, NnrpTransportProviderKind.NativeDynamic)]
        [InlineData((TransportId)999, NnrpTransportProviderKind.NativeDynamic)]
        [InlineData(TransportId.Tcp, (NnrpTransportProviderKind)999)]
        public void ProviderDescriptorRejectsUnknownIdentity(
            TransportId transportId,
            NnrpTransportProviderKind kind)
        {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => Descriptor(transportId, kind));
        }

        [Fact]
        public void ProviderDescriptorExposesFrozenFields()
        {
            var descriptor = Descriptor(TransportId.Tcp, NnrpTransportProviderKind.NativeDynamic);

            Assert.Equal("TCP", descriptor.Name);
            Assert.Equal("1.0.0-preview.4", descriptor.Version);
            Assert.True(descriptor.Available);
            Assert.Equal("runtimes/win-x64/native/nnrp_ffi_tcp.dll", descriptor.LibraryPath);
            Assert.Null(descriptor.Diagnostic);
            Assert.Throws<ArgumentException>(() => new NnrpTransportProviderDescriptor(
                "",
                "1",
                TransportId.Tcp,
                NnrpTransportProviderKind.NativeDynamic,
                true,
                null,
                descriptor.Metadata));
            Assert.Throws<ArgumentException>(() => new NnrpTransportProviderDescriptor(
                "TCP",
                "",
                TransportId.Tcp,
                NnrpTransportProviderKind.NativeDynamic,
                true,
                null,
                descriptor.Metadata));
            Assert.Throws<ArgumentNullException>(() => new NnrpTransportProviderDescriptor(
                "TCP",
                "1",
                TransportId.Tcp,
                NnrpTransportProviderKind.NativeDynamic,
                true,
                null,
                null!));
        }

        [Fact]
        public void ProbeMetricsValidateSuccessfulSamples()
        {
            var metrics = new NnrpTransportProbeMetrics(3, 2, 1_000_000, 500);

            Assert.Equal((uint)3, metrics.SampleCount);
            Assert.Equal((uint)2, metrics.SuccessCount);
            Assert.Equal((ulong)1_000_000, metrics.MedianThroughputBytesPerSecond);
            Assert.Equal((ulong)500, metrics.MedianRttMicroseconds);
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeMetrics(0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeMetrics(1, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeMetrics(1, 2, 0, 0));
        }

        [Fact]
        public void ProviderOperationOptionsOwnFrozenEndpointSecurityAndLimits()
        {
            var endpoint = NnrpEndpoint.Parse("nnrps://render.example:7443");
            var providerEndpoint = NnrpProviderEndpoint.Parse("tcp://render.example:7443");
            var clientSecurity = new NnrpTransportClientSecurity("render.example", new byte[] { 1 });
            var serverSecurity = new NnrpTransportServerSecurity(new byte[] { 2 }, new byte[] { 3 });
            var connect = new NnrpTransportConnectOptions(endpoint, providerEndpoint, clientSecurity, 4096, 50);
            var listen = new NnrpTransportListenOptions(endpoint, providerEndpoint, serverSecurity, 8192, 60);
            var probe = new NnrpTransportProbeOptions(
                endpoint,
                providerEndpoint,
                4,
                1024,
                clientSecurity,
                16384,
                70,
                includeWarmup: true);

            Assert.Same(endpoint, connect.Endpoint);
            Assert.Same(providerEndpoint, connect.ProviderEndpoint);
            Assert.Same(clientSecurity, connect.Security);
            Assert.Equal((ulong)4096, connect.MaxPacketBytes);
            Assert.Equal((uint)50, connect.TimeoutMilliseconds);
            Assert.Same(endpoint, listen.Endpoint);
            Assert.Same(providerEndpoint, listen.ProviderEndpoint);
            Assert.Same(serverSecurity, listen.Security);
            Assert.Equal((ulong)8192, listen.MaxPacketBytes);
            Assert.Equal((uint)60, listen.TimeoutMilliseconds);
            Assert.Same(endpoint, probe.Endpoint);
            Assert.Same(providerEndpoint, probe.ProviderEndpoint);
            Assert.Same(clientSecurity, probe.Security);
            Assert.Equal((ulong)16384, probe.MaxPacketBytes);
            Assert.Equal((uint)70, probe.TimeoutMilliseconds);
            Assert.Equal((uint)4, probe.SampleCount);
            Assert.Equal((uint)1024, probe.PayloadBytes);
            Assert.True(probe.IncludeWarmup);
            Assert.Throws<ArgumentNullException>(() => new NnrpTransportConnectOptions(null!, providerEndpoint));
            Assert.Throws<ArgumentNullException>(() => new NnrpTransportConnectOptions(endpoint, null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportConnectOptions(
                endpoint,
                providerEndpoint,
                maxPacketBytes: 0));
            Assert.Throws<ArgumentNullException>(() => new NnrpTransportListenOptions(null!, providerEndpoint));
            Assert.Throws<ArgumentNullException>(() => new NnrpTransportListenOptions(endpoint, null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportListenOptions(
                endpoint,
                providerEndpoint,
                maxPacketBytes: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeOptions(
                endpoint,
                providerEndpoint,
                0,
                1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeOptions(
                endpoint,
                providerEndpoint,
                1,
                0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeOptions(
                endpoint,
                providerEndpoint,
                1,
                2,
                maxPacketBytes: 1));
        }

        [Fact]
        public void CandidateEnforcesProbeAndRejectionState()
        {
            var metadata = Metadata("nnrp.transport.tcp.native", Array.Empty<NnrpTransportProviderLimitation>());
            var metrics = new NnrpTransportProbeMetrics(1, 1, 10, 1);
            var selected = new NnrpTransportCandidate(
                TransportId.Tcp,
                metadata,
                true,
                true,
                true,
                NnrpTransportProbeState.Succeeded,
                metrics,
                0);

            Assert.Equal((uint)0, selected.SelectionRank);
            Assert.Equal(metrics, selected.Probe);
            Assert.Throws<ArgumentException>(() => new NnrpTransportCandidate(
                TransportId.Tcp,
                metadata,
                true,
                true,
                true,
                NnrpTransportProbeState.Succeeded));
            Assert.Throws<ArgumentException>(() => new NnrpTransportCandidate(
                TransportId.Tcp,
                metadata,
                true,
                true,
                true,
                NnrpTransportProbeState.NotRun,
                metrics));
            Assert.Throws<ArgumentException>(() => new NnrpTransportCandidate(
                TransportId.Tcp,
                metadata,
                false,
                true,
                true,
                NnrpTransportProbeState.NotRun,
                selectionRank: 0,
                rejectionReason: NnrpTransportRejectionReason.LocalUnavailable));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportCandidate(
                TransportId.Tcp,
                metadata,
                true,
                true,
                true,
                (NnrpTransportProbeState)999));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportCandidate(
                TransportId.Tcp,
                metadata,
                true,
                true,
                true,
                NnrpTransportProbeState.NotRun,
                rejectionReason: (NnrpTransportRejectionReason)999));
        }

        [Fact]
        public void SelectionOwnsOrderedCandidatesAndRequiresRankZeroProvider()
        {
            var descriptor = Descriptor(TransportId.Tcp, NnrpTransportProviderKind.NativeDynamic);
            var candidate = new NnrpTransportCandidate(
                TransportId.Tcp,
                descriptor.Metadata,
                true,
                true,
                true,
                NnrpTransportProbeState.NotRun,
                selectionRank: 0);
            var candidates = new[] { candidate };
            var selection = new NnrpTransportSelection(descriptor, candidates, TransportPolicy.Auto);

            candidates[0] = new NnrpTransportCandidate(
                TransportId.Tcp,
                descriptor.Metadata,
                false,
                true,
                true,
                NnrpTransportProbeState.NotRun,
                rejectionReason: NnrpTransportRejectionReason.LocalUnavailable);

            Assert.Same(candidate, selection.Candidates[0]);
            Assert.Throws<ArgumentException>(() => new NnrpTransportSelection(
                descriptor,
                Array.Empty<NnrpTransportCandidate>(),
                TransportPolicy.Auto));
            Assert.Throws<ArgumentException>(() => new NnrpTransportSelection(
                descriptor,
                new NnrpTransportCandidate[] { candidate, null! },
                TransportPolicy.Auto));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportSelection(
                descriptor,
                new[] { candidate },
                (TransportPolicy)255));
        }

        [Fact]
        public void SelectionEvidenceOwnsValidatedCollections()
        {
            var peers = new[] { TransportId.Tcp, TransportId.Quic, TransportId.Tcp };
            var readiness = new[]
            {
                new NnrpTransportCandidateReadiness(
                    TransportId.Tcp,
                    "nnrp.transport.tcp.native",
                    routeResolved: true,
                    securitySatisfied: true),
            };
            var observations = new[]
            {
                new NnrpTransportProbeObservation(
                    TransportId.Tcp,
                    "nnrp.transport.tcp.native",
                    NnrpTransportProbeState.Succeeded,
                    new NnrpTransportProbeMetrics(1, 1, 10, 1)),
            };
            var options = new NnrpTransportSelectionOptions(
                peers,
                readiness,
                TransportPolicy.PreferTcp,
                1024,
                observations);

            peers[0] = TransportId.Ipc;
            readiness[0] = new NnrpTransportCandidateReadiness(
                TransportId.Quic,
                "nnrp.transport.quic.native",
                true,
                true);
            observations[0] = new NnrpTransportProbeObservation(
                TransportId.Tcp,
                "nnrp.transport.tcp.native",
                NnrpTransportProbeState.Failed);

            Assert.Equal(new[] { TransportId.Quic, TransportId.Tcp }, options.PeerSupportedTransports);
            var ownedReadiness = Assert.Single(options.CandidateReadiness);
            Assert.Equal(TransportId.Tcp, ownedReadiness.TransportId);
            Assert.Equal("nnrp.transport.tcp.native", ownedReadiness.ProviderId);
            Assert.True(ownedReadiness.RouteResolved);
            Assert.True(ownedReadiness.SecuritySatisfied);
            Assert.Null(ownedReadiness.Diagnostic);
            var ownedObservation = Assert.Single(options.ProbeObservations);
            Assert.Equal(TransportId.Tcp, ownedObservation.TransportId);
            Assert.Equal("nnrp.transport.tcp.native", ownedObservation.ProviderId);
            Assert.Equal(NnrpTransportProbeState.Succeeded, ownedObservation.State);
            Assert.NotNull(ownedObservation.Metrics);
            Assert.Null(ownedObservation.Diagnostic);
            Assert.Equal((ulong)1024, options.RequestedMaxFrameBytes);
            Assert.Throws<ArgumentNullException>(() => new NnrpTransportSelectionOptions(null!, Array.Empty<NnrpTransportCandidateReadiness>()));
            Assert.Throws<ArgumentNullException>(() => new NnrpTransportSelectionOptions(Array.Empty<TransportId>(), null!));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportSelectionOptions(
                Array.Empty<TransportId>(),
                Array.Empty<NnrpTransportCandidateReadiness>(),
                (TransportPolicy)255));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportSelectionOptions(
                Array.Empty<TransportId>(),
                Array.Empty<NnrpTransportCandidateReadiness>(),
                requestedMaxFrameBytes: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportSelectionOptions(
                new[] { TransportId.Unspecified },
                Array.Empty<NnrpTransportCandidateReadiness>()));
            Assert.Throws<ArgumentException>(() => new NnrpTransportSelectionOptions(
                Array.Empty<TransportId>(),
                new NnrpTransportCandidateReadiness[] { null! }));
            Assert.Throws<ArgumentException>(() => new NnrpTransportSelectionOptions(
                Array.Empty<TransportId>(),
                Array.Empty<NnrpTransportCandidateReadiness>(),
                probeObservations: new NnrpTransportProbeObservation[] { null! }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeObservation(
                TransportId.Tcp,
                "nnrp.transport.tcp.native",
                NnrpTransportProbeState.Missing));
            Assert.Throws<ArgumentException>(() => new NnrpTransportProbeObservation(
                TransportId.Tcp,
                "nnrp.transport.tcp.native",
                NnrpTransportProbeState.Succeeded));
            Assert.Throws<ArgumentException>(() => new NnrpTransportProbeObservation(
                TransportId.Tcp,
                "nnrp.transport.tcp.native",
                NnrpTransportProbeState.Failed,
                new NnrpTransportProbeMetrics(1, 1, 1, 1)));
        }

        [Fact]
        public void SelectionExceptionOwnsTypedFailureContext()
        {
            var descriptor = Descriptor(TransportId.Tcp, NnrpTransportProviderKind.NativeDynamic);
            var candidate = new NnrpTransportCandidate(
                TransportId.Tcp,
                descriptor.Metadata,
                false,
                true,
                true,
                NnrpTransportProbeState.NotRun,
                rejectionReason: NnrpTransportRejectionReason.LocalUnavailable);
            var candidates = new[] { candidate };
            var error = new NnrpTransportSelectionException(
                NnrpTransportSelectionErrorCode.ForcedTransportUnavailable,
                "forced provider unavailable",
                TransportPolicy.ForceTcp,
                candidates);

            candidates[0] = new NnrpTransportCandidate(
                TransportId.Tcp,
                descriptor.Metadata,
                true,
                true,
                true,
                NnrpTransportProbeState.NotRun,
                selectionRank: 0);

            Assert.Equal(NnrpTransportSelectionErrorCode.ForcedTransportUnavailable, error.Code);
            Assert.Equal(TransportPolicy.ForceTcp, error.Policy);
            Assert.Same(candidate, Assert.Single(error.Candidates));
            Assert.Equal("forced provider unavailable", error.Diagnostic);
            Assert.Equal(error.Diagnostic, error.Message);
            Assert.Empty(new NnrpTransportSelectionException(
                NnrpTransportSelectionErrorCode.InvalidEvidence,
                "invalid evidence").Candidates);
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportSelectionException(
                (NnrpTransportSelectionErrorCode)999,
                "invalid code"));
            Assert.Throws<ArgumentException>(() => new NnrpTransportSelectionException(
                NnrpTransportSelectionErrorCode.NoViableTransport,
                " "));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportSelectionException(
                NnrpTransportSelectionErrorCode.NoViableTransport,
                "invalid policy",
                (TransportPolicy)255));
            Assert.Throws<ArgumentException>(() => new NnrpTransportSelectionException(
                NnrpTransportSelectionErrorCode.NoViableTransport,
                "invalid candidates",
                candidates: new NnrpTransportCandidate[] { null! }));
        }

        private static NnrpTransportProviderMetadata Metadata(
            string id,
            NnrpTransportProviderLimitation[] limitations)
        {
            return new NnrpTransportProviderMetadata(
                id,
                default,
                2,
                new NnrpTransportProviderLimits(67_108_864),
                limitations);
        }

        private static NnrpTransportProviderDescriptor Descriptor(
            TransportId transportId,
            NnrpTransportProviderKind kind)
        {
            return new NnrpTransportProviderDescriptor(
                "TCP",
                "1.0.0-preview.4",
                transportId,
                kind,
                true,
                "runtimes/win-x64/native/nnrp_ffi_tcp.dll",
                Metadata("nnrp.transport.tcp.native", Array.Empty<NnrpTransportProviderLimitation>()));
        }
    }
}
