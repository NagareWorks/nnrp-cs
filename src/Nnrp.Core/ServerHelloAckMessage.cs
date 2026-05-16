using System;

namespace Nnrp.Core
{
    public readonly struct ServerHelloAckMessage
    {
        public ServerHelloAckMessage(NnrpHeader header, ServerHelloAckMetadata metadata)
            : this(header, metadata, Array.Empty<ControlExtensionBlock>())
        {
        }

        public ServerHelloAckMessage(
            NnrpHeader header,
            ServerHelloAckMetadata metadata,
            ReadOnlyMemory<ControlExtensionBlock> extensions)
        {
            if (header.MessageType != MessageType.ServerHelloAck)
            {
                throw new ArgumentException("Header message type must be ServerHelloAck.", nameof(header));
            }

            var expectedBodyLength = ControlExtensionSequence.GetTotalLength(extensions);
            if (header.MetaLength != ServerHelloAckMetadata.MetadataLength
                || metadata.ControlExtensionBytes != (uint)expectedBodyLength
                || header.BodyLength != (uint)expectedBodyLength)
            {
                throw new ArgumentException("Header lengths must match the fixed-width ServerHelloAck layout.", nameof(header));
            }

            Header = header;
            Metadata = metadata;
            Extensions = extensions;
        }

        public NnrpHeader Header { get; }
        public ServerHelloAckMetadata Metadata { get; }
        public ReadOnlyMemory<ControlExtensionBlock> Extensions { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            return new NnrpFramedMessage(Header, Metadata.ToArray(), ControlExtensionSequence.ToArray(Extensions));
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out ServerHelloAckMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out ServerHelloAckMessage message, out NnrpParseError error)
        {
            message = default;
            error = NnrpParseError.None;
            if (framed.Header.MessageType != MessageType.ServerHelloAck
                || framed.Header.MetaLength != ServerHelloAckMetadata.MetadataLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (!ServerHelloAckMetadata.TryParse(framed.Metadata.Span, out var metadata, out error))
            {
                return false;
            }

            if (framed.Body.Length != metadata.ControlExtensionBytes)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (!ControlExtensionSequence.TryParse(framed.Body.Span, out var extensions, out error))
            {
                return false;
            }

            message = new ServerHelloAckMessage(framed.Header, metadata, extensions);
            return true;
        }

        public bool TryGetServerTransportPolicyAckExtension(out ServerTransportPolicyAckExtension extension, out NnrpParseError error)
        {
            var span = Extensions.Span;
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i].TypeCode == (ushort)ControlExtensionType.ServerTransportPolicyAck)
                {
                    return ServerTransportPolicyAckExtension.TryParse(span[i], out extension, out error);
                }
            }

            extension = default;
            error = NnrpParseError.None;
            return false;
        }

        public bool TryGetServerLossToleranceAckExtension(out ServerLossToleranceAckExtension extension, out NnrpParseError error)
        {
            var span = Extensions.Span;
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i].TypeCode == (ushort)ControlExtensionType.ServerLossToleranceAck)
                {
                    return ServerLossToleranceAckExtension.TryParse(span[i], out extension, out error);
                }
            }

            extension = default;
            error = NnrpParseError.None;
            return false;
        }

        public bool TryGetServerPayloadCapabilitiesAckExtension(out ServerPayloadCapabilitiesAckExtension extension, out NnrpParseError error)
        {
            var span = Extensions.Span;
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i].TypeCode == (ushort)ControlExtensionType.ServerPayloadCapabilitiesAck)
                {
                    return ServerPayloadCapabilitiesAckExtension.TryParse(span[i], out extension, out error);
                }
            }

            extension = default;
            error = NnrpParseError.None;
            return false;
        }
    }
}
