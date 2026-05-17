using System;

namespace Nnrp.Core
{
    public readonly struct TransportProbeAckMessage
    {
        public TransportProbeAckMessage(NnrpHeader header, TransportProbeAckMetadata metadata)
        {
            SessionPatchMessage.ValidateHeader(header, MessageType.TransportProbeAck, TransportProbeAckMetadata.MetadataLength);

            Header = header;
            Metadata = metadata;
        }

        public NnrpHeader Header { get; }

        public TransportProbeAckMetadata Metadata { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            return new NnrpFramedMessage(Header, Metadata.ToArray(), Array.Empty<byte>());
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out TransportProbeAckMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out TransportProbeAckMessage message, out NnrpParseError error)
        {
            message = default;
            if (!SessionPatchMessage.ValidateFramedMessage(framed, MessageType.TransportProbeAck, TransportProbeAckMetadata.MetadataLength, out error)
                || framed.Body.Length != 0
                || !TransportProbeAckMetadata.TryParse(framed.Metadata.Span, out var metadata, out error))
            {
                if (error == NnrpParseError.None)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                }

                return false;
            }

            message = new TransportProbeAckMessage(framed.Header, metadata);
            return true;
        }
    }
}
