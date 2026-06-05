using System;

namespace Nnrp.Core
{
    public readonly struct SessionPatchAckMessage
    {
        public SessionPatchAckMessage(NnrpHeader header, SessionPatchAckMetadata metadata, TensorProfilePatchAckBlock? profilePatchAckBlock = null)
        {
            SessionPatchMessage.ValidateHeader(header, MessageType.SessionPatchAck, SessionPatchAckMetadata.MetadataLength, metadata.ProfilePatchAckBytes);
            if (!TryValidateProfilePatchAckBlock(metadata.ProfilePatchAckBytes, profilePatchAckBlock, out var error))
            {
                throw new ArgumentException($"SessionPatchAck body does not match metadata: {error}.", nameof(profilePatchAckBlock));
            }

            Header = header;
            Metadata = metadata;
            ProfilePatchAckBlock = profilePatchAckBlock;
        }

        public NnrpHeader Header { get; }
        public SessionPatchAckMetadata Metadata { get; }
        public TensorProfilePatchAckBlock? ProfilePatchAckBlock { get; }

        public NnrpFramedMessage ToFramedMessage() => new NnrpFramedMessage(Header, Metadata.ToArray(), ProfilePatchAckBlock?.ToArray() ?? Array.Empty<byte>());

        public byte[] ToArray() => ToFramedMessage().ToArray();

        public static bool TryParse(ReadOnlyMemory<byte> source, out SessionPatchAckMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            if (!SessionPatchMessage.ValidateFramedMessage(framed, MessageType.SessionPatchAck, SessionPatchAckMetadata.MetadataLength, out error)
                || !SessionPatchAckMetadata.TryParse(framed.Metadata.Span, out var metadata, out error))
            {
                return false;
            }

            if (!TryParseProfilePatchAckBlock(framed.Body.Span, metadata.ProfilePatchAckBytes, out var profilePatchAckBlock, out error))
            {
                return false;
            }

            message = new SessionPatchAckMessage(framed.Header, metadata, profilePatchAckBlock);
            return true;
        }

        private static bool TryValidateProfilePatchAckBlock(uint profilePatchAckBytes, TensorProfilePatchAckBlock? profilePatchAckBlock, out NnrpParseError error)
        {
            error = NnrpParseError.None;
            if (profilePatchAckBytes == 0)
            {
                return !profilePatchAckBlock.HasValue;
            }

            if (profilePatchAckBytes != TensorProfilePatchAckBlock.BlockLength || !profilePatchAckBlock.HasValue)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        private static bool TryParseProfilePatchAckBlock(ReadOnlySpan<byte> body, uint profilePatchAckBytes, out TensorProfilePatchAckBlock? profilePatchAckBlock, out NnrpParseError error)
        {
            profilePatchAckBlock = null;
            error = NnrpParseError.None;

            if (profilePatchAckBytes == 0)
            {
                if (body.Length != 0)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                return true;
            }

            if (profilePatchAckBytes != TensorProfilePatchAckBlock.BlockLength || body.Length != profilePatchAckBytes)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (!TensorProfilePatchAckBlock.TryParse(body, out var parsedBlock, out error))
            {
                return false;
            }

            profilePatchAckBlock = parsedBlock;
            return true;
        }
    }
}
