using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class TokenProfileTests
    {
        [Fact]
        public void DescriptorFlagsExposeFrozenPublicBits()
        {
            Assert.Equal((ushort)0x000F, TypedPayloadDescriptor.KnownDescriptorFlagMask);

            var flags = TypedPayloadDescriptorFlags.Partial | TypedPayloadDescriptorFlags.ProfileHintPresent;
            var descriptor = new TypedPayloadDescriptor(
                PayloadKind.TokenChunk,
                TypedPayloadProfileId.Token,
                flags,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 0,
                payloadLength: 4);

            Assert.Equal(flags, descriptor.Flags);
            Assert.Equal((ushort)0x000A, descriptor.DescriptorFlags);
        }

        [Fact]
        public void TokenDescriptorCreatesStandardDeltaBinding()
        {
            var token = TokenPayloadDescriptor.CreateDelta(
                payloadOffset: 4,
                payloadLength: 6,
                TypedPayloadDescriptorFlags.Partial | TypedPayloadDescriptorFlags.ProfileHintPresent);

            Assert.Equal(TypedPayloadProfileId.Token, token.Profile);
            Assert.Equal(PayloadKind.TokenChunk, token.PayloadKind);
            Assert.Equal(TokenPayloadDescriptor.DeltaSchemaId, token.SchemaId);
            Assert.Equal(TokenPayloadDescriptor.DeltaSchemaVersion, token.SchemaVersion);
            Assert.Equal(TokenPayloadDescriptor.DeltaStreamSemantics, token.StreamSemantics);
            Assert.Equal(4u, token.PayloadOffset);
            Assert.Equal(6u, token.PayloadLength);
            Assert.True(token.IsStandardDeltaSchema);
            Assert.True(token.IsPartial);
            Assert.False(token.IsTerminal);
            Assert.False(token.HasSchemaOverride);
            Assert.True(token.HasProfileHint);
            Assert.Equal(token, new TokenPayloadDescriptor(token.Descriptor));
            Assert.Equal(token.GetHashCode(), new TokenPayloadDescriptor(token.Descriptor).GetHashCode());
            Assert.True(token.Equals((object)new TokenPayloadDescriptor(token.Descriptor)));
            Assert.False(token.Equals("token"));
        }

        [Fact]
        public void TokenDescriptorAcceptsCustomTokenSchemaWithoutBodyInterpretation()
        {
            var token = new TokenPayloadDescriptor(
                schemaId: 0x00002000,
                schemaVersion: 1,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsSnapshot,
                payloadOffset: 0,
                payloadLength: 2,
                TypedPayloadDescriptorFlags.Terminal | TypedPayloadDescriptorFlags.SchemaOverride);

            Assert.False(token.IsStandardDeltaSchema);
            Assert.True(token.IsTerminal);
            Assert.False(token.IsPartial);
            Assert.True(token.HasSchemaOverride);
            Assert.False(token.HasProfileHint);
        }

        [Fact]
        public void TokenDescriptorRejectsNonTokenDescriptorBindings()
        {
            var wrongProfile = new TypedPayloadDescriptor(
                PayloadKind.TokenChunk,
                TypedPayloadProfileId.Tensor,
                TypedPayloadDescriptorFlags.Partial,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 0,
                payloadLength: 1);
            var wrongKind = new TypedPayloadDescriptor(
                PayloadKind.ToolDelta,
                TypedPayloadProfileId.Token,
                TypedPayloadDescriptorFlags.Partial,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 0,
                payloadLength: 1);
            var unknownFlags = new TypedPayloadDescriptor(
                PayloadKind.TokenChunk,
                TypedPayloadProfileId.Token,
                descriptorFlags: 0x0010,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 0,
                payloadLength: 1);
            var nonZeroReserved = new TypedPayloadDescriptor(
                PayloadKind.TokenChunk,
                TypedPayloadProfileId.Token,
                TypedPayloadDescriptorFlags.Partial,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 0,
                payloadLength: 1,
                reserved0: 1);

            Assert.False(TokenPayloadDescriptor.TryFromDescriptor(wrongProfile, out _));
            Assert.False(TokenPayloadDescriptor.TryFromDescriptor(wrongKind, out _));
            Assert.False(TokenPayloadDescriptor.TryFromDescriptor(unknownFlags, out _));
            Assert.False(TokenPayloadDescriptor.TryFromDescriptor(nonZeroReserved, out _));
            Assert.Throws<ArgumentException>(() => new TokenPayloadDescriptor(wrongProfile));
        }

        [Fact]
        public void TokenFrameViewPreservesPayloadSliceAndRejectsLengthMismatch()
        {
            var descriptor = TokenPayloadDescriptor.CreateDelta(0, 3);
            var payload = new byte[] { 0x61, 0x62, 0x63 };
            var frame = new TokenPayloadFrameView(descriptor, payload);

            Assert.Equal(payload, frame.Payload.ToArray());
            Assert.True(frame.IsPartial);
            Assert.False(frame.IsTerminal);
            Assert.Equal(0u, frame.PayloadOffset);
            Assert.Equal(3u, frame.PayloadLength);

            var typedFrame = frame.ToTypedPayloadFrameView();
            Assert.True(TokenPayloadFrameView.TryFromFrame(typedFrame, out var projected));
            Assert.Equal(frame, projected);
            Assert.Equal(frame.GetHashCode(), projected.GetHashCode());
            Assert.True(frame.Equals((object)projected));
            Assert.False(frame.Equals("frame"));

            Assert.Throws<ArgumentException>(() => new TokenPayloadFrameView(descriptor, new byte[] { 0x61 }));
            Assert.Throws<ArgumentException>(() => new TokenPayloadFrames(new[] { default(TokenPayloadFrameView) }));
            Assert.False(TokenPayloadFrameView.TryFromFrame(
                new TypedPayloadFrameView(new TypedPayloadDescriptor(
                    PayloadKind.TokenChunk,
                    TypedPayloadProfileId.Tensor,
                    TypedPayloadDescriptorFlags.Partial,
                    schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                    schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                    streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                    payloadOffset: 0,
                    payloadLength: 1),
                    new byte[] { 0x61 }),
                out _));
        }

        [Fact]
        public void ResultPushExposesTokenPayloadWrappers()
        {
            var descriptors = new[]
            {
                TokenPayloadDescriptor.CreateDelta(0, 2).Descriptor,
                TokenPayloadDescriptor.CreateDelta(2, 3, TypedPayloadDescriptorFlags.Terminal).Descriptor,
            };
            var payload = new byte[] { 0x68, 0x69, 0x21, 0x21, 0x0A };
            var message = new ResultPushMessage(
                Header(),
                Metadata(payloadFrameCount: 2),
                Array.Empty<ushort>(),
                Array.Empty<TensorSectionBlock>(),
                descriptors,
                payload);

            Assert.True(ResultPushMessage.TryParse(message.ToArray(), out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);

            var tokenFrames = parsed.GetTokenPayloadFrames();
            Assert.Equal(2, tokenFrames.FrameCount);
            Assert.Equal(5, tokenFrames.PayloadBytes);
            Assert.True(tokenFrames.Frames.Span[0].IsPartial);
            Assert.False(tokenFrames.Frames.Span[0].IsTerminal);
            Assert.False(tokenFrames.Frames.Span[1].IsPartial);
            Assert.True(tokenFrames.Frames.Span[1].IsTerminal);
            Assert.Equal(new byte[] { 0x68, 0x69 }, tokenFrames.Frames.Span[0].Payload.ToArray());
            Assert.Equal(new byte[] { 0x21, 0x21, 0x0A }, tokenFrames.Frames.Span[1].Payload.ToArray());

            var explicitFrames = TokenPayloadFrames.FromTypedPayloadFrames(parsed.GetTokenChunkFrames());
            Assert.Equal(tokenFrames, explicitFrames);
            Assert.Equal(tokenFrames.GetHashCode(), explicitFrames.GetHashCode());
            Assert.True(tokenFrames.Equals((object)explicitFrames));
            Assert.False(tokenFrames.Equals("frames"));
            Assert.False(tokenFrames.IsEmpty);
            Assert.Throws<ArgumentException>(() => TokenPayloadFrames.FromTypedPayloadFrames(
                new TypedPayloadProfileFrames(PayloadKind.ToolDelta, 99, ReadOnlyMemory<TypedPayloadFrameView>.Empty)));
        }

        private static NnrpHeader Header()
        {
            return new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                wireFormat: NnrpHeader.CurrentWireFormat,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.CurrentMetadataLength,
                bodyLength: 0,
                sessionId: 42,
                frameId: 303,
                viewId: 0,
                routeId: 0,
                traceId: 99);
        }

        private static ResultPushMetadata Metadata(ushort payloadFrameCount)
        {
            return new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.Partial,
                sectionCount: 0,
                tileCount: 0,
                activeProfileId: TypedPayloadProfileId.Token.Value,
                inferenceMilliseconds: 1,
                queueMilliseconds: 0,
                serverTotalMilliseconds: 1,
                tileBaseId: 0,
                tileIndexBytes: 0,
                resultClass: ResultClass.Partial,
                payloadKindBitmap: PayloadKind.TokenChunk,
                payloadFrameCount: payloadFrameCount);
        }
    }
}
