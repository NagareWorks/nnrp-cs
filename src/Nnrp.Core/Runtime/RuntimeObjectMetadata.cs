using System;

namespace Nnrp.Runtime
{
    /// <summary>Marker implemented by every frozen Preview4 runtime-object metadata value.</summary>
    public interface IRuntimeObjectMetadata
    {
    }

    public enum RuntimeObjectKind : ushort
    {
        Unspecified = 0,
        Tensor = 1,
        TokenBlock = 2,
        ImageTile = 3,
        FeatureMap = 4,
        ToolResult = 5,
        TraceSegment = 6,
        OpaqueBytes = 7,
        DocumentChunk = 8,
        AudioChunk = 9,
        VideoChunk = 10,
        RoutePlan = 11,
        CacheManifest = 12,
    }

    public enum MemoryLocationHint : ushort
    {
        Unspecified = 0,
        HostMemory = 1,
        DeviceMemory = 2,
        SharedMemory = 3,
        RemoteMemory = 4,
        MmapFile = 5,
        ObjectStore = 6,
    }

    public enum OwnershipHint : ushort
    {
        Unspecified = 0,
        ProducerOwned = 1,
        ConsumerOwned = 2,
        SessionOwned = 3,
        Borrowed = 4,
        TransferOnRef = 5,
        ReleaseOnDrop = 6,
    }

    public enum ObjectReleaseReason : ushort
    {
        Completed = 0,
        Cancelled = 1,
        Expired = 2,
        Replaced = 3,
        Invalidated = 4,
        OwnerClosed = 5,
        LeaseExpired = 6,
        ConformanceInjection = 7,
    }

    public enum CacheReuseScope : ushort
    {
        Operation = 0,
        Session = 1,
        Connection = 2,
        Global = 3,
        Tenant = 4,
        Profile = 5,
    }

    public enum CacheMissReason : ushort
    {
        Unknown = 0,
        NotFound = 1,
        Expired = 2,
        Invalidated = 3,
        SchemaMismatch = 4,
        ProducerUnavailable = 5,
        LeaseRequired = 6,
        PermissionDenied = 7,
    }

    /// <summary>Descriptor for a reusable runtime object.</summary>
    public readonly record struct ObjectDescriptorMetadata(
        ulong ObjectId,
        RuntimeObjectKind ObjectKind,
        RuntimeRole ProducerRole,
        RuntimeRole ConsumerRole,
        uint SessionId,
        ulong ByteSize,
        uint ComputeCostUnits,
        MemoryLocationHint MemoryLocationHint,
        OwnershipHint OwnershipHint,
        uint LifetimeHintMs,
        uint MetadataBytes) : IRuntimeObjectMetadata
    {
        public const int EncodedLength = 48;
    }

    /// <summary>Reference to a region of a versioned runtime object.</summary>
    public readonly record struct ObjectReferenceMetadata(
        ulong ObjectId,
        ulong OperationId,
        ulong ObjectVersion,
        ulong Offset,
        ulong Length,
        uint Flags,
        uint MetadataBytes) : IRuntimeObjectMetadata
    {
        public const int EncodedLength = 48;
    }

    /// <summary>Release metadata for a runtime object.</summary>
    public readonly record struct ObjectReleaseMetadata(
        ulong ObjectId,
        ulong OperationId,
        ObjectReleaseReason ReleaseReason,
        RuntimeRole SourceRole,
        byte Flags,
        uint DiagnosticBytes) : IRuntimeObjectMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>Patch or delta metadata for a runtime object region.</summary>
    public readonly record struct ObjectDeltaMetadata(
        ulong ObjectId,
        ulong DeltaSequence,
        ulong RegionOffset,
        uint RegionBytes,
        uint DeltaBytes,
        uint Flags,
        uint MetadataBytes) : IRuntimeObjectMetadata
    {
        public const int EncodedLength = 40;
    }

    /// <summary>Explicit reference to a reusable cache object.</summary>
    public readonly record struct CacheReferenceMetadata(
        uint CacheNamespace,
        ulong CacheKeyHi,
        ulong CacheKeyLo,
        ushort ProfileId,
        CacheReuseScope ReuseScope,
        ulong LeaseId,
        ulong ProducerTraceId,
        uint ExpirationHintMs,
        uint MetadataBytes,
        uint Flags) : IRuntimeObjectMetadata
    {
        public const int EncodedLength = 56;
    }

    /// <summary>Typed cache miss metadata with optional diagnostic bytes.</summary>
    public readonly record struct CacheMissMetadata(
        uint CacheNamespace,
        ulong CacheKeyHi,
        ulong CacheKeyLo,
        CacheMissReason MissReason,
        ushort ProfileId,
        uint DiagnosticBytes) : IRuntimeObjectMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>A decoded runtime-object metadata value and its declared tail bytes.</summary>
    public sealed class DecodedRuntimeObjectMetadata
    {
        public DecodedRuntimeObjectMetadata(IRuntimeObjectMetadata metadata, ReadOnlyMemory<byte> tail)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Tail = tail;
        }

        public IRuntimeObjectMetadata Metadata { get; }

        public ReadOnlyMemory<byte> Tail { get; }

        public T GetMetadata<T>()
            where T : struct, IRuntimeObjectMetadata
        {
            if (Metadata is T typed)
            {
                return typed;
            }

            throw new InvalidOperationException(
                "Decoded runtime object metadata is " + Metadata.GetType().Name + ", not " + typeof(T).Name + ".");
        }
    }
}
