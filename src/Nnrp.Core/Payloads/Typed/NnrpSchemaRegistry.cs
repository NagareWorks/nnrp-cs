using System;
using System.Collections.Generic;

namespace Nnrp.Core
{
    public enum SchemaRegistryAction
    {
        None = 0,
        Installed = 1,
        AlreadyInstalled = 2,
        Updated = 3,
        Invalidated = 4,
    }

    public sealed class NnrpSchemaRegistry
    {
        private const ulong StandardTokenDeltaSchemaHash = 0x6E6E7270746F6B33UL;

        private readonly Dictionary<SchemaRegistryKey, NnrpSchemaDescriptorHeader> _entries;

        public NnrpSchemaRegistry()
        {
            _entries = new Dictionary<SchemaRegistryKey, NnrpSchemaDescriptorHeader>();
        }

        public int Count => _entries.Count;

        internal IReadOnlyCollection<NnrpSchemaDescriptorHeader> SnapshotDescriptors()
        {
            return new List<NnrpSchemaDescriptorHeader>(_entries.Values);
        }

        public static NnrpSchemaRegistry WithStandardProfiles()
        {
            var registry = new NnrpSchemaRegistry();
            if (!registry.TryInstall(CreateStandardTokenDeltaDescriptor(), out _, out var errorCode))
            {
                throw new InvalidOperationException($"Standard token schema descriptor failed to install: {errorCode}.");
            }

            return registry;
        }

        public bool TryInstall(NnrpSchemaDescriptorHeader descriptor, out SchemaRegistryAction action, out SchemaErrorCode errorCode)
        {
            action = SchemaRegistryAction.None;
            errorCode = SchemaErrorCode.None;

            if (!TryValidateProfileAssignment(descriptor.ProfileId, out errorCode))
            {
                return false;
            }

            if ((descriptor.SchemaFlags & ~NnrpSchemaDescriptorHeader.KnownSchemaFlagMask) != 0)
            {
                errorCode = SchemaErrorCode.UpdateRejected;
                return false;
            }

            var key = new SchemaRegistryKey(descriptor.SchemaId, descriptor.SchemaVersion);
            if (_entries.TryGetValue(key, out var existing))
            {
                if (existing.SchemaHash == descriptor.SchemaHash && existing.ProfileId == descriptor.ProfileId)
                {
                    action = SchemaRegistryAction.AlreadyInstalled;
                    return true;
                }

                errorCode = SchemaErrorCode.HashConflict;
                return false;
            }

            var hasOlderVersion = false;
            foreach (var existingKey in _entries.Keys)
            {
                if (existingKey.SchemaId == descriptor.SchemaId && existingKey.SchemaVersion < descriptor.SchemaVersion)
                {
                    hasOlderVersion = true;
                    break;
                }
            }

            _entries.Add(key, descriptor);
            action = hasOlderVersion ? SchemaRegistryAction.Updated : SchemaRegistryAction.Installed;
            return true;
        }

        public bool TryGet(uint schemaId, uint schemaVersion, out NnrpSchemaDescriptorHeader descriptor)
        {
            return _entries.TryGetValue(new SchemaRegistryKey(schemaId, schemaVersion), out descriptor);
        }

        public bool TryInvalidate(uint schemaId, uint schemaVersion, out SchemaRegistryAction action, out SchemaErrorCode errorCode)
        {
            action = SchemaRegistryAction.None;
            if (_entries.Remove(new SchemaRegistryKey(schemaId, schemaVersion)))
            {
                errorCode = SchemaErrorCode.None;
                action = SchemaRegistryAction.Invalidated;
                return true;
            }

            errorCode = SchemaErrorCode.VersionUnknown;
            return false;
        }

        public bool TryValidateDescriptorBinding(TypedPayloadDescriptor descriptor, out SchemaErrorCode errorCode)
        {
            errorCode = SchemaErrorCode.None;
            if (!TryValidateProfileAssignment(descriptor.ProfileId, out errorCode))
            {
                return false;
            }

            if (descriptor.ProfileId == NnrpSchemaDescriptorHeader.ProfileUnspecified)
            {
                if (descriptor.SchemaId == 0 && descriptor.SchemaVersion == 0)
                {
                    return true;
                }

                errorCode = SchemaErrorCode.Incompatible;
                return false;
            }

            if (descriptor.SchemaId == 0)
            {
                errorCode = SchemaErrorCode.Unknown;
                return false;
            }

            if (!_entries.TryGetValue(new SchemaRegistryKey(descriptor.SchemaId, descriptor.SchemaVersion), out var schema))
            {
                errorCode = ContainsSchemaId(descriptor.SchemaId)
                    ? SchemaErrorCode.VersionUnknown
                    : SchemaErrorCode.Unknown;
                return false;
            }

            if (schema.ProfileId != descriptor.ProfileId)
            {
                errorCode = SchemaErrorCode.Incompatible;
                return false;
            }

            return true;
        }

        public static bool TryValidateProfileAssignment(ushort profileId, out SchemaErrorCode errorCode)
        {
            if (profileId == NnrpSchemaDescriptorHeader.ProfileUnspecified
                || profileId == NnrpSchemaDescriptorHeader.ProfileTensor
                || profileId == NnrpSchemaDescriptorHeader.ProfileToken)
            {
                errorCode = SchemaErrorCode.None;
                return true;
            }

            errorCode = SchemaErrorCode.UpdateRejected;
            return false;
        }

        private bool ContainsSchemaId(uint schemaId)
        {
            foreach (var key in _entries.Keys)
            {
                if (key.SchemaId == schemaId)
                {
                    return true;
                }
            }

            return false;
        }

        private static NnrpSchemaDescriptorHeader CreateStandardTokenDeltaDescriptor()
        {
            return new NnrpSchemaDescriptorHeader(
                NnrpSchemaDescriptorHeader.TokenDeltaSchemaId,
                NnrpSchemaDescriptorHeader.TokenDeltaSchemaVersion,
                NnrpSchemaDescriptorHeader.ProfileToken,
                schemaFlags: 0,
                minVersionMajor: 1,
                maxVersionMajor: 1,
                bodyBytes: 0,
                dependencyCount: 0,
                NnrpSchemaDescriptorHeader.TokenDeltaDefaultStreamSemantics,
                StandardTokenDeltaSchemaHash);
        }

        private readonly struct SchemaRegistryKey : IEquatable<SchemaRegistryKey>
        {
            public SchemaRegistryKey(uint schemaId, uint schemaVersion)
            {
                SchemaId = schemaId;
                SchemaVersion = schemaVersion;
            }

            public uint SchemaId { get; }

            public uint SchemaVersion { get; }

            public bool Equals(SchemaRegistryKey other)
            {
                return SchemaId == other.SchemaId && SchemaVersion == other.SchemaVersion;
            }

            public override bool Equals(object obj)
            {
                return obj is SchemaRegistryKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (SchemaId.GetHashCode() * 397) ^ SchemaVersion.GetHashCode();
                }
            }
        }
    }
}
