using System;

namespace Nnrp.Core
{
    public readonly struct CacheInvalidateMessage
    {
        public CacheInvalidateMessage(NnrpHeader header, CacheInvalidateMetadata metadata)
        {
            SessionPatchMessage.ValidateHeader(header, MessageType.CacheInvalidate, CacheInvalidateMetadata.MetadataLength);
            Header = header;
            Metadata = metadata;
        }

        public NnrpHeader Header { get; }
        public CacheInvalidateMetadata Metadata { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            var metadata = new byte[CacheInvalidateMetadata.MetadataLength];
            if (!Metadata.TryWrite(metadata, out _))
            {
                throw new InvalidOperationException("CacheInvalidate metadata is invalid.");
            }

            return new NnrpFramedMessage(Header, metadata, Array.Empty<byte>());
        }

        public byte[] ToArray() => ToFramedMessage().ToArray();

        public static bool TryParse(ReadOnlyMemory<byte> source, out CacheInvalidateMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            if (!SessionPatchMessage.ValidateFramedMessage(framed, MessageType.CacheInvalidate, CacheInvalidateMetadata.MetadataLength, out error)
                || !CacheInvalidateMetadata.TryParse(framed.Metadata.Span, out var metadata, out error))
            {
                return false;
            }

            message = new CacheInvalidateMessage(framed.Header, metadata);
            return true;
        }
    }
}
