using Nnrp.Core;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class SchemaRegistryTests
    {
        [Fact]
        public void SchemaErrorCodeValuesRemainStable()
        {
            Assert.Equal(0x00040000u, (uint)SchemaErrorCode.None);
            Assert.Equal(0x00040001u, (uint)SchemaErrorCode.Unknown);
            Assert.Equal(0x00040002u, (uint)SchemaErrorCode.VersionUnknown);
            Assert.Equal(0x00040003u, (uint)SchemaErrorCode.HashConflict);
            Assert.Equal(0x00040004u, (uint)SchemaErrorCode.Incompatible);
            Assert.Equal(0x00040005u, (uint)SchemaErrorCode.DependencyMissing);
            Assert.Equal(0x00040006u, (uint)SchemaErrorCode.UpdateRejected);
        }

        [Fact]
        public void RegistryInstallsUpdatesAndRejectsHashConflicts()
        {
            var registry = new NnrpSchemaRegistry();
            var descriptor = CreateDescriptor(0x20, 1, NnrpSchemaDescriptorHeader.ProfileTensor, 0x11);

            Assert.True(registry.TryInstall(descriptor, out var action, out var errorCode));
            Assert.Equal(SchemaRegistryAction.Installed, action);
            Assert.Equal(SchemaErrorCode.None, errorCode);
            Assert.Equal(1, registry.Count);
            Assert.True(registry.TryGet(0x20, 1, out var installed));
            Assert.Equal(descriptor, installed);
            Assert.Contains(descriptor, registry.SnapshotDescriptors());

            Assert.True(registry.TryInstall(descriptor, out action, out errorCode));
            Assert.Equal(SchemaRegistryAction.AlreadyInstalled, action);
            Assert.Equal(SchemaErrorCode.None, errorCode);

            var conflict = CreateDescriptor(0x20, 1, NnrpSchemaDescriptorHeader.ProfileTensor, 0x12);
            Assert.False(registry.TryInstall(conflict, out action, out errorCode));
            Assert.Equal(SchemaRegistryAction.None, action);
            Assert.Equal(SchemaErrorCode.HashConflict, errorCode);

            var newer = CreateDescriptor(0x20, 2, NnrpSchemaDescriptorHeader.ProfileTensor, 0x22);
            Assert.True(registry.TryInstall(newer, out action, out errorCode));
            Assert.Equal(SchemaRegistryAction.Updated, action);
            Assert.Equal(SchemaErrorCode.None, errorCode);

            var lowerNewSchema = CreateDescriptor(0x20, 0, NnrpSchemaDescriptorHeader.ProfileTensor, 0x01);
            Assert.True(registry.TryInstall(lowerNewSchema, out action, out errorCode));
            Assert.Equal(SchemaRegistryAction.Installed, action);
            Assert.Equal(SchemaErrorCode.None, errorCode);
        }

        [Fact]
        public void RegistryInvalidatesKnownVersionsAndReportsUnknownVersions()
        {
            var registry = new NnrpSchemaRegistry();
            Assert.True(registry.TryInstall(CreateDescriptor(0x30, 1, NnrpSchemaDescriptorHeader.ProfileToken, 0x30), out _, out _));

            Assert.True(registry.TryInvalidate(0x30, 1, out var action, out var errorCode));
            Assert.Equal(SchemaRegistryAction.Invalidated, action);
            Assert.Equal(SchemaErrorCode.None, errorCode);
            Assert.Equal(0, registry.Count);
            Assert.False(registry.TryGet(0x30, 1, out _));

            Assert.False(registry.TryInvalidate(0x30, 1, out action, out errorCode));
            Assert.Equal(SchemaRegistryAction.None, action);
            Assert.Equal(SchemaErrorCode.VersionUnknown, errorCode);
        }

        [Fact]
        public void RegistryValidatesDescriptorBindingsWithoutImplicitTensorDefault()
        {
            var registry = new NnrpSchemaRegistry();
            Assert.True(registry.TryInstall(CreateDescriptor(0x40, 1, NnrpSchemaDescriptorHeader.ProfileTensor, 0x40), out _, out _));
            Assert.True(registry.TryInstall(CreateDescriptor(0x50, 1, NnrpSchemaDescriptorHeader.ProfileToken, 0x50), out _, out _));

            var tensorDescriptor = CreatePayloadDescriptor(NnrpSchemaDescriptorHeader.ProfileTensor, 0x40, 1);
            Assert.True(registry.TryValidateDescriptorBinding(tensorDescriptor, out var errorCode));
            Assert.Equal(SchemaErrorCode.None, errorCode);

            var unspecifiedWithoutSchema = CreatePayloadDescriptor(NnrpSchemaDescriptorHeader.ProfileUnspecified, 0, 0);
            Assert.True(registry.TryValidateDescriptorBinding(unspecifiedWithoutSchema, out errorCode));
            Assert.Equal(SchemaErrorCode.None, errorCode);

            var unspecifiedWithSchema = CreatePayloadDescriptor(NnrpSchemaDescriptorHeader.ProfileUnspecified, 0x40, 1);
            Assert.False(registry.TryValidateDescriptorBinding(unspecifiedWithSchema, out errorCode));
            Assert.Equal(SchemaErrorCode.Incompatible, errorCode);

            var missingSchema = CreatePayloadDescriptor(NnrpSchemaDescriptorHeader.ProfileTensor, 0x60, 1);
            Assert.False(registry.TryValidateDescriptorBinding(missingSchema, out errorCode));
            Assert.Equal(SchemaErrorCode.Unknown, errorCode);

            var missingVersion = CreatePayloadDescriptor(NnrpSchemaDescriptorHeader.ProfileTensor, 0x40, 9);
            Assert.False(registry.TryValidateDescriptorBinding(missingVersion, out errorCode));
            Assert.Equal(SchemaErrorCode.VersionUnknown, errorCode);

            var wrongProfile = CreatePayloadDescriptor(NnrpSchemaDescriptorHeader.ProfileToken, 0x40, 1);
            Assert.False(registry.TryValidateDescriptorBinding(wrongProfile, out errorCode));
            Assert.Equal(SchemaErrorCode.Incompatible, errorCode);

            var profileWithoutSchema = CreatePayloadDescriptor(NnrpSchemaDescriptorHeader.ProfileTensor, 0, 1);
            Assert.False(registry.TryValidateDescriptorBinding(profileWithoutSchema, out errorCode));
            Assert.Equal(SchemaErrorCode.Unknown, errorCode);
        }

        [Fact]
        public void RegistryRejectsUnknownPublicProfileAssignments()
        {
            var registry = new NnrpSchemaRegistry();
            var unsupported = CreateDescriptor(0x70, 1, 0xFFFF, 0x70);

            Assert.False(registry.TryInstall(unsupported, out var action, out var errorCode));
            Assert.Equal(SchemaRegistryAction.None, action);
            Assert.Equal(SchemaErrorCode.UpdateRejected, errorCode);

            var unknownFlags = CreateDescriptor(0x71, 1, NnrpSchemaDescriptorHeader.ProfileTensor, 0x71, schemaFlags: 0x0010);
            Assert.False(registry.TryInstall(unknownFlags, out action, out errorCode));
            Assert.Equal(SchemaRegistryAction.None, action);
            Assert.Equal(SchemaErrorCode.UpdateRejected, errorCode);

            Assert.False(NnrpSchemaRegistry.TryValidateProfileAssignment(0xFFFF, out errorCode));
            Assert.Equal(SchemaErrorCode.UpdateRejected, errorCode);

            Assert.True(NnrpSchemaRegistry.TryValidateProfileAssignment(NnrpSchemaDescriptorHeader.ProfileUnspecified, out errorCode));
            Assert.Equal(SchemaErrorCode.None, errorCode);

            var descriptor = CreatePayloadDescriptor(0xFFFF, 0x70, 1);
            Assert.False(registry.TryValidateDescriptorBinding(descriptor, out errorCode));
            Assert.Equal(SchemaErrorCode.UpdateRejected, errorCode);
        }

        [Fact]
        public void RegistrySeedsStandardTokenProfileBinding()
        {
            var registry = NnrpSchemaRegistry.WithStandardProfiles();

            Assert.Equal(1, registry.Count);
            Assert.True(registry.TryGet(
                NnrpSchemaDescriptorHeader.TokenDeltaSchemaId,
                NnrpSchemaDescriptorHeader.TokenDeltaSchemaVersion,
                out var descriptor));
            Assert.Equal(NnrpSchemaDescriptorHeader.ProfileToken, descriptor.ProfileId);
            Assert.Equal(NnrpSchemaDescriptorHeader.TokenDeltaDefaultStreamSemantics, descriptor.DefaultStreamSemantics);

            var payloadDescriptor = CreatePayloadDescriptor(
                NnrpSchemaDescriptorHeader.ProfileToken,
                NnrpSchemaDescriptorHeader.TokenDeltaSchemaId,
                NnrpSchemaDescriptorHeader.TokenDeltaSchemaVersion);
            Assert.True(registry.TryValidateDescriptorBinding(payloadDescriptor, out var errorCode));
            Assert.Equal(SchemaErrorCode.None, errorCode);
        }

        private static NnrpSchemaDescriptorHeader CreateDescriptor(
            uint schemaId,
            uint schemaVersion,
            ushort profileId,
            ulong schemaHash,
            ushort schemaFlags = NnrpSchemaDescriptorHeader.SchemaFlagCacheable)
        {
            return new NnrpSchemaDescriptorHeader(
                schemaId,
                schemaVersion,
                profileId,
                schemaFlags,
                minVersionMajor: 1,
                maxVersionMajor: 1,
                bodyBytes: 0,
                dependencyCount: 0,
                defaultStreamSemantics: TypedPayloadDescriptor.StreamSemanticsSnapshot,
                schemaHash);
        }

        private static TypedPayloadDescriptor CreatePayloadDescriptor(ushort profileId, uint schemaId, uint schemaVersion)
        {
            return new TypedPayloadDescriptor(
                PayloadKind.Tensor,
                profileId,
                descriptorFlags: 0,
                schemaId,
                schemaVersion,
                TypedPayloadDescriptor.StreamSemanticsSnapshot,
                payloadOffset: 0,
                payloadLength: 0);
        }
    }
}
