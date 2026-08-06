using System;
using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class SchemaDescriptorHeaderTests
    {
        [Fact]
        public void SchemaDescriptorHeaderRoundTripsGoldenVector()
        {
            var bytes = Convert.FromHexString("011000000300000002000f000101000040000000020002008877665544332211");

            Assert.True(NnrpSchemaDescriptorHeader.TryParse(bytes, out var header, out var error));

            Assert.Equal(NnrpParseError.None, error);
            Assert.Equal(0x00001001u, header.SchemaId);
            Assert.Equal(3u, header.SchemaVersion);
            Assert.Equal(NnrpSchemaDescriptorHeader.ProfileToken, header.ProfileId);
            Assert.Equal(TypedPayloadProfileId.Token, header.Profile);
            Assert.Equal(0x000Fu, header.SchemaFlags);
            Assert.Equal(1, header.MinVersionMajor);
            Assert.Equal(1, header.MaxVersionMajor);
            Assert.Equal(64u, header.BodyBytes);
            Assert.Equal(2, header.DependencyCount);
            Assert.Equal(TypedPayloadDescriptor.StreamSemanticsAppend, header.DefaultStreamSemantics);
            Assert.Equal(0x1122334455667788UL, header.SchemaHash);
            Assert.True(header.IsCacheable);
            Assert.True(header.IsCritical);
            Assert.True(header.IsDefaultBindable);
            Assert.True(header.IsHashStable);
            Assert.Equal(bytes, header.ToArray());

            var destination = new byte[NnrpSchemaDescriptorHeader.HeaderLength + 3];
            Assert.True(header.TryWrite(destination, out var bytesWritten));
            Assert.Equal(NnrpSchemaDescriptorHeader.HeaderLength, bytesWritten);
            Assert.Equal(bytes, destination.AsSpan(0, NnrpSchemaDescriptorHeader.HeaderLength).ToArray());
        }

        [Fact]
        public void SchemaDescriptorHeaderExposesStandardTokenDeltaAnchor()
        {
            Assert.Equal(TypedPayloadProfileId.UnspecifiedValue, NnrpSchemaDescriptorHeader.ProfileUnspecified);
            Assert.Equal(TypedPayloadProfileId.TensorValue, NnrpSchemaDescriptorHeader.ProfileTensor);
            Assert.Equal(TypedPayloadProfileId.TokenValue, NnrpSchemaDescriptorHeader.ProfileToken);
            Assert.Equal(TypedPayloadDescriptor.TokenDeltaSchemaId, NnrpSchemaDescriptorHeader.TokenDeltaSchemaId);
            Assert.Equal(TypedPayloadDescriptor.TokenDeltaSchemaVersion, NnrpSchemaDescriptorHeader.TokenDeltaSchemaVersion);
            Assert.Equal(TypedPayloadDescriptor.StreamSemanticsAppend, NnrpSchemaDescriptorHeader.TokenDeltaDefaultStreamSemantics);
        }

        [Fact]
        public void SchemaDescriptorHeaderRejectsUnknownFlagsAndReservedFields()
        {
            var bytes = Convert.FromHexString("011000000300000002000f000101000040000000020002008877665544332211");
            bytes[10] = 0x10;

            Assert.False(NnrpSchemaDescriptorHeader.TryParse(bytes, out _, out var error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);

            bytes = Convert.FromHexString("011000000300000002000f000101000040000000020002008877665544332211");
            bytes[14] = 0x01;

            Assert.False(NnrpSchemaDescriptorHeader.TryParse(bytes, out _, out error));
            Assert.Equal(NnrpParseError.NonZeroReservedField, error);

            var header = new NnrpSchemaDescriptorHeader(
                schemaId: 1,
                schemaVersion: 1,
                profileId: NnrpSchemaDescriptorHeader.ProfileTensor,
                schemaFlags: 0x0010,
                minVersionMajor: 1,
                maxVersionMajor: 1,
                bodyBytes: 0,
                dependencyCount: 0,
                defaultStreamSemantics: TypedPayloadDescriptor.StreamSemanticsSnapshot,
                schemaHash: 0);

            Assert.False(header.TryWrite(new byte[NnrpSchemaDescriptorHeader.HeaderLength], out var bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.Throws<ArgumentException>(() => header.Write(new byte[NnrpSchemaDescriptorHeader.HeaderLength]));
        }

        [Fact]
        public void SchemaDescriptorHeaderRejectsShortBuffers()
        {
            Assert.False(NnrpSchemaDescriptorHeader.TryParse(new byte[NnrpSchemaDescriptorHeader.HeaderLength - 1], out _, out var error));
            Assert.Equal(NnrpParseError.SourceTooShort, error);

            var header = new NnrpSchemaDescriptorHeader(
                NnrpSchemaDescriptorHeader.TokenDeltaSchemaId,
                NnrpSchemaDescriptorHeader.TokenDeltaSchemaVersion,
                NnrpSchemaDescriptorHeader.ProfileToken,
                schemaFlags: 0,
                minVersionMajor: 1,
                maxVersionMajor: 1,
                bodyBytes: 0,
                dependencyCount: 0,
                NnrpSchemaDescriptorHeader.TokenDeltaDefaultStreamSemantics,
                schemaHash: 0);
            Assert.False(header.TryWrite(new byte[NnrpSchemaDescriptorHeader.HeaderLength - 1], out var bytesWritten));
            Assert.Equal(0, bytesWritten);
            Assert.Throws<ArgumentException>(() => header.Write(new byte[NnrpSchemaDescriptorHeader.HeaderLength - 1]));
        }

        [Fact]
        public void SchemaDescriptorHeaderEqualityUsesAllPublicHeaderFields()
        {
            var header = new NnrpSchemaDescriptorHeader(
                schemaId: 0x20,
                schemaVersion: 4,
                profileId: NnrpSchemaDescriptorHeader.ProfileTensor,
                schemaFlags: NnrpSchemaDescriptorHeader.SchemaFlagCacheable,
                minVersionMajor: 1,
                maxVersionMajor: 2,
                bodyBytes: 128,
                dependencyCount: 3,
                defaultStreamSemantics: TypedPayloadDescriptor.StreamSemanticsSnapshot,
                schemaHash: 0x1122);
            var same = new NnrpSchemaDescriptorHeader(
                schemaId: 0x20,
                schemaVersion: 4,
                profileId: NnrpSchemaDescriptorHeader.ProfileTensor,
                schemaFlags: NnrpSchemaDescriptorHeader.SchemaFlagCacheable,
                minVersionMajor: 1,
                maxVersionMajor: 2,
                bodyBytes: 128,
                dependencyCount: 3,
                defaultStreamSemantics: TypedPayloadDescriptor.StreamSemanticsSnapshot,
                schemaHash: 0x1122);
            var different = new NnrpSchemaDescriptorHeader(
                schemaId: 0x20,
                schemaVersion: 5,
                profileId: NnrpSchemaDescriptorHeader.ProfileTensor,
                schemaFlags: NnrpSchemaDescriptorHeader.SchemaFlagCacheable,
                minVersionMajor: 1,
                maxVersionMajor: 2,
                bodyBytes: 128,
                dependencyCount: 3,
                defaultStreamSemantics: TypedPayloadDescriptor.StreamSemanticsSnapshot,
                schemaHash: 0x1122);

            Assert.Equal(header, same);
            Assert.True(header.Equals((object)same));
            Assert.False(header.Equals(different));
            Assert.False(header.Equals("not-schema-header"));
            Assert.Equal(header.GetHashCode(), same.GetHashCode());
        }
    }
}
