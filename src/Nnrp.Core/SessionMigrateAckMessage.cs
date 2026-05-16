using System;

namespace Nnrp.Core
{
    public readonly struct SessionMigrateAckMessage
    {
        public SessionMigrateAckMessage(NnrpHeader header, SessionMigrateAckMetadata metadata)
        {
            SessionPatchMessage.ValidateHeader(header, MessageType.SessionMigrateAck, SessionMigrateAckMetadata.MetadataLength);

            Header = header;
            Metadata = metadata;
        }

        public NnrpHeader Header { get; }

        public SessionMigrateAckMetadata Metadata { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            return new NnrpFramedMessage(Header, Metadata.ToArray(), Array.Empty<byte>());
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out SessionMigrateAckMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out SessionMigrateAckMessage message, out NnrpParseError error)
        {
            message = default;
            if (!SessionPatchMessage.ValidateFramedMessage(framed, MessageType.SessionMigrateAck, SessionMigrateAckMetadata.MetadataLength, out error)
                || !SessionMigrateAckMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out error))
            {
                if (error == NnrpParseError.None)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                }

                return false;
            }

            message = new SessionMigrateAckMessage(framed.Header, metadata);
            return true;
        }
    }
}
