using System;

namespace Nnrp.Core
{
    public readonly struct FlowUpdateMessage
    {
        public FlowUpdateMessage(NnrpHeader header, FlowUpdateMetadata metadata)
        {
            SessionPatchMessage.ValidateHeader(header, MessageType.FlowUpdate, FlowUpdateMetadata.MetadataLength);

            if (!TryValidateSessionScope(header.SessionId, metadata.ScopeKind, out _))
            {
                throw new ArgumentException("Header session id must match FLOW_UPDATE scope.", nameof(header));
            }

            Header = header;
            Metadata = metadata;
        }

        public NnrpHeader Header { get; }

        public FlowUpdateMetadata Metadata { get; }

        public FlowCreditUpdate CreditUpdate => FlowCreditUpdate.FromMessage(this);

        public NnrpFramedMessage ToFramedMessage()
        {
            return new NnrpFramedMessage(Header, Metadata.ToArray(), Array.Empty<byte>());
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out FlowUpdateMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out FlowUpdateMessage message, out NnrpParseError error)
        {
            message = default;
            if (!SessionPatchMessage.ValidateFramedMessage(framed, MessageType.FlowUpdate, FlowUpdateMetadata.MetadataLength, out error)
                || framed.Body.Length != 0
                || !FlowUpdateMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out error)
                || !TryValidateSessionScope(framed.Header.SessionId, metadata.ScopeKind, out error))
            {
                if (error == NnrpParseError.None)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                }

                return false;
            }

            message = new FlowUpdateMessage(framed.Header, metadata);
            return true;
        }

        private static bool TryValidateSessionScope(uint sessionId, FlowUpdateScopeKind scopeKind, out NnrpParseError error)
        {
            error = NnrpParseError.None;
            if (scopeKind == FlowUpdateScopeKind.Connection)
            {
                if (sessionId != 0)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                return true;
            }

            if (scopeKind == FlowUpdateScopeKind.Session || scopeKind == FlowUpdateScopeKind.Operation)
            {
                if (sessionId == 0)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                return true;
            }

            error = NnrpParseError.InvalidMessageLayout;
            return false;
        }
    }
}
