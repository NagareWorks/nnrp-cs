using System;

namespace Nnrp.Core
{
    public readonly struct TransportProbeMessage
    {
        public TransportProbeMessage(NnrpHeader header, TransportProbeMetadata metadata, ReadOnlyMemory<byte> payload)
        {
            if (header.MessageType != MessageType.TransportProbe)
            {
                throw new ArgumentException("Header message type must be TransportProbe.", nameof(header));
            }

            if (header.MetaLength != TransportProbeMetadata.MetadataLength)
            {
                throw new ArgumentException("Header metadata length must match TransportProbeMetadata.MetadataLength.", nameof(header));
            }

            if (metadata.ProbePayloadBytes != (uint)payload.Length || header.BodyLength != (uint)payload.Length)
            {
                throw new ArgumentException("Header/body payload length must match metadata.ProbePayloadBytes.", nameof(payload));
            }

            Header = header;
            Metadata = metadata;
            Payload = payload;
        }

        public NnrpHeader Header { get; }

        public TransportProbeMetadata Metadata { get; }

        public ReadOnlyMemory<byte> Payload { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            return new NnrpFramedMessage(Header, Metadata.ToArray(), Payload);
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out TransportProbeMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out TransportProbeMessage message, out NnrpParseError error)
        {
            message = default;
            error = NnrpParseError.None;
            if (framed.Header.MessageType != MessageType.TransportProbe
                || framed.Header.MetaLength != TransportProbeMetadata.MetadataLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (!TransportProbeMetadata.TryParse(framed.Metadata.Span, out var metadata, out error))
            {
                return false;
            }

            if (metadata.ProbePayloadBytes != framed.Body.Length)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            message = new TransportProbeMessage(framed.Header, metadata, framed.Body);
            return true;
        }
    }
}
