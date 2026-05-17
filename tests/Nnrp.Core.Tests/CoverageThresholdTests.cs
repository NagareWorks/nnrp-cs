using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class CoverageThresholdTests
    {
        [Fact]
        public void FlowUpdateMetadataCoversValidationAndEqualityBranches()
        {
            var metadata = new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Operation,
                updateReason: FlowUpdateReason.Reduce,
                backpressureLevel: FlowUpdateBackpressureLevel.Hard,
                connectionCredit: 0,
                sessionCredit: 0,
                operationCredit: 3,
                operationId: 42,
                retryAfterMilliseconds: 25,
                creditEpoch: 9,
                flags: FlowUpdateFlags.CreditValid | FlowUpdateFlags.RetryAfterValid | FlowUpdateFlags.BackgroundOnly);

            Assert.False(metadata.TryWrite(new byte[FlowUpdateMetadata.MetadataLength - 1], out var bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.Throws<ArgumentException>(() => metadata.Write(new byte[FlowUpdateMetadata.MetadataLength - 1]));
            Assert.False(FlowUpdateMetadata.TryParse(new byte[FlowUpdateMetadata.MetadataLength - 1], strict: true, out _, out var shortError));
            Assert.Equal(NnrpParseError.SourceTooShort, shortError);

            var payload = metadata.ToArray();
            Assert.True(FlowUpdateMetadata.TryParse(payload, strict: true, out var parsed, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(metadata, parsed);
            Assert.True(metadata.Equals((object)parsed));
            Assert.False(metadata.Equals("not-flow-update"));
            Assert.Equal(metadata.GetHashCode(), parsed.GetHashCode());

            payload[3] = 0x01;
            Assert.False(FlowUpdateMetadata.TryParse(payload, strict: true, out _, out var reservedError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, reservedError);

            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowUpdateMetadata(
                scopeKind: (FlowUpdateScopeKind)99,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                connectionCredit: 1,
                sessionCredit: 0,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.CreditValid));

            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Connection,
                updateReason: (FlowUpdateReason)99,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                connectionCredit: 1,
                sessionCredit: 0,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.CreditValid));

            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Connection,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: (FlowUpdateBackpressureLevel)99,
                connectionCredit: 1,
                sessionCredit: 0,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.CreditValid));

            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Connection,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                connectionCredit: 1,
                sessionCredit: 0,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: (FlowUpdateFlags)0x100));

            Assert.Throws<ArgumentException>(() => new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Connection,
                updateReason: FlowUpdateReason.Pause,
                backpressureLevel: FlowUpdateBackpressureLevel.Soft,
                connectionCredit: 0,
                sessionCredit: 0,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 12,
                creditEpoch: 0,
                flags: FlowUpdateFlags.None));

            Assert.Throws<ArgumentException>(() => new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Connection,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                connectionCredit: 1,
                sessionCredit: 1,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.CreditValid));

            Assert.Throws<ArgumentException>(() => new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Session,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                connectionCredit: 1,
                sessionCredit: 1,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.CreditValid));

            Assert.Throws<ArgumentException>(() => new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Operation,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                connectionCredit: 0,
                sessionCredit: 0,
                operationCredit: 1,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 0,
                flags: FlowUpdateFlags.CreditValid));
        }

        [Fact]
        public void FixedWidthStructsRoundTripAndRejectShortBuffers()
        {
            var resultBlock = new TensorResultBlock(sectionCount: 2, tileCount: 3, tileIndexMode: TileIndexMode.RawUInt16, tensorFlags: 0x5A, reserved0: 7, tileBaseId: 11, tileIndexBytes: 6);
            Assert.False(resultBlock.TryWrite(new byte[TensorResultBlock.BlockLength - 1], out var resultBytesWritten));
            Assert.Equal(0, resultBytesWritten);
            Assert.Throws<ArgumentException>(() => resultBlock.Write(new byte[TensorResultBlock.BlockLength - 1]));
            Assert.False(TensorResultBlock.TryParse(new byte[TensorResultBlock.BlockLength - 1], out _, out var resultShortError));
            Assert.Equal(NnrpParseError.SourceTooShort, resultShortError);
            Assert.True(TensorResultBlock.TryParse(resultBlock.ToArray(), out var parsedResultBlock, out var resultParseError));
            Assert.Equal(NnrpParseError.None, resultParseError);
            Assert.Equal(resultBlock, parsedResultBlock);
            Assert.True(resultBlock.Equals((object)parsedResultBlock));
            Assert.False(resultBlock.Equals("not-result-block"));
            Assert.Equal(resultBlock.GetHashCode(), parsedResultBlock.GetHashCode());

            var submitBlock = new TensorSubmitBlock(sourceWidth: 1920, sourceHeight: 1080, tileWidth: 64, tileHeight: 64, tileCount: 4, sectionCount: 2, tileIndexMode: TileIndexMode.DenseRange, tensorFlags: 0x03, reserved0: 9, tileBaseId: 21, cameraBytes: 12, tileIndexBytes: 0, reserved1: 17);
            Assert.False(submitBlock.TryWrite(new byte[TensorSubmitBlock.BlockLength - 1], out var submitBytesWritten));
            Assert.Equal(0, submitBytesWritten);
            Assert.Throws<ArgumentException>(() => submitBlock.Write(new byte[TensorSubmitBlock.BlockLength - 1]));
            Assert.False(TensorSubmitBlock.TryParse(new byte[TensorSubmitBlock.BlockLength - 1], out _, out var submitShortError));
            Assert.Equal(NnrpParseError.SourceTooShort, submitShortError);
            Assert.True(TensorSubmitBlock.TryParse(submitBlock.ToArray(), out var parsedSubmitBlock, out var submitParseError));
            Assert.Equal(NnrpParseError.None, submitParseError);
            Assert.Equal(submitBlock, parsedSubmitBlock);
            Assert.True(submitBlock.Equals((object)parsedSubmitBlock));
            Assert.False(submitBlock.Equals("not-submit-block"));
            Assert.Equal(submitBlock.GetHashCode(), parsedSubmitBlock.GetHashCode());

            var inlineHeader = new InlineObjectBlockHeader(CacheObjectKind.CameraBlock, objectFlags: 0, profileId: 3, reserved0: 0, objectBytes: 5, reserved1: 0);
            Assert.Equal(24u, inlineHeader.GetAlignedBlockLength());
            Assert.False(inlineHeader.TryWrite(new byte[InlineObjectBlockHeader.HeaderLength - 1], out var headerBytesWritten));
            Assert.Equal(0, headerBytesWritten);
            Assert.Throws<ArgumentException>(() => inlineHeader.Write(new byte[InlineObjectBlockHeader.HeaderLength - 1]));
            Assert.False(InlineObjectBlockHeader.TryParse(new byte[InlineObjectBlockHeader.HeaderLength - 1], strict: true, out _, out var headerShortError));
            Assert.Equal(NnrpParseError.SourceTooShort, headerShortError);
            Assert.True(InlineObjectBlockHeader.TryParse(inlineHeader.ToArray(), strict: true, out var parsedInlineHeader, out var headerParseError));
            Assert.Equal(NnrpParseError.None, headerParseError);
            Assert.Equal(inlineHeader, parsedInlineHeader);
            Assert.True(inlineHeader.Equals((object)parsedInlineHeader));
            Assert.False(inlineHeader.Equals("not-inline-header"));
            Assert.Equal(inlineHeader.GetHashCode(), parsedInlineHeader.GetHashCode());

            var nonStrictInlineHeader = new InlineObjectBlockHeader(CacheObjectKind.TileIndexBlock, objectFlags: 1, profileId: 4, reserved0: 1, objectBytes: 8, reserved1: 2);
            Assert.True(InlineObjectBlockHeader.TryParse(nonStrictInlineHeader.ToArray(), strict: false, out _, out var nonStrictInlineError));
            Assert.Equal(NnrpParseError.None, nonStrictInlineError);
            Assert.False(InlineObjectBlockHeader.TryParse(nonStrictInlineHeader.ToArray(), strict: true, out _, out var strictInlineError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, strictInlineError);

            var objectReference = new ObjectReferenceBlock(CacheObjectKind.TensorSectionTable, referenceFlags: 0, cacheNamespace: 7, cacheKeyHigh: 8, cacheKeyLow: 9);
            Assert.False(objectReference.TryWrite(new byte[ObjectReferenceBlock.BlockLength - 1], out var referenceBytesWritten));
            Assert.Equal(0, referenceBytesWritten);
            Assert.Throws<ArgumentException>(() => objectReference.Write(new byte[ObjectReferenceBlock.BlockLength - 1]));
            Assert.False(ObjectReferenceBlock.TryParse(new byte[ObjectReferenceBlock.BlockLength - 1], strict: true, out _, out var objectShortError));
            Assert.Equal(NnrpParseError.SourceTooShort, objectShortError);
            Assert.True(ObjectReferenceBlock.TryParse(objectReference.ToArray(), strict: true, out var parsedReference, out var objectParseError));
            Assert.Equal(NnrpParseError.None, objectParseError);
            Assert.Equal(objectReference, parsedReference);
            Assert.True(objectReference.Equals((object)parsedReference));
            Assert.False(objectReference.Equals("not-reference"));
            Assert.Equal(objectReference.GetHashCode(), parsedReference.GetHashCode());

            var nonStrictReference = new ObjectReferenceBlock(CacheObjectKind.TileIndexBlock, referenceFlags: 2, cacheNamespace: 1, cacheKeyHigh: 2, cacheKeyLow: 3);
            Assert.True(ObjectReferenceBlock.TryParse(nonStrictReference.ToArray(), strict: false, out _, out var nonStrictReferenceError));
            Assert.Equal(NnrpParseError.None, nonStrictReferenceError);
            Assert.False(ObjectReferenceBlock.TryParse(nonStrictReference.ToArray(), strict: true, out _, out var strictReferenceError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, strictReferenceError);

            var probeMetadata = new TransportProbeMetadata(probeId: 10, probePayloadBytes: 128, clientSendTimestampMicroseconds: 999);
            Assert.False(probeMetadata.TryWrite(new byte[TransportProbeMetadata.MetadataLength - 1], out var probeBytesWritten));
            Assert.Equal(0, probeBytesWritten);
            Assert.Throws<ArgumentException>(() => probeMetadata.Write(new byte[TransportProbeMetadata.MetadataLength - 1]));
            Assert.False(TransportProbeMetadata.TryParse(new byte[TransportProbeMetadata.MetadataLength - 1], out _, out var probeShortError));
            Assert.Equal(NnrpParseError.SourceTooShort, probeShortError);
            Assert.True(TransportProbeMetadata.TryParse(probeMetadata.ToArray(), out var parsedProbeMetadata, out var probeParseError));
            Assert.Equal(NnrpParseError.None, probeParseError);
            Assert.Equal(probeMetadata, parsedProbeMetadata);
            Assert.True(probeMetadata.Equals((object)parsedProbeMetadata));
            Assert.False(probeMetadata.Equals("not-probe"));
            Assert.Equal(probeMetadata.GetHashCode(), parsedProbeMetadata.GetHashCode());

            var probeAckMetadata = new TransportProbeAckMetadata(probeId: 10, reserved: 0, serverReceiveTimestampMicroseconds: 1001);
            Assert.False(probeAckMetadata.TryWrite(new byte[TransportProbeAckMetadata.MetadataLength - 1], out var probeAckBytesWritten));
            Assert.Equal(0, probeAckBytesWritten);
            Assert.Throws<ArgumentException>(() => probeAckMetadata.Write(new byte[TransportProbeAckMetadata.MetadataLength - 1]));
            Assert.False(TransportProbeAckMetadata.TryParse(new byte[TransportProbeAckMetadata.MetadataLength - 1], out _, out var probeAckShortError));
            Assert.Equal(NnrpParseError.SourceTooShort, probeAckShortError);
            Assert.True(TransportProbeAckMetadata.TryParse(probeAckMetadata.ToArray(), out var parsedProbeAckMetadata, out var probeAckParseError));
            Assert.Equal(NnrpParseError.None, probeAckParseError);
            Assert.Equal(probeAckMetadata, parsedProbeAckMetadata);
            Assert.True(probeAckMetadata.Equals((object)parsedProbeAckMetadata));
            Assert.False(probeAckMetadata.Equals("not-probe-ack"));
            Assert.Equal(probeAckMetadata.GetHashCode(), parsedProbeAckMetadata.GetHashCode());
        }

        [Fact]
        public void TypedPayloadValueObjectsCoverEqualityAndValidationBranches()
        {
            var descriptor = new TypedPayloadDescriptor(
                payloadKind: PayloadKind.ToolDelta,
                descriptorFlags: 0,
                profileId: 7,
                payloadOffset: 0,
                payloadLength: 3,
                reserved: 0);
            var frame = new TypedPayloadFrameView(descriptor, new byte[] { 1, 2, 3 });
            var sameFrame = new TypedPayloadFrameView(descriptor, new byte[] { 1, 2, 3 });
            var differentFrame = new TypedPayloadFrameView(descriptor, new byte[] { 1, 2, 4 });

            Assert.True(frame.Equals(sameFrame));
            Assert.True(frame.Equals((object)sameFrame));
            Assert.False(frame.Equals(differentFrame));
            Assert.False(frame.Equals("not-frame"));
            Assert.Equal(frame.GetHashCode(), sameFrame.GetHashCode());
            Assert.Equal(PayloadKind.ToolDelta, frame.PayloadKind);
            Assert.Equal((ushort)7, frame.ProfileId);

            var profileFrames = new TypedPayloadProfileFrames(PayloadKind.ToolDelta, 7, new[] { frame, sameFrame });
            Assert.False(profileFrames.IsEmpty);
            Assert.Equal(2, profileFrames.FrameCount);
            Assert.Equal(6, profileFrames.PayloadBytes);
            Assert.True(profileFrames.Equals((object)new TypedPayloadProfileFrames(PayloadKind.ToolDelta, 7, new[] { frame, sameFrame })));
            Assert.False(profileFrames.Equals("not-profile-frames"));

            var emptyFrames = new TypedPayloadProfileFrames(PayloadKind.StructuredEvent, 3, ReadOnlyMemory<TypedPayloadFrameView>.Empty);
            Assert.True(emptyFrames.IsEmpty);
            Assert.Equal(0, emptyFrames.PayloadBytes);

            Assert.Throws<ArgumentOutOfRangeException>(() => new TypedPayloadProfileFrames((PayloadKind)0, 1, new[] { frame }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TypedPayloadProfileFrames(PayloadKind.ToolDelta | PayloadKind.StructuredEvent, 1, new[] { frame }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TypedPayloadProfileFrames((PayloadKind)0x80, 1, new[] { frame }));
            Assert.Throws<ArgumentException>(() => new TypedPayloadProfileFrames(PayloadKind.ToolDelta, 7, new[]
            {
                frame,
                new TypedPayloadFrameView(
                    new TypedPayloadDescriptor(PayloadKind.StructuredEvent, 0, 7, 3, 1, 0),
                    new byte[] { 9 })
            }));
            Assert.Throws<ArgumentException>(() => new TypedPayloadProfileFrames(PayloadKind.ToolDelta, 7, new[]
            {
                frame,
                new TypedPayloadFrameView(
                    new TypedPayloadDescriptor(PayloadKind.ToolDelta, 0, 8, 3, 1, 0),
                    new byte[] { 9 })
            }));

            var coverage = new TypedPayloadProfileCoverage(PayloadKind.ToolDelta, 7, frameCount: 2, payloadBytes: 6);
            var sameCoverage = new TypedPayloadProfileCoverage(PayloadKind.ToolDelta, 7, frameCount: 2, payloadBytes: 6);
            Assert.True(coverage.Equals(sameCoverage));
            Assert.True(coverage.Equals((object)sameCoverage));
            Assert.False(coverage.Equals("not-coverage"));
            Assert.Equal(coverage.GetHashCode(), sameCoverage.GetHashCode());

            Assert.True(PayloadKindValidator.IsDefinedBitmap(PayloadKind.Tensor | PayloadKind.ToolDelta));
            Assert.False(PayloadKindValidator.IsDefinedBitmap((PayloadKind)0x80));
            Assert.True(PayloadKindValidator.IsSingleDefinedKind(PayloadKind.Tensor));
            Assert.False(PayloadKindValidator.IsSingleDefinedKind((PayloadKind)0));
            Assert.False(PayloadKindValidator.IsSingleDefinedKind(PayloadKind.Tensor | PayloadKind.ToolDelta));
            Assert.False(PayloadKindValidator.IsSingleDefinedKind((PayloadKind)0x80));
        }

        [Fact]
        public void SessionPatchMessagesValidateBodyContracts()
        {
            var profilePatchBlock = new TensorProfilePatchBlock(320, 180, 1920, 1080);
            Assert.False(profilePatchBlock.TryWrite(new byte[TensorProfilePatchBlock.BlockLength - 1], out var patchBytesWritten));
            Assert.Equal(0, patchBytesWritten);
            Assert.Throws<ArgumentException>(() => profilePatchBlock.Write(new byte[TensorProfilePatchBlock.BlockLength - 1]));
            Assert.False(TensorProfilePatchBlock.TryParse(new byte[TensorProfilePatchBlock.BlockLength - 1], out _, out var patchShortError));
            Assert.Equal(NnrpParseError.SourceTooShort, patchShortError);
            Assert.True(TensorProfilePatchBlock.TryParse(profilePatchBlock.ToArray(), out var parsedPatchBlock, out var patchParseError));
            Assert.Equal(NnrpParseError.None, patchParseError);
            Assert.Equal(profilePatchBlock, parsedPatchBlock);
            Assert.True(profilePatchBlock.Equals((object)parsedPatchBlock));
            Assert.False(profilePatchBlock.Equals("not-patch-block"));
            Assert.Equal(profilePatchBlock.GetHashCode(), parsedPatchBlock.GetHashCode());

            var metadataWithoutBody = new SessionPatchMetadata(
                profileId: 0,
                patchMask: SessionPatchField.TargetCadence,
                targetCadenceX100: 6000,
                qualityTier: 2,
                degradePolicy: 1,
                activeLaneMask: 0x1,
                preferredCodecBitmap: 0x3,
                preferredCompressionBitmap: 0x1,
                profilePatchBytes: 0);
            var headerWithoutBody = new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.SessionPatch, HeaderFlags.None, SessionPatchMetadata.MetadataLength, 0, 11, 0, 0, 0, 1);
            var messageWithoutBody = new SessionPatchMessage(headerWithoutBody, metadataWithoutBody);
            Assert.True(SessionPatchMessage.TryParse(messageWithoutBody.ToArray(), out var parsedWithoutBody, out var messageWithoutBodyError));
            Assert.Equal(NnrpParseError.None, messageWithoutBodyError);
            Assert.False(parsedWithoutBody.ProfilePatchBlock.HasValue);

            var invalidBodyFramed = new NnrpFramedMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.SessionPatch, HeaderFlags.None, SessionPatchMetadata.MetadataLength, 1, 11, 0, 0, 0, 1),
                metadataWithoutBody.ToArray(),
                new byte[] { 0xFF });
            Assert.False(SessionPatchMessage.TryParse(invalidBodyFramed.ToArray(), out _, out var invalidBodyError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, invalidBodyError);

            var metadataWithBody = new SessionPatchMetadata(
                profileId: 0,
                patchMask: SessionPatchField.ProfilePatch,
                targetCadenceX100: 6000,
                qualityTier: 2,
                degradePolicy: 1,
                activeLaneMask: 0x1,
                preferredCodecBitmap: 0x3,
                preferredCompressionBitmap: 0x1,
                profilePatchBytes: TensorProfilePatchBlock.BlockLength);
            var headerWithBody = new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.SessionPatch, HeaderFlags.None, SessionPatchMetadata.MetadataLength, TensorProfilePatchBlock.BlockLength, 11, 0, 0, 0, 1);
            Assert.Throws<ArgumentException>(() => new SessionPatchMessage(headerWithBody, metadataWithBody));
            Assert.Throws<ArgumentException>(() => new SessionPatchMessage(headerWithoutBody, metadataWithBody, profilePatchBlock));

            var validMessageWithBody = new SessionPatchMessage(headerWithBody, metadataWithBody, profilePatchBlock);
            Assert.True(SessionPatchMessage.TryParse(validMessageWithBody.ToArray(), out var parsedMessageWithBody, out var messageWithBodyError));
            Assert.Equal(NnrpParseError.None, messageWithBodyError);
            Assert.True(parsedMessageWithBody.ProfilePatchBlock.HasValue);
            Assert.Equal(profilePatchBlock, parsedMessageWithBody.ProfilePatchBlock.Value);

            var profilePatchAckBlock = new TensorProfilePatchAckBlock(320, 180, 1920, 1080);
            Assert.False(profilePatchAckBlock.TryWrite(new byte[TensorProfilePatchAckBlock.BlockLength - 1], out var ackBlockBytesWritten));
            Assert.Equal(0, ackBlockBytesWritten);
            Assert.Throws<ArgumentException>(() => profilePatchAckBlock.Write(new byte[TensorProfilePatchAckBlock.BlockLength - 1]));
            Assert.False(TensorProfilePatchAckBlock.TryParse(new byte[TensorProfilePatchAckBlock.BlockLength - 1], out _, out var ackBlockShortError));
            Assert.Equal(NnrpParseError.SourceTooShort, ackBlockShortError);
            Assert.True(TensorProfilePatchAckBlock.TryParse(profilePatchAckBlock.ToArray(), out var parsedPatchAckBlock, out var ackBlockParseError));
            Assert.Equal(NnrpParseError.None, ackBlockParseError);
            Assert.Equal(profilePatchAckBlock, parsedPatchAckBlock);
            Assert.True(profilePatchAckBlock.Equals((object)parsedPatchAckBlock));
            Assert.False(profilePatchAckBlock.Equals("not-patch-ack-block"));
            Assert.Equal(profilePatchAckBlock.GetHashCode(), parsedPatchAckBlock.GetHashCode());

            var ackMetadataWithoutBody = new SessionPatchAckMetadata(
                ackStatus: SessionPatchAckStatus.Accepted,
                rejectReason: SessionPatchRejectReason.None,
                appliedPatchMask: SessionPatchField.TargetCadence,
                rejectedPatchMask: SessionPatchField.None,
                retryAfterMilliseconds: 0,
                effectiveProfileId: 0,
                effectiveTargetCadenceX100: 6000,
                effectiveQualityTier: 2,
                effectiveDegradePolicy: 1,
                effectiveLaneMask: 0x1,
                preferredCodecBitmap: 0x3,
                preferredCompressionBitmap: 0x1,
                profilePatchAckBytes: 0);
            var ackHeaderWithoutBody = new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.SessionPatchAck, HeaderFlags.None, SessionPatchAckMetadata.MetadataLength, 0, 11, 0, 0, 0, 2);
            var ackMessageWithoutBody = new SessionPatchAckMessage(ackHeaderWithoutBody, ackMetadataWithoutBody);
            Assert.True(SessionPatchAckMessage.TryParse(ackMessageWithoutBody.ToArray(), out var parsedAckWithoutBody, out var ackWithoutBodyError));
            Assert.Equal(NnrpParseError.None, ackWithoutBodyError);
            Assert.False(parsedAckWithoutBody.ProfilePatchAckBlock.HasValue);

            var invalidAckBodyFramed = new NnrpFramedMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.SessionPatchAck, HeaderFlags.None, SessionPatchAckMetadata.MetadataLength, 1, 11, 0, 0, 0, 2),
                ackMetadataWithoutBody.ToArray(),
                new byte[] { 0xFF });
            Assert.False(SessionPatchAckMessage.TryParse(invalidAckBodyFramed.ToArray(), out _, out var invalidAckBodyError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, invalidAckBodyError);

            var ackMetadataWithBody = new SessionPatchAckMetadata(
                ackStatus: SessionPatchAckStatus.PartiallyApplied,
                rejectReason: SessionPatchRejectReason.UnsupportedValue,
                appliedPatchMask: SessionPatchField.ProfilePatch,
                rejectedPatchMask: SessionPatchField.PreferredCompression,
                retryAfterMilliseconds: 25,
                effectiveProfileId: 1,
                effectiveTargetCadenceX100: 5500,
                effectiveQualityTier: 3,
                effectiveDegradePolicy: 1,
                effectiveLaneMask: 0x3,
                preferredCodecBitmap: 0x3,
                preferredCompressionBitmap: 0x1,
                profilePatchAckBytes: TensorProfilePatchAckBlock.BlockLength);
            var ackHeaderWithBody = new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.SessionPatchAck, HeaderFlags.None, SessionPatchAckMetadata.MetadataLength, TensorProfilePatchAckBlock.BlockLength, 11, 0, 0, 0, 2);
            Assert.Throws<ArgumentException>(() => new SessionPatchAckMessage(ackHeaderWithBody, ackMetadataWithBody));
            Assert.Throws<ArgumentException>(() => new SessionPatchAckMessage(ackHeaderWithoutBody, ackMetadataWithBody, profilePatchAckBlock));

            var validAckMessageWithBody = new SessionPatchAckMessage(ackHeaderWithBody, ackMetadataWithBody, profilePatchAckBlock);
            Assert.True(SessionPatchAckMessage.TryParse(validAckMessageWithBody.ToArray(), out var parsedAckWithBody, out var ackWithBodyError));
            Assert.Equal(NnrpParseError.None, ackWithBodyError);
            Assert.True(parsedAckWithBody.ProfilePatchAckBlock.HasValue);
            Assert.Equal(profilePatchAckBlock, parsedAckWithBody.ProfilePatchAckBlock.Value);
        }

        [Fact]
        public void AdditionalMetadataAndProbeMessagesCoverRemainingBranches()
        {
            var resultHint = new ResultHintMetadata(
                appliedBudgetPolicy: ResultHintBudgetPolicy.Drop,
                congestionState: ResultHintCongestionState.Saturated,
                reason: ResultHintReason.BudgetExceeded,
                retryAfterMilliseconds: 44);
            Assert.False(resultHint.TryWrite(new byte[ResultHintMetadata.MetadataLength - 1], out var resultHintBytesWritten));
            Assert.Equal(0, resultHintBytesWritten);
            Assert.Throws<ArgumentException>(() => resultHint.Write(new byte[ResultHintMetadata.MetadataLength - 1]));
            Assert.False(ResultHintMetadata.TryParse(new byte[ResultHintMetadata.MetadataLength - 1], strict: true, out _, out var resultHintShortError));
            Assert.Equal(NnrpParseError.SourceTooShort, resultHintShortError);
            Assert.True(ResultHintMetadata.TryParse(resultHint.ToArray(), strict: true, out var parsedResultHint, out var resultHintParseError));
            Assert.Equal(NnrpParseError.None, resultHintParseError);
            Assert.Equal(resultHint, parsedResultHint);
            Assert.True(resultHint.Equals((object)parsedResultHint));
            Assert.False(resultHint.Equals("not-result-hint"));
            Assert.Equal(resultHint.GetHashCode(), parsedResultHint.GetHashCode());
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResultHintMetadata((ResultHintBudgetPolicy)99, ResultHintCongestionState.Steady, ResultHintReason.None, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResultHintMetadata(ResultHintBudgetPolicy.Full, (ResultHintCongestionState)99, ResultHintReason.None, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ResultHintMetadata(ResultHintBudgetPolicy.Full, ResultHintCongestionState.Steady, (ResultHintReason)99, 0));

            var migrateMetadata = new SessionMigrateMetadata(
                oldTransportId: TransportId.Quic,
                newTransportId: TransportId.Tcp,
                lastResultFrameId: 88,
                clientMigrateTimestampMicroseconds: 12345);
            Assert.False(migrateMetadata.TryWrite(new byte[SessionMigrateMetadata.MetadataLength - 1], out var migrateBytesWritten));
            Assert.Equal(0, migrateBytesWritten);
            Assert.Throws<ArgumentException>(() => migrateMetadata.Write(new byte[SessionMigrateMetadata.MetadataLength - 1]));
            Assert.False(SessionMigrateMetadata.TryParse(new byte[SessionMigrateMetadata.MetadataLength - 1], strict: true, out _, out var migrateShortError));
            Assert.Equal(NnrpParseError.SourceTooShort, migrateShortError);
            Assert.True(SessionMigrateMetadata.TryParse(migrateMetadata.ToArray(), strict: true, out var parsedMigrateMetadata, out var migrateParseError));
            Assert.Equal(NnrpParseError.None, migrateParseError);
            Assert.Equal(migrateMetadata, parsedMigrateMetadata);
            Assert.True(migrateMetadata.Equals((object)parsedMigrateMetadata));
            Assert.False(migrateMetadata.Equals("not-migrate"));
            Assert.Equal(migrateMetadata.GetHashCode(), parsedMigrateMetadata.GetHashCode());
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionMigrateMetadata(TransportId.Unspecified, TransportId.Tcp, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionMigrateMetadata(TransportId.Quic, TransportId.Unspecified, 0, 0));

            var invalidMigrateBytes = migrateMetadata.ToArray();
            Array.Clear(invalidMigrateBytes, 0, sizeof(uint));
            Assert.False(SessionMigrateMetadata.TryParse(invalidMigrateBytes, strict: true, out _, out var invalidMigrateError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, invalidMigrateError);

            var migrateAckMetadata = new SessionMigrateAckMetadata(
                acceptCode: 1,
                resumeFromFrameId: 77,
                graceWindowMilliseconds: 500,
                serverMigrateTimestampMicroseconds: 54321);
            Assert.False(migrateAckMetadata.TryWrite(new byte[SessionMigrateAckMetadata.MetadataLength - 1], out var migrateAckBytesWritten));
            Assert.Equal(0, migrateAckBytesWritten);
            Assert.Throws<ArgumentException>(() => migrateAckMetadata.Write(new byte[SessionMigrateAckMetadata.MetadataLength - 1]));
            Assert.False(SessionMigrateAckMetadata.TryParse(new byte[SessionMigrateAckMetadata.MetadataLength - 1], strict: true, out _, out var migrateAckShortError));
            Assert.Equal(NnrpParseError.SourceTooShort, migrateAckShortError);
            Assert.True(SessionMigrateAckMetadata.TryParse(migrateAckMetadata.ToArray(), strict: true, out var parsedMigrateAckMetadata, out var migrateAckParseError));
            Assert.Equal(NnrpParseError.None, migrateAckParseError);
            Assert.Equal(migrateAckMetadata, parsedMigrateAckMetadata);
            Assert.True(migrateAckMetadata.Equals((object)parsedMigrateAckMetadata));
            Assert.False(migrateAckMetadata.Equals("not-migrate-ack"));
            Assert.Equal(migrateAckMetadata.GetHashCode(), parsedMigrateAckMetadata.GetHashCode());

            var extensionDescriptor = new ExtensionFrameDescriptor(extensionKind: 9, extensionFlags: 0x0001, profileId: 4, reserved0: 0, payloadOffset: 3, payloadLength: 12);
            Assert.False(extensionDescriptor.TryWrite(new byte[ExtensionFrameDescriptor.DescriptorLength - 1], out var extensionBytesWritten));
            Assert.Equal(0, extensionBytesWritten);
            Assert.Throws<ArgumentException>(() => extensionDescriptor.Write(new byte[ExtensionFrameDescriptor.DescriptorLength - 1]));
            Assert.False(ExtensionFrameDescriptor.TryParse(new byte[ExtensionFrameDescriptor.DescriptorLength - 1], strict: true, out _, out var extensionShortError));
            Assert.Equal(NnrpParseError.SourceTooShort, extensionShortError);
            Assert.True(ExtensionFrameDescriptor.TryParse(extensionDescriptor.ToArray(), strict: true, out var parsedExtensionDescriptor, out var extensionParseError));
            Assert.Equal(NnrpParseError.None, extensionParseError);
            Assert.Equal(extensionDescriptor, parsedExtensionDescriptor);
            Assert.True(extensionDescriptor.Equals((object)parsedExtensionDescriptor));
            Assert.False(extensionDescriptor.Equals("not-extension"));
            Assert.Equal(extensionDescriptor.GetHashCode(), parsedExtensionDescriptor.GetHashCode());

            var invalidStrictExtensionDescriptor = new ExtensionFrameDescriptor(extensionKind: 9, extensionFlags: 0x0002, profileId: 4, reserved0: 1, payloadOffset: 3, payloadLength: 12);
            Assert.True(ExtensionFrameDescriptor.TryParse(invalidStrictExtensionDescriptor.ToArray(), strict: false, out _, out var extensionLooseError));
            Assert.Equal(NnrpParseError.None, extensionLooseError);
            Assert.False(ExtensionFrameDescriptor.TryParse(invalidStrictExtensionDescriptor.ToArray(), strict: true, out _, out var extensionStrictError));
            Assert.Equal(NnrpParseError.NonZeroReservedField, extensionStrictError);

            var probePayload = new byte[] { 1, 2, 3, 4 };
            var probeMetadata = new TransportProbeMetadata(probeId: 5, probePayloadBytes: (uint)probePayload.Length, clientSendTimestampMicroseconds: 555);
            var probeHeader = new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.TransportProbe, HeaderFlags.None, TransportProbeMetadata.MetadataLength, (uint)probePayload.Length, 9, 0, 0, 0, 10);
            var probeMessage = new TransportProbeMessage(probeHeader, probeMetadata, probePayload);
            Assert.True(TransportProbeMessage.TryParse(probeMessage.ToArray(), out var parsedProbeMessage, out var probeMessageError));
            Assert.Equal(NnrpParseError.None, probeMessageError);
            Assert.Equal(probePayload, parsedProbeMessage.Payload.ToArray());

            Assert.Throws<ArgumentException>(() => new TransportProbeMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.Ping, HeaderFlags.None, TransportProbeMetadata.MetadataLength, (uint)probePayload.Length, 9, 0, 0, 0, 10),
                probeMetadata,
                probePayload));
            Assert.Throws<ArgumentException>(() => new TransportProbeMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.TransportProbe, HeaderFlags.None, 0, (uint)probePayload.Length, 9, 0, 0, 0, 10),
                probeMetadata,
                probePayload));
            Assert.Throws<ArgumentException>(() => new TransportProbeMessage(
                probeHeader,
                new TransportProbeMetadata(probeId: 5, probePayloadBytes: 99, clientSendTimestampMicroseconds: 555),
                probePayload));

            var invalidProbeFramed = new NnrpFramedMessage(
                probeHeader,
                new TransportProbeMetadata(probeId: 5, probePayloadBytes: 9, clientSendTimestampMicroseconds: 555).ToArray(),
                probePayload);
            Assert.False(TransportProbeMessage.TryParse(invalidProbeFramed.ToArray(), out _, out var invalidProbeError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, invalidProbeError);

            var probeAckMetadata = new TransportProbeAckMetadata(probeId: 5, reserved: 0, serverReceiveTimestampMicroseconds: 777);
            var probeAckHeader = new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.TransportProbeAck, HeaderFlags.None, TransportProbeAckMetadata.MetadataLength, 0, 9, 0, 0, 0, 11);
            var probeAckMessage = new TransportProbeAckMessage(probeAckHeader, probeAckMetadata);
            Assert.True(TransportProbeAckMessage.TryParse(probeAckMessage.ToArray(), out var parsedProbeAckMessage, out var probeAckMessageError));
            Assert.Equal(NnrpParseError.None, probeAckMessageError);
            Assert.Equal(probeAckMetadata, parsedProbeAckMessage.Metadata);

            Assert.Throws<ArgumentException>(() => new TransportProbeAckMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.TransportProbe, HeaderFlags.None, TransportProbeAckMetadata.MetadataLength, 0, 9, 0, 0, 0, 11),
                probeAckMetadata));

            var invalidProbeAckFramed = new NnrpFramedMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.TransportProbeAck, HeaderFlags.None, TransportProbeAckMetadata.MetadataLength, 1, 9, 0, 0, 0, 11),
                probeAckMetadata.ToArray(),
                new byte[] { 0x01 });
            Assert.False(TransportProbeAckMessage.TryParse(invalidProbeAckFramed.ToArray(), out _, out var invalidProbeAckError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, invalidProbeAckError);

            var sessionScopedMetadata = new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Session,
                updateReason: FlowUpdateReason.Resume,
                backpressureLevel: FlowUpdateBackpressureLevel.None,
                connectionCredit: 0,
                sessionCredit: 4,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: 10,
                flags: FlowUpdateFlags.CreditValid);
            var validFlowHeader = new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.FlowUpdate, HeaderFlags.None, FlowUpdateMetadata.MetadataLength, 0, 9, 0, 0, 0, 12);
            var flowMessage = new FlowUpdateMessage(validFlowHeader, sessionScopedMetadata);
            Assert.Equal(flowMessage.ToFramedMessage().Header, flowMessage.Header);

            var invalidFlowFramed = new NnrpFramedMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.FlowUpdate, HeaderFlags.None, FlowUpdateMetadata.MetadataLength, 1, 9, 0, 0, 0, 12),
                sessionScopedMetadata.ToArray(),
                new byte[] { 0x01 });
            Assert.False(FlowUpdateMessage.TryParse(invalidFlowFramed.ToArray(), out _, out var invalidFlowBodyError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, invalidFlowBodyError);

            var zeroSessionFlowFramed = new NnrpFramedMessage(
                new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.FlowUpdate, HeaderFlags.None, FlowUpdateMetadata.MetadataLength, 0, 0, 0, 0, 0, 12),
                sessionScopedMetadata.ToArray(),
                Array.Empty<byte>());
            Assert.False(FlowUpdateMessage.TryParse(zeroSessionFlowFramed.ToArray(), out _, out var invalidFlowSessionError));
            Assert.Equal(NnrpParseError.InvalidMessageLayout, invalidFlowSessionError);
        }

        [Fact]
        public void ServerHelloAckMetadataCoversDerivedMembersAndOverflowPaths()
        {
            var metadata = new ServerHelloAckMetadata(
                selectedVersionMajor: 1,
                selectedWireFormat: 0,
                authStatus: 0,
                reserved0: 0,
                sessionId: 77,
                acceptedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                acceptedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                acceptedCodecBitmap: 0x3,
                acceptedCompressionBitmap: 0x3,
                acceptedDTypeBitmap: 0x6,
                acceptedLayoutBitmap: 0x1,
                cacheDigestBitmap: 0,
                cacheObjectBitmap: 0,
                maxCacheEntries: 64,
                maxCacheBytes: 1024,
                maxLaneCount: 2,
                maxConcurrentFrames: 3,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 16,
                qualityTier: 1,
                degradePolicy: 0,
                maxBodyBytes: 4096,
                tokenTtlMilliseconds: 1000,
                retryAfterMilliseconds: 0,
                controlExtensionBytes: 0,
                serverFlags: 0);

            Assert.Equal(0u, metadata.CacheEnabled);
            var metadataWithExtensions = metadata.WithControlExtensionBytes(12);
            Assert.Equal(12u, metadataWithExtensions.ControlExtensionBytes);
            Assert.NotEqual(metadata, metadataWithExtensions);
            Assert.False(metadata.Equals("not-server-ack-metadata"));
            Assert.NotEqual(metadata.GetHashCode(), metadataWithExtensions.GetHashCode());

            var cacheEnabledMetadata = new ServerHelloAckMetadata(
                selectedVersionMajor: 1,
                selectedWireFormat: 0,
                authStatus: 0,
                reserved0: 0,
                sessionId: 77,
                acceptedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                acceptedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                acceptedCodecBitmap: 0x3,
                acceptedCompressionBitmap: 0x3,
                acceptedDTypeBitmap: 0x6,
                acceptedLayoutBitmap: 0x1,
                cacheDigestBitmap: 1,
                cacheObjectBitmap: 0,
                maxCacheEntries: 64,
                maxCacheBytes: 1024,
                maxLaneCount: 2,
                maxConcurrentFrames: 3,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 16,
                qualityTier: 1,
                degradePolicy: 0,
                maxBodyBytes: 4096,
                tokenTtlMilliseconds: 1000,
                retryAfterMilliseconds: 0,
                controlExtensionBytes: 0,
                serverFlags: 0);
            Assert.Equal(1u, cacheEnabledMetadata.CacheEnabled);

            Assert.Throws<OverflowException>(() => new ServerHelloAckMetadata(
                selectedVersionMajor: 256,
                selectedWireFormat: 0,
                authStatus: 0,
                reserved0: 0,
                sessionId: 1,
                acceptedProfileBitmap: 0,
                acceptedPayloadKindBitmap: 0,
                acceptedCodecBitmap: 0,
                acceptedCompressionBitmap: 0,
                acceptedDTypeBitmap: 0,
                acceptedLayoutBitmap: 0,
                cacheDigestBitmap: 0,
                cacheObjectBitmap: 0,
                maxCacheEntries: 0,
                maxCacheBytes: 0,
                maxLaneCount: 0,
                maxConcurrentFrames: 0,
                targetCadenceX100: 0,
                latencyBudgetMilliseconds: 0,
                qualityTier: 0,
                degradePolicy: 0,
                maxBodyBytes: 0,
                tokenTtlMilliseconds: 0,
                retryAfterMilliseconds: 0,
                controlExtensionBytes: 0,
                serverFlags: 0).ToArray());

            Assert.Throws<OverflowException>(() => new ServerHelloAckMetadata(
                selectedVersionMajor: 1,
                selectedWireFormat: 0,
                authStatus: 0,
                reserved0: 0,
                sessionId: 1,
                acceptedProfileBitmap: 0,
                acceptedPayloadKindBitmap: 0,
                acceptedCodecBitmap: 0,
                acceptedCompressionBitmap: 0,
                acceptedDTypeBitmap: 0,
                acceptedLayoutBitmap: 0,
                cacheDigestBitmap: 0,
                cacheObjectBitmap: 0,
                maxCacheEntries: 0,
                maxCacheBytes: 0,
                maxLaneCount: 70000,
                maxConcurrentFrames: 0,
                targetCadenceX100: 0,
                latencyBudgetMilliseconds: 0,
                qualityTier: 0,
                degradePolicy: 0,
                maxBodyBytes: 0,
                tokenTtlMilliseconds: 0,
                retryAfterMilliseconds: 0,
                controlExtensionBytes: 0,
                serverFlags: 0).ToArray());
        }

        [Fact]
        public void ClientHelloExtensionLookupsReturnFalseWhenMissing()
        {
            var metadata = new ClientHelloMetadata(
                minVersionMajor: 1,
                maxVersionMajor: 1,
                supportedWireFormatBitmap: 1,
                supportedProfileBitmap: ControlMetadataBitmaps.TensorProfileBitmap,
                supportedPayloadKindBitmap: ControlMetadataBitmaps.TensorPayloadKindBitmap,
                supportedCodecBitmap: 0x3,
                supportedCompressionBitmap: 0x1,
                supportedDTypeBitmap: 0x6,
                supportedLayoutBitmap: 0x1,
                cacheDigestBitmap: 0,
                cacheObjectBitmap: 0,
                cacheNamespaceCount: 0,
                maxLaneCount: 1,
                maxCacheEntries: 0,
                maxCacheBytes: 0,
                targetCadenceX100: 6000,
                latencyBudgetMilliseconds: 16,
                qualityTier: 1,
                degradePolicy: 0,
                requestedSessionId: 0,
                authBytes: 0,
                controlExtensionBytes: 0);
            var header = new NnrpHeader(1, NnrpHeader.CurrentWireFormat, MessageType.ClientHello, HeaderFlags.None, ClientHelloMetadata.MetadataLength, 0, 0, 0, 0, 0, 13);
            var message = new ClientHelloMessage(header, metadata, Array.Empty<byte>());

            Assert.False(message.TryGetClientTransportPolicyExtension(out _, out var transportPolicyError));
            Assert.Equal(NnrpParseError.None, transportPolicyError);
            Assert.False(message.TryGetClientLossToleranceExtension(out _, out var lossToleranceError));
            Assert.Equal(NnrpParseError.None, lossToleranceError);
            Assert.False(message.TryGetClientPayloadCapabilitiesExtension(out _, out var payloadCapabilitiesError));
            Assert.Equal(NnrpParseError.None, payloadCapabilitiesError);
        }

        [Fact]
        public void BodyRegionPreludeRejectsShortBuffersAndSupportsEquality()
        {
            var prelude = new BodyRegionPrelude(
                inlineObjectBytes: 8,
                objectReferenceBytes: 16,
                typedPayloadDescriptorBytes: 32,
                typedPayloadFrameBytes: 64,
                extensionDescriptorBytes: 12,
                extensionPayloadBytes: 20,
                bodyFlags: 1,
                reserved: 0);

            Assert.False(prelude.TryWrite(new byte[BodyRegionPrelude.PreludeLength - 1], out var bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.Throws<ArgumentException>(() => prelude.Write(new byte[BodyRegionPrelude.PreludeLength - 1]));
            Assert.False(BodyRegionPrelude.TryParse(new byte[BodyRegionPrelude.PreludeLength - 1], strict: true, out _, out var shortError));
            Assert.Equal(NnrpParseError.SourceTooShort, shortError);

            Assert.True(BodyRegionPrelude.TryParse(prelude.ToArray(), strict: true, out var parsedPrelude, out var parseError));
            Assert.Equal(NnrpParseError.None, parseError);
            Assert.Equal(prelude, parsedPrelude);
            Assert.True(prelude.Equals((object)parsedPrelude));
            Assert.False(prelude.Equals("not-prelude"));
            Assert.Equal(prelude.GetHashCode(), parsedPrelude.GetHashCode());
            Assert.Equal((uint)(BodyRegionPrelude.PreludeLength + 8 + 16 + 32 + 64 + 12 + 20), prelude.GetTotalBodyBytes());
        }
    }
}
