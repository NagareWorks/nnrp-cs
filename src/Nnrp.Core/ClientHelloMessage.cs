using System;

namespace Nnrp.Core
{
    public readonly struct ClientHelloMessage
    {
        public ClientHelloMessage(NnrpHeader header, ClientHelloMetadata metadata, ReadOnlyMemory<byte> authBlock)
            : this(header, metadata, authBlock, Array.Empty<ControlExtensionBlock>())
        {
        }

        public ClientHelloMessage(
            NnrpHeader header,
            ClientHelloMetadata metadata,
            ReadOnlyMemory<byte> authBlock,
            ReadOnlyMemory<ControlExtensionBlock> extensions)
        {
            if (header.MessageType != MessageType.ClientHello)
            {
                throw new ArgumentException("Header message type must be ClientHello.", nameof(header));
            }

            if (header.MetaLength != ClientHelloMetadata.MetadataLength)
            {
                throw new ArgumentException("Header metadata length must match ClientHelloMetadata.MetadataLength.", nameof(header));
            }

            var extensionBytes = ControlExtensionSequence.GetTotalLength(extensions);
            var expectedBodyLength = checked(authBlock.Length + extensionBytes);
            if (metadata.AuthBytes != (uint)authBlock.Length
                || metadata.ControlExtensionBytes != (uint)extensionBytes
                || header.BodyLength != (uint)expectedBodyLength)
            {
                throw new ArgumentException("Header/body auth block length must match metadata.AuthBytes.", nameof(authBlock));
            }

            Header = header;
            Metadata = metadata;
            AuthBlock = authBlock;
            Extensions = extensions;
        }

        public NnrpHeader Header { get; }
        public ClientHelloMetadata Metadata { get; }
        public ReadOnlyMemory<byte> AuthBlock { get; }
        public ReadOnlyMemory<ControlExtensionBlock> Extensions { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            var extensionBytes = ControlExtensionSequence.ToArray(Extensions);
            if (extensionBytes.Length == 0)
            {
                return new NnrpFramedMessage(Header, Metadata.ToArray(), AuthBlock);
            }

            var body = new byte[AuthBlock.Length + extensionBytes.Length];
            extensionBytes.AsSpan().CopyTo(body);
            AuthBlock.Span.CopyTo(body.AsSpan(extensionBytes.Length));
            return new NnrpFramedMessage(Header, Metadata.ToArray(), body);
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public NnrpClientCapabilities ToCapabilities()
        {
            const ushort defaultTileWidth = 32;
            const ushort defaultTileHeight = 32;
            const ushort defaultMinSourceExtent = 1;
            const ushort defaultMaxSourceExtent = 8192;

            return new NnrpClientCapabilities(
                ControlMetadataBitmaps.DecodeCodecBitmap<CodecId>(Metadata.SupportedCodecBitmap),
                ControlMetadataBitmaps.DecodeCodecBitmap<DTypeId>(Metadata.SupportedDTypeBitmap),
                ControlMetadataBitmaps.DecodeCodecBitmap<TensorLayoutId>(Metadata.SupportedLayoutBitmap),
                Metadata.SupportedPayloadKindBitmap,
                Metadata.CacheObjectBitmap,
                (BudgetPolicy)Metadata.DegradePolicy,
                checked((int)Metadata.MaxLaneCount),
                Metadata.CacheNamespaceCount > 0,
                checked((int)Metadata.MaxCacheEntries),
                defaultTileWidth,
                defaultTileHeight,
                defaultMinSourceExtent,
                defaultMaxSourceExtent,
                defaultMinSourceExtent,
                defaultMaxSourceExtent,
                checked((ushort)Metadata.TargetCadenceX100),
                checked((ushort)Metadata.TargetCadenceX100),
                checked((ushort)Metadata.LatencyBudgetMilliseconds));
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out ClientHelloMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out ClientHelloMessage message, out NnrpParseError error)
        {
            message = default;
            error = NnrpParseError.None;
            if (framed.Header.MessageType != MessageType.ClientHello
                || framed.Header.MetaLength != ClientHelloMetadata.MetadataLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (!ClientHelloMetadata.TryParse(framed.Metadata.Span, out var metadata, out error))
            {
                return false;
            }

            if (metadata.AuthBytes > framed.Body.Length)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var authBlockLength = checked((int)metadata.AuthBytes);
            var extensionBytesLength = checked((int)metadata.ControlExtensionBytes);
            if (framed.Body.Length != extensionBytesLength + authBlockLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var extensionBytes = framed.Body.Slice(0, extensionBytesLength);
            var authBlock = framed.Body.Slice(extensionBytesLength, authBlockLength);
            if (!ControlExtensionSequence.TryParse(extensionBytes.Span, out var extensions, out error))
            {
                return false;
            }

            message = new ClientHelloMessage(framed.Header, metadata, authBlock, extensions);
            return true;
        }

        public bool TryGetClientTransportPolicyExtension(out ClientTransportPolicyExtension extension, out NnrpParseError error)
        {
            var span = Extensions.Span;
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i].TypeCode == (ushort)ControlExtensionType.ClientTransportPolicy)
                {
                    return ClientTransportPolicyExtension.TryParse(span[i], out extension, out error);
                }
            }

            extension = default;
            error = NnrpParseError.None;
            return false;
        }

        public bool TryGetClientLossToleranceExtension(out ClientLossToleranceExtension extension, out NnrpParseError error)
        {
            var span = Extensions.Span;
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i].TypeCode == (ushort)ControlExtensionType.ClientLossTolerance)
                {
                    return ClientLossToleranceExtension.TryParse(span[i], out extension, out error);
                }
            }

            extension = default;
            error = NnrpParseError.None;
            return false;
        }

        public bool TryGetClientPayloadCapabilitiesExtension(out ClientPayloadCapabilitiesExtension extension, out NnrpParseError error)
        {
            var span = Extensions.Span;
            for (var i = 0; i < span.Length; i++)
            {
                if (span[i].TypeCode == (ushort)ControlExtensionType.ClientPayloadCapabilities)
                {
                    return ClientPayloadCapabilitiesExtension.TryParse(span[i], out extension, out error);
                }
            }

            extension = default;
            error = NnrpParseError.None;
            return false;
        }
    }
}
