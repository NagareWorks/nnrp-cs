using System;

namespace Nnrp.Core
{
    public readonly struct SessionOpenMessage
    {
        public SessionOpenMessage(NnrpHeader header, SessionOpenMetadata metadata, ReadOnlyMemory<byte> body)
        {
            SessionPatchMessage.ValidateHeader(header, MessageType.SessionOpen, SessionOpenMetadata.MetadataLength, metadata.BodyLength);
            if (body.Length != metadata.BodyLength)
            {
                throw new ArgumentException("Body length must match SESSION_OPEN metadata.", nameof(body));
            }

            Header = header;
            Metadata = metadata;
            Body = body;
        }

        public NnrpHeader Header { get; }

        public SessionOpenMetadata Metadata { get; }

        public ReadOnlyMemory<byte> Body { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            return new NnrpFramedMessage(Header, Metadata.ToArray(), Body);
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out SessionOpenMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out SessionOpenMessage message, out NnrpParseError error)
        {
            message = default;
            if (!SessionPatchMessage.ValidateFramedMessage(framed, MessageType.SessionOpen, SessionOpenMetadata.MetadataLength, out error)
                || !SessionOpenMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out error)
                || framed.Body.Length != metadata.BodyLength)
            {
                if (error == NnrpParseError.None)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                }

                return false;
            }

            message = new SessionOpenMessage(framed.Header, metadata, framed.Body);
            return true;
        }
    }

    public readonly struct SessionOpenAckMessage
    {
        public SessionOpenAckMessage(NnrpHeader header, SessionOpenAckMetadata metadata, ReadOnlyMemory<byte> body)
        {
            SessionPatchMessage.ValidateHeader(header, MessageType.SessionOpenAck, SessionOpenAckMetadata.MetadataLength, metadata.BodyLength);
            if (body.Length != metadata.BodyLength)
            {
                throw new ArgumentException("Body length must match SESSION_OPEN_ACK metadata.", nameof(body));
            }

            Header = header;
            Metadata = metadata;
            Body = body;
        }

        public NnrpHeader Header { get; }

        public SessionOpenAckMetadata Metadata { get; }

        public ReadOnlyMemory<byte> Body { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            return new NnrpFramedMessage(Header, Metadata.ToArray(), Body);
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out SessionOpenAckMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out SessionOpenAckMessage message, out NnrpParseError error)
        {
            message = default;
            if (!SessionPatchMessage.ValidateFramedMessage(framed, MessageType.SessionOpenAck, SessionOpenAckMetadata.MetadataLength, out error)
                || !SessionOpenAckMetadata.TryParse(framed.Metadata.Span, out var metadata, out error)
                || framed.Body.Length != metadata.BodyLength)
            {
                if (error == NnrpParseError.None)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                }

                return false;
            }

            message = new SessionOpenAckMessage(framed.Header, metadata, framed.Body);
            return true;
        }
    }

    public readonly struct SessionCloseMessage
    {
        public SessionCloseMessage(NnrpHeader header, SessionCloseMetadata metadata)
        {
            SessionPatchMessage.ValidateHeader(header, MessageType.SessionClose, SessionCloseMetadata.MetadataLength);
            Header = header;
            Metadata = metadata;
        }

        public NnrpHeader Header { get; }

        public SessionCloseMetadata Metadata { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            return new NnrpFramedMessage(Header, Metadata.ToArray(), Array.Empty<byte>());
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out SessionCloseMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out SessionCloseMessage message, out NnrpParseError error)
        {
            message = default;
            if (!SessionPatchMessage.ValidateFramedMessage(framed, MessageType.SessionClose, SessionCloseMetadata.MetadataLength, out error)
                || framed.Body.Length != 0
                || !SessionCloseMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out error))
            {
                if (error == NnrpParseError.None)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                }

                return false;
            }

            message = new SessionCloseMessage(framed.Header, metadata);
            return true;
        }
    }

    public readonly struct SessionCloseAckMessage
    {
        public SessionCloseAckMessage(NnrpHeader header, SessionCloseAckMetadata metadata)
        {
            SessionPatchMessage.ValidateHeader(header, MessageType.SessionCloseAck, SessionCloseAckMetadata.MetadataLength);
            Header = header;
            Metadata = metadata;
        }

        public NnrpHeader Header { get; }

        public SessionCloseAckMetadata Metadata { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            return new NnrpFramedMessage(Header, Metadata.ToArray(), Array.Empty<byte>());
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out SessionCloseAckMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out SessionCloseAckMessage message, out NnrpParseError error)
        {
            message = default;
            if (!SessionPatchMessage.ValidateFramedMessage(framed, MessageType.SessionCloseAck, SessionCloseAckMetadata.MetadataLength, out error)
                || framed.Body.Length != 0
                || !SessionCloseAckMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out error))
            {
                if (error == NnrpParseError.None)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                }

                return false;
            }

            message = new SessionCloseAckMessage(framed.Header, metadata);
            return true;
        }
    }
}
