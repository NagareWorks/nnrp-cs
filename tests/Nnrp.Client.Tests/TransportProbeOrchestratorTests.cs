using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Client.Tests
{
    public sealed class TransportProbeOrchestratorTests
    {
        [Fact]
        public void ClientProfileDefaultsToAutoTransportPolicy()
        {
            var profile = new ClientProfile();

            Assert.Equal(TransportPolicy.Auto, profile.TransportPolicy);
        }

        [Fact]
        public void TransportPolicyHelperResolvesForceAndPreferenceModes()
        {
            var auto = NnrpTransportPolicyHelper.ResolveSelectionDecision(TransportPolicy.Auto);
            var preferTcp = NnrpTransportPolicyHelper.ResolveSelectionDecision(TransportPolicy.PreferTcp);
            var forceQuic = NnrpTransportPolicyHelper.ResolveSelectionDecision(TransportPolicy.ForceQuic);

            Assert.True(auto.ShouldProbe);
            Assert.Equal(TransportId.Unspecified, auto.PreferredTransportId);
            Assert.False(auto.IsForced);

            Assert.True(preferTcp.ShouldProbe);
            Assert.Equal(TransportId.Tcp, preferTcp.PreferredTransportId);
            Assert.False(preferTcp.IsForced);

            Assert.False(forceQuic.ShouldProbe);
            Assert.Equal(TransportId.Quic, forceQuic.PreferredTransportId);
            Assert.True(forceQuic.IsForced);
        }

        [Fact]
        public async Task ProbeAsyncSelectsBindingBySuccessCountThenThroughputThenRtt()
        {
            var options = new NnrpTransportProbeOptions
            {
                WarmupProbeCount = 1,
                ScoredProbeCount = 3,
                PayloadBytes = 1000,
                ProbeTimeout = TimeSpan.FromMilliseconds(500),
            };

            var tcpSamples = new Queue<NnrpTransportProbeSampleResult>(new[]
            {
                Sample(TransportId.Tcp, "tcp", true, 1000, 4000),
                Sample(TransportId.Tcp, "tcp", true, 1000, 3000),
                Sample(TransportId.Tcp, "tcp", true, 1000, 3200),
                Sample(TransportId.Tcp, "tcp", true, 1000, 3100),
            });
            var quicSamples = new Queue<NnrpTransportProbeSampleResult>(new[]
            {
                Sample(TransportId.Quic, "quic", true, 1000, 1000),
                Sample(TransportId.Quic, "quic", true, 1000, 1100),
                Sample(TransportId.Quic, "quic", false, 1000, 0, "timeout"),
                Sample(TransportId.Quic, "quic", true, 1000, 1200),
            });

            var result = await NnrpTransportProbeOrchestrator.ProbeAsync(
                new[]
                {
                    CreateBinding(TransportId.Tcp, "tcp", tcpSamples),
                    CreateBinding(TransportId.Quic, "quic", quicSamples),
                },
                options,
                CancellationToken.None);

            Assert.Equal(TransportId.Tcp, result.SelectedTransportId);
            Assert.Equal("tcp", result.SelectedBindingName);
            Assert.Equal(2, result.Summaries.Length);
            Assert.Contains(result.Summaries, summary => summary.TransportId == TransportId.Tcp && summary.SuccessCount == 3);
            Assert.Contains(result.Summaries, summary => summary.TransportId == TransportId.Quic && summary.SuccessCount == 2);
        }

        [Fact]
        public async Task ProbeAsyncExcludesWarmupSamplesFromScoringAndExposesSummaries()
        {
            var options = new NnrpTransportProbeOptions
            {
                WarmupProbeCount = 1,
                ScoredProbeCount = 2,
                PayloadBytes = 1200,
                ProbeTimeout = TimeSpan.FromMilliseconds(500),
            };

            var binding = new Queue<NnrpTransportProbeSampleResult>(new[]
            {
                Sample(TransportId.Tcp, "tcp", true, 1200, 20000),
                Sample(TransportId.Tcp, "tcp", true, 1200, 2000),
                Sample(TransportId.Tcp, "tcp", true, 1200, 1000),
            });

            var result = await NnrpTransportProbeOrchestrator.ProbeAsync(
                new[] { CreateBinding(TransportId.Tcp, "tcp", binding) },
                options,
                CancellationToken.None);

            var summary = Assert.Single(result.Summaries);
            Assert.Equal(1, summary.WarmupSampleCount);
            Assert.Equal(2, summary.ScoredSampleCount);
            Assert.Equal(2, summary.SuccessCount);
            Assert.Equal(1500, summary.MedianRttMicroseconds);
            Assert.True(summary.MedianThroughputBytesPerSecond > 0);
        }

        [Fact]
        public async Task ProbeAsyncTreatsExceptionsAsFailuresAndKeepsScoring()
        {
            var invocationCount = 0;
            var binding = new NnrpTransportProbeBinding(
                TransportId.Quic,
                "quic",
                (request, cancellationToken) =>
                {
                    invocationCount++;
                    if (request.IsWarmup)
                    {
                        return new ValueTask<NnrpTransportProbeSampleResult>(Sample(TransportId.Quic, "quic", true, request.PayloadBytes, 2000));
                    }

                    if (request.SampleIndex == 1)
                    {
                        throw new InvalidOperationException("simulated failure");
                    }

                    return new ValueTask<NnrpTransportProbeSampleResult>(Sample(TransportId.Quic, "quic", true, request.PayloadBytes, 1000));
                });

            var result = await NnrpTransportProbeOrchestrator.ProbeAsync(
                new[] { binding },
                new NnrpTransportProbeOptions { WarmupProbeCount = 1, ScoredProbeCount = 2, PayloadBytes = 2048 },
                CancellationToken.None);

            var summary = Assert.Single(result.Summaries);
            Assert.Equal(3, invocationCount);
            Assert.Equal(1, summary.SuccessCount);
            Assert.Equal(1, summary.FailureCount);
        }

        [Fact]
        public async Task ProbeAsyncExposesMedianJitterInBindingSummary()
        {
            var result = await NnrpTransportProbeOrchestrator.ProbeAsync(
                new[]
                {
                    CreateBinding(
                        TransportId.Tcp,
                        "tcp",
                        new Queue<NnrpTransportProbeSampleResult>(new[]
                        {
                            Sample(TransportId.Tcp, "tcp", true, 1024, 1000),
                            Sample(TransportId.Tcp, "tcp", true, 1024, 1600),
                            Sample(TransportId.Tcp, "tcp", true, 1024, 2200),
                        })),
                },
                new NnrpTransportProbeOptions { WarmupProbeCount = 0, ScoredProbeCount = 3, PayloadBytes = 1024 },
                CancellationToken.None);

            var summary = Assert.Single(result.Summaries);
            Assert.Equal(600L, summary.MedianJitterMicroseconds);
        }

        [Fact]
        public void MigrationTriggerRequestsMigrationOnFailureRegression()
        {
            var selection = new NnrpTransportProbeSelectionResult(
                TransportId.Quic,
                "quic",
                new[]
                {
                    new NnrpTransportProbeBindingSummary(TransportId.Tcp, "tcp", successCount: 1, failureCount: 2, warmupSampleCount: 0, scoredSampleCount: 3, medianThroughputBytesPerSecond: 400_000d, medianRttMicroseconds: 2200, medianJitterMicroseconds: 900),
                    new NnrpTransportProbeBindingSummary(TransportId.Quic, "quic", successCount: 3, failureCount: 0, warmupSampleCount: 0, scoredSampleCount: 3, medianThroughputBytesPerSecond: 450_000d, medianRttMicroseconds: 2000, medianJitterMicroseconds: 700),
                });

            var decision = NnrpTransportMigrationTrigger.Evaluate(TransportId.Tcp, selection, new NnrpTransportMigrationTriggerOptions());

            Assert.True(decision.ShouldMigrate);
            Assert.Equal(TransportId.Quic, decision.TargetTransportId);
            Assert.Equal(NnrpTransportMigrationTriggerMetric.FailureRegression, decision.TriggerMetric);
        }

        [Fact]
        public void MigrationTriggerRequestsMigrationOnJitterImprovement()
        {
            var selection = new NnrpTransportProbeSelectionResult(
                TransportId.Quic,
                "quic",
                new[]
                {
                    new NnrpTransportProbeBindingSummary(TransportId.Tcp, "tcp", successCount: 3, failureCount: 0, warmupSampleCount: 0, scoredSampleCount: 3, medianThroughputBytesPerSecond: 400_000d, medianRttMicroseconds: 1600, medianJitterMicroseconds: 2000),
                    new NnrpTransportProbeBindingSummary(TransportId.Quic, "quic", successCount: 3, failureCount: 0, warmupSampleCount: 0, scoredSampleCount: 3, medianThroughputBytesPerSecond: 405_000d, medianRttMicroseconds: 1500, medianJitterMicroseconds: 600),
                });

            var decision = NnrpTransportMigrationTrigger.Evaluate(
                TransportId.Tcp,
                selection,
                new NnrpTransportMigrationTriggerOptions
                {
                    MinimumThroughputGainRatio = 1.5d,
                    MaximumRttRatio = 0.9d,
                    MaximumJitterRatio = 0.5d,
                });

            Assert.True(decision.ShouldMigrate);
            Assert.Equal(NnrpTransportMigrationTriggerMetric.Jitter, decision.TriggerMetric);
        }

        [Fact]
        public void MigrationTriggerSkipsWhenSelectedTransportMatchesCurrent()
        {
            var selection = new NnrpTransportProbeSelectionResult(
                TransportId.Tcp,
                "tcp",
                new[]
                {
                    new NnrpTransportProbeBindingSummary(TransportId.Tcp, "tcp", successCount: 3, failureCount: 0, warmupSampleCount: 0, scoredSampleCount: 3, medianThroughputBytesPerSecond: 500_000d, medianRttMicroseconds: 1200, medianJitterMicroseconds: 200),
                    new NnrpTransportProbeBindingSummary(TransportId.Quic, "quic", successCount: 3, failureCount: 0, warmupSampleCount: 0, scoredSampleCount: 3, medianThroughputBytesPerSecond: 450_000d, medianRttMicroseconds: 1300, medianJitterMicroseconds: 250),
                });

            var decision = NnrpTransportMigrationTrigger.Evaluate(TransportId.Tcp, selection, new NnrpTransportMigrationTriggerOptions());

            Assert.False(decision.ShouldMigrate);
            Assert.Equal(NnrpTransportMigrationTriggerMetric.None, decision.TriggerMetric);
        }

        [Fact]
        public async Task ProbeAsyncValidatesDuplicateBindings()
        {
            var binding1 = new NnrpTransportProbeBinding(
                TransportId.Tcp,
                "tcp-a",
                (request, cancellationToken) => new ValueTask<NnrpTransportProbeSampleResult>(Sample(TransportId.Tcp, "tcp-a", true, request.PayloadBytes, 1000)));
            var binding2 = new NnrpTransportProbeBinding(
                TransportId.Tcp,
                "tcp-b",
                (request, cancellationToken) => new ValueTask<NnrpTransportProbeSampleResult>(Sample(TransportId.Tcp, "tcp-b", true, request.PayloadBytes, 900)));

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await NnrpTransportProbeOrchestrator.ProbeAsync(
                    new[] { binding1, binding2 },
                    new NnrpTransportProbeOptions(),
                    CancellationToken.None));
        }

        [Fact]
        public async Task ProbeExchangeSendsTransportProbeAndParsesAck()
        {
            var transport = new ProbeQueueTransport(
                new TransportProbeAckMessage(
                    new NnrpHeader(
                        NnrpHeader.CurrentVersionMajor,
                        NnrpHeader.CurrentWireFormat,
                        MessageType.TransportProbeAck,
                        HeaderFlags.None,
                        TransportProbeAckMetadata.MetadataLength,
                        0,
                        0,
                        0,
                        0,
                        0,
                        55),
                    new TransportProbeAckMetadata(0, 0, 1200)).ToFramedMessage());

            var sample = await NnrpTransportProbeExchange.ProbeAsync(
                TransportId.Tcp,
                "tcp",
                transport,
                new NnrpTransportProbeRequest(0, 2, isWarmup: false, payloadBytes: 16, timeout: TimeSpan.FromMilliseconds(500)),
                traceId: 55,
                cancellationToken: CancellationToken.None);

            Assert.True(sample.IsSuccess);
            Assert.Equal(TransportId.Tcp, sample.TransportId);
            Assert.Single(transport.Sent);
            Assert.True(TransportProbeMessage.TryParse(transport.Sent[0].ToArray(), out var sentProbe, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(16u, sentProbe.Metadata.ProbePayloadBytes);
        }

        [Fact]
        public async Task AutoProbeBootstrapConnectsSelectedBinding()
        {
            var connectTransport = new ClientTestsAccessor.QueueTransport(
                ClientTestsAccessor.CreateServerHelloAck(
                    sessionId: 41,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    echoedTransportPolicy: TransportPolicy.Auto,
                    activeTransportId: TransportId.Tcp).ToFramedMessage());
            var connectInvocations = 0;
            var binding = new NnrpTransportConnectBinding(
                new NnrpTransportProbeBinding(
                    TransportId.Tcp,
                    "tcp",
                    (request, cancellationToken) => new ValueTask<NnrpTransportProbeSampleResult>(
                        Sample(TransportId.Tcp, "tcp", true, request.PayloadBytes, 1000))),
                cancellationToken =>
                {
                    connectInvocations++;
                    return new ValueTask<INnrpMessageTransport>(connectTransport);
                });

            var result = await NnrpClientBootstrapper.ConnectWithAutoProbeAsync(
                new ClientProfile { TransportPolicy = TransportPolicy.Auto },
                new[] { binding },
                new NnrpTransportProbeOptions { WarmupProbeCount = 0, ScoredProbeCount = 1, PayloadBytes = 256 },
                requestedSessionId: 41,
                cancellationToken: CancellationToken.None);

            Assert.True(result.WasProbed);
            Assert.True(result.ConnectResult.IsConnected);
            Assert.Equal(1, connectInvocations);
            Assert.NotNull(result.ProbeSelection);
            Assert.Equal(TransportId.Tcp, result.ProbeSelection.Value.SelectedTransportId);
            Assert.Single(connectTransport.Sent);
            Assert.True(ClientHelloMessage.TryParse(connectTransport.Sent[0].ToArray(), out var hello, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(NnrpHeader.CurrentWireFormat, hello.Header.WireFormat);
            Assert.True(hello.TryGetClientTransportPolicyExtension(out var extension, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(TransportPolicy.Auto, extension.TransportPolicy);
            Assert.Equal(TransportId.Tcp, extension.PreferredTransportId);
        }

        [Fact]
        public async Task AutoProbeBootstrapSkipsProbeForForcedTransportPolicy()
        {
            var connectTransport = new ClientTestsAccessor.QueueTransport(
                ClientTestsAccessor.CreateServerHelloAck(
                    sessionId: 9,
                    wireFormat: NnrpHeader.CurrentWireFormat,
                    echoedTransportPolicy: TransportPolicy.ForceTcp,
                    activeTransportId: TransportId.Tcp).ToFramedMessage());
            var probeInvocations = 0;
            var binding = new NnrpTransportConnectBinding(
                new NnrpTransportProbeBinding(
                    TransportId.Tcp,
                    "tcp",
                    (request, cancellationToken) =>
                    {
                        probeInvocations++;
                        return new ValueTask<NnrpTransportProbeSampleResult>(
                            Sample(TransportId.Tcp, "tcp", true, request.PayloadBytes, 900));
                    }),
                cancellationToken => new ValueTask<INnrpMessageTransport>(connectTransport));

            var result = await NnrpClientBootstrapper.ConnectWithAutoProbeAsync(
                new ClientProfile { TransportPolicy = TransportPolicy.ForceTcp },
                new[] { binding },
                new NnrpTransportProbeOptions(),
                requestedSessionId: 9,
                cancellationToken: CancellationToken.None);

            Assert.False(result.WasProbed);
            Assert.Null(result.ProbeSelection);
            Assert.True(result.ConnectResult.IsConnected);
            Assert.Equal(0, probeInvocations);
            Assert.Single(connectTransport.Sent);
            Assert.True(ClientHelloMessage.TryParse(connectTransport.Sent[0].ToArray(), out var hello, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.True(hello.TryGetClientTransportPolicyExtension(out var extension, out error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(TransportPolicy.ForceTcp, extension.TransportPolicy);
            Assert.Equal(TransportId.Tcp, extension.PreferredTransportId);
        }

        private static NnrpTransportProbeBinding CreateBinding(
            TransportId transportId,
            string bindingName,
            Queue<NnrpTransportProbeSampleResult> samples)
        {
            return new NnrpTransportProbeBinding(
                transportId,
                bindingName,
                (request, cancellationToken) => new ValueTask<NnrpTransportProbeSampleResult>(samples.Dequeue()));
        }

        private static NnrpTransportProbeSampleResult Sample(
            TransportId transportId,
            string bindingName,
            bool isSuccess,
            int payloadBytes,
            long roundTripMicroseconds,
            string failureDetail = "")
        {
            return new NnrpTransportProbeSampleResult(
                transportId,
                bindingName,
                isSuccess,
                payloadBytes,
                roundTripMicroseconds,
                failureDetail);
        }

        private sealed class ProbeQueueTransport : INnrpMessageTransport
        {
            private readonly Queue<NnrpFramedMessage> inbound;

            public ProbeQueueTransport(params NnrpFramedMessage[] inbound)
            {
                this.inbound = new Queue<NnrpFramedMessage>(inbound ?? Array.Empty<NnrpFramedMessage>());
            }

            public List<NnrpFramedMessage> Sent { get; } = new List<NnrpFramedMessage>();

            public ValueTask SendAsync(NnrpFramedMessage message, CancellationToken cancellationToken)
            {
                Sent.Add(message);
                return default;
            }

            public ValueTask<NnrpFramedMessage> ReceiveAsync(CancellationToken cancellationToken)
            {
                return new ValueTask<NnrpFramedMessage>(inbound.Dequeue());
            }
        }

        private static class ClientTestsAccessor
        {
            internal static ServerHelloAckMessage CreateServerHelloAck(
                uint sessionId,
                byte wireFormat = NnrpHeader.CurrentWireFormat,
                TransportPolicy echoedTransportPolicy = TransportPolicy.Auto,
                TransportId activeTransportId = TransportId.Unspecified)
            {
                var extensions = activeTransportId != TransportId.Unspecified
                    ? new[]
                    {
                        new ServerTransportPolicyAckExtension(echoedTransportPolicy, echoedTransportPolicy, activeTransportId).ToControlExtension(),
                    }
                    : Array.Empty<ControlExtensionBlock>();
                var metadata = new ServerHelloAckMetadata(
                    selectedVersionMajor: NnrpHeader.CurrentVersionMajor,
                    selectedWireFormat: wireFormat,
                    authStatus: 0,
                    reserved0: 0,
                    sessionId: sessionId,
                    acceptedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                    acceptedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                    acceptedCodecBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(new[] { CodecId.Raw }),
                    acceptedCompressionBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(new[] { CodecId.Raw }),
                    acceptedDTypeBitmap: ControlMetadataBitmaps.EncodeDTypeBitmap(new[] { DTypeId.UInt8, DTypeId.Float16 }),
                    acceptedLayoutBitmap: ControlMetadataBitmaps.EncodeTensorLayoutBitmap(new[] { TensorLayoutId.Nhwc }),
                    cacheDigestBitmap: ControlMetadataBitmaps.CacheDigestBitmap,
                    cacheObjectBitmap: ControlMetadataBitmaps.TensorProfileCacheObjectBitmap,
                    maxCacheEntries: 128,
                    maxCacheBytes: ControlMetadataBitmaps.DefaultCacheBytes,
                    maxLaneCount: 1,
                    maxConcurrentFrames: 2,
                    targetCadenceX100: 6000,
                    latencyBudgetMilliseconds: 16,
                    qualityTier: ControlMetadataBitmaps.DefaultQualityTier,
                    degradePolicy: 0,
                    maxBodyBytes: 1024 * 1024,
                    tokenTtlMilliseconds: 60000,
                    retryAfterMilliseconds: 0,
                    controlExtensionBytes: (uint)GetExtensionBodyLength(extensions),
                    serverFlags: 0);
                var header = new NnrpHeader(
                    versionMajor: NnrpHeader.CurrentVersionMajor,
                    messageType: MessageType.ServerHelloAck,
                    flags: HeaderFlags.None,
                    metaLength: ServerHelloAckMetadata.MetadataLength,
                    bodyLength: (uint)GetExtensionBodyLength(extensions),
                    sessionId: sessionId,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: 0,
                    wireFormat: wireFormat);
                return new ServerHelloAckMessage(header, metadata, extensions);
            }

            internal sealed class QueueTransport : INnrpMessageTransport
            {
                private readonly Queue<NnrpFramedMessage> inbound;

                public QueueTransport(params NnrpFramedMessage[] inbound)
                {
                    this.inbound = new Queue<NnrpFramedMessage>(inbound ?? Array.Empty<NnrpFramedMessage>());
                }

                public List<NnrpFramedMessage> Sent { get; } = new List<NnrpFramedMessage>();

                public ValueTask SendAsync(NnrpFramedMessage message, CancellationToken cancellationToken)
                {
                    Sent.Add(message);
                    return default;
                }

                public ValueTask<NnrpFramedMessage> ReceiveAsync(CancellationToken cancellationToken)
                {
                    return new ValueTask<NnrpFramedMessage>(inbound.Dequeue());
                }
            }

            private static int GetExtensionBodyLength(ControlExtensionBlock[] extensions)
            {
                var total = 0;
                for (var i = 0; i < extensions.Length; i++)
                {
                    total += extensions[i].TotalLength;
                }

                return total;
            }
        }
    }
}
