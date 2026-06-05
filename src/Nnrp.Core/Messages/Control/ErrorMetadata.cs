using System;

namespace Nnrp.Core
{
    public readonly struct ErrorMetadata : IEquatable<ErrorMetadata>
    {
        public const int MetadataLength = 8 * sizeof(uint);

        public ErrorMetadata(
            ErrorCode errorCode,
            NnrpErrorScope errorScope,
            bool isFatal,
            uint retryAfterMilliseconds,
            uint relatedSessionId,
            uint relatedFrameId,
            uint relatedViewId,
            uint diagnosticBytes)
        {
            ErrorCode = errorCode;
            ErrorScope = errorScope;
            IsFatal = isFatal;
            RetryAfterMilliseconds = retryAfterMilliseconds;
            RelatedSessionId = relatedSessionId;
            RelatedFrameId = relatedFrameId;
            RelatedViewId = relatedViewId;
            DiagnosticBytes = diagnosticBytes;
        }

        public ErrorCode ErrorCode { get; }
        public NnrpErrorScope ErrorScope { get; }
        public bool IsFatal { get; }
        public uint RetryAfterMilliseconds { get; }
        public uint RelatedSessionId { get; }
        public uint RelatedFrameId { get; }
        public uint RelatedViewId { get; }
        public uint DiagnosticBytes { get; }

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
            if (!writer.TryWriteUInt32((uint)ErrorCode)
                || !writer.TryWriteUInt32((uint)ErrorScope)
                || !writer.TryWriteUInt32(IsFatal ? 1u : 0u)
                || !writer.TryWriteUInt32(RetryAfterMilliseconds)
                || !writer.TryWriteUInt32(RelatedSessionId)
                || !writer.TryWriteUInt32(RelatedFrameId)
                || !writer.TryWriteUInt32(RelatedViewId)
                || !writer.TryWriteUInt32(DiagnosticBytes))
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

        public static bool TryParse(ReadOnlySpan<byte> source, out ErrorMetadata metadata)
        {
            return TryParse(source, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out ErrorMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt32(out var errorCode)
                || !reader.TryReadUInt32(out var errorScope)
                || !reader.TryReadUInt32(out var isFatal)
                || !reader.TryReadUInt32(out var retryAfterMilliseconds)
                || !reader.TryReadUInt32(out var relatedSessionId)
                || !reader.TryReadUInt32(out var relatedFrameId)
                || !reader.TryReadUInt32(out var relatedViewId)
                || !reader.TryReadUInt32(out var diagnosticBytes))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            metadata = new ErrorMetadata(
                (ErrorCode)errorCode,
                (NnrpErrorScope)errorScope,
                isFatal != 0,
                retryAfterMilliseconds,
                relatedSessionId,
                relatedFrameId,
                relatedViewId,
                diagnosticBytes);
            return true;
        }

        public bool Equals(ErrorMetadata other)
        {
            return ErrorCode == other.ErrorCode
                && ErrorScope == other.ErrorScope
                && IsFatal == other.IsFatal
                && RetryAfterMilliseconds == other.RetryAfterMilliseconds
                && RelatedSessionId == other.RelatedSessionId
                && RelatedFrameId == other.RelatedFrameId
                && RelatedViewId == other.RelatedViewId
                && DiagnosticBytes == other.DiagnosticBytes;
        }

        public override bool Equals(object obj)
        {
            return obj is ErrorMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ErrorCode.GetHashCode();
                hash = (hash * 397) ^ ErrorScope.GetHashCode();
                hash = (hash * 397) ^ IsFatal.GetHashCode();
                hash = (hash * 397) ^ RetryAfterMilliseconds.GetHashCode();
                hash = (hash * 397) ^ RelatedSessionId.GetHashCode();
                hash = (hash * 397) ^ RelatedFrameId.GetHashCode();
                hash = (hash * 397) ^ RelatedViewId.GetHashCode();
                hash = (hash * 397) ^ DiagnosticBytes.GetHashCode();
                return hash;
            }
        }
    }
}
