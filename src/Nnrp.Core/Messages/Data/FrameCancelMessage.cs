using System;

namespace Nnrp.Core
{
    public readonly struct FrameCancelMessage
    {
        public FrameCancelMessage(NnrpHeader header)
        {
            if (header.MessageType != MessageType.FrameCancel || header.MetaLength != 0 || header.BodyLength != 0)
            {
                throw new ArgumentException("Header lengths must match the FrameCancel layout.", nameof(header));
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

        public static FrameCancelMessage Create(uint sessionId, uint frameId, ushort viewId = 0, ulong traceId = 0)
        {
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.FrameCancel,
                flags: HeaderFlags.CanDrop,
                metaLength: 0,
                bodyLength: 0,
                sessionId: sessionId,
                frameId: frameId,
                viewId: viewId,
                routeId: 0,
                traceId: traceId);
            return new FrameCancelMessage(header);
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out FrameCancelMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out FrameCancelMessage message, out NnrpParseError error)
        {
            message = default;
            error = NnrpParseError.None;
            if (framed.Header.MessageType != MessageType.FrameCancel || framed.Header.MetaLength != 0 || framed.Body.Length != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            message = new FrameCancelMessage(framed.Header);
            return true;
        }
    }
}
