using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class StandardProfileTests
    {
        [Fact]
        public void ProfileIdSurfacesStandardAndUnspecifiedProfiles()
        {
            Assert.Equal((ushort)0, TypedPayloadProfileId.Unspecified.Value);
            Assert.Equal((ushort)1, TypedPayloadProfileId.Tensor.Value);
            Assert.Equal((ushort)2, TypedPayloadProfileId.Token.Value);
            Assert.True(TypedPayloadProfileId.Unspecified.IsUnspecified);
            Assert.False(TypedPayloadProfileId.Unspecified.IsStandardProfile);
            Assert.True(TypedPayloadProfileId.Tensor.IsTensor);
            Assert.True(TypedPayloadProfileId.Tensor.IsStandardProfile);
            Assert.True(TypedPayloadProfileId.Token.IsToken);
            Assert.True(TypedPayloadProfileId.Token.IsStandardProfile);
            Assert.Equal(PayloadKind.None, TypedPayloadProfileId.Unspecified.PayloadKind);
            Assert.Equal(PayloadKind.Tensor, TypedPayloadProfileId.Tensor.PayloadKind);
            Assert.Equal(PayloadKind.TokenChunk, TypedPayloadProfileId.Token.PayloadKind);
            Assert.Equal("unspecified", TypedPayloadProfileId.Unspecified.Name);
            Assert.Equal("tensor", TypedPayloadProfileId.Tensor.ToString());
            Assert.Equal("token", TypedPayloadProfileId.Token.Name);
        }

        [Fact]
        public void ProfileIdPreservesExtensionProfilesWithoutStandardPayloadInference()
        {
            var profile = TypedPayloadProfileId.FromValue(99);

            Assert.Equal((ushort)99, profile.Value);
            Assert.False(profile.IsKnown);
            Assert.False(profile.IsStandardProfile);
            Assert.True(profile.IsExtension);
            Assert.Equal(PayloadKind.None, profile.PayloadKind);
            Assert.Equal("99", profile.Name);
            Assert.Equal(profile, new TypedPayloadProfileId(99));
            Assert.Equal(profile.GetHashCode(), new TypedPayloadProfileId(99).GetHashCode());
            Assert.True(profile.Equals((object)new TypedPayloadProfileId(99)));
            Assert.False(profile.Equals("not-profile"));
        }

        [Fact]
        public void ProfileIdMapsOnlyStandardPayloadKinds()
        {
            Assert.True(TypedPayloadProfileId.TryFromPayloadKind(PayloadKind.Tensor, out var tensor));
            Assert.Equal(TypedPayloadProfileId.Tensor, tensor);
            Assert.True(TypedPayloadProfileId.TryFromPayloadKind(PayloadKind.TokenChunk, out var token));
            Assert.Equal(TypedPayloadProfileId.Token, token);

            Assert.False(TypedPayloadProfileId.TryFromPayloadKind(PayloadKind.ToolDelta, out var tool));
            Assert.Equal(TypedPayloadProfileId.Unspecified, tool);
            Assert.False(TypedPayloadProfileId.TryFromPayloadKind(PayloadKind.Tensor | PayloadKind.TokenChunk, out var bitmap));
            Assert.Equal(TypedPayloadProfileId.Unspecified, bitmap);
        }

        [Fact]
        public void DescriptorAndFrameViewsExposeProfileIdentity()
        {
            var descriptor = new TypedPayloadDescriptor(
                PayloadKind.TokenChunk,
                TypedPayloadProfileId.Token,
                descriptorFlags: 0,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 0,
                payloadLength: 4);
            var frame = new TypedPayloadFrameView(descriptor, new byte[] { 1, 2, 3, 4 });
            var frames = new TypedPayloadProfileFrames(PayloadKind.TokenChunk, TypedPayloadProfileId.Token, new[] { frame });
            var coverage = new TypedPayloadProfileCoverage(PayloadKind.TokenChunk, TypedPayloadProfileId.Token.Value, 1, 4);

            Assert.Equal(TypedPayloadProfileId.Token, descriptor.Profile);
            Assert.Equal(TypedPayloadProfileId.Token, frame.Profile);
            Assert.Equal(TypedPayloadProfileId.Token, frames.Profile);
            Assert.Equal(TypedPayloadProfileId.Token, coverage.Profile);
            Assert.Equal(1, frames.FrameCount);
            Assert.Equal(4, frames.PayloadBytes);
        }

        [Fact]
        public void DescriptorPreservesExplicitKindForUnspecifiedProfile()
        {
            var descriptor = new TypedPayloadDescriptor(
                PayloadKind.OpaqueBytes,
                TypedPayloadProfileId.Unspecified,
                descriptorFlags: 0,
                schemaId: 0,
                schemaVersion: 0,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsDefault,
                payloadOffset: 0,
                payloadLength: 0);

            Assert.True(TypedPayloadDescriptor.TryParse(descriptor.ToArray(), strict: true, out var parsed, out var error));
            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(TypedPayloadProfileId.Unspecified, parsed.Profile);
            Assert.Equal(PayloadKind.OpaqueBytes, parsed.PayloadKind);
        }

        [Fact]
        public void SessionAndCommonMetadataExposeProfileIdentity()
        {
            var open = new SessionOpenMetadata(
                requestedSessionId: 41,
                profileId: TypedPayloadProfileId.Token.Value,
                priorityClass: SessionPriorityClass.Balanced,
                sessionFlags: SessionFlags.None,
                schemaId: 7,
                schemaVersion: 3,
                defaultDeadlineMilliseconds: 500,
                maxInFlightOperations: 4,
                leaseTtlHintMilliseconds: 0,
                resumeTokenBytes: 0,
                authBytes: 0,
                sessionExtensionBytes: 0,
                clientSessionTag: 0);
            var ack = new SessionOpenAckMetadata(
                sessionId: 41,
                acceptedProfileId: TypedPayloadProfileId.Tensor.Value,
                acceptedPriorityClass: SessionPriorityClass.Balanced,
                sessionStatus: SessionStatus.Opened,
                schemaId: 7,
                schemaVersion: 3,
                grantedOperationCredit: 4,
                maxInFlightOperations: 4,
                leaseTtlMilliseconds: 0,
                resumeWindowMilliseconds: 0,
                resumeTokenBytes: 0,
                sessionExtensionBytes: 0,
                serverSessionTag: 0,
                routeScopeId: 0,
                sessionErrorCode: SessionErrorCode.None,
                sessionFlagsAck: SessionAckFlags.None);
            var patch = new SessionPatchMetadata(
                TypedPayloadProfileId.Token.Value,
                SessionPatchField.ProfilePatch,
                targetCadenceX100: 6000,
                qualityTier: 1,
                degradePolicy: 0,
                activeLaneMask: 1,
                preferredCodecBitmap: 0,
                preferredCompressionBitmap: 0,
                profilePatchBytes: 0);
            var patchAck = new SessionPatchAckMetadata(
                SessionPatchAckStatus.Accepted,
                SessionPatchRejectReason.None,
                SessionPatchField.ProfilePatch,
                SessionPatchField.None,
                retryAfterMilliseconds: 0,
                effectiveProfileId: TypedPayloadProfileId.Token.Value,
                effectiveTargetCadenceX100: 6000,
                effectiveQualityTier: 1,
                effectiveDegradePolicy: 0,
                effectiveLaneMask: 1,
                preferredCodecBitmap: 0,
                preferredCompressionBitmap: 0,
                profilePatchAckBytes: 0);
            var inlineHeader = new InlineObjectBlockHeader(CacheObjectKind.ToolSchema, 0, TypedPayloadProfileId.Token.Value, 0, 4);
            var extension = new ExtensionFrameDescriptor(1, 0, TypedPayloadProfileId.Tensor.Value, 0, 0, 4);

            Assert.Equal(TypedPayloadProfileId.Token, open.Profile);
            Assert.Equal(TypedPayloadProfileId.Tensor, ack.AcceptedProfile);
            Assert.Equal(TypedPayloadProfileId.Token, patch.Profile);
            Assert.Equal(TypedPayloadProfileId.Token, patchAck.EffectiveProfile);
            Assert.Equal(TypedPayloadProfileId.Token, inlineHeader.Profile);
            Assert.Equal(TypedPayloadProfileId.Tensor, extension.Profile);
        }

        [Fact]
        public void ResultMetadataAndLookupsExposeProfileIdentity()
        {
            var descriptor = new TypedPayloadDescriptor(
                PayloadKind.TokenChunk,
                TypedPayloadProfileId.Token,
                descriptorFlags: 0,
                schemaId: TypedPayloadDescriptor.TokenDeltaSchemaId,
                schemaVersion: TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsAppend,
                payloadOffset: 0,
                payloadLength: 3);
            var metadata = new ResultPushMetadata(
                statusCode: ResultStatusCode.Success,
                resultFlags: ResultFlags.None,
                sectionCount: 0,
                tileCount: 0,
                activeProfileId: TypedPayloadProfileId.Unspecified.Value,
                inferenceMilliseconds: 2,
                queueMilliseconds: 1,
                serverTotalMilliseconds: 3,
                tileBaseId: 0,
                tileIndexBytes: 0,
                payloadKindBitmap: PayloadKind.TokenChunk,
                payloadFrameCount: 1);
            var message = new ResultPushMessage(
                Header(bodyLength: 0),
                metadata,
                Array.Empty<ushort>(),
                Array.Empty<TensorSectionBlock>(),
                new[] { descriptor },
                new byte[] { 0x41, 0x42, 0x43 });

            Assert.Equal(TypedPayloadProfileId.Unspecified, message.Metadata.ActiveProfile);
            Assert.Single(message.GetTypedPayloadFrames(PayloadKind.TokenChunk, TypedPayloadProfileId.Token));

            var tokenFrames = message.GetTokenChunkFrames();
            Assert.Equal(TypedPayloadProfileId.Token, tokenFrames.Profile);
            Assert.Equal(3, tokenFrames.PayloadBytes);
            Assert.True(message.TryGetPayloadCoverage(PayloadKind.TokenChunk, TypedPayloadProfileId.Token, out var coverage));
            Assert.Equal(TypedPayloadProfileId.Token, coverage.Profile);

            var explicitFrames = message.GetPayloadFrames(PayloadKind.TokenChunk, TypedPayloadProfileId.Token);
            Assert.Equal(tokenFrames, explicitFrames);
        }

        private static NnrpHeader Header(uint bodyLength)
        {
            return new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.MetadataLength,
                bodyLength: bodyLength,
                sessionId: 41,
                frameId: 303,
                viewId: 0,
                routeId: 0,
                traceId: 0);
        }
    }
}
