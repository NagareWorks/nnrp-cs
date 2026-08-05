using System;
using System.Buffers.Binary;

namespace Nnrp.Core
{
    public sealed class NnrpSessionRecoveryTicket
    {
        private const int PrefixLength = 28;
        private const ushort EncodingVersion = 1;
        private const ushort ResumeFromOperationIdPresent = 1;

        private readonly byte[] resumeToken;

        private NnrpSessionRecoveryTicket(
            uint sessionId,
            byte[] resumeToken,
            ulong? resumeFromOperationId,
            uint resumeWindowMilliseconds)
        {
            if (sessionId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sessionId));
            }

            if (resumeToken == null || resumeToken.Length == 0)
            {
                throw new ArgumentException("A recovery ticket requires a non-empty runtime-issued token.", nameof(resumeToken));
            }

            SessionId = sessionId;
            this.resumeToken = (byte[])resumeToken.Clone();
            ResumeFromOperationId = resumeFromOperationId;
            ResumeWindowMilliseconds = resumeWindowMilliseconds;
        }

        public uint SessionId { get; }

        public ReadOnlyMemory<byte> ResumeToken => resumeToken;

        public ulong? ResumeFromOperationId { get; }

        public uint ResumeWindowMilliseconds { get; }

        public byte[] ToBytes()
        {
            var encoded = new byte[checked(PrefixLength + resumeToken.Length)];
            encoded[0] = (byte)'N';
            encoded[1] = (byte)'R';
            encoded[2] = (byte)'T';
            encoded[3] = (byte)'K';
            BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(4), EncodingVersion);
            BinaryPrimitives.WriteUInt16LittleEndian(
                encoded.AsSpan(6),
                ResumeFromOperationId.HasValue ? ResumeFromOperationIdPresent : (ushort)0);
            BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(8), SessionId);
            BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(12), checked((uint)resumeToken.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(16), ResumeWindowMilliseconds);
            BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(20), ResumeFromOperationId.GetValueOrDefault());
            resumeToken.CopyTo(encoded, PrefixLength);
            return encoded;
        }

        public static NnrpSessionRecoveryTicket FromBytes(ReadOnlySpan<byte> encoded)
        {
            if (encoded.Length < PrefixLength
                || encoded[0] != (byte)'N'
                || encoded[1] != (byte)'R'
                || encoded[2] != (byte)'T'
                || encoded[3] != (byte)'K')
            {
                throw new ArgumentException("Recovery ticket magic is invalid.", nameof(encoded));
            }

            if (BinaryPrimitives.ReadUInt16LittleEndian(encoded.Slice(4)) != EncodingVersion)
            {
                throw new ArgumentException("Recovery ticket version is unsupported.", nameof(encoded));
            }

            var flags = BinaryPrimitives.ReadUInt16LittleEndian(encoded.Slice(6));
            if ((flags & ~ResumeFromOperationIdPresent) != 0)
            {
                throw new ArgumentException("Recovery ticket reserved flags must be zero.", nameof(encoded));
            }

            var sessionId = BinaryPrimitives.ReadUInt32LittleEndian(encoded.Slice(8));
            var tokenLength = BinaryPrimitives.ReadUInt32LittleEndian(encoded.Slice(12));
            var resumeWindowMilliseconds = BinaryPrimitives.ReadUInt32LittleEndian(encoded.Slice(16));
            var resumeFromOperationId = BinaryPrimitives.ReadUInt64LittleEndian(encoded.Slice(20));
            if (sessionId == 0 || tokenLength == 0 || tokenLength > int.MaxValue)
            {
                throw new ArgumentException("Recovery ticket identity or token length is invalid.", nameof(encoded));
            }

            if (encoded.Length != checked(PrefixLength + (int)tokenLength))
            {
                throw new ArgumentException("Recovery ticket length is not canonical.", nameof(encoded));
            }

            return new NnrpSessionRecoveryTicket(
                sessionId,
                encoded.Slice(PrefixLength, (int)tokenLength).ToArray(),
                (flags & ResumeFromOperationIdPresent) != 0 ? resumeFromOperationId : (ulong?)null,
                resumeWindowMilliseconds);
        }
    }
}
