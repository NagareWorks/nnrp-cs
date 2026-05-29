using System;

namespace Nnrp.Core
{
    public readonly struct PingMessage
    {
        public PingMessage(NnrpHeader header)
        {
            if (header.MessageType != MessageType.Ping || header.MetaLength != 0 || header.BodyLength != 0)
            {
                throw new ArgumentException("Header lengths must match the Ping layout.", nameof(header));
            }

            Header = header;
        }

        public NnrpHeader Header { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            return new NnrpFramedMessage(Header, Array.Empty<byte>(), Array.Empty<byte>());
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static PingMessage Create(uint sessionId, ulong traceId = 0)
        {
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.Ping,
                flags: HeaderFlags.CanDrop,
                metaLength: 0,
                bodyLength: 0,
                sessionId: sessionId,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: traceId);
            return new PingMessage(header);
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out PingMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out PingMessage message, out NnrpParseError error)
        {
            message = default;
            error = NnrpParseError.None;
            if (framed.Header.MessageType != MessageType.Ping || framed.Header.MetaLength != 0 || framed.Body.Length != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            message = new PingMessage(framed.Header);
            return true;
        }
    }
}
