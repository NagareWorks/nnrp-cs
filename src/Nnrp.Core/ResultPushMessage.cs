using System;

namespace Nnrp.Core
{
    public readonly struct ResultPushMessage
    {
        public ResultPushMessage(
            NnrpHeader header,
            ResultPushMetadata metadata,
            ReadOnlyMemory<ushort> tileIds,
            ReadOnlyMemory<TensorSectionBlock> sections,
            ReadOnlyMemory<TypedPayloadDescriptor> typedPayloadDescriptors = default,
            ReadOnlyMemory<byte> typedPayloadFrameRegion = default,
            ReadOnlyMemory<TypedPayloadProfileCoverage> typedPayloadCoverages = default)
        {
            if (header.MessageType != MessageType.ResultPush)
            {
                throw new ArgumentException("Header message type must be ResultPush.", nameof(header));
            }

            if (header.MetaLength != ResultPushMetadata.MetadataLength)
            {
                throw new ArgumentException("Header metadata length must match ResultPushMetadata.MetadataLength.", nameof(header));
            }

            if (metadata.TileCount != tileIds.Length)
            {
                throw new ArgumentException("Tile id count must match metadata.TileCount.", nameof(tileIds));
            }

            if (metadata.SectionCount != sections.Length)
            {
                throw new ArgumentException("Section count must match metadata.SectionCount.", nameof(sections));
            }

            var resolvedTileIndexMode = InferTileIndexMode(metadata);
            var resolvedTileIndexLength = TileIndexBlockCodec.GetEncodedLength(tileIds.Span, resolvedTileIndexMode);
            if (metadata.TileIndexBytes != (uint)resolvedTileIndexLength)
            {
                throw new ArgumentException("Tile index block length must match metadata.TileIndexBytes.", nameof(tileIds));
            }

            if (!ResultPushMetadata.TryValidateCoverageContract(metadata, tileIds.Length, out var coverageError))
            {
                throw new ArgumentException($"Result coverage contract is invalid: {coverageError}.", nameof(metadata));
            }

            if (IsTensorOnly(metadata))
            {
                if (!typedPayloadDescriptors.IsEmpty || !typedPayloadFrameRegion.IsEmpty || !typedPayloadCoverages.IsEmpty)
                {
                    throw new ArgumentException("Tensor-only results cannot carry typed payload bookkeeping.", nameof(typedPayloadDescriptors));
                }

                TypedPayloadFrames = ReadOnlyMemory<TypedPayloadFrameView>.Empty;
            }
            else
            {
                if (typedPayloadDescriptors.Length != metadata.PayloadFrameCount)
                {
                    throw new ArgumentException("Typed payload descriptor count must match metadata.PayloadFrameCount for composite results.", nameof(typedPayloadDescriptors));
                }

                if (!TypedPayloadRegionValidator.TryProjectTypedPayloadFrames(
                        metadata.PayloadKindBitmap,
                        metadata.PayloadFrameCount,
                        typedPayloadDescriptors.Span,
                        typedPayloadFrameRegion,
                        out var typedPayloadFrames,
                        out var typedPayloadValidationError))
                {
                    throw new ArgumentException($"Typed payload layout is invalid: {typedPayloadValidationError}.", nameof(typedPayloadDescriptors));
                }

                if (!TypedPayloadRegionValidator.TrySummarizeProfileCoverage(
                        typedPayloadDescriptors.Span,
                        out var normalizedCoverages,
                        out _,
                        out var typedPayloadCoverageError))
                {
                        throw new ArgumentException($"Typed payload coverage summary is invalid: {typedPayloadCoverageError}.", nameof(typedPayloadDescriptors));
                }

                if (typedPayloadCoverages.IsEmpty)
                {
                    typedPayloadCoverages = normalizedCoverages;
                }
                else if (!typedPayloadCoverages.Span.SequenceEqual(normalizedCoverages))
                {
                    throw new ArgumentException("Typed payload coverage must match the descriptor-derived summary.", nameof(typedPayloadCoverages));
                }

                TypedPayloadFrames = typedPayloadFrames;
            }

            var normalizedHeader = header;
            if (IsTensorOnly(metadata))
            {
                if (!TryGetAlignedBodyLength(resolvedTileIndexLength, sections.Span, out var resolvedBodyLength))
                {
                    throw new ArgumentException("Header body length must match the computed ResultPush body length.", nameof(header));
                }

                normalizedHeader = CreateHeaderWithBodyLength(header, (uint)resolvedBodyLength);
            }
            else
            {
                var compositeBody = BuildCompositeBody(
                    metadata,
                    resolvedTileIndexMode,
                    tileIds.Span,
                    sections.Span,
                    typedPayloadDescriptors.Span,
                    typedPayloadFrameRegion);
                normalizedHeader = CreateHeaderWithBodyLength(header, checked((uint)compositeBody.Length));
            }

            Header = normalizedHeader;
            Metadata = metadata;
            TensorResultBlock = default;
            TileIndexMode = resolvedTileIndexMode;
            TileIds = tileIds;
            Sections = sections;
            TypedPayloadDescriptors = typedPayloadDescriptors.IsEmpty ? ReadOnlyMemory<TypedPayloadDescriptor>.Empty : typedPayloadDescriptors;
            TypedPayloadFrameRegion = typedPayloadFrameRegion.IsEmpty ? ReadOnlyMemory<byte>.Empty : typedPayloadFrameRegion;
            TypedPayloadCoverages = typedPayloadCoverages.IsEmpty ? ReadOnlyMemory<TypedPayloadProfileCoverage>.Empty : typedPayloadCoverages;
        }

        public NnrpHeader Header { get; }

        public ResultPushMetadata Metadata { get; }

        public TensorResultBlock TensorResultBlock { get; }

        public TileIndexMode TileIndexMode { get; }

        public ReadOnlyMemory<ushort> TileIds { get; }

        public ReadOnlyMemory<TensorSectionBlock> Sections { get; }

        public ReadOnlyMemory<TypedPayloadDescriptor> TypedPayloadDescriptors { get; }

        public ReadOnlyMemory<byte> TypedPayloadFrameRegion { get; }

        public ReadOnlyMemory<TypedPayloadFrameView> TypedPayloadFrames { get; }

        public ReadOnlyMemory<TypedPayloadProfileCoverage> TypedPayloadCoverages { get; }

        public TypedPayloadFrameView[] GetTypedPayloadFrames(PayloadKind payloadKind, ushort profileId)
        {
            var rawPayloadKind = (uint)payloadKind;
            if (rawPayloadKind == 0
                || (rawPayloadKind & (rawPayloadKind - 1)) != 0
                || !PayloadKindValidator.IsDefinedBitmap(payloadKind))
            {
                throw new ArgumentOutOfRangeException(nameof(payloadKind), "Typed payload frame lookup requires a single defined payload kind bit.");
            }

            if (TypedPayloadFrames.IsEmpty)
            {
                return Array.Empty<TypedPayloadFrameView>();
            }

            var matchCount = 0;
            foreach (var frame in TypedPayloadFrames.Span)
            {
                if (frame.PayloadKind == payloadKind && frame.ProfileId == profileId)
                {
                    matchCount++;
                }
            }

            if (matchCount == 0)
            {
                return Array.Empty<TypedPayloadFrameView>();
            }

            var matches = new TypedPayloadFrameView[matchCount];
            var nextIndex = 0;
            foreach (var frame in TypedPayloadFrames.Span)
            {
                if (frame.PayloadKind == payloadKind && frame.ProfileId == profileId)
                {
                    matches[nextIndex++] = frame;
                }
            }

            return matches;
        }

        public TypedPayloadProfileFrames GetPayloadFrames(PayloadKind payloadKind, ushort profileId)
        {
            return new TypedPayloadProfileFrames(payloadKind, profileId, GetTypedPayloadFrames(payloadKind, profileId));
        }

        public TypedPayloadProfileFrames GetTokenChunkFrames(ushort profileId)
        {
            return GetPayloadFrames(PayloadKind.TokenChunk, profileId);
        }

        public TypedPayloadProfileFrames GetAudioChunkFrames(ushort profileId)
        {
            return GetPayloadFrames(PayloadKind.AudioChunk, profileId);
        }

        public TypedPayloadProfileFrames GetVideoChunkFrames(ushort profileId)
        {
            return GetPayloadFrames(PayloadKind.VideoChunk, profileId);
        }

        public TypedPayloadProfileFrames GetStructuredEventFrames(ushort profileId)
        {
            return GetPayloadFrames(PayloadKind.StructuredEvent, profileId);
        }

        public TypedPayloadProfileFrames GetToolDeltaFrames(ushort profileId)
        {
            return GetPayloadFrames(PayloadKind.ToolDelta, profileId);
        }

        public TypedPayloadProfileFrames GetOpaqueBytesFrames(ushort profileId)
        {
            return GetPayloadFrames(PayloadKind.OpaqueBytes, profileId);
        }

        public bool TryGetPayloadCoverage(PayloadKind payloadKind, ushort profileId, out TypedPayloadProfileCoverage coverage)
        {
            var rawPayloadKind = (uint)payloadKind;
            if (rawPayloadKind == 0
                || (rawPayloadKind & (rawPayloadKind - 1)) != 0
                || !PayloadKindValidator.IsDefinedBitmap(payloadKind))
            {
                throw new ArgumentOutOfRangeException(nameof(payloadKind), "Typed payload coverage lookup requires a single defined payload kind bit.");
            }

            foreach (var entry in TypedPayloadCoverages.Span)
            {
                if (entry.PayloadKind == payloadKind && entry.ProfileId == profileId)
                {
                    coverage = entry;
                    return true;
                }
            }

            coverage = default;
            return false;
        }

        public NnrpFramedMessage ToFramedMessage()
        {
            var metadataBytes = Metadata.ToArray();

            if (IsTensorOnly(Metadata))
            {
                var tileIndexBlock = TileIndexBlockCodec.Encode(TileIds.Span, TileIndexMode, Metadata.TileBaseId);
                var body = new byte[checked((int)Header.BodyLength)];
                var offset = 0;
                tileIndexBlock.CopyTo(body.AsSpan(offset, tileIndexBlock.Length));
                offset += tileIndexBlock.Length;

                foreach (var section in Sections.Span)
                {
                    offset = BinaryAlignment.AlignUp(offset, 8);
                    if (!section.TryCopyTo(body.AsSpan(offset), out var sectionBytes))
                    {
                        throw new InvalidOperationException("Tensor section serialization failed.");
                    }

                    offset += sectionBytes;
                }

                return new NnrpFramedMessage(Header, metadataBytes, body);
            }

            var compositeBody = BuildCompositeBody(
                Metadata,
                TileIndexMode,
                TileIds.Span,
                Sections.Span,
                TypedPayloadDescriptors.Span,
                TypedPayloadFrameRegion);
            return new NnrpFramedMessage(Header, metadataBytes, compositeBody);
        }

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out ResultPushMessage message, out NnrpParseError error)
        {
            return TryParse(source, cacheStore: null, out message, out error);
        }

        public static bool TryParse(
            ReadOnlyMemory<byte> source,
            NnrpCacheStore? cacheStore,
            out ResultPushMessage message,
            out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, cacheStore, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out ResultPushMessage message, out NnrpParseError error)
        {
            return TryParse(framed, cacheStore: null, out message, out error);
        }

        public static bool TryParse(
            NnrpFramedMessage framed,
            NnrpCacheStore? cacheStore,
            out ResultPushMessage message,
            out NnrpParseError error)
        {
            message = default;
            error = NnrpParseError.None;

            if (framed.Header.MessageType != MessageType.ResultPush
                || framed.Header.MetaLength != ResultPushMetadata.MetadataLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (!ResultPushMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out error))
            {
                return false;
            }

            if (IsTensorOnly(metadata))
            {
                return TryParseTensorOnly(framed, metadata, out message, out error);
            }

            return TryParseComposite(framed, metadata, cacheStore, out message, out error);
        }

        private static bool TryParseComposite(
            NnrpFramedMessage framed,
            ResultPushMetadata metadata,
            NnrpCacheStore? cacheStore,
            out ResultPushMessage message,
            out NnrpParseError error)
        {
            message = default;

            if (!BodyCodec.TryParse(framed.Body, out var bodyView, out error))
            {
                return false;
            }

            if (!ResultPushBodyValidator.TryValidate(
                    metadata,
                    bodyView,
                    out var inlineBlocks,
                    out var objectReferenceBlocks,
                    out var typedPayloadDescriptors,
                    out _,
                    out error))
            {
                return false;
            }

            if (!TypedPayloadRegionValidator.TrySummarizeProfileCoverage(
                    typedPayloadDescriptors,
                    out var typedPayloadCoverages,
                    out _,
                    out error))
            {
                return false;
            }

            var tileIds = metadata.TileCount == 0 ? Array.Empty<ushort>() : new ushort[metadata.TileCount];
            var sections = metadata.SectionCount == 0 ? Array.Empty<TensorSectionBlock>() : new TensorSectionBlock[metadata.SectionCount];
            var hasTileIds = false;
            var hasSections = false;

            for (var index = 0; index < inlineBlocks.Length; index++)
            {
                var block = inlineBlocks[index];
                if (block.Header.ObjectKind == CacheObjectKind.TileIndexBlock)
                {
                    if (!TryDecodeTileIds(
                            metadata,
                            block.Payload,
                            tileIds,
                            out error))
                    {
                        return false;
                    }

                    hasTileIds = true;
                }
                else if (block.Header.ObjectKind == CacheObjectKind.TensorSectionTable)
                {
                    if (!TryParseInlineTensorSections(block.Payload, sections, metadata.TileCount, out error))
                    {
                        return false;
                    }

                    hasSections = true;
                }
            }

            if (objectReferenceBlocks.Length != 0)
            {
                if (cacheStore == null)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                for (var index = 0; index < objectReferenceBlocks.Length; index++)
                {
                    var block = objectReferenceBlocks[index];
                    if (!TryResolveReferencedObject(block, cacheStore, out var payload, out error))
                    {
                        return false;
                    }

                    if (block.ObjectKind == CacheObjectKind.TileIndexBlock)
                    {
                        if (metadata.TileIndexBytes != 0 && payload.Length != metadata.TileIndexBytes)
                        {
                            error = NnrpParseError.InvalidMessageLayout;
                            return false;
                        }

                        if (!TryDecodeTileIds(metadata, payload, tileIds, out error))
                        {
                            return false;
                        }

                        hasTileIds = true;
                    }
                    else if (block.ObjectKind == CacheObjectKind.TensorSectionTable)
                    {
                        if (!TryParseInlineTensorSections(payload, sections, metadata.TileCount, out error))
                        {
                            return false;
                        }

                        hasSections = true;
                    }
                    else
                    {
                        error = NnrpParseError.InvalidMessageLayout;
                        return false;
                    }
                }
            }

            if (!hasTileIds && tileIds.Length != 0)
            {
                if (metadata.TileIndexBytes != 0
                    || !TryDecodeTileIds(metadata, ReadOnlyMemory<byte>.Empty, tileIds, out error))
                {
                    return false;
                }

                hasTileIds = true;
            }

            if (!hasSections && sections.Length != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            message = new ResultPushMessage(
                framed.Header,
                metadata,
                tileIds,
                sections,
                typedPayloadDescriptors,
                bodyView.TypedPayloadFrameRegion,
                typedPayloadCoverages);
            error = NnrpParseError.None;
            return true;
        }

        private static NnrpHeader CreateHeaderWithBodyLength(NnrpHeader header, uint bodyLength)
        {
            return new NnrpHeader(
                versionMajor: header.VersionMajor,
                messageType: header.MessageType,
                flags: header.Flags,
                metaLength: header.MetaLength,
                bodyLength: bodyLength,
                sessionId: header.SessionId,
                frameId: header.FrameId,
                viewId: header.ViewId,
                routeId: header.RouteId,
                traceId: header.TraceId,
                headerLength: header.HeaderLengthValue);
        }

        private static byte[] BuildCompositeBody(
            ResultPushMetadata metadata,
            TileIndexMode tileIndexMode,
            ReadOnlySpan<ushort> tileIds,
            ReadOnlySpan<TensorSectionBlock> sections,
            ReadOnlySpan<TypedPayloadDescriptor> typedPayloadDescriptors,
            ReadOnlyMemory<byte> typedPayloadFrameRegion)
        {
            byte[]? tileIndexInlineBlock = null;
            if (metadata.TileIndexBytes != 0)
            {
                var tileIndexPayload = TileIndexBlockCodec.Encode(tileIds, tileIndexMode, metadata.TileBaseId);
                tileIndexInlineBlock = BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, tileIndexPayload);
            }

            byte[]? tensorSectionTableInlineBlock = null;
            if (sections.Length != 0)
            {
                tensorSectionTableInlineBlock = BodyCodec.BuildInlineObjectBlock(
                    CacheObjectKind.TensorSectionTable,
                    BuildTensorSectionTablePayload(sections));
            }

            var inlineObjectRegion = tileIndexInlineBlock == null
                ? (tensorSectionTableInlineBlock == null
                    ? Array.Empty<byte>()
                    : BodyCodec.PackInlineObjectRegion(tensorSectionTableInlineBlock))
                : (tensorSectionTableInlineBlock == null
                    ? BodyCodec.PackInlineObjectRegion(tileIndexInlineBlock)
                    : BodyCodec.PackInlineObjectRegion(tileIndexInlineBlock, tensorSectionTableInlineBlock));

            return BodyCodec.Pack(
                inlineObjectRegion: inlineObjectRegion,
                typedPayloadDescriptorRegion: BuildTypedPayloadDescriptorRegion(typedPayloadDescriptors),
                typedPayloadFrameRegion: typedPayloadFrameRegion.IsEmpty ? Array.Empty<byte>() : typedPayloadFrameRegion.ToArray());
        }

        private static byte[] BuildTensorSectionTablePayload(ReadOnlySpan<TensorSectionBlock> sections)
        {
            if (sections.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var totalBytes = 0;
            for (var index = 0; index < sections.Length; index++)
            {
                totalBytes = checked(totalBytes + sections[index].TotalLength);
            }

            var payload = new byte[totalBytes];
            var cursor = 0;
            for (var index = 0; index < sections.Length; index++)
            {
                var sectionPayload = sections[index].ToArray();
                Buffer.BlockCopy(sectionPayload, 0, payload, cursor, sectionPayload.Length);
                cursor += sectionPayload.Length;
            }

            return payload;
        }

        private static byte[] BuildTypedPayloadDescriptorRegion(ReadOnlySpan<TypedPayloadDescriptor> descriptors)
        {
            if (descriptors.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var descriptorRegion = new byte[checked(descriptors.Length * TypedPayloadDescriptor.DescriptorLength)];
            for (var index = 0; index < descriptors.Length; index++)
            {
                descriptors[index].Write(descriptorRegion.AsSpan(index * TypedPayloadDescriptor.DescriptorLength, TypedPayloadDescriptor.DescriptorLength));
            }

            return descriptorRegion;
        }

        private static TileIndexMode InferTileIndexMode(ResultPushMetadata metadata)
        {
            if (!TryInferTileIndexMode(metadata, out var tileIndexMode, out var error))
            {
                throw new ArgumentException($"ResultPush metadata cannot represent the tile index block: {error}", nameof(metadata));
            }

            return tileIndexMode;
        }

        private static bool TryInferTileIndexMode(ResultPushMetadata metadata, out TileIndexMode tileIndexMode, out NnrpParseError error)
        {
            tileIndexMode = TileIndexMode.DenseRange;
            error = NnrpParseError.None;

            if (metadata.TileIndexBytes == 0)
            {
                return true;
            }

            var expectedRawBytes = checked((uint)(metadata.TileCount * sizeof(ushort)));
            if (metadata.TileIndexBytes == expectedRawBytes)
            {
                tileIndexMode = TileIndexMode.RawUInt16;
                return true;
            }

            error = NnrpParseError.InvalidMessageLayout;
            return false;
        }

        private static bool TryParseTensorOnly(
            NnrpFramedMessage framed,
            ResultPushMetadata metadata,
            out ResultPushMessage message,
            out NnrpParseError error)
        {
            message = default;
            error = NnrpParseError.None;

            if (!TryInferTileIndexMode(metadata, out var tileIndexMode, out error))
            {
                return false;
            }

            if (metadata.TileIndexBytes > int.MaxValue)
            {
                error = NnrpParseError.MessageTooLarge;
                return false;
            }

            var tileIndexBytes = (int)metadata.TileIndexBytes;
            if (framed.Body.Length < tileIndexBytes)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var tileIndexBlock = framed.Body.Slice(0, tileIndexBytes);
            var tileIds = metadata.TileCount == 0 ? Array.Empty<ushort>() : new ushort[metadata.TileCount];
            if (!TileIndexBlockCodec.TryDecode(
                    tileIndexBlock.Span,
                    tileIndexMode,
                    metadata.TileCount,
                    tileIds,
                    out var tileIdsWritten,
                    out error,
                    metadata.TileBaseId))
            {
                return false;
            }

            if (tileIdsWritten != tileIds.Length)
            {
                error = NnrpParseError.InvalidTileIndexBlock;
                return false;
            }

            var sections = metadata.SectionCount == 0 ? Array.Empty<TensorSectionBlock>() : new TensorSectionBlock[metadata.SectionCount];
            var cursor = tileIndexBytes;
            if (sections.Length > 0
                && !TryValidateZeroPadding(framed.Body, tileIndexBytes, out cursor, out error))
            {
                return false;
            }

            if (!TryParseInlineTensorSections(framed.Body.Slice(cursor), sections, metadata.TileCount, out error))
            {
                return false;
            }

            if (!TrySummarizeAndValidateTensorCoverage(metadata, tileIds.Length, out error))
            {
                return false;
            }

            message = new ResultPushMessage(
                framed.Header,
                metadata,
                tileIds,
                sections);
            return true;
        }

        private static void SummarizeSections(ReadOnlySpan<TensorSectionBlock> sections, out uint payloadDescriptorBytes, out uint payloadDataBytes)
        {
            ulong descriptorBytes = 0;
            ulong dataBytes = 0;

            foreach (var section in sections)
            {
                descriptorBytes += (ulong)TensorSectionDescriptor.DescriptorLength
                    + section.Descriptor.CodecTableBytes
                    + section.Descriptor.LengthTableBytes;
                dataBytes += section.Descriptor.PayloadBytes;
            }

            if (descriptorBytes > uint.MaxValue || dataBytes > uint.MaxValue)
            {
                throw new InvalidOperationException("Tensor section summary exceeds UInt32.MaxValue.");
            }

            payloadDescriptorBytes = (uint)descriptorBytes;
            payloadDataBytes = (uint)dataBytes;
        }

        private static bool TryGetAlignedBodyLength(int tileIndexBytes, ReadOnlySpan<TensorSectionBlock> sections, out int bodyLength)
        {
            bodyLength = 0;
            var runningTotal = tileIndexBytes;
            foreach (var section in sections)
            {
                runningTotal = BinaryAlignment.AlignUp(runningTotal, 8);
                if (!CheckedArithmetic.TryAdd(runningTotal, section.TotalLength, out runningTotal))
                {
                    return false;
                }
            }

            bodyLength = runningTotal;
            return true;
        }

        private static bool TrySummarizeAndValidateTensorCoverage(
            ResultPushMetadata metadata,
            int tileCount,
            out NnrpParseError error)
        {
            return ResultPushMetadata.TryValidateCoverageContract(metadata, tileCount, out error);
        }

        private static bool IsTensorOnly(ResultPushMetadata metadata)
        {
            return metadata.PayloadFrameCount == 0
                && metadata.PayloadKindBitmap == PayloadKind.Tensor;
        }

        private static bool TryValidateZeroPadding(ReadOnlyMemory<byte> source, int offset, out int alignedOffset, out NnrpParseError error)
        {
            alignedOffset = 0;
            error = NnrpParseError.None;

            if (!BinaryAlignment.TryAlignUp(offset, out alignedOffset))
            {
                error = NnrpParseError.MessageTooLarge;
                return false;
            }

            if (source.Length < alignedOffset)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (!BinaryAlignment.ValidateZeroPadding(source.Span.Slice(offset, alignedOffset - offset)))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        private static bool TryParseInlineTensorSections(
            ReadOnlyMemory<byte> payload,
            TensorSectionBlock[] sections,
            ushort tileCount,
            out NnrpParseError error)
        {
            error = NnrpParseError.None;
            var cursor = 0;
            TensorRole? previousRole = null;

            for (var index = 0; index < sections.Length; index++)
            {
                if (payload.Length < cursor)
                {
                    error = NnrpParseError.SourceTooShort;
                    return false;
                }

                if (!TensorSectionBlock.TryParse(payload.Slice(cursor), tileCount, out var section, out var sectionBytes, out error))
                {
                    return false;
                }

                if (previousRole.HasValue && section.Descriptor.Role <= previousRole.Value)
                {
                    error = NnrpParseError.InconsistentSectionDescriptor;
                    return false;
                }

                sections[index] = section;
                previousRole = section.Descriptor.Role;

                if (!CheckedArithmetic.TryAdd(cursor, sectionBytes, out var nextOffset))
                {
                    error = NnrpParseError.MessageTooLarge;
                    return false;
                }

                if (index + 1 < sections.Length)
                {
                    if (!TryValidateZeroPadding(payload, nextOffset, out cursor, out error))
                    {
                        return false;
                    }
                }
                else
                {
                    cursor = nextOffset;
                }
            }

            if (cursor != payload.Length)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        private static bool TryDecodeTileIds(
            ResultPushMetadata metadata,
            ReadOnlyMemory<byte> payload,
            ushort[] tileIds,
            out NnrpParseError error)
        {
            error = NnrpParseError.None;
            if (!TryInferTileIndexMode(metadata, out var tileIndexMode, out error)
                || !TileIndexBlockCodec.TryDecode(
                    payload.Span,
                    tileIndexMode,
                    metadata.TileCount,
                    tileIds,
                    out var tileIdsWritten,
                    out error,
                    metadata.TileBaseId))
            {
                return false;
            }

            if (tileIdsWritten != tileIds.Length)
            {
                error = NnrpParseError.InvalidTileIndexBlock;
                return false;
            }

            return true;
        }

        private static bool TryResolveReferencedObject(
            ObjectReferenceBlock block,
            NnrpCacheStore cacheStore,
            out ReadOnlyMemory<byte> payload,
            out NnrpParseError error)
        {
            payload = default;
            error = NnrpParseError.None;

            if (block.CacheNamespace > ushort.MaxValue)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var key = new NnrpCacheKey((ushort)block.CacheNamespace, block.CacheKeyHigh, block.CacheKeyLow);
            var cacheResult = cacheStore.TryGet(key);
            if (!cacheResult.IsSuccess || cacheResult.Entry == null)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            payload = cacheResult.Entry.ObjectBytes;
            return true;
        }
    }
}
