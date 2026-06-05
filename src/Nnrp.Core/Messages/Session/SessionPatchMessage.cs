using System;

namespace Nnrp.Core
{
    public readonly struct SessionPatchMessage
    {
        public SessionPatchMessage(NnrpHeader header, SessionPatchMetadata metadata, TensorProfilePatchBlock? profilePatchBlock = null)
        {
            ValidateHeader(header, MessageType.SessionPatch, SessionPatchMetadata.MetadataLength, metadata.ProfilePatchBytes);
            if (!TryValidateProfilePatchBlock(metadata.ProfilePatchBytes, profilePatchBlock, out var error))
            {
                throw new ArgumentException($"SessionPatch body does not match metadata: {error}.", nameof(profilePatchBlock));
            }

            Header = header;
            Metadata = metadata;
            ProfilePatchBlock = profilePatchBlock;
        }

        public NnrpHeader Header { get; }
        public SessionPatchMetadata Metadata { get; }
        public TensorProfilePatchBlock? ProfilePatchBlock { get; }

        public NnrpFramedMessage ToFramedMessage() => new NnrpFramedMessage(Header, Metadata.ToArray(), ProfilePatchBlock?.ToArray() ?? Array.Empty<byte>());

        public byte[] ToArray() => ToFramedMessage().ToArray();

        public static bool TryParse(ReadOnlyMemory<byte> source, out SessionPatchMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            if (!ValidateFramedMessage(framed, MessageType.SessionPatch, SessionPatchMetadata.MetadataLength, out error)
                || !SessionPatchMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out error))
            {
                return false;
            }

            if (!TryParseProfilePatchBlock(framed.Body.Span, metadata.ProfilePatchBytes, out var profilePatchBlock, out error))
            {
                return false;
            }

            message = new SessionPatchMessage(framed.Header, metadata, profilePatchBlock);
            return true;
        }

        internal static void ValidateHeader(NnrpHeader header, MessageType messageType, int metadataLength)
        {
            ValidateHeader(header, messageType, metadataLength, 0);
        }

        internal static void ValidateHeader(NnrpHeader header, MessageType messageType, int metadataLength, uint bodyLength)
        {
            if (header.MessageType != messageType || header.MetaLength != metadataLength || header.BodyLength != bodyLength)
            {
                throw new ArgumentException("Header lengths must match the fixed-width control message layout.", nameof(header));
            }
        }

        internal static bool ValidateFramedMessage(NnrpFramedMessage framed, MessageType messageType, int metadataLength, out NnrpParseError error)
        {
            error = NnrpParseError.None;
            if (framed.Header.MessageType != messageType || framed.Header.MetaLength != metadataLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        private static bool TryValidateProfilePatchBlock(uint profilePatchBytes, TensorProfilePatchBlock? profilePatchBlock, out NnrpParseError error)
        {
            error = NnrpParseError.None;
            if (profilePatchBytes == 0)
            {
                return !profilePatchBlock.HasValue;
            }

            if (profilePatchBytes != TensorProfilePatchBlock.BlockLength || !profilePatchBlock.HasValue)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        private static bool TryParseProfilePatchBlock(ReadOnlySpan<byte> body, uint profilePatchBytes, out TensorProfilePatchBlock? profilePatchBlock, out NnrpParseError error)
        {
            profilePatchBlock = null;
            error = NnrpParseError.None;

            if (profilePatchBytes == 0)
            {
                if (body.Length != 0)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                return true;
            }

            if (profilePatchBytes != TensorProfilePatchBlock.BlockLength || body.Length != profilePatchBytes)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (!TensorProfilePatchBlock.TryParse(body, out var parsedBlock, out error))
            {
                return false;
            }

            profilePatchBlock = parsedBlock;
            return true;
        }
    }
}
