using System;
using System.Buffers.Binary;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class SessionRecoveryTicketTests
    {
        [Fact]
        public void RecoveryTicketRoundTripsTheCanonicalNrtkEncoding()
        {
            var encoded = TicketBytes(
                sessionId: 42,
                token: new byte[] { 7, 8, 9 },
                resumeFromOperationId: 99,
                resumeWindowMilliseconds: 120_000);

            var ticket = NnrpSessionRecoveryTicket.FromBytes(encoded);

            Assert.Equal((uint)42, ticket.SessionId);
            Assert.Equal(new byte[] { 7, 8, 9 }, ticket.ResumeToken.ToArray());
            Assert.Equal((ulong)99, ticket.ResumeFromOperationId);
            Assert.Equal((uint)120_000, ticket.ResumeWindowMilliseconds);
            Assert.Equal(encoded, ticket.ToBytes());

            encoded[28] = 0;
            Assert.Equal(new byte[] { 7, 8, 9 }, ticket.ResumeToken.ToArray());
        }

        [Fact]
        public void RecoveryTicketRejectsEveryNonCanonicalIdentityAndLayout()
        {
            var valid = TicketBytes(42, new byte[] { 7, 8, 9 }, null, 30_000);
            Assert.Null(NnrpSessionRecoveryTicket.FromBytes(valid).ResumeFromOperationId);

            Assert.Throws<ArgumentException>(() => NnrpSessionRecoveryTicket.FromBytes(valid.AsSpan(0, 27)));
            AssertMalformed(valid, bytes => bytes[0] = (byte)'X');
            AssertMalformed(valid, bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 2));
            AssertMalformed(valid, bytes => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 2));
            AssertMalformed(valid, bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 0));
            AssertMalformed(valid, bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0));
            AssertMalformed(valid, bytes => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 4));
        }

        private static void AssertMalformed(byte[] source, Action<byte[]> mutate)
        {
            var malformed = (byte[])source.Clone();
            mutate(malformed);
            Assert.Throws<ArgumentException>(() => NnrpSessionRecoveryTicket.FromBytes(malformed));
        }

        private static byte[] TicketBytes(
            uint sessionId,
            byte[] token,
            ulong? resumeFromOperationId,
            uint resumeWindowMilliseconds)
        {
            var encoded = new byte[checked(28 + token.Length)];
            encoded[0] = (byte)'N';
            encoded[1] = (byte)'R';
            encoded[2] = (byte)'T';
            encoded[3] = (byte)'K';
            BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(4), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(encoded.AsSpan(6), resumeFromOperationId.HasValue ? (ushort)1 : (ushort)0);
            BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(8), sessionId);
            BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(12), checked((uint)token.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(encoded.AsSpan(16), resumeWindowMilliseconds);
            BinaryPrimitives.WriteUInt64LittleEndian(encoded.AsSpan(20), resumeFromOperationId.GetValueOrDefault());
            token.CopyTo(encoded, 28);
            return encoded;
        }
    }
}
