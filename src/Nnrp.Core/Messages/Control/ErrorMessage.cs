using System;
using System.Text;

namespace Nnrp.Core
{
    public readonly struct ErrorMessage
    {
        public ErrorMessage(NnrpHeader header, ErrorMetadata metadata, ReadOnlyMemory<byte> diagnosticBytes)
        {
            if (header.MessageType != MessageType.Error
                || header.MetaLength != ErrorMetadata.MetadataLength
                || header.BodyLength != (uint)diagnosticBytes.Length
                || metadata.DiagnosticBytes != (uint)diagnosticBytes.Length)
            {
                throw new ArgumentException("Header/body lengths must match the Error layout.", nameof(header));
            }

            Header = header;
            Metadata = metadata;
            DiagnosticBytes = diagnosticBytes;
        }

        public NnrpHeader Header { get; }
        public ErrorMetadata Metadata { get; }
        public ReadOnlyMemory<byte> DiagnosticBytes { get; }
        public string DiagnosticText => Encoding.UTF8.GetString(DiagnosticBytes.Span);

        public NnrpFramedMessage ToFramedMessage() => new NnrpFramedMessage(Header, Metadata.ToArray(), DiagnosticBytes);

        public byte[] ToArray() => ToFramedMessage().ToArray();

        public NnrpProtocolFailure ToProtocolFailure()
        {
            return new NnrpProtocolFailure(Metadata.ErrorCode, Metadata.ErrorScope, DiagnosticText, Metadata.IsFatal);
        }

        public static ErrorMessage FromProtocolFailure(
            NnrpProtocolFailure failure,
            uint relatedSessionId = 0,
            uint relatedFrameId = 0,
            uint relatedViewId = 0,
            uint retryAfterMilliseconds = 0,
            ulong traceId = 0)
        {
            var diagnosticBytes = Encoding.UTF8.GetBytes(failure.Message ?? string.Empty);
            var metadata = new ErrorMetadata(
                failure.ErrorCode,
                failure.Scope,
                failure.IsFatal,
                retryAfterMilliseconds,
                relatedSessionId,
                relatedFrameId,
                relatedViewId,
                checked((uint)diagnosticBytes.Length));
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.Error,
                flags: HeaderFlags.None,
                metaLength: ErrorMetadata.MetadataLength,
                bodyLength: checked((uint)diagnosticBytes.Length),
                sessionId: relatedSessionId,
                frameId: relatedFrameId,
                viewId: checked((ushort)relatedViewId),
                routeId: 0,
                traceId: traceId);
            return new ErrorMessage(header, metadata, diagnosticBytes);
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out ErrorMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            if (framed.Header.MessageType != MessageType.Error || framed.Header.MetaLength != ErrorMetadata.MetadataLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (!ErrorMetadata.TryParse(framed.Metadata.Span, out var metadata, out error)
                || metadata.DiagnosticBytes != (uint)framed.Body.Length)
            {
                error = error == NnrpParseError.None ? NnrpParseError.InvalidMessageLayout : error;
                return false;
            }

            message = new ErrorMessage(framed.Header, metadata, framed.Body);
            return true;
        }
    }
}
