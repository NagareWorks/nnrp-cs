using System;

namespace Nnrp.Core
{
    public readonly struct ClientHelloMetadata : IEquatable<ClientHelloMetadata>
    {
        public const int MetadataLength = 64;

        public ClientHelloMetadata(
            uint minVersionMajor,
            uint maxVersionMajor,
            uint supportedWireFormatBitmap,
            uint supportedProfileBitmap,
            uint supportedPayloadKindBitmap,
            uint supportedCodecBitmap,
            uint supportedCompressionBitmap,
            uint supportedDTypeBitmap,
            uint supportedLayoutBitmap,
            uint cacheDigestBitmap,
            uint cacheObjectBitmap,
            uint cacheNamespaceCount,
            uint maxLaneCount,
            uint maxCacheEntries,
            uint maxCacheBytes,
            uint targetCadenceX100,
            uint latencyBudgetMilliseconds,
            uint qualityTier,
            uint degradePolicy,
            uint requestedSessionId,
            uint authBytes,
            uint controlExtensionBytes)
        {
            MinVersionMajor = minVersionMajor;
            MaxVersionMajor = maxVersionMajor;
            SupportedWireFormatBitmap = supportedWireFormatBitmap;
            SupportedProfileBitmap = supportedProfileBitmap;
            SupportedPayloadKindBitmap = supportedPayloadKindBitmap;
            SupportedCodecBitmap = supportedCodecBitmap;
            SupportedCompressionBitmap = supportedCompressionBitmap;
            SupportedDTypeBitmap = supportedDTypeBitmap;
            SupportedLayoutBitmap = supportedLayoutBitmap;
            CacheDigestBitmap = cacheDigestBitmap;
            CacheObjectBitmap = cacheObjectBitmap;
            CacheNamespaceCount = cacheNamespaceCount;
            MaxLaneCount = maxLaneCount;
            MaxCacheEntries = maxCacheEntries;
            MaxCacheBytes = maxCacheBytes;
            TargetCadenceX100 = targetCadenceX100;
            LatencyBudgetMilliseconds = latencyBudgetMilliseconds;
            QualityTier = qualityTier;
            DegradePolicy = degradePolicy;
            RequestedSessionId = requestedSessionId;
            AuthBytes = authBytes;
            ControlExtensionBytes = controlExtensionBytes;
        }

        public uint MinVersionMajor { get; }
        public uint MaxVersionMajor { get; }
        public uint SupportedWireFormatBitmap { get; }
        public uint SupportedProfileBitmap { get; }
        public uint SupportedPayloadKindBitmap { get; }
        public uint SupportedCodecBitmap { get; }
        public uint SupportedCompressionBitmap { get; }
        public uint SupportedDTypeBitmap { get; }
        public uint SupportedLayoutBitmap { get; }
        public uint CacheDigestBitmap { get; }
        public uint CacheObjectBitmap { get; }
        public uint CacheNamespaceCount { get; }
        public uint MaxLaneCount { get; }
        public uint MaxCacheEntries { get; }
        public uint MaxCacheBytes { get; }
        public uint TargetCadenceX100 { get; }
        public uint LatencyBudgetMilliseconds { get; }
        public uint QualityTier { get; }
        public uint DegradePolicy { get; }
        public uint RequestedSessionId { get; }
        public uint AuthBytes { get; }
        public uint ControlExtensionBytes { get; }

        public void Write(Span<byte> destination)
        {
            if (!TryWrite(destination, out _))
            {
                throw new ArgumentException($"Destination must be at least {MetadataLength} bytes.", nameof(destination));
            }
        }

        public bool TryWrite(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Length < MetadataLength)
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteByte(checked((byte)MinVersionMajor))
                || !writer.TryWriteByte(checked((byte)MaxVersionMajor))
                || !writer.TryWriteUInt16(checked((ushort)SupportedWireFormatBitmap))
                || !writer.TryWriteUInt32(SupportedProfileBitmap)
                || !writer.TryWriteUInt32(SupportedPayloadKindBitmap)
                || !writer.TryWriteUInt32(SupportedCodecBitmap)
                || !writer.TryWriteUInt32(SupportedCompressionBitmap)
                || !writer.TryWriteUInt32(SupportedDTypeBitmap)
                || !writer.TryWriteUInt32(SupportedLayoutBitmap)
                || !writer.TryWriteUInt16(checked((ushort)CacheDigestBitmap))
                || !writer.TryWriteUInt16(checked((ushort)CacheObjectBitmap))
                || !writer.TryWriteUInt16(checked((ushort)CacheNamespaceCount))
                || !writer.TryWriteUInt16(checked((ushort)MaxLaneCount))
                || !writer.TryWriteUInt32(MaxCacheEntries)
                || !writer.TryWriteUInt32(MaxCacheBytes)
                || !writer.TryWriteUInt16(checked((ushort)TargetCadenceX100))
                || !writer.TryWriteUInt16(checked((ushort)LatencyBudgetMilliseconds))
                || !writer.TryWriteUInt16(checked((ushort)QualityTier))
                || !writer.TryWriteUInt16(checked((ushort)DegradePolicy))
                || !writer.TryWriteUInt32(RequestedSessionId)
                || !writer.TryWriteUInt32(AuthBytes)
                || !writer.TryWriteUInt32(ControlExtensionBytes))
            {
                return false;
            }

            bytesWritten = writer.Offset;
            return bytesWritten == MetadataLength;
        }

        public byte[] ToArray()
        {
            var payload = new byte[MetadataLength];
            Write(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out ClientHelloMetadata metadata)
        {
            return TryParse(source, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out ClientHelloMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadByte(out var minVersionMajor)
                || !reader.TryReadByte(out var maxVersionMajor)
                || !reader.TryReadUInt16(out var supportedWireFormatBitmap)
                || !reader.TryReadUInt32(out var supportedProfileBitmap)
                || !reader.TryReadUInt32(out var supportedPayloadKindBitmap)
                || !reader.TryReadUInt32(out var supportedCodecBitmap)
                || !reader.TryReadUInt32(out var supportedCompressionBitmap)
                || !reader.TryReadUInt32(out var supportedDTypeBitmap)
                || !reader.TryReadUInt32(out var supportedLayoutBitmap)
                || !reader.TryReadUInt16(out var cacheDigestBitmap)
                || !reader.TryReadUInt16(out var cacheObjectBitmap)
                || !reader.TryReadUInt16(out var cacheNamespaceCount)
                || !reader.TryReadUInt16(out var maxLaneCount)
                || !reader.TryReadUInt32(out var maxCacheEntries)
                || !reader.TryReadUInt32(out var maxCacheBytes)
                || !reader.TryReadUInt16(out var targetCadenceX100)
                || !reader.TryReadUInt16(out var latencyBudgetMilliseconds)
                || !reader.TryReadUInt16(out var qualityTier)
                || !reader.TryReadUInt16(out var degradePolicy)
                || !reader.TryReadUInt32(out var requestedSessionId)
                || !reader.TryReadUInt32(out var authBytes)
                || !reader.TryReadUInt32(out var controlExtensionBytes))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            metadata = new ClientHelloMetadata(
                minVersionMajor,
                maxVersionMajor,
                supportedWireFormatBitmap,
                supportedProfileBitmap,
                supportedPayloadKindBitmap,
                supportedCodecBitmap,
                supportedCompressionBitmap,
                supportedDTypeBitmap,
                supportedLayoutBitmap,
                cacheDigestBitmap,
                cacheObjectBitmap,
                cacheNamespaceCount,
                maxLaneCount,
                maxCacheEntries,
                maxCacheBytes,
                targetCadenceX100,
                latencyBudgetMilliseconds,
                qualityTier,
                degradePolicy,
                requestedSessionId,
                authBytes,
                controlExtensionBytes);
            return true;
        }

        public bool Equals(ClientHelloMetadata other)
        {
            return MinVersionMajor == other.MinVersionMajor
                && MaxVersionMajor == other.MaxVersionMajor
                && SupportedWireFormatBitmap == other.SupportedWireFormatBitmap
                && SupportedProfileBitmap == other.SupportedProfileBitmap
                && SupportedPayloadKindBitmap == other.SupportedPayloadKindBitmap
                && SupportedCodecBitmap == other.SupportedCodecBitmap
                && SupportedCompressionBitmap == other.SupportedCompressionBitmap
                && SupportedDTypeBitmap == other.SupportedDTypeBitmap
                && SupportedLayoutBitmap == other.SupportedLayoutBitmap
                && CacheDigestBitmap == other.CacheDigestBitmap
                && CacheObjectBitmap == other.CacheObjectBitmap
                && CacheNamespaceCount == other.CacheNamespaceCount
                && MaxLaneCount == other.MaxLaneCount
                && MaxCacheEntries == other.MaxCacheEntries
                && MaxCacheBytes == other.MaxCacheBytes
                && TargetCadenceX100 == other.TargetCadenceX100
                && LatencyBudgetMilliseconds == other.LatencyBudgetMilliseconds
                && QualityTier == other.QualityTier
                && DegradePolicy == other.DegradePolicy
                && RequestedSessionId == other.RequestedSessionId
                && AuthBytes == other.AuthBytes
                && ControlExtensionBytes == other.ControlExtensionBytes;
        }

        public override bool Equals(object obj)
        {
            return obj is ClientHelloMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = MinVersionMajor.GetHashCode();
                hash = (hash * 397) ^ MaxVersionMajor.GetHashCode();
                hash = (hash * 397) ^ SupportedWireFormatBitmap.GetHashCode();
                hash = (hash * 397) ^ SupportedProfileBitmap.GetHashCode();
                hash = (hash * 397) ^ SupportedPayloadKindBitmap.GetHashCode();
                hash = (hash * 397) ^ SupportedCodecBitmap.GetHashCode();
                hash = (hash * 397) ^ SupportedCompressionBitmap.GetHashCode();
                hash = (hash * 397) ^ SupportedDTypeBitmap.GetHashCode();
                hash = (hash * 397) ^ SupportedLayoutBitmap.GetHashCode();
                hash = (hash * 397) ^ CacheDigestBitmap.GetHashCode();
                hash = (hash * 397) ^ CacheObjectBitmap.GetHashCode();
                hash = (hash * 397) ^ CacheNamespaceCount.GetHashCode();
                hash = (hash * 397) ^ MaxLaneCount.GetHashCode();
                hash = (hash * 397) ^ MaxCacheEntries.GetHashCode();
                hash = (hash * 397) ^ MaxCacheBytes.GetHashCode();
                hash = (hash * 397) ^ TargetCadenceX100.GetHashCode();
                hash = (hash * 397) ^ LatencyBudgetMilliseconds.GetHashCode();
                hash = (hash * 397) ^ QualityTier.GetHashCode();
                hash = (hash * 397) ^ DegradePolicy.GetHashCode();
                hash = (hash * 397) ^ RequestedSessionId.GetHashCode();
                hash = (hash * 397) ^ AuthBytes.GetHashCode();
                hash = (hash * 397) ^ ControlExtensionBytes.GetHashCode();
                return hash;
            }
        }
    }
}
