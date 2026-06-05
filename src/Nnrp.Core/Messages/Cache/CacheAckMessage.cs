using System;

namespace Nnrp.Core
{
    public readonly struct CacheAckMessage
    {
        public CacheAckMessage(NnrpHeader header, CacheAckMetadata metadata)
        {
            SessionPatchMessage.ValidateHeader(header, MessageType.CacheAck, CacheAckMetadata.MetadataLength);
            Header = header;
            Metadata = metadata;
        }

        public NnrpHeader Header { get; }
        public CacheAckMetadata Metadata { get; }

        public NnrpFramedMessage ToFramedMessage() => new NnrpFramedMessage(Header, Metadata.ToArray(), Array.Empty<byte>());

        public byte[] ToArray() => ToFramedMessage().ToArray();

        public static bool TryParse(ReadOnlyMemory<byte> source, out CacheAckMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            if (!SessionPatchMessage.ValidateFramedMessage(framed, MessageType.CacheAck, CacheAckMetadata.MetadataLength, out error)
                || !CacheAckMetadata.TryParse(framed.Metadata.Span, out var metadata, out error))
            {
                return false;
            }

            message = new CacheAckMessage(framed.Header, metadata);
            return true;
        }
    }
}
