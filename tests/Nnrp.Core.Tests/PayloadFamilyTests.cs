using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class PayloadFamilyTests
    {
        [Fact]
        public void FamilyClassifiesStandardProfilesAndRegistryBoundFamilies()
        {
            Assert.Equal(PayloadKind.Tensor, PayloadFamily.Tensor.PayloadKind);
            Assert.Equal((uint)PayloadKind.Tensor, PayloadFamily.Tensor.Bit);
            Assert.Equal("tensor", PayloadFamily.Tensor.Name);
            Assert.True(PayloadFamily.Tensor.IsDefined);
            Assert.True(PayloadFamily.Tensor.IsStandardProfile);
            Assert.False(PayloadFamily.Tensor.IsRegistryBoundFamily);
            Assert.Equal(TypedPayloadProfileId.Tensor, PayloadFamily.Tensor.StandardProfile);

            Assert.Equal("token_chunk", PayloadFamily.TokenChunk.ToString());
            Assert.True(PayloadFamily.TokenChunk.IsStandardProfile);
            Assert.Equal(TypedPayloadProfileId.Token, PayloadFamily.TokenChunk.StandardProfile);

            Assert.False(PayloadFamily.StructuredEvent.IsStandardProfile);
            Assert.True(PayloadFamily.StructuredEvent.IsRegistryBoundFamily);
            Assert.True(PayloadFamily.StructuredEvent.IsStructuredEvent);
            Assert.False(PayloadFamily.StructuredEvent.IsToolDelta);
            Assert.Equal(TypedPayloadProfileId.Unspecified, PayloadFamily.StructuredEvent.StandardProfile);
            Assert.Equal("structured_event", PayloadFamily.StructuredEvent.Name);

            Assert.False(PayloadFamily.ToolDelta.IsStandardProfile);
            Assert.True(PayloadFamily.ToolDelta.IsRegistryBoundFamily);
            Assert.False(PayloadFamily.ToolDelta.IsStructuredEvent);
            Assert.True(PayloadFamily.ToolDelta.IsToolDelta);
            Assert.Equal("tool_delta", PayloadFamily.ToolDelta.Name);

            Assert.True(PayloadFamily.AudioChunk.IsRegistryBoundFamily);
            Assert.True(PayloadFamily.VideoChunk.IsRegistryBoundFamily);
            Assert.True(PayloadFamily.OpaqueBytes.IsRegistryBoundFamily);
            Assert.Equal("audio_chunk", PayloadFamily.AudioChunk.Name);
            Assert.Equal("video_chunk", PayloadFamily.VideoChunk.Name);
            Assert.Equal("opaque_bytes", PayloadFamily.OpaqueBytes.Name);
        }

        [Fact]
        public void FamilyRejectsUndefinedOrBitmapPayloadKinds()
        {
            Assert.False(PayloadFamily.TryFromPayloadKind(PayloadKind.None, out var none));
            Assert.False(none.IsDefined);
            Assert.Equal("0", none.Name);

            Assert.False(PayloadFamily.TryFromPayloadKind(PayloadKind.Tensor | PayloadKind.ToolDelta, out _));
            Assert.False(PayloadFamily.TryFromPayloadKind((PayloadKind)0x80000000u, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => PayloadFamily.FromPayloadKind(PayloadKind.Tensor | PayloadKind.ToolDelta));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PayloadFamily(PayloadKind.None));
        }

        [Fact]
        public void FamilyEqualityUsesPayloadKindBit()
        {
            var family = PayloadFamily.FromPayloadKind(PayloadKind.ToolDelta);

            Assert.Equal(PayloadFamily.ToolDelta, family);
            Assert.True(family.Equals((object)PayloadFamily.ToolDelta));
            Assert.False(family.Equals("tool_delta"));
            Assert.Equal(PayloadFamily.ToolDelta.GetHashCode(), family.GetHashCode());
        }

        [Fact]
        public void PayloadKindValidatorUsesFamilyClassification()
        {
            Assert.True(PayloadKindValidator.IsStandardProfileFamily(PayloadKind.Tensor));
            Assert.True(PayloadKindValidator.IsStandardProfileFamily(PayloadKind.TokenChunk));
            Assert.False(PayloadKindValidator.IsStandardProfileFamily(PayloadKind.StructuredEvent));
            Assert.False(PayloadKindValidator.IsStandardProfileFamily(PayloadKind.Tensor | PayloadKind.TokenChunk));

            Assert.True(PayloadKindValidator.IsRegistryBoundFamily(PayloadKind.StructuredEvent));
            Assert.True(PayloadKindValidator.IsRegistryBoundFamily(PayloadKind.ToolDelta));
            Assert.True(PayloadKindValidator.IsRegistryBoundFamily(PayloadKind.OpaqueBytes));
            Assert.False(PayloadKindValidator.IsRegistryBoundFamily(PayloadKind.TokenChunk));
            Assert.False(PayloadKindValidator.IsRegistryBoundFamily((PayloadKind)0x80000000u));
        }

        [Fact]
        public void TypedPayloadViewsExposeProtocolVisibleFamilyWithoutBodyInterpretation()
        {
            var descriptor = new TypedPayloadDescriptor(
                PayloadKind.ToolDelta,
                profileId: 9,
                descriptorFlags: 0,
                schemaId: 0x00002001,
                schemaVersion: 4,
                streamSemantics: TypedPayloadDescriptor.StreamSemanticsToolUpdate,
                payloadOffset: 0,
                payloadLength: 3);
            var frame = new TypedPayloadFrameView(descriptor, new byte[] { 0x41, 0x42, 0x43 });
            var frames = new TypedPayloadProfileFrames(PayloadKind.ToolDelta, 9, new[] { frame });
            var coverage = new TypedPayloadProfileCoverage(PayloadKind.ToolDelta, 9, frameCount: 1, payloadBytes: 3);

            Assert.Equal(PayloadFamily.ToolDelta, frame.Family);
            Assert.Equal(PayloadFamily.ToolDelta, frames.Family);
            Assert.Equal(PayloadFamily.ToolDelta, coverage.Family);
            Assert.True(frame.Family.IsRegistryBoundFamily);
            Assert.Equal(0x00002001u, frame.Descriptor.SchemaId);
            Assert.Equal(new byte[] { 0x41, 0x42, 0x43 }, frame.Payload.ToArray());
        }
    }
}
