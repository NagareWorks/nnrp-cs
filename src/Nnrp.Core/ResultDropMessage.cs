using System;

namespace Nnrp.Core
{
    /// <summary>
    /// A header-only RESULT_DROP packet. The server sends this when a result
    /// is stale, superseded, or otherwise discardable; the client should fall back to
    /// its local fallback render for the affected frame.
    /// </summary>
    public readonly struct ResultDropMessage
    {
        public ResultDropMessage(NnrpHeader header)
        {
            if (header.MessageType != MessageType.ResultDrop || header.MetaLength != 0 || header.BodyLength != 0)
            {
                throw new ArgumentException("Header lengths must match the ResultDrop layout.", nameof(header));
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

        public static ResultDropMessage Create(uint sessionId, uint frameId, ulong traceId = 0)
        {
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ResultDrop,
                flags: HeaderFlags.CanDrop,
                metaLength: 0,
                bodyLength: 0,
                sessionId: sessionId,
                frameId: frameId,
                viewId: 0,
                routeId: 0,
                traceId: traceId);
            return new ResultDropMessage(header);
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out ResultDropMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out ResultDropMessage message, out NnrpParseError error)
        {
            message = default;
            error = NnrpParseError.None;
            if (framed.Header.MessageType != MessageType.ResultDrop || framed.Header.MetaLength != 0 || framed.Body.Length != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            message = new ResultDropMessage(framed.Header);
            return true;
        }
    }
}
