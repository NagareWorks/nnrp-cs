using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Client;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Client.Tests
{
    public sealed class CoverageThresholdTests
    {
        [Fact]
        public void TransportMigrationTriggerCoversRemainingDecisionBranches()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NnrpTransportMigrationTrigger.Evaluate(
                    TransportId.Unspecified,
                    new NnrpTransportProbeSelectionResult(
                        TransportId.Tcp,
                        "tcp",
                        new[] { Summary(TransportId.Tcp, "tcp", 2, 0, 300_000d, 1200, 200) })));

            Assert.Throws<ArgumentException>(() =>
                NnrpTransportMigrationTrigger.Evaluate(
                    TransportId.Tcp,
                    new NnrpTransportProbeSelectionResult(
                        TransportId.Quic,
                        "quic",
                        new[] { Summary(TransportId.Quic, "quic", 3, 0, 400_000d, 800, 100) })));

            var successCountGuardDecision = NnrpTransportMigrationTrigger.Evaluate(
                TransportId.Tcp,
                new NnrpTransportProbeSelectionResult(
                    TransportId.Quic,
                    "quic",
                    new[]
                    {
                        Summary(TransportId.Tcp, "tcp", 4, 0, 300_000d, 1200, 200),
                        Summary(TransportId.Quic, "quic", 3, 0, 450_000d, 800, 100),
                    }),
                new NnrpTransportMigrationTriggerOptions());
            Assert.False(successCountGuardDecision.ShouldMigrate);
            Assert.Equal(NnrpTransportMigrationTriggerMetric.None, successCountGuardDecision.TriggerMetric);

            var minimumComparableDecision = NnrpTransportMigrationTrigger.Evaluate(
                TransportId.Tcp,
                new NnrpTransportProbeSelectionResult(
                    TransportId.Quic,
                    "quic",
                    new[]
                    {
                        Summary(TransportId.Tcp, "tcp", 1, 0, 300_000d, 1200, 200),
                        Summary(TransportId.Quic, "quic", 1, 0, 450_000d, 800, 100),
                    }),
                new NnrpTransportMigrationTriggerOptions
                {
                    TriggerOnFailureRegression = false,
                });
            Assert.False(minimumComparableDecision.ShouldMigrate);
            Assert.Equal(NnrpTransportMigrationTriggerMetric.None, minimumComparableDecision.TriggerMetric);

            var throughputDecision = NnrpTransportMigrationTrigger.Evaluate(
                TransportId.Tcp,
                new NnrpTransportProbeSelectionResult(
                    TransportId.Quic,
                    "quic",
                    new[]
                    {
                        Summary(TransportId.Tcp, "tcp", 3, 0, 300_000d, 1200, 300),
                        Summary(TransportId.Quic, "quic", 3, 0, 400_000d, 1100, 290),
                    }),
                new NnrpTransportMigrationTriggerOptions
                {
                    TriggerOnFailureRegression = false,
                });
            Assert.True(throughputDecision.ShouldMigrate);
            Assert.Equal(NnrpTransportMigrationTriggerMetric.Throughput, throughputDecision.TriggerMetric);

            var roundTripDecision = NnrpTransportMigrationTrigger.Evaluate(
                TransportId.Tcp,
                new NnrpTransportProbeSelectionResult(
                    TransportId.Quic,
                    "quic",
                    new[]
                    {
                        Summary(TransportId.Tcp, "tcp", 3, 0, 300_000d, 2000, 300),
                        Summary(TransportId.Quic, "quic", 3, 0, 305_000d, 1500, 290),
                    }),
                new NnrpTransportMigrationTriggerOptions
                {
                    TriggerOnFailureRegression = false,
                    MinimumThroughputGainRatio = 2.0d,
                });
            Assert.True(roundTripDecision.ShouldMigrate);
            Assert.Equal(NnrpTransportMigrationTriggerMetric.RoundTripTime, roundTripDecision.TriggerMetric);

            var noMigrationDecision = NnrpTransportMigrationTrigger.Evaluate(
                TransportId.Tcp,
                new NnrpTransportProbeSelectionResult(
                    TransportId.Quic,
                    "quic",
                    new[]
                    {
                        Summary(TransportId.Tcp, "tcp", 3, 0, 300_000d, 1200, 200),
                        Summary(TransportId.Quic, "quic", 3, 0, 305_000d, 1300, 220),
                    }),
                new NnrpTransportMigrationTriggerOptions
                {
                    TriggerOnFailureRegression = false,
                    MinimumThroughputGainRatio = 2.0d,
                    MaximumRttRatio = 0.5d,
                    MaximumJitterRatio = 0.5d,
                });
            Assert.False(noMigrationDecision.ShouldMigrate);
            Assert.Equal(NnrpTransportMigrationTriggerMetric.None, noMigrationDecision.TriggerMetric);
        }

        [Fact]
        public async Task ClientValueObjectsCoverValidationAndEqualityBranches()
        {
            var submittedFrame = new NnrpSubmittedFrame(sessionId: 7, frameId: 9, viewId: 2, traceId: 11);
            var sameSubmittedFrame = new NnrpSubmittedFrame(sessionId: 7, frameId: 9, viewId: 2, traceId: 11);
            Assert.Equal(NnrpHeader.CurrentWireFormat, submittedFrame.WireFormat);
            Assert.True(submittedFrame.Equals(sameSubmittedFrame));
            Assert.True(submittedFrame.Equals((object)sameSubmittedFrame));
            Assert.False(submittedFrame.Equals("not-submitted-frame"));
            Assert.Equal(submittedFrame.GetHashCode(), sameSubmittedFrame.GetHashCode());

            var resultPushEvent = NnrpSessionEvent.FromResultPush(default);
            Assert.True(resultPushEvent.IsResultPush);
            Assert.False(resultPushEvent.IsFlowUpdate);
            Assert.Equal(default(ResultPushMessage), resultPushEvent.GetResultPush());
            Assert.Throws<InvalidOperationException>(() => resultPushEvent.GetResultDrop());
            Assert.Throws<InvalidOperationException>(() => resultPushEvent.GetFlowUpdate());
            Assert.Throws<InvalidOperationException>(() => resultPushEvent.GetResultHint());

            var resultDropEvent = NnrpSessionEvent.FromResultDrop(default);
            Assert.True(resultDropEvent.IsResultDrop);
            Assert.Equal(default(ResultDropMessage), resultDropEvent.GetResultDrop());
            Assert.Throws<InvalidOperationException>(() => resultDropEvent.GetResultPush());

            var flowUpdateEvent = NnrpSessionEvent.FromFlowUpdate(default);
            Assert.True(flowUpdateEvent.IsFlowUpdate);
            Assert.Equal(default(FlowUpdateMessage), flowUpdateEvent.GetFlowUpdate());

            var resultHintEvent = NnrpSessionEvent.FromResultHint(default);
            Assert.True(resultHintEvent.IsResultHint);
            Assert.Equal(default(ResultHintMessage), resultHintEvent.GetResultHint());

            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportMigrationDecision(
                shouldMigrate: true,
                currentTransportId: TransportId.Unspecified,
                currentBindingName: "tcp",
                targetTransportId: TransportId.Quic,
                targetBindingName: "quic",
                triggerMetric: NnrpTransportMigrationTriggerMetric.Throughput));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportMigrationDecision(
                shouldMigrate: true,
                currentTransportId: TransportId.Tcp,
                currentBindingName: "tcp",
                targetTransportId: TransportId.Unspecified,
                targetBindingName: "quic",
                triggerMetric: NnrpTransportMigrationTriggerMetric.Throughput));
            Assert.Throws<ArgumentException>(() => new NnrpTransportMigrationDecision(true, TransportId.Tcp, " ", TransportId.Quic, "quic", NnrpTransportMigrationTriggerMetric.Throughput));
            Assert.Throws<ArgumentException>(() => new NnrpTransportMigrationDecision(true, TransportId.Tcp, "tcp", TransportId.Quic, " ", NnrpTransportMigrationTriggerMetric.Throughput));

            var migrationDecision = new NnrpTransportMigrationDecision(
                shouldMigrate: true,
                currentTransportId: TransportId.Tcp,
                currentBindingName: "tcp",
                targetTransportId: TransportId.Quic,
                targetBindingName: "quic",
                triggerMetric: NnrpTransportMigrationTriggerMetric.Throughput);
            var migrationResult = new NnrpClientMigrationResult(migrationDecision, default, wasMigrated: true);
            Assert.True(migrationResult.WasMigrated);
            Assert.Equal(TransportId.Quic, migrationResult.Decision.TargetTransportId);

            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeRequest(-1, 1, false, 16, TimeSpan.FromMilliseconds(100)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeRequest(0, 0, false, 16, TimeSpan.FromMilliseconds(100)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeRequest(0, 1, false, -1, TimeSpan.FromMilliseconds(100)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeRequest(0, 1, false, 16, TimeSpan.FromMilliseconds(-1)));

            var probeRequest = new NnrpTransportProbeRequest(1, 3, isWarmup: true, payloadBytes: 32, timeout: TimeSpan.FromMilliseconds(200));
            Assert.Equal(1, probeRequest.SampleIndex);
            Assert.True(probeRequest.IsWarmup);
            Assert.Equal(32, probeRequest.PayloadBytes);

            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeSampleResult(TransportId.Unspecified, "tcp", true, 1, 1));
            Assert.Throws<ArgumentException>(() => new NnrpTransportProbeSampleResult(TransportId.Tcp, " ", true, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeSampleResult(TransportId.Tcp, "tcp", true, -1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeSampleResult(TransportId.Tcp, "tcp", true, 1, -1));

            var sampleResult = new NnrpTransportProbeSampleResult(TransportId.Tcp, "tcp", true, payloadBytes: 1000, roundTripMicroseconds: 5000);
            Assert.Equal(200000d, sampleResult.ThroughputBytesPerSecond);
            var failedSampleResult = new NnrpTransportProbeSampleResult(TransportId.Tcp, "tcp", false, payloadBytes: 1000, roundTripMicroseconds: 5000, failureDetail: null!);
            Assert.Equal(0d, failedSampleResult.ThroughputBytesPerSecond);
            Assert.Equal(string.Empty, failedSampleResult.FailureDetail);

            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeSelectionResult(TransportId.Unspecified, "tcp", Array.Empty<NnrpTransportProbeBindingSummary>()));
            Assert.Throws<ArgumentException>(() => new NnrpTransportProbeSelectionResult(TransportId.Tcp, " ", Array.Empty<NnrpTransportProbeBindingSummary>()));
            Assert.Throws<ArgumentNullException>(() => new NnrpTransportProbeSelectionResult(TransportId.Tcp, "tcp", null!));

            var selection = new NnrpTransportProbeSelectionResult(TransportId.Tcp, "tcp", new[] { Summary(TransportId.Tcp, "tcp", 2, 0, 300_000d, 1200, 200) });
            Assert.Equal(TransportId.Tcp, selection.SelectedTransportId);
            Assert.Single(selection.Summaries);

            Assert.Throws<ArgumentOutOfRangeException>(() => new NnrpTransportProbeBinding(TransportId.Unspecified, "tcp", (request, token) => new ValueTask<NnrpTransportProbeSampleResult>(sampleResult)));
            Assert.Throws<ArgumentException>(() => new NnrpTransportProbeBinding(TransportId.Tcp, " ", (request, token) => new ValueTask<NnrpTransportProbeSampleResult>(sampleResult)));
            Assert.Throws<ArgumentNullException>(() => new NnrpTransportProbeBinding(TransportId.Tcp, "tcp", null!));

            var binding = new NnrpTransportProbeBinding(
                TransportId.Tcp,
                "tcp",
                (request, token) => new ValueTask<NnrpTransportProbeSampleResult>(new NnrpTransportProbeSampleResult(TransportId.Tcp, "tcp", true, request.PayloadBytes, 4000)));
            var probed = await binding.ProbeAsync(probeRequest, CancellationToken.None);
            Assert.Equal(TransportId.Tcp, binding.TransportId);
            Assert.Equal("tcp", binding.BindingName);
            Assert.True(probed.IsSuccess);
            Assert.Equal(probeRequest.PayloadBytes, probed.PayloadBytes);

            var inFlightFrame = new NnrpInFlightFrame(frameId: 13, viewId: 4);
            var sameInFlightFrame = new NnrpInFlightFrame(frameId: 13, viewId: 4);
            Assert.True(inFlightFrame.Equals(sameInFlightFrame));
            Assert.True(inFlightFrame.Equals((object)sameInFlightFrame));
            Assert.False(inFlightFrame.Equals("not-in-flight"));
            Assert.Equal(inFlightFrame.GetHashCode(), sameInFlightFrame.GetHashCode());

            var autoDecision = NnrpTransportPolicyHelper.ResolveSelectionDecision(TransportPolicy.Auto);
            Assert.True(autoDecision.ShouldProbe);
            Assert.Equal(TransportId.Unspecified, autoDecision.PreferredTransportId);
            Assert.False(autoDecision.IsForced);

            var forcedDecision = NnrpTransportPolicyHelper.ResolveSelectionDecision(TransportPolicy.ForceQuic);
            Assert.False(forcedDecision.ShouldProbe);
            Assert.Equal(TransportId.Quic, forcedDecision.PreferredTransportId);
            Assert.True(forcedDecision.IsForced);
            Assert.Throws<ArgumentOutOfRangeException>(() => NnrpTransportPolicyHelper.ResolveSelectionDecision((TransportPolicy)255));

            var connectBinding = new NnrpTransportConnectBinding(
                binding,
                token => new ValueTask<INnrpMessageTransport>(new TestTransport()));
            var connectedTransport = await connectBinding.ConnectAsync(CancellationToken.None);
            Assert.Equal(binding, connectBinding.ProbeBinding);
            Assert.Equal(TransportId.Tcp, connectBinding.TransportId);
            Assert.Equal("tcp", connectBinding.BindingName);
            Assert.IsType<TestTransport>(connectedTransport);
            Assert.Throws<ArgumentNullException>(() => new NnrpTransportConnectBinding(null!, token => new ValueTask<INnrpMessageTransport>(new TestTransport())));
            Assert.Throws<ArgumentNullException>(() => new NnrpTransportConnectBinding(binding, null!));

            var typedFrames = new[]
            {
                new TypedPayloadFrameView(new TypedPayloadDescriptor(PayloadKind.TokenChunk, 0, 3, 0, 2, 0), new byte[] { 0x01, 0x02 }),
                new TypedPayloadFrameView(new TypedPayloadDescriptor(PayloadKind.TokenChunk, 0, 3, 2, 2, 0), new byte[] { 0x03, 0x04 }),
                new TypedPayloadFrameView(new TypedPayloadDescriptor(PayloadKind.VideoChunk, 0, 5, 4, 2, 0), new byte[] { 0x05, 0x06 }),
            };
            var submitResult = new NnrpSubmitResult(
                sessionId: 7,
                frameId: 9,
                viewId: 2,
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.Partial,
                activeProfileId: 1,
                inferenceMilliseconds: 10,
                queueMilliseconds: 2,
                serverTotalMilliseconds: 12,
                tileIds: new ushort[] { 1, 3 },
                sections: Array.Empty<TensorSectionBlock>(),
                resultClass: ResultClass.Partial,
                appliedBudgetPolicy: BudgetPolicy.AllowDrop,
                reusedFrameId: 4,
                coveredTileCount: 2,
                droppedTileCount: 1,
                payloadKindBitmap: PayloadKind.TokenChunk | PayloadKind.VideoChunk,
                payloadFrameCount: 3,
                typedPayloadFrames: typedFrames);
            Assert.Equal(7u, submitResult.SessionId);
            Assert.Equal(9u, submitResult.FrameId);
            Assert.Equal((ushort)2, submitResult.ViewId);
            Assert.Equal(ResultStatusCode.Success, submitResult.StatusCode);
            Assert.Equal(ResultFlags.Partial, submitResult.ResultFlags);
            Assert.Equal((ushort)1, submitResult.ActiveProfileId);
            Assert.Equal((ushort)10, submitResult.InferenceMilliseconds);
            Assert.Equal((ushort)2, submitResult.QueueMilliseconds);
            Assert.Equal((ushort)12, submitResult.ServerTotalMilliseconds);
            Assert.Equal(ResultClass.Partial, submitResult.ResultClass);
            Assert.Equal(BudgetPolicy.AllowDrop, submitResult.AppliedBudgetPolicy);
            Assert.Equal(4u, submitResult.ReusedFrameId);
            Assert.Equal((ushort)2, submitResult.CoveredTileCount);
            Assert.Equal((ushort)1, submitResult.DroppedTileCount);
            Assert.Equal(PayloadKind.TokenChunk | PayloadKind.VideoChunk, submitResult.PayloadKindBitmap);
            Assert.Equal((ushort)3, submitResult.PayloadFrameCount);
            Assert.Equal(new ushort[] { 1, 3 }, submitResult.TileIds.ToArray());
            Assert.Empty(submitResult.Sections.ToArray());
            Assert.Equal(2, submitResult.GetTypedPayloadFrames(PayloadKind.TokenChunk, profileId: 3).Length);
            Assert.Empty(submitResult.GetTypedPayloadFrames(PayloadKind.AudioChunk, profileId: 3));
            Assert.Equal(2, submitResult.GetTokenChunkFrames(profileId: 3).Frames.Length);
            Assert.Equal(1, submitResult.GetVideoChunkFrames(profileId: 5).Frames.Length);
            Assert.True(submitResult.GetStructuredEventFrames(profileId: 7).Frames.IsEmpty);
            Assert.True(submitResult.GetToolDeltaFrames(profileId: 7).Frames.IsEmpty);
            Assert.True(submitResult.GetOpaqueBytesFrames(profileId: 7).Frames.IsEmpty);
            Assert.Throws<ArgumentOutOfRangeException>(() => submitResult.GetTypedPayloadFrames(PayloadKind.TokenChunk | PayloadKind.VideoChunk, profileId: 3));

            var emptyTypedPayloadSubmitResult = new NnrpSubmitResult(
                sessionId: 1,
                frameId: 2,
                viewId: 0,
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.None,
                activeProfileId: 0,
                inferenceMilliseconds: 0,
                queueMilliseconds: 0,
                serverTotalMilliseconds: 0,
                tileIds: Array.Empty<ushort>(),
                sections: Array.Empty<TensorSectionBlock>());
            Assert.True(emptyTypedPayloadSubmitResult.TypedPayloadFrames.IsEmpty);
        }

        [Fact]
        public async Task ClientBootstrapperCoversGuardClausesAndForcedSelectionFailure()
        {
            var probeBinding = new NnrpTransportProbeBinding(
                TransportId.Tcp,
                "tcp",
                (request, token) => new ValueTask<NnrpTransportProbeSampleResult>(new NnrpTransportProbeSampleResult(TransportId.Tcp, "tcp", true, request.PayloadBytes, 4000)));
            var connectBinding = new NnrpTransportConnectBinding(
                probeBinding,
                token => new ValueTask<INnrpMessageTransport>(new TestTransport()));
            var options = new NnrpTransportProbeOptions();

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await NnrpClientBootstrapper.ConnectWithAutoProbeAsync(
                    null!,
                    new[] { connectBinding },
                    options,
                    cancellationToken: CancellationToken.None));

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await NnrpClientBootstrapper.ConnectWithAutoProbeAsync(
                    new ClientProfile(),
                    null!,
                    options,
                    cancellationToken: CancellationToken.None));

            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await NnrpClientBootstrapper.ConnectWithAutoProbeAsync(
                    new ClientProfile(),
                    Array.Empty<NnrpTransportConnectBinding>(),
                    options,
                    cancellationToken: CancellationToken.None));

            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpClientBootstrapper.ConnectWithAutoProbeAsync(
                    new ClientProfile { TransportPolicy = TransportPolicy.ForceQuic },
                    new[] { connectBinding },
                    options,
                    cancellationToken: CancellationToken.None));
        }

        [Fact]
        public async Task ProbeOrchestratorCoversGuardClausesAndComparisonBranches()
        {
            var binding = new NnrpTransportProbeBinding(
                TransportId.Tcp,
                "tcp",
                (request, token) => new ValueTask<NnrpTransportProbeSampleResult>(new NnrpTransportProbeSampleResult(TransportId.Tcp, "tcp", true, request.PayloadBytes, 1000)));

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await NnrpTransportProbeOrchestrator.ProbeAsync(null!, new NnrpTransportProbeOptions(), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await NnrpTransportProbeOrchestrator.ProbeAsync(new[] { binding }, null!, CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await NnrpTransportProbeOrchestrator.ProbeAsync(Array.Empty<NnrpTransportProbeBinding>(), new NnrpTransportProbeOptions(), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await NnrpTransportProbeOrchestrator.ProbeAsync(new NnrpTransportProbeBinding[] { null! }, new NnrpTransportProbeOptions(), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await NnrpTransportProbeOrchestrator.ProbeAsync(new[] { binding, binding }, new NnrpTransportProbeOptions(), CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpTransportProbeOrchestrator.ProbeAsync(new[] { binding }, new NnrpTransportProbeOptions { WarmupProbeCount = -1 }, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpTransportProbeOrchestrator.ProbeAsync(new[] { binding }, new NnrpTransportProbeOptions { ScoredProbeCount = 0 }, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpTransportProbeOrchestrator.ProbeAsync(new[] { binding }, new NnrpTransportProbeOptions { PayloadBytes = -1 }, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpTransportProbeOrchestrator.ProbeAsync(new[] { binding }, new NnrpTransportProbeOptions { ProbeTimeout = TimeSpan.FromMilliseconds(-1) }, CancellationToken.None));

            var throughputSelection = await NnrpTransportProbeOrchestrator.ProbeAsync(
                new[]
                {
                    new NnrpTransportProbeBinding(
                        TransportId.Tcp,
                        "tcp",
                        (request, token) => new ValueTask<NnrpTransportProbeSampleResult>(new NnrpTransportProbeSampleResult(TransportId.Tcp, "tcp", true, request.PayloadBytes, 1000))),
                    new NnrpTransportProbeBinding(
                        TransportId.Quic,
                        "quic",
                        (request, token) => new ValueTask<NnrpTransportProbeSampleResult>(new NnrpTransportProbeSampleResult(TransportId.Quic, "quic", true, request.PayloadBytes, 500))),
                },
                new NnrpTransportProbeOptions { WarmupProbeCount = 0, ScoredProbeCount = 1, PayloadBytes = 64 },
                CancellationToken.None);
            Assert.Equal(TransportId.Quic, throughputSelection.SelectedTransportId);

            var tieSelection = await NnrpTransportProbeOrchestrator.ProbeAsync(
                new[]
                {
                    new NnrpTransportProbeBinding(
                        TransportId.Tcp,
                        "tcp",
                        (request, token) => throw new InvalidOperationException("tcp-failed")),
                    new NnrpTransportProbeBinding(
                        TransportId.Quic,
                        "quic",
                        (request, token) => throw new InvalidOperationException("quic-failed")),
                },
                new NnrpTransportProbeOptions { WarmupProbeCount = 0, ScoredProbeCount = 1, PayloadBytes = 64 },
                CancellationToken.None);
            Assert.Equal(TransportId.Tcp, tieSelection.SelectedTransportId);
        }

        [Fact]
        public async Task ProbeExchangeCoversGuardClausesAndDisposesConnectionTransport()
        {
            var request = new NnrpTransportProbeRequest(0, 1, isWarmup: false, payloadBytes: 16, timeout: TimeSpan.FromMilliseconds(500));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
                await NnrpTransportProbeExchange.ProbeAsync(TransportId.Unspecified, "tcp", new ProbeQueueTransport(), request, cancellationToken: CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await NnrpTransportProbeExchange.ProbeAsync(TransportId.Tcp, " ", new ProbeQueueTransport(), request, cancellationToken: CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
                await NnrpTransportProbeExchange.ProbeAsync(TransportId.Tcp, "tcp", null!, request, cancellationToken: CancellationToken.None));

            var wrongMessageTransport = new ProbeQueueTransport(
                new ServerHelloAckMessage(
                    new NnrpHeader(
                        NnrpHeader.CurrentVersionMajor,
                        NnrpHeader.CurrentWireFormat,
                        MessageType.ServerHelloAck,
                        HeaderFlags.None,
                        ServerHelloAckMetadata.MetadataLength,
                        0,
                        0,
                        0,
                        0,
                        0,
                        0),
                    new ServerHelloAckMetadata(
                        NnrpHeader.CurrentVersionMajor,
                        NnrpHeader.CurrentWireFormat,
                        authStatus: 0,
                        reserved0: 0,
                        sessionId: 0,
                        acceptedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                        acceptedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                        acceptedCodecBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(new[] { CodecId.Raw }),
                        acceptedCompressionBitmap: ControlMetadataBitmaps.EncodeCodecBitmap(new[] { CodecId.Raw }),
                        acceptedDTypeBitmap: ControlMetadataBitmaps.EncodeDTypeBitmap(new[] { DTypeId.UInt8 }),
                        acceptedLayoutBitmap: ControlMetadataBitmaps.EncodeTensorLayoutBitmap(new[] { TensorLayoutId.Nhwc }),
                        cacheDigestBitmap: 0,
                        cacheObjectBitmap: 0,
                        maxCacheEntries: 0,
                        maxCacheBytes: 0,
                        maxLaneCount: 1,
                        maxConcurrentFrames: 1,
                        targetCadenceX100: 0,
                        latencyBudgetMilliseconds: 0,
                        qualityTier: 0,
                        degradePolicy: 0,
                        maxBodyBytes: 1024,
                        tokenTtlMilliseconds: 0,
                        retryAfterMilliseconds: 0,
                        controlExtensionBytes: 0,
                        serverFlags: 0)).ToFramedMessage());
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpTransportProbeExchange.ProbeAsync(TransportId.Tcp, "tcp", wrongMessageTransport, request, cancellationToken: CancellationToken.None));

            var mismatchedAckTransport = new ProbeQueueTransport(CreateProbeAck(probeId: 1));
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await NnrpTransportProbeExchange.ProbeAsync(TransportId.Tcp, "tcp", mismatchedAckTransport, request, cancellationToken: CancellationToken.None));

            Assert.Throws<ArgumentNullException>(() =>
                NnrpTransportProbeExchange.CreateConnectionBinding(TransportId.Tcp, "tcp", null!));

            var disposableTransport = new DisposableProbeTransport(CreateProbeAck(probeId: 0));
            var connectionBinding = NnrpTransportProbeExchange.CreateConnectionBinding(
                TransportId.Tcp,
                "tcp",
                token => new ValueTask<INnrpMessageTransport>(disposableTransport));
            var sample = await connectionBinding.ProbeBinding.ProbeAsync(request, CancellationToken.None);
            Assert.True(sample.IsSuccess);
            Assert.True(disposableTransport.Disposed);
        }

        private static NnrpTransportProbeBindingSummary Summary(
            TransportId transportId,
            string bindingName,
            int successCount,
            int failureCount,
            double throughputBytesPerSecond,
            long roundTripMicroseconds,
            long jitterMicroseconds)
        {
            return new NnrpTransportProbeBindingSummary(
                transportId,
                bindingName,
                successCount,
                failureCount,
                warmupSampleCount: 0,
                scoredSampleCount: 3,
                medianThroughputBytesPerSecond: throughputBytesPerSecond,
                medianRttMicroseconds: roundTripMicroseconds,
                medianJitterMicroseconds: jitterMicroseconds);
        }

        private static NnrpFramedMessage CreateProbeAck(uint probeId)
        {
            return new TransportProbeAckMessage(
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
                    0),
                new TransportProbeAckMetadata(probeId, reserved: 0, serverReceiveTimestampMicroseconds: 1200)).ToFramedMessage();
        }

        private sealed class TestTransport : INnrpMessageTransport
        {
            public ValueTask SendAsync(NnrpFramedMessage message, CancellationToken cancellationToken)
            {
                return default;
            }

            public ValueTask<NnrpFramedMessage> ReceiveAsync(CancellationToken cancellationToken)
            {
                return new ValueTask<NnrpFramedMessage>(default(NnrpFramedMessage));
            }
        }

        private class ProbeQueueTransport : INnrpMessageTransport
        {
            private readonly Queue<NnrpFramedMessage> inbound;

            public ProbeQueueTransport(params NnrpFramedMessage[] inbound)
            {
                this.inbound = new Queue<NnrpFramedMessage>(inbound ?? Array.Empty<NnrpFramedMessage>());
            }

            public ValueTask SendAsync(NnrpFramedMessage message, CancellationToken cancellationToken)
            {
                return default;
            }

            public ValueTask<NnrpFramedMessage> ReceiveAsync(CancellationToken cancellationToken)
            {
                return new ValueTask<NnrpFramedMessage>(inbound.Dequeue());
            }
        }

        private sealed class DisposableProbeTransport : ProbeQueueTransport, IDisposable
        {
            public DisposableProbeTransport(params NnrpFramedMessage[] inbound)
                : base(inbound)
            {
            }

            public bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }
    }
}
