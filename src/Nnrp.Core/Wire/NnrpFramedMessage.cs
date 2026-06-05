using System;

namespace Nnrp.Core
{
    /// <summary>
    /// Transport-neutral view of one NNRP frame: common header, metadata bytes, and body bytes.
    /// </summary>
    public readonly struct NnrpFramedMessage
    {
        public NnrpFramedMessage(NnrpHeader header, ReadOnlyMemory<byte> metadata, ReadOnlyMemory<byte> body)
        {
            if (header.MetaLength != (uint)metadata.Length)
            {
                throw new ArgumentException("Metadata length must match the header metadata length.", nameof(metadata));
            }

            if (header.BodyLength != (uint)body.Length)
            {
                throw new ArgumentException("Body length must match the header body length.", nameof(body));
            }

            Header = header;
            Metadata = metadata;
            Body = body;
        }

        public NnrpHeader Header { get; }

        public ReadOnlyMemory<byte> Metadata { get; }

        public ReadOnlyMemory<byte> Body { get; }

        public int Length
        {
            get
            {
                if (!TryGetLength(out var length))
                {
                    throw new InvalidOperationException("Framed message length exceeds Int32.MaxValue.");
                }

                return length;
            }
        }

        public void CopyTo(Span<byte> destination)
        {
            var length = Length;
            if (destination.Length < length)
            {
                throw new ArgumentException($"Destination must be at least {length} bytes.", nameof(destination));
            }

            if (!TryCopyTo(destination, out _))
            {
                throw new InvalidOperationException("Framed message header is not writable.");
            }
        }

        public bool TryCopyTo(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (!TryGetLength(out var length) || destination.Length < length)
            {
                return false;
            }

            if (!Header.TryWrite(destination, out var headerBytes))
            {
                return false;
            }

            Metadata.Span.CopyTo(destination.Slice(headerBytes, Metadata.Length));
            Body.Span.CopyTo(destination.Slice(headerBytes + Metadata.Length, Body.Length));
            bytesWritten = length;
            return true;
        }

        public byte[] ToArray()
        {
            var payload = new byte[Length];
            CopyTo(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out NnrpFramedMessage message, out NnrpParseError error)
        {
            return TryParse(source, NnrpHeaderParseOptions.Default, out message, out error);
        }

        public static bool TryParse(
            ReadOnlyMemory<byte> source,
            NnrpHeaderParseOptions options,
            out NnrpFramedMessage message,
            out NnrpParseError error)
        {
            message = default;
            if (!NnrpHeader.TryParse(source.Span, options, out var header, out error))
            {
                return false;
            }

            if (!CheckedArithmetic.TryAdd((ulong)NnrpHeader.HeaderLength, header.MetaLength, out var headerAndMetadata)
                || !CheckedArithmetic.TryAdd(headerAndMetadata, header.BodyLength, out var totalLength)
                || totalLength > int.MaxValue)
            {
                error = NnrpParseError.MessageTooLarge;
                return false;
            }

            if (source.Length < (int)totalLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var metadataLength = (int)header.MetaLength;
            var bodyLength = (int)header.BodyLength;
            message = new NnrpFramedMessage(
                header,
                source.Slice(NnrpHeader.HeaderLength, metadataLength),
                source.Slice(NnrpHeader.HeaderLength + metadataLength, bodyLength));
            error = NnrpParseError.None;
            return true;
        }

        private bool TryGetLength(out int length)
        {
            length = 0;
            if (!CheckedArithmetic.TryAdd(NnrpHeader.HeaderLength, Metadata.Length, out var headerAndMetadata)
                || !CheckedArithmetic.TryAdd(headerAndMetadata, Body.Length, out length))
            {
                length = 0;
                return false;
            }

            return true;
        }
    }
}
