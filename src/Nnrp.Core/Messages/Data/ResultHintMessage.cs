using System;

namespace Nnrp.Core
{
    public readonly struct ResultHintMessage
    {
        public ResultHintMessage(NnrpHeader header, ResultHintMetadata metadata)
        {
            SessionPatchMessage.ValidateHeader(header, MessageType.ResultHint, ResultHintMetadata.MetadataLength);

            Header = header;
            Metadata = metadata;
        }

        public NnrpHeader Header { get; }

        public ResultHintMetadata Metadata { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            return new NnrpFramedMessage(Header, Metadata.ToArray(), Array.Empty<byte>());
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out ResultHintMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out ResultHintMessage message, out NnrpParseError error)
        {
            message = default;
            if (!SessionPatchMessage.ValidateFramedMessage(framed, MessageType.ResultHint, ResultHintMetadata.MetadataLength, out error)
                || framed.Body.Length != 0
                || !ResultHintMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out error))
            {
                if (error == NnrpParseError.None)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                }

                return false;
            }

            message = new ResultHintMessage(framed.Header, metadata);
            return true;
        }
    }
}
