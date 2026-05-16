using System;
using System.Buffers.Binary;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class FlowControlMessageTests
    {
        [Fact]
        public void FlowUpdateMetadataRoundTripsThroughBytes()
        {
            var metadata = new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Session,
                updateReason: FlowUpdateReason.Congestion,
                backpressureLevel: FlowUpdateBackpressureLevel.Soft,
                connectionCredit: 0,
                sessionCredit: 2,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 33,
                creditEpoch: 7,
                flags: FlowUpdateFlags.CreditValid | FlowUpdateFlags.RetryAfterValid);

            var payload = metadata.ToArray();

            Assert.Equal(FlowUpdateMetadata.MetadataLength, payload.Length);
            Assert.True(FlowUpdateMetadata.TryParse(payload, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);
        }

        [Fact]
        public void FlowUpdateMetadataRejectsInvalidScopePayload()
        {
            var payload = CreateFlowUpdatePayload(
                scopeKind: FlowUpdateScopeKind.Session,
                updateReason: FlowUpdateReason.Reduce,
                backpressureLevel: FlowUpdateBackpressureLevel.Hard,
                connectionCredit: 1,
                sessionCredit: 3,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 11,
                flags: FlowUpdateFlags.CreditValid);

            Assert.False(FlowUpdateMetadata.TryParse(payload, strict: true, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void FlowUpdateMetadataRejectsUnknownFlags()
        {
            var payload = CreateFlowUpdatePayload(
                scopeKind: FlowUpdateScopeKind.Connection,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                connectionCredit: 2,
                sessionCredit: 0,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: (FlowUpdateFlags)0x00000010);

            Assert.False(FlowUpdateMetadata.TryParse(payload, strict: true, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void FlowUpdateMetadataRejectsRetryAfterWithoutFlag()
        {
            var payload = CreateFlowUpdatePayload(
                scopeKind: FlowUpdateScopeKind.Connection,
                updateReason: FlowUpdateReason.Pause,
                backpressureLevel: FlowUpdateBackpressureLevel.Hard,
                connectionCredit: 0,
                sessionCredit: 0,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 12,
                creditEpoch: 0,
                flags: FlowUpdateFlags.None);

            Assert.False(FlowUpdateMetadata.TryParse(payload, strict: true, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void FlowUpdateMessageRoundTripsFixedMetadata()
        {
            var metadata = new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Session,
                updateReason: FlowUpdateReason.Resume,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                connectionCredit: 0,
                sessionCredit: 4,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 12,
                flags: FlowUpdateFlags.CreditValid);
            var message = new FlowUpdateMessage(
                new NnrpHeader(
                    NnrpHeader.CurrentVersionMajor,
                    NnrpHeader.CurrentWireFormat,
                    MessageType.FlowUpdate,
                    HeaderFlags.None,
                    FlowUpdateMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: 41,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: 601),
                metadata);

            Assert.True(FlowUpdateMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed.Metadata);
            Assert.Equal(41u, parsed.Header.SessionId);
        }

        [Fact]
        public void FlowUpdateMessageRejectsConnectionScopeSessionMismatch()
        {
            var metadata = new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Connection,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                connectionCredit: 2,
                sessionCredit: 0,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.CreditValid);
            var framed = new NnrpFramedMessage(
                new NnrpHeader(
                    NnrpHeader.CurrentVersionMajor,
                    NnrpHeader.CurrentWireFormat,
                    MessageType.FlowUpdate,
                    HeaderFlags.None,
                    FlowUpdateMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: 41,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: 602),
                metadata.ToArray(),
                System.Array.Empty<byte>());

            Assert.False(FlowUpdateMessage.TryParse(framed, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
            Assert.Throws<System.ArgumentException>(() => new FlowUpdateMessage(framed.Header, metadata));
        }

        [Fact]
        public void ResultHintMetadataRoundTripsThroughBytes()
        {
            var metadata = new ResultHintMetadata(
                appliedBudgetPolicy: ResultHintBudgetPolicy.Partial,
                congestionState: ResultHintCongestionState.Elevated,
                reason: ResultHintReason.ServerBusy,
                retryAfterMilliseconds: 20);

            var payload = metadata.ToArray();

            Assert.Equal(ResultHintMetadata.MetadataLength, payload.Length);
            Assert.True(ResultHintMetadata.TryParse(payload, strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed);
        }

        [Fact]
        public void ResultHintMetadataRejectsUnknownReason()
        {
            var payload = new byte[ResultHintMetadata.MetadataLength];
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(0), (uint)ResultHintBudgetPolicy.Full);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(4), (uint)ResultHintCongestionState.Steady);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(8), 99u);

            Assert.False(ResultHintMetadata.TryParse(payload, strict: true, out _, out var error));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, error);
        }

        [Fact]
        public void ResultHintMessageRoundTripsFixedMetadata()
        {
            var metadata = new ResultHintMetadata(
                appliedBudgetPolicy: ResultHintBudgetPolicy.StaleReuse,
                congestionState: ResultHintCongestionState.Saturated,
                reason: ResultHintReason.BudgetExceeded,
                retryAfterMilliseconds: 50);
            var message = new ResultHintMessage(
                new NnrpHeader(
                    NnrpHeader.CurrentVersionMajor,
                    NnrpHeader.CurrentWireFormat,
                    MessageType.ResultHint,
                    HeaderFlags.None,
                    ResultHintMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: 41,
                    frameId: 303,
                    viewId: 0,
                    routeId: 0,
                    traceId: 603),
                metadata);

            Assert.True(ResultHintMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(metadata, parsed.Metadata);
            Assert.Equal(303u, parsed.Header.FrameId);
        }

        [Fact]
        public void ResultHintMessageRejectsUnknownWireFormat()
        {
            var metadata = new ResultHintMetadata(
                appliedBudgetPolicy: ResultHintBudgetPolicy.Full,
                congestionState: ResultHintCongestionState.Steady,
                reason: ResultHintReason.None,
                retryAfterMilliseconds: 0);
            var framed = new ResultHintMessage(
                new NnrpHeader(
                    NnrpHeader.CurrentVersionMajor,
                    MessageType.ResultHint,
                    HeaderFlags.None,
                    ResultHintMetadata.MetadataLength,
                    bodyLength: 0,
                    sessionId: 41,
                    frameId: 0,
                    viewId: 0,
                    routeId: 0,
                    traceId: 604),
                metadata).ToArray();
            framed[5] = 0x7F;

            Assert.False(ResultHintMessage.TryParse(framed, out _, out var error));
            Assert.Equal(NnrpParseError.UnknownWireFormat, error);
        }

        private static byte[] CreateFlowUpdatePayload(
            FlowUpdateScopeKind scopeKind,
            FlowUpdateReason updateReason,
            FlowUpdateBackpressureLevel backpressureLevel,
            ushort connectionCredit,
            ushort sessionCredit,
            ushort operationCredit,
            ulong operationId,
            uint retryAfterMilliseconds,
            uint creditEpoch,
            FlowUpdateFlags flags)
        {
            var payload = new byte[FlowUpdateMetadata.MetadataLength];
            payload[0] = (byte)scopeKind;
            payload[1] = (byte)updateReason;
            payload[2] = (byte)backpressureLevel;
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4), connectionCredit);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6), sessionCredit);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8), operationCredit);
            BinaryPrimitives.WriteUInt64LittleEndian(payload.AsSpan(12), operationId);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(20), retryAfterMilliseconds);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(24), creditEpoch);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(28), (uint)flags);
            return payload;
        }
    }
}