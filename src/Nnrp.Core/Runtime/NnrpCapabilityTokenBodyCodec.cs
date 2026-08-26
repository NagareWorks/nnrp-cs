using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Nnrp.Runtime
{
    internal static class NnrpCapabilityTokenBodyCodec
    {
        internal const string ErrorCode = "NNRP_CONTROL_CAPABILITY_BODY_INVALID";

        internal static byte[] Encode(IEnumerable<string> tokens)
        {
            if (tokens == null)
            {
                throw new ArgumentNullException(nameof(tokens));
            }

            var ordered = new List<string>(tokens);
            foreach (var token in ordered)
            {
                ValidateToken(token, nameof(tokens));
            }

            ordered.Sort(StringComparer.Ordinal);
            for (var index = 1; index < ordered.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(ordered[index - 1], ordered[index]))
                {
                    throw Invalid("capability tokens must be unique", nameof(tokens));
                }
            }

            var encodedLength = 0;
            foreach (var token in ordered)
            {
                int tokenLength = Encoding.ASCII.GetByteCount(token);
                if (tokenLength > ushort.MaxValue)
                {
                    throw Invalid("capability token exceeds the u16 length range", nameof(tokens));
                }

                encodedLength = checked(encodedLength + sizeof(ushort) + tokenLength);
            }

            var encoded = new byte[encodedLength];
            var offset = 0;
            foreach (var token in ordered)
            {
                int tokenLength = Encoding.ASCII.GetByteCount(token);
                BinaryPrimitives.WriteUInt16LittleEndian(
                    encoded.AsSpan(offset, sizeof(ushort)),
                    checked((ushort)tokenLength));
                offset += sizeof(ushort);
                Encoding.ASCII.GetBytes(token.AsSpan(), encoded.AsSpan(offset, tokenLength));
                offset += tokenLength;
            }

            return encoded;
        }

        internal static IReadOnlyList<string> Decode(ReadOnlySpan<byte> body, ushort expectedCount)
        {
            if (expectedCount == 0)
            {
                if (body.IsEmpty)
                {
                    return Array.Empty<string>();
                }

                throw Invalid("zero capability count requires an empty body", nameof(body));
            }

            if (body.IsEmpty)
            {
                throw Invalid("non-zero capability count requires a non-empty body", nameof(body));
            }

            var tokens = new List<string>(expectedCount);
            var offset = 0;
            string? previous = null;
            while (offset < body.Length)
            {
                if (body.Length - offset < sizeof(ushort))
                {
                    throw Invalid("capability entry is missing its token length", nameof(body));
                }

                int tokenLength = BinaryPrimitives.ReadUInt16LittleEndian(
                    body.Slice(offset, sizeof(ushort)));
                offset += sizeof(ushort);
                if (tokenLength == 0)
                {
                    throw Invalid("capability token length must be non-zero", nameof(body));
                }

                if (tokenLength > body.Length - offset)
                {
                    throw Invalid("capability token exceeds the declared body", nameof(body));
                }

                ReadOnlySpan<byte> tokenBytes = body.Slice(offset, tokenLength);
                ValidateToken(tokenBytes, nameof(body));
                string token = Encoding.ASCII.GetString(tokenBytes.ToArray());
                if (previous != null)
                {
                    int ordering = StringComparer.Ordinal.Compare(previous, token);
                    if (ordering == 0)
                    {
                        throw Invalid("capability tokens must be unique", nameof(body));
                    }

                    if (ordering > 0)
                    {
                        throw Invalid(
                            "capability tokens must use canonical byte order",
                            nameof(body));
                    }
                }

                tokens.Add(token);
                previous = token;
                offset += tokenLength;
            }

            if (tokens.Count != expectedCount)
            {
                throw Invalid(
                    $"capability count declares {expectedCount} entries but received {tokens.Count}",
                    nameof(body));
            }

            return tokens;
        }

        private static void ValidateToken(string token, string parameterName)
        {
            if (token == null)
            {
                throw Invalid("capability token must not be null", parameterName);
            }

            if (token.Length == 0)
            {
                throw Invalid("capability token length must be non-zero", parameterName);
            }

            foreach (char value in token)
            {
                if (value > 0x7f || !IsTokenByte((byte)value))
                {
                    throw Invalid(
                        "capability token must use canonical lowercase ASCII spelling",
                        parameterName);
                }
            }
        }

        private static void ValidateToken(ReadOnlySpan<byte> token, string parameterName)
        {
            foreach (byte value in token)
            {
                if (!IsTokenByte(value))
                {
                    throw Invalid(
                        "capability token must use canonical lowercase ASCII spelling",
                        parameterName);
                }
            }
        }

        private static bool IsTokenByte(byte value) =>
            (value >= (byte)'a' && value <= (byte)'z')
            || (value >= (byte)'0' && value <= (byte)'9')
            || value == (byte)'.'
            || value == (byte)'_'
            || value == (byte)'-';

        private static ArgumentException Invalid(string message, string parameterName) =>
            new($"{ErrorCode}: {message}.", parameterName);
    }
}
