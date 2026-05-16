using System;

namespace Nnrp.Core
{
    public readonly struct SessionMigrateMessage
    {
        public SessionMigrateMessage(NnrpHeader header, SessionMigrateMetadata metadata)
        {
            SessionPatchMessage.ValidateHeader(header, MessageType.SessionMigrate, SessionMigrateMetadata.MetadataLength);

            Header = header;
            Metadata = metadata;
        }

        public NnrpHeader Header { get; }

        public SessionMigrateMetadata Metadata { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            return new NnrpFramedMessage(Header, Metadata.ToArray(), Array.Empty<byte>());
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out SessionMigrateMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out SessionMigrateMessage message, out NnrpParseError error)
        {
            message = default;
            if (!SessionPatchMessage.ValidateFramedMessage(framed, MessageType.SessionMigrate, SessionMigrateMetadata.MetadataLength, out error)
                || !SessionMigrateMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out error))
            {
                if (error == NnrpParseError.None)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                }

                return false;
            }

            message = new SessionMigrateMessage(framed.Header, metadata);
            return true;
        }
    }
}
