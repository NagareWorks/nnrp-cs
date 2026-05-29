using System;

namespace Nnrp.Core
{
    public readonly struct ServerHelloAckMetadata : IEquatable<ServerHelloAckMetadata>
    {
        public const int MetadataLength = 80;

        public ServerHelloAckMetadata(
            uint selectedVersionMajor,
            uint selectedWireFormat,
            uint authStatus,
            uint reserved0,
            uint sessionId,
            uint acceptedProfileBitmap,
            uint acceptedPayloadKindBitmap,
            uint acceptedCodecBitmap,
            uint acceptedCompressionBitmap,
            uint acceptedDTypeBitmap,
            uint acceptedLayoutBitmap,
            uint cacheDigestBitmap,
            uint cacheObjectBitmap,
            uint maxCacheEntries,
            uint maxCacheBytes,
            uint maxLaneCount,
            uint maxConcurrentFrames,
            uint targetCadenceX100,
            uint latencyBudgetMilliseconds,
            uint qualityTier,
            uint degradePolicy,
            uint maxBodyBytes,
            uint tokenTtlMilliseconds,
            uint retryAfterMilliseconds,
            uint controlExtensionBytes,
            uint serverFlags)
        {
            SelectedVersionMajor = selectedVersionMajor;
            SelectedWireFormat = selectedWireFormat;
            AuthStatus = authStatus;
            Reserved0 = reserved0;
            SessionId = sessionId;
            AcceptedProfileBitmap = acceptedProfileBitmap;
            AcceptedPayloadKindBitmap = acceptedPayloadKindBitmap;
            AcceptedCodecBitmap = acceptedCodecBitmap;
            AcceptedCompressionBitmap = acceptedCompressionBitmap;
            AcceptedDTypeBitmap = acceptedDTypeBitmap;
            AcceptedLayoutBitmap = acceptedLayoutBitmap;
            CacheDigestBitmap = cacheDigestBitmap;
            CacheObjectBitmap = cacheObjectBitmap;
            MaxCacheEntries = maxCacheEntries;
            MaxCacheBytes = maxCacheBytes;
            MaxLaneCount = maxLaneCount;
            MaxConcurrentFrames = maxConcurrentFrames;
            TargetCadenceX100 = targetCadenceX100;
            LatencyBudgetMilliseconds = latencyBudgetMilliseconds;
            QualityTier = qualityTier;
            DegradePolicy = degradePolicy;
            MaxBodyBytes = maxBodyBytes;
            TokenTtlMilliseconds = tokenTtlMilliseconds;
            RetryAfterMilliseconds = retryAfterMilliseconds;
            ControlExtensionBytes = controlExtensionBytes;
            ServerFlags = serverFlags;
        }

        public uint SelectedVersionMajor { get; }
        public uint SelectedWireFormat { get; }
        public uint AuthStatus { get; }
        public uint Reserved0 { get; }
        public uint SessionId { get; }
        public uint AcceptedProfileBitmap { get; }
        public uint AcceptedPayloadKindBitmap { get; }
        public uint AcceptedCodecBitmap { get; }
        public uint AcceptedCompressionBitmap { get; }
        public uint AcceptedDTypeBitmap { get; }
        public uint AcceptedLayoutBitmap { get; }
        public uint CacheDigestBitmap { get; }
        public uint CacheObjectBitmap { get; }
        public uint MaxCacheEntries { get; }
        public uint MaxCacheBytes { get; }
        public uint MaxLaneCount { get; }
        public uint MaxConcurrentFrames { get; }
        public uint TargetCadenceX100 { get; }
        public uint LatencyBudgetMilliseconds { get; }
        public uint QualityTier { get; }
        public uint DegradePolicy { get; }
        public uint MaxBodyBytes { get; }
        public uint TokenTtlMilliseconds { get; }
        public uint RetryAfterMilliseconds { get; }
        public uint ControlExtensionBytes { get; }
        public uint ServerFlags { get; }

        public uint CacheEnabled => CacheDigestBitmap != 0 || CacheObjectBitmap != 0 ? 1u : 0u;

        public ServerHelloAckMetadata WithControlExtensionBytes(uint controlExtensionBytes)
        {
            return new ServerHelloAckMetadata(
                SelectedVersionMajor,
                SelectedWireFormat,
                AuthStatus,
                Reserved0,
                SessionId,
                AcceptedProfileBitmap,
                AcceptedPayloadKindBitmap,
                AcceptedCodecBitmap,
                AcceptedCompressionBitmap,
                AcceptedDTypeBitmap,
                AcceptedLayoutBitmap,
                CacheDigestBitmap,
                CacheObjectBitmap,
                MaxCacheEntries,
                MaxCacheBytes,
                MaxLaneCount,
                MaxConcurrentFrames,
                TargetCadenceX100,
                LatencyBudgetMilliseconds,
                QualityTier,
                DegradePolicy,
                MaxBodyBytes,
                TokenTtlMilliseconds,
                RetryAfterMilliseconds,
                controlExtensionBytes,
                ServerFlags);
        }

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
            if (!writer.TryWriteByte(checked((byte)SelectedVersionMajor))
                || !writer.TryWriteByte(checked((byte)SelectedWireFormat))
                || !writer.TryWriteByte(checked((byte)AuthStatus))
                || !writer.TryWriteByte(checked((byte)Reserved0))
                || !writer.TryWriteUInt32(SessionId)
                || !writer.TryWriteUInt32(AcceptedProfileBitmap)
                || !writer.TryWriteUInt32(AcceptedPayloadKindBitmap)
                || !writer.TryWriteUInt32(AcceptedCodecBitmap)
                || !writer.TryWriteUInt32(AcceptedCompressionBitmap)
                || !writer.TryWriteUInt32(AcceptedDTypeBitmap)
                || !writer.TryWriteUInt32(AcceptedLayoutBitmap)
                || !writer.TryWriteUInt32(CacheDigestBitmap)
                || !writer.TryWriteUInt32(CacheObjectBitmap)
                || !writer.TryWriteUInt32(MaxCacheEntries)
                || !writer.TryWriteUInt32(MaxCacheBytes)
                || !writer.TryWriteUInt16(checked((ushort)MaxLaneCount))
                || !writer.TryWriteUInt16(checked((ushort)MaxConcurrentFrames))
                || !writer.TryWriteUInt16(checked((ushort)TargetCadenceX100))
                || !writer.TryWriteUInt16(checked((ushort)LatencyBudgetMilliseconds))
                || !writer.TryWriteUInt16(checked((ushort)QualityTier))
                || !writer.TryWriteUInt16(checked((ushort)DegradePolicy))
                || !writer.TryWriteUInt32(MaxBodyBytes)
                || !writer.TryWriteUInt32(TokenTtlMilliseconds)
                || !writer.TryWriteUInt32(RetryAfterMilliseconds)
                || !writer.TryWriteUInt32(ControlExtensionBytes)
                || !writer.TryWriteUInt32(ServerFlags))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out ServerHelloAckMetadata metadata)
        {
            return TryParse(source, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out ServerHelloAckMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadByte(out var selectedVersionMajor)
                || !reader.TryReadByte(out var selectedWireFormat)
                || !reader.TryReadByte(out var authStatus)
                || !reader.TryReadByte(out var reserved0)
                || !reader.TryReadUInt32(out var sessionId)
                || !reader.TryReadUInt32(out var acceptedProfileBitmap)
                || !reader.TryReadUInt32(out var acceptedPayloadKindBitmap)
                || !reader.TryReadUInt32(out var acceptedCodecBitmap)
                || !reader.TryReadUInt32(out var acceptedCompressionBitmap)
                || !reader.TryReadUInt32(out var acceptedDTypeBitmap)
                || !reader.TryReadUInt32(out var acceptedLayoutBitmap)
                || !reader.TryReadUInt32(out var cacheDigestBitmap)
                || !reader.TryReadUInt32(out var cacheObjectBitmap)
                || !reader.TryReadUInt32(out var maxCacheEntries)
                || !reader.TryReadUInt32(out var maxCacheBytes)
                || !reader.TryReadUInt16(out var maxLaneCount)
                || !reader.TryReadUInt16(out var maxConcurrentFrames)
                || !reader.TryReadUInt16(out var targetCadenceX100)
                || !reader.TryReadUInt16(out var latencyBudgetMilliseconds)
                || !reader.TryReadUInt16(out var qualityTier)
                || !reader.TryReadUInt16(out var degradePolicy)
                || !reader.TryReadUInt32(out var maxBodyBytes)
                || !reader.TryReadUInt32(out var tokenTtlMilliseconds)
                || !reader.TryReadUInt32(out var retryAfterMilliseconds)
                || !reader.TryReadUInt32(out var controlExtensionBytes)
                || !reader.TryReadUInt32(out var serverFlags))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            metadata = new ServerHelloAckMetadata(
                selectedVersionMajor,
                selectedWireFormat,
                authStatus,
                reserved0,
                sessionId,
                acceptedProfileBitmap,
                acceptedPayloadKindBitmap,
                acceptedCodecBitmap,
                acceptedCompressionBitmap,
                acceptedDTypeBitmap,
                acceptedLayoutBitmap,
                cacheDigestBitmap,
                cacheObjectBitmap,
                maxCacheEntries,
                maxCacheBytes,
                maxLaneCount,
                maxConcurrentFrames,
                targetCadenceX100,
                latencyBudgetMilliseconds,
                qualityTier,
                degradePolicy,
                maxBodyBytes,
                tokenTtlMilliseconds,
                retryAfterMilliseconds,
                controlExtensionBytes,
                serverFlags);
            return true;
        }

        public bool Equals(ServerHelloAckMetadata other)
        {
            return SelectedVersionMajor == other.SelectedVersionMajor
                && SelectedWireFormat == other.SelectedWireFormat
                && AuthStatus == other.AuthStatus
                && Reserved0 == other.Reserved0
                && SessionId == other.SessionId
                && AcceptedProfileBitmap == other.AcceptedProfileBitmap
                && AcceptedPayloadKindBitmap == other.AcceptedPayloadKindBitmap
                && AcceptedCodecBitmap == other.AcceptedCodecBitmap
                && AcceptedCompressionBitmap == other.AcceptedCompressionBitmap
                && AcceptedDTypeBitmap == other.AcceptedDTypeBitmap
                && AcceptedLayoutBitmap == other.AcceptedLayoutBitmap
                && CacheDigestBitmap == other.CacheDigestBitmap
                && CacheObjectBitmap == other.CacheObjectBitmap
                && MaxCacheEntries == other.MaxCacheEntries
                && MaxCacheBytes == other.MaxCacheBytes
                && MaxLaneCount == other.MaxLaneCount
                && MaxConcurrentFrames == other.MaxConcurrentFrames
                && TargetCadenceX100 == other.TargetCadenceX100
                && LatencyBudgetMilliseconds == other.LatencyBudgetMilliseconds
                && QualityTier == other.QualityTier
                && DegradePolicy == other.DegradePolicy
                && MaxBodyBytes == other.MaxBodyBytes
                && TokenTtlMilliseconds == other.TokenTtlMilliseconds
                && RetryAfterMilliseconds == other.RetryAfterMilliseconds
                && ControlExtensionBytes == other.ControlExtensionBytes
                && ServerFlags == other.ServerFlags;
        }

        public override bool Equals(object obj)
        {
            return obj is ServerHelloAckMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SelectedVersionMajor.GetHashCode();
                hash = (hash * 397) ^ SelectedWireFormat.GetHashCode();
                hash = (hash * 397) ^ AuthStatus.GetHashCode();
                hash = (hash * 397) ^ Reserved0.GetHashCode();
                hash = (hash * 397) ^ SessionId.GetHashCode();
                hash = (hash * 397) ^ AcceptedProfileBitmap.GetHashCode();
                hash = (hash * 397) ^ AcceptedPayloadKindBitmap.GetHashCode();
                hash = (hash * 397) ^ AcceptedCodecBitmap.GetHashCode();
                hash = (hash * 397) ^ AcceptedCompressionBitmap.GetHashCode();
                hash = (hash * 397) ^ AcceptedDTypeBitmap.GetHashCode();
                hash = (hash * 397) ^ AcceptedLayoutBitmap.GetHashCode();
                hash = (hash * 397) ^ CacheDigestBitmap.GetHashCode();
                hash = (hash * 397) ^ CacheObjectBitmap.GetHashCode();
                hash = (hash * 397) ^ MaxCacheEntries.GetHashCode();
                hash = (hash * 397) ^ MaxCacheBytes.GetHashCode();
                hash = (hash * 397) ^ MaxLaneCount.GetHashCode();
                hash = (hash * 397) ^ MaxConcurrentFrames.GetHashCode();
                hash = (hash * 397) ^ TargetCadenceX100.GetHashCode();
                hash = (hash * 397) ^ LatencyBudgetMilliseconds.GetHashCode();
                hash = (hash * 397) ^ QualityTier.GetHashCode();
                hash = (hash * 397) ^ DegradePolicy.GetHashCode();
                hash = (hash * 397) ^ MaxBodyBytes.GetHashCode();
                hash = (hash * 397) ^ TokenTtlMilliseconds.GetHashCode();
                hash = (hash * 397) ^ RetryAfterMilliseconds.GetHashCode();
                hash = (hash * 397) ^ ControlExtensionBytes.GetHashCode();
                hash = (hash * 397) ^ ServerFlags.GetHashCode();
                return hash;
            }
        }
    }
}
