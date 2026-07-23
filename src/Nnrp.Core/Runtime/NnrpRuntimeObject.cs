using System;
using System.Buffers.Binary;
using Nnrp.Core;

namespace Nnrp.Runtime
{
    /// <summary>Encodes and decodes frozen Preview4 runtime-object and cache-reference metadata.</summary>
    public static class NnrpRuntimeObject
    {
        public static byte[] Encode(
            MessageType messageType,
            IRuntimeObjectMetadata metadata,
            ReadOnlySpan<byte> tail = default)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            var fixedLength = GetFixedLength(messageType);
            var declaredTailLength = GetDeclaredTailLength(messageType, metadata);
            if (declaredTailLength != (ulong)tail.Length)
            {
                throw new ArgumentException("Runtime object tail length does not match its metadata declaration.", nameof(tail));
            }

            var encoded = new byte[checked(fixedLength + tail.Length)];
            WriteMetadata(messageType, metadata, encoded.AsSpan(0, fixedLength));
            tail.CopyTo(encoded.AsSpan(fixedLength));
            return encoded;
        }

        public static DecodedRuntimeObjectMetadata Decode(MessageType messageType, ReadOnlySpan<byte> payload)
        {
            var fixedLength = GetFixedLength(messageType);
            if (payload.Length < fixedLength)
            {
                throw new ArgumentException("Runtime object payload is shorter than the frozen metadata layout.", nameof(payload));
            }

            var metadata = ReadMetadata(messageType, payload.Slice(0, fixedLength));
            var tail = payload.Slice(fixedLength).ToArray();
            if (GetDeclaredTailLength(messageType, metadata) != (ulong)tail.Length)
            {
                throw new ArgumentException("Runtime object tail length does not match its metadata declaration.", nameof(payload));
            }

            return new DecodedRuntimeObjectMetadata(metadata, tail);
        }

        private static int GetFixedLength(MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.ObjectDeclare:
                    return ObjectDescriptorMetadata.EncodedLength;
                case MessageType.ObjectRef:
                    return ObjectReferenceMetadata.EncodedLength;
                case MessageType.ObjectRelease:
                    return ObjectReleaseMetadata.EncodedLength;
                case MessageType.ObjectPatch:
                case MessageType.ObjectDelta:
                    return ObjectDeltaMetadata.EncodedLength;
                case MessageType.CacheReference:
                    return CacheReferenceMetadata.EncodedLength;
                case MessageType.CacheMiss:
                    return CacheMissMetadata.EncodedLength;
                default:
                    throw new ArgumentOutOfRangeException(nameof(messageType), messageType, "Message type does not select a runtime-object metadata layout.");
            }
        }

        private static ulong GetDeclaredTailLength(MessageType messageType, IRuntimeObjectMetadata metadata)
        {
            switch (messageType)
            {
                case MessageType.ObjectDeclare:
                    return RequireType<ObjectDescriptorMetadata>(metadata, messageType).MetadataBytes;
                case MessageType.ObjectRef:
                    return RequireType<ObjectReferenceMetadata>(metadata, messageType).MetadataBytes;
                case MessageType.ObjectRelease:
                    return RequireType<ObjectReleaseMetadata>(metadata, messageType).DiagnosticBytes;
                case MessageType.ObjectPatch:
                case MessageType.ObjectDelta:
                    var delta = RequireType<ObjectDeltaMetadata>(metadata, messageType);
                    return checked((ulong)delta.MetadataBytes + delta.DeltaBytes);
                case MessageType.CacheReference:
                    return RequireType<CacheReferenceMetadata>(metadata, messageType).MetadataBytes;
                case MessageType.CacheMiss:
                    return RequireType<CacheMissMetadata>(metadata, messageType).DiagnosticBytes;
                default:
                    GetFixedLength(messageType);
                    return 0;
            }
        }

        private static void WriteMetadata(MessageType messageType, IRuntimeObjectMetadata metadata, Span<byte> destination)
        {
            destination.Clear();
            switch (messageType)
            {
                case MessageType.ObjectDeclare:
                    WriteDescriptor(RequireType<ObjectDescriptorMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.ObjectRef:
                    WriteReference(RequireType<ObjectReferenceMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.ObjectRelease:
                    WriteRelease(RequireType<ObjectReleaseMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.ObjectPatch:
                case MessageType.ObjectDelta:
                    WriteDelta(RequireType<ObjectDeltaMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.CacheReference:
                    WriteCacheReference(RequireType<CacheReferenceMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.CacheMiss:
                    WriteCacheMiss(RequireType<CacheMissMetadata>(metadata, messageType), destination);
                    return;
                default:
                    GetFixedLength(messageType);
                    return;
            }
        }

        private static IRuntimeObjectMetadata ReadMetadata(MessageType messageType, ReadOnlySpan<byte> source)
        {
            switch (messageType)
            {
                case MessageType.ObjectDeclare:
                    return ReadDescriptor(source);
                case MessageType.ObjectRef:
                    return ReadReference(source);
                case MessageType.ObjectRelease:
                    return ReadRelease(source);
                case MessageType.ObjectPatch:
                case MessageType.ObjectDelta:
                    return ReadDelta(source);
                case MessageType.CacheReference:
                    return ReadCacheReference(source);
                case MessageType.CacheMiss:
                    return ReadCacheMiss(source);
                default:
                    GetFixedLength(messageType);
                    throw new InvalidOperationException();
            }
        }

        private static void WriteDescriptor(ObjectDescriptorMetadata value, Span<byte> destination)
        {
            RequireEnum(value.ObjectKind, nameof(value.ObjectKind));
            RequireEnum(value.ProducerRole, nameof(value.ProducerRole));
            RequireEnum(value.ConsumerRole, nameof(value.ConsumerRole));
            RequireEnum(value.MemoryLocationHint, nameof(value.MemoryLocationHint));
            RequireEnum(value.OwnershipHint, nameof(value.OwnershipHint));
            WriteUInt64(destination, 0, value.ObjectId);
            WriteUInt16(destination, 8, (ushort)value.ObjectKind);
            destination[10] = (byte)value.ProducerRole;
            destination[11] = (byte)value.ConsumerRole;
            WriteUInt32(destination, 12, value.SessionId);
            WriteUInt64(destination, 16, value.ByteSize);
            WriteUInt32(destination, 24, value.ComputeCostUnits);
            WriteUInt16(destination, 28, (ushort)value.MemoryLocationHint);
            WriteUInt16(destination, 30, (ushort)value.OwnershipHint);
            WriteUInt32(destination, 32, value.LifetimeHintMs);
            WriteUInt32(destination, 36, value.MetadataBytes);
        }

        private static ObjectDescriptorMetadata ReadDescriptor(ReadOnlySpan<byte> source)
        {
            RequireZero(ReadUInt64(source, 40), "object_descriptor.reserved");
            return new ObjectDescriptorMetadata(
                ReadUInt64(source, 0),
                ReadEnum<RuntimeObjectKind>(ReadUInt16(source, 8), "object_kind"),
                ReadEnum<RuntimeRole>(source[10], "producer_role"),
                ReadEnum<RuntimeRole>(source[11], "consumer_role"),
                ReadUInt32(source, 12),
                ReadUInt64(source, 16),
                ReadUInt32(source, 24),
                ReadEnum<MemoryLocationHint>(ReadUInt16(source, 28), "memory_location_hint"),
                ReadEnum<OwnershipHint>(ReadUInt16(source, 30), "ownership_hint"),
                ReadUInt32(source, 32),
                ReadUInt32(source, 36));
        }

        private static void WriteReference(ObjectReferenceMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x00000007, "object_reference.flags");
            WriteUInt64(destination, 0, value.ObjectId);
            WriteUInt64(destination, 8, value.OperationId);
            WriteUInt64(destination, 16, value.ObjectVersion);
            WriteUInt64(destination, 24, value.Offset);
            WriteUInt64(destination, 32, value.Length);
            WriteUInt32(destination, 40, value.Flags);
            WriteUInt32(destination, 44, value.MetadataBytes);
        }

        private static ObjectReferenceMetadata ReadReference(ReadOnlySpan<byte> source)
        {
            var value = new ObjectReferenceMetadata(ReadUInt64(source, 0), ReadUInt64(source, 8), ReadUInt64(source, 16), ReadUInt64(source, 24), ReadUInt64(source, 32), ReadUInt32(source, 40), ReadUInt32(source, 44));
            RequireMask(value.Flags, 0x00000007, "object_reference.flags");
            return value;
        }

        private static void WriteRelease(ObjectReleaseMetadata value, Span<byte> destination)
        {
            RequireEnum(value.ReleaseReason, nameof(value.ReleaseReason));
            RequireEnum(value.SourceRole, nameof(value.SourceRole));
            RequireMask(value.Flags, 0x03, "object_release.flags");
            WriteUInt64(destination, 0, value.ObjectId);
            WriteUInt64(destination, 8, value.OperationId);
            WriteUInt16(destination, 16, (ushort)value.ReleaseReason);
            destination[18] = (byte)value.SourceRole;
            destination[19] = value.Flags;
            WriteUInt32(destination, 20, value.DiagnosticBytes);
        }

        private static ObjectReleaseMetadata ReadRelease(ReadOnlySpan<byte> source)
        {
            RequireZero(ReadUInt64(source, 24), "object_release.reserved");
            var value = new ObjectReleaseMetadata(ReadUInt64(source, 0), ReadUInt64(source, 8), ReadEnum<ObjectReleaseReason>(ReadUInt16(source, 16), "release_reason"), ReadEnum<RuntimeRole>(source[18], "source_role"), source[19], ReadUInt32(source, 20));
            RequireMask(value.Flags, 0x03, "object_release.flags");
            return value;
        }

        private static void WriteDelta(ObjectDeltaMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x00000007, "object_delta.flags");
            WriteUInt64(destination, 0, value.ObjectId);
            WriteUInt64(destination, 8, value.DeltaSequence);
            WriteUInt64(destination, 16, value.RegionOffset);
            WriteUInt32(destination, 24, value.RegionBytes);
            WriteUInt32(destination, 28, value.DeltaBytes);
            WriteUInt32(destination, 32, value.Flags);
            WriteUInt32(destination, 36, value.MetadataBytes);
        }

        private static ObjectDeltaMetadata ReadDelta(ReadOnlySpan<byte> source)
        {
            var value = new ObjectDeltaMetadata(ReadUInt64(source, 0), ReadUInt64(source, 8), ReadUInt64(source, 16), ReadUInt32(source, 24), ReadUInt32(source, 28), ReadUInt32(source, 32), ReadUInt32(source, 36));
            RequireMask(value.Flags, 0x00000007, "object_delta.flags");
            return value;
        }

        private static void WriteCacheReference(CacheReferenceMetadata value, Span<byte> destination)
        {
            RequireEnum(value.ReuseScope, nameof(value.ReuseScope));
            RequireMask(value.Flags, 0x00000003, "cache_reference.flags");
            WriteUInt32(destination, 0, value.CacheNamespace);
            WriteUInt16(destination, 4, value.ProfileId);
            WriteUInt16(destination, 6, (ushort)value.ReuseScope);
            WriteUInt64(destination, 8, value.CacheKeyHi);
            WriteUInt64(destination, 16, value.CacheKeyLo);
            WriteUInt64(destination, 24, value.LeaseId);
            WriteUInt64(destination, 32, value.ProducerTraceId);
            WriteUInt32(destination, 40, value.ExpirationHintMs);
            WriteUInt32(destination, 44, value.MetadataBytes);
            WriteUInt32(destination, 48, value.Flags);
        }

        private static CacheReferenceMetadata ReadCacheReference(ReadOnlySpan<byte> source)
        {
            RequireZero(ReadUInt32(source, 52), "cache_reference.reserved");
            var value = new CacheReferenceMetadata(ReadUInt32(source, 0), ReadUInt64(source, 8), ReadUInt64(source, 16), ReadUInt16(source, 4), ReadEnum<CacheReuseScope>(ReadUInt16(source, 6), "reuse_scope"), ReadUInt64(source, 24), ReadUInt64(source, 32), ReadUInt32(source, 40), ReadUInt32(source, 44), ReadUInt32(source, 48));
            RequireMask(value.Flags, 0x00000003, "cache_reference.flags");
            return value;
        }

        private static void WriteCacheMiss(CacheMissMetadata value, Span<byte> destination)
        {
            RequireEnum(value.MissReason, nameof(value.MissReason));
            WriteUInt32(destination, 0, value.CacheNamespace);
            WriteUInt16(destination, 4, value.ProfileId);
            WriteUInt16(destination, 6, (ushort)value.MissReason);
            WriteUInt64(destination, 8, value.CacheKeyHi);
            WriteUInt64(destination, 16, value.CacheKeyLo);
            WriteUInt32(destination, 24, value.DiagnosticBytes);
        }

        private static CacheMissMetadata ReadCacheMiss(ReadOnlySpan<byte> source)
        {
            RequireZero(ReadUInt32(source, 28), "cache_miss.reserved");
            return new CacheMissMetadata(ReadUInt32(source, 0), ReadUInt64(source, 8), ReadUInt64(source, 16), ReadEnum<CacheMissReason>(ReadUInt16(source, 6), "miss_reason"), ReadUInt16(source, 4), ReadUInt32(source, 24));
        }

        private static T RequireType<T>(IRuntimeObjectMetadata metadata, MessageType messageType)
            where T : struct, IRuntimeObjectMetadata
        {
            if (metadata is T typed)
            {
                return typed;
            }

            throw new ArgumentException(messageType + " requires " + typeof(T).Name + ".", nameof(metadata));
        }

        private static void RequireEnum<T>(T value, string field)
            where T : struct
        {
            if (!Enum.IsDefined(typeof(T), value))
            {
                throw new ArgumentOutOfRangeException(field, value, "Unknown frozen enum value.");
            }
        }

        private static T ReadEnum<T>(object value, string field)
            where T : struct
        {
            var typed = (T)Enum.ToObject(typeof(T), value);
            RequireEnum(typed, field);
            return typed;
        }

        private static void RequireMask(uint value, uint mask, string field)
        {
            if ((value & ~mask) != 0)
            {
                throw new ArgumentOutOfRangeException(field, value, "Reserved flag bits must be zero.");
            }
        }

        private static void RequireZero(ulong value, string field)
        {
            if (value != 0)
            {
                throw new ArgumentException(field + " must be zero.");
            }
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> source, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2));

        private static uint ReadUInt32(ReadOnlySpan<byte> source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));

        private static ulong ReadUInt64(ReadOnlySpan<byte> source, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset, 8));

        private static void WriteUInt16(Span<byte> destination, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, 2), value);

        private static void WriteUInt32(Span<byte> destination, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, 4), value);

        private static void WriteUInt64(Span<byte> destination, int offset, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(offset, 8), value);
    }
}
