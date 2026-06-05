using System;

namespace Nnrp.Core
{
    public readonly struct CachePutMessage
    {
        public CachePutMessage(NnrpHeader header, CachePutMetadata metadata, ReadOnlyMemory<byte> objectBytes)
        {
            if (header.MessageType != MessageType.CachePut
                || header.MetaLength != CachePutMetadata.MetadataLength
                || header.BodyLength != (uint)objectBytes.Length
                || metadata.ObjectBytes != (uint)objectBytes.Length)
            {
                throw new ArgumentException("Header/body lengths must match the current CachePut layout.", nameof(header));
            }

            Header = header;
            Metadata = metadata;
            ObjectBytes = objectBytes;
        }

        public NnrpHeader Header { get; }
        public CachePutMetadata Metadata { get; }
        public ReadOnlyMemory<byte> ObjectBytes { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            var metadata = new byte[CachePutMetadata.MetadataLength];
            if (!Metadata.TryWrite(metadata, out _))
            {
                throw new InvalidOperationException("CachePut metadata is invalid.");
            }

            return new NnrpFramedMessage(Header, metadata, ObjectBytes);
        }

        public byte[] ToArray() => ToFramedMessage().ToArray();

        public static bool TryParse(ReadOnlyMemory<byte> source, out CachePutMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            if (framed.Header.MessageType != MessageType.CachePut || framed.Header.MetaLength != CachePutMetadata.MetadataLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (!CachePutMetadata.TryParse(framed.Metadata.Span, out var metadata, out error)
                || metadata.ObjectBytes != (uint)framed.Body.Length)
            {
                error = error == NnrpParseError.None ? NnrpParseError.InvalidMessageLayout : error;
                return false;
            }

            message = new CachePutMessage(framed.Header, metadata, framed.Body);
            return true;
        }
    }
}
