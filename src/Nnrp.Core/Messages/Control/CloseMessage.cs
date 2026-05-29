using System;
using System.Text;

namespace Nnrp.Core
{
    public readonly struct CloseMessage
    {
        public CloseMessage(NnrpHeader header, ReadOnlyMemory<byte> reasonBytes)
        {
            if (header.MessageType != MessageType.Close || header.MetaLength != 0 || header.BodyLength != (uint)reasonBytes.Length)
            {
                throw new ArgumentException("Header lengths must match the Close layout.", nameof(header));
            }

            Header = header;
            ReasonBytes = reasonBytes;
        }

        public NnrpHeader Header { get; }

        public ReadOnlyMemory<byte> ReasonBytes { get; }

        public string Reason => Encoding.UTF8.GetString(ReasonBytes.Span);

        public NnrpFramedMessage ToFramedMessage() => new NnrpFramedMessage(Header, Array.Empty<byte>(), ReasonBytes);

        public byte[] ToArray() => ToFramedMessage().ToArray();

        public static CloseMessage Create(uint sessionId, string reason, ulong traceId = 0)
        {
            var reasonBytes = Encoding.UTF8.GetBytes(reason ?? string.Empty);
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.Close,
                flags: HeaderFlags.None,
                metaLength: 0,
                bodyLength: checked((uint)reasonBytes.Length),
                sessionId: sessionId,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: traceId);
            return new CloseMessage(header, reasonBytes);
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out CloseMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            if (framed.Header.MessageType != MessageType.Close || framed.Header.MetaLength != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            message = new CloseMessage(framed.Header, framed.Body);
            return true;
        }
    }
}
