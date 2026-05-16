using System;

namespace Nnrp.Core
{
    public readonly struct FrameSubmitMessage
    {
        public const int MetadataLength = 32;

        private const byte DefaultLossTolerancePolicy = 0xFF;

        public FrameSubmitMessage(
            NnrpHeader header,
            FrameSubmitMetadata metadata,
            ReadOnlyMemory<byte> cameraBlock,
            ReadOnlyMemory<ushort> tileIds,
            ReadOnlyMemory<TensorSectionBlock> sections)
        {
            if (header.MessageType != MessageType.FrameSubmit)
            {
                throw new ArgumentException("Header message type must be FrameSubmit.", nameof(header));
            }

            if (header.MetaLength != MetadataLength)
            {
                throw new ArgumentException("Header metadata length must match FrameSubmitMessage.MetadataLength.", nameof(header));
            }

            var tileIndexLength = TileIndexBlockCodec.GetEncodedLength(tileIds.Span, metadata.TileIndexMode);
            var tensorSubmitBlock = new TensorSubmitBlock(
                metadata.SourceWidth,
                metadata.SourceHeight,
                metadata.TileWidth,
                metadata.TileHeight,
                checked((ushort)tileIds.Length),
                checked((ushort)sections.Length),
                metadata.TileIndexMode,
                tensorFlags: 0,
                reserved0: 0,
                metadata.TileBaseId,
                checked((uint)cameraBlock.Length),
                checked((uint)tileIndexLength));

            if (tensorSubmitBlock.CameraBytes != (uint)cameraBlock.Length)
            {
                throw new ArgumentException("Camera block length must match metadata.CameraBytes.", nameof(cameraBlock));
            }

            if (tensorSubmitBlock.TileCount != tileIds.Length)
            {
                throw new ArgumentException("Tile id count must match metadata.TileCount.", nameof(tileIds));
            }

            if (tensorSubmitBlock.SectionCount != sections.Length)
            {
                throw new ArgumentException("Section count must match metadata.SectionCount.", nameof(sections));
            }

            if (tensorSubmitBlock.TileIndexBytes != (uint)tileIndexLength)
            {
                throw new ArgumentException("Tile index block length must match metadata.TileIndexBytes.", nameof(tileIds));
            }

            if (!TryGetAlignedBodyLength(
                    TensorSubmitBlock.BlockLength,
                    cameraBlock.Length,
                    tileIndexLength,
                    sections.Span,
                    out var bodyLength)
                || header.BodyLength != (uint)bodyLength)
            {
                throw new ArgumentException("Header body length must match the computed FrameSubmit body length.", nameof(header));
            }

            Header = header;
            Metadata = NormalizeMetadata(metadata, tensorSubmitBlock);
            TensorSubmitBlock = tensorSubmitBlock;
            CameraBlock = cameraBlock;
            TileIds = tileIds;
            Sections = sections;
        }

        public NnrpHeader Header { get; }

        public FrameSubmitMetadata Metadata { get; }

        public TensorSubmitBlock TensorSubmitBlock { get; }

        public ReadOnlyMemory<byte> CameraBlock { get; }

        public ReadOnlyMemory<ushort> TileIds { get; }

        public ReadOnlyMemory<TensorSectionBlock> Sections { get; }

        public NnrpFramedMessage ToFramedMessage()
        {
            var metadataBytes = SerializeMetadata(Metadata, TensorSubmitBlock, Sections.Span);
            var tileIndexBlock = TileIndexBlockCodec.Encode(TileIds.Span, TensorSubmitBlock.TileIndexMode, TensorSubmitBlock.TileBaseId);
            var body = new byte[checked((int)Header.BodyLength)];

            var offset = 0;
            var submitBlockBytes = TensorSubmitBlock.ToArray();
            submitBlockBytes.CopyTo(body.AsSpan(offset, submitBlockBytes.Length));
            offset += submitBlockBytes.Length;

            CameraBlock.Span.CopyTo(body.AsSpan(offset, CameraBlock.Length));
            offset += CameraBlock.Length;
            offset = BinaryAlignment.AlignUp(offset, 8);

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

        public byte[] ToArray()
        {
            return ToFramedMessage().ToArray();
        }

        public static bool TryParseMetadata(ReadOnlySpan<byte> source, out FrameSubmitMetadata metadata)
        {
            return TryParseMetadata(source, strict: false, out metadata, out _);
        }

        public static bool TryParseMetadata(ReadOnlySpan<byte> source, bool strict, out FrameSubmitMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            if (!LegacyWireMetadata.TryParse(source, strict, out var legacyMetadata, out error))
            {
                return false;
            }

            metadata = BuildMetadata(legacyMetadata);
            return true;
        }

        public static bool TryParse(ReadOnlyMemory<byte> source, out FrameSubmitMessage message, out NnrpParseError error)
        {
            message = default;
            if (!NnrpFramedMessage.TryParse(source, NnrpHeaderParseOptions.Strict, out var framed, out error))
            {
                return false;
            }

            return TryParse(framed, out message, out error);
        }

        public static bool TryParse(NnrpFramedMessage framed, out FrameSubmitMessage message, out NnrpParseError error)
        {
            message = default;
            error = NnrpParseError.None;

            if (framed.Header.MessageType != MessageType.FrameSubmit
                || framed.Header.MetaLength != MetadataLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (!LegacyWireMetadata.TryParse(framed.Metadata.Span, strict: true, out var legacyMetadata, out error))
            {
                return false;
            }

            if (framed.Body.Length < TensorSubmitBlock.BlockLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (!TensorSubmitBlock.TryParse(framed.Body.Span.Slice(0, TensorSubmitBlock.BlockLength), out var submitBlock, out error))
            {
                return false;
            }

            if (submitBlock.CameraBytes > int.MaxValue || submitBlock.TileIndexBytes > int.MaxValue)
            {
                error = NnrpParseError.MessageTooLarge;
                return false;
            }

            var expectedProfileBlockBytes = checked((uint)(TensorSubmitBlock.BlockLength + submitBlock.CameraBytes + submitBlock.TileIndexBytes));
            if (legacyMetadata.ProfileBlockBytes != expectedProfileBlockBytes)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var cameraBytes = (int)submitBlock.CameraBytes;
            var tileIndexBytes = (int)submitBlock.TileIndexBytes;
            var bodyWithoutSubmitBlock = framed.Body.Slice(TensorSubmitBlock.BlockLength);

            if (bodyWithoutSubmitBlock.Length < cameraBytes)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var cameraBlock = bodyWithoutSubmitBlock.Slice(0, cameraBytes);
            var cursor = cameraBytes;

            if ((tileIndexBytes > 0 || submitBlock.SectionCount > 0)
                && !TryValidateZeroPadding(bodyWithoutSubmitBlock, cameraBytes, out cursor, out error))
            {
                return false;
            }

            if (bodyWithoutSubmitBlock.Length < cursor || bodyWithoutSubmitBlock.Length - cursor < tileIndexBytes)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var tileIndexBlock = bodyWithoutSubmitBlock.Slice(cursor, tileIndexBytes);
            var tileIds = submitBlock.TileCount == 0 ? Array.Empty<ushort>() : new ushort[submitBlock.TileCount];
            if (!TileIndexBlockCodec.TryDecode(
                    tileIndexBlock.Span,
                    submitBlock.TileIndexMode,
                    submitBlock.TileCount,
                    tileIds,
                    out var tileIdsWritten,
                    out error,
                    submitBlock.TileBaseId))
            {
                return false;
            }

            if (tileIdsWritten != tileIds.Length)
            {
                error = NnrpParseError.InvalidTileIndexBlock;
                return false;
            }

            if (!CheckedArithmetic.TryAdd(cursor, tileIndexBytes, out var nextOffset))
            {
                error = NnrpParseError.MessageTooLarge;
                return false;
            }

            cursor = nextOffset;
            if (submitBlock.SectionCount > 0
                && !TryValidateZeroPadding(bodyWithoutSubmitBlock, nextOffset, out cursor, out error))
            {
                return false;
            }

            var sections = submitBlock.SectionCount == 0 ? Array.Empty<TensorSectionBlock>() : new TensorSectionBlock[submitBlock.SectionCount];
            TensorRole? previousRole = null;
            for (var index = 0; index < sections.Length; index++)
            {
                if (bodyWithoutSubmitBlock.Length < cursor)
                {
                    error = NnrpParseError.SourceTooShort;
                    return false;
                }

                if (!TensorSectionBlock.TryParse(bodyWithoutSubmitBlock.Slice(cursor), submitBlock.TileCount, out var section, out var sectionBytes, out error))
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

                if (!CheckedArithmetic.TryAdd(cursor, sectionBytes, out nextOffset))
                {
                    error = NnrpParseError.MessageTooLarge;
                    return false;
                }

                if (index + 1 < sections.Length)
                {
                    if (!TryValidateZeroPadding(bodyWithoutSubmitBlock, nextOffset, out cursor, out error))
                    {
                        return false;
                    }
                }
                else
                {
                    cursor = nextOffset;
                }
            }

            if (cursor != bodyWithoutSubmitBlock.Length)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            SummarizeSections(sections, out var payloadDescriptorBytes, out var payloadDataBytes);
            if (legacyMetadata.PayloadDescriptorBytes != payloadDescriptorBytes || legacyMetadata.PayloadDataBytes != payloadDataBytes)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            message = new FrameSubmitMessage(
                framed.Header,
                BuildMetadata(legacyMetadata, submitBlock),
                cameraBlock,
                tileIds,
                sections);
            error = NnrpParseError.None;
            return true;
        }

        private static FrameSubmitMetadata NormalizeMetadata(FrameSubmitMetadata metadata, TensorSubmitBlock submitBlock)
        {
            var legacyDependencyFrameId = GetLegacyDependencyFrameId(metadata);
            return new FrameSubmitMetadata(
                sourceWidth: submitBlock.SourceWidth,
                sourceHeight: submitBlock.SourceHeight,
                tileWidth: submitBlock.TileWidth,
                tileHeight: submitBlock.TileHeight,
                tileCount: submitBlock.TileCount,
                sectionCount: submitBlock.SectionCount,
                frameClass: metadata.FrameClass,
                inputProfile: metadata.InputProfile,
                tileIndexMode: submitBlock.TileIndexMode,
                reserved0: 0,
                latencyBudgetMilliseconds: metadata.LatencyBudgetMilliseconds,
                targetFpsTimes100: metadata.TargetFpsTimes100,
                retryOfFrame: legacyDependencyFrameId,
                tileBaseId: submitBlock.TileBaseId,
                cameraBytes: submitBlock.CameraBytes,
                tileIndexBytes: submitBlock.TileIndexBytes,
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Inline,
                budgetPolicy: BudgetPolicy.None,
                lossTolerancePolicy: DefaultLossTolerancePolicy,
                reserved3: 0,
                objectRefMask: 0,
                dependencyFrameId: legacyDependencyFrameId,
                payloadKindBitmap: GetLegacyPayloadKind(metadata),
                payloadFrameCount: 0,
                reserved4: 0);
        }

        private static FrameSubmitMetadata BuildMetadata(LegacyWireMetadata legacyMetadata)
        {
            return new FrameSubmitMetadata(
                sourceWidth: 0,
                sourceHeight: 0,
                tileWidth: 0,
                tileHeight: 0,
                tileCount: 0,
                sectionCount: 0,
                frameClass: legacyMetadata.FrameClass,
                inputProfile: (InputProfile)legacyMetadata.ProfileId,
                tileIndexMode: TileIndexMode.DenseRange,
                reserved0: 0,
                latencyBudgetMilliseconds: legacyMetadata.LatencyBudgetMilliseconds,
                targetFpsTimes100: legacyMetadata.CadenceHintX100,
                retryOfFrame: legacyMetadata.DependencyFrameId,
                tileBaseId: 0,
                cameraBytes: 0,
                tileIndexBytes: 0,
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Inline,
                budgetPolicy: BudgetPolicy.None,
                lossTolerancePolicy: DefaultLossTolerancePolicy,
                reserved3: 0,
                objectRefMask: 0,
                dependencyFrameId: legacyMetadata.DependencyFrameId,
                payloadKindBitmap: legacyMetadata.PayloadKind,
                payloadFrameCount: 0,
                reserved4: 0);
        }

        private static FrameSubmitMetadata BuildMetadata(LegacyWireMetadata legacyMetadata, TensorSubmitBlock submitBlock)
        {
            return new FrameSubmitMetadata(
                sourceWidth: submitBlock.SourceWidth,
                sourceHeight: submitBlock.SourceHeight,
                tileWidth: submitBlock.TileWidth,
                tileHeight: submitBlock.TileHeight,
                tileCount: submitBlock.TileCount,
                sectionCount: submitBlock.SectionCount,
                frameClass: legacyMetadata.FrameClass,
                inputProfile: (InputProfile)legacyMetadata.ProfileId,
                tileIndexMode: submitBlock.TileIndexMode,
                reserved0: 0,
                latencyBudgetMilliseconds: legacyMetadata.LatencyBudgetMilliseconds,
                targetFpsTimes100: legacyMetadata.CadenceHintX100,
                retryOfFrame: legacyMetadata.DependencyFrameId,
                tileBaseId: submitBlock.TileBaseId,
                cameraBytes: submitBlock.CameraBytes,
                tileIndexBytes: submitBlock.TileIndexBytes,
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Inline,
                budgetPolicy: BudgetPolicy.None,
                lossTolerancePolicy: DefaultLossTolerancePolicy,
                reserved3: 0,
                objectRefMask: 0,
                dependencyFrameId: legacyMetadata.DependencyFrameId,
                payloadKindBitmap: legacyMetadata.PayloadKind,
                payloadFrameCount: 0,
                reserved4: 0);
        }

        private static byte[] SerializeMetadata(FrameSubmitMetadata metadata, TensorSubmitBlock submitBlock, ReadOnlySpan<TensorSectionBlock> sections)
        {
            SummarizeSections(sections, out var payloadDescriptorBytes, out var payloadDataBytes);
            var legacyMetadata = new LegacyWireMetadata(
                profileId: (ushort)metadata.InputProfile,
                payloadKind: GetLegacyPayloadKind(metadata),
                frameClass: metadata.FrameClass,
                submitFlags: 0,
                profileFlags: 0,
                latencyBudgetMilliseconds: metadata.LatencyBudgetMilliseconds,
                cadenceHintX100: metadata.TargetFpsTimes100,
                dependencyFrameId: GetLegacyDependencyFrameId(metadata),
                profileBlockBytes: checked((uint)(TensorSubmitBlock.BlockLength + submitBlock.CameraBytes + submitBlock.TileIndexBytes)),
                payloadDescriptorBytes: payloadDescriptorBytes,
                payloadDataBytes: payloadDataBytes,
                reserved0: 0);

            var buffer = new byte[MetadataLength];
            legacyMetadata.Write(buffer);
            return buffer;
        }

        private static uint GetLegacyDependencyFrameId(FrameSubmitMetadata metadata)
        {
            return metadata.DependencyFrameId != 0 ? metadata.DependencyFrameId : metadata.RetryOfFrame;
        }

        private static PayloadKind GetLegacyPayloadKind(FrameSubmitMetadata metadata)
        {
            var payloadKind = metadata.PayloadKindBitmap == 0 ? PayloadKind.Tensor : metadata.PayloadKindBitmap;
            var rawPayloadKind = (uint)payloadKind;
            if ((rawPayloadKind & (rawPayloadKind - 1)) != 0)
            {
                if ((payloadKind & PayloadKind.Tensor) != 0)
                {
                    return PayloadKind.Tensor;
                }

                throw new InvalidOperationException("FrameSubmitMessage only supports a single legacy payload kind bit.");
            }

            return payloadKind;
        }

        private readonly struct LegacyWireMetadata
        {
            public LegacyWireMetadata(
                ushort profileId,
                PayloadKind payloadKind,
                FrameClass frameClass,
                ushort submitFlags,
                ushort profileFlags,
                ushort latencyBudgetMilliseconds,
                ushort cadenceHintX100,
                uint dependencyFrameId,
                uint profileBlockBytes,
                uint payloadDescriptorBytes,
                uint payloadDataBytes,
                uint reserved0)
            {
                ProfileId = profileId;
                PayloadKind = payloadKind;
                FrameClass = frameClass;
                SubmitFlags = submitFlags;
                ProfileFlags = profileFlags;
                LatencyBudgetMilliseconds = latencyBudgetMilliseconds;
                CadenceHintX100 = cadenceHintX100;
                DependencyFrameId = dependencyFrameId;
                ProfileBlockBytes = profileBlockBytes;
                PayloadDescriptorBytes = payloadDescriptorBytes;
                PayloadDataBytes = payloadDataBytes;
                Reserved0 = reserved0;
            }

            public ushort ProfileId { get; }

            public PayloadKind PayloadKind { get; }

            public FrameClass FrameClass { get; }

            public ushort SubmitFlags { get; }

            public ushort ProfileFlags { get; }

            public ushort LatencyBudgetMilliseconds { get; }

            public ushort CadenceHintX100 { get; }

            public uint DependencyFrameId { get; }

            public uint ProfileBlockBytes { get; }

            public uint PayloadDescriptorBytes { get; }

            public uint PayloadDataBytes { get; }

            public uint Reserved0 { get; }

            public void Write(Span<byte> destination)
            {
                var writer = new FixedBinaryWriter(destination);
                if (!writer.TryWriteUInt16(ProfileId)
                    || !writer.TryWriteByte(checked((byte)PayloadKind))
                    || !writer.TryWriteByte((byte)FrameClass)
                    || !writer.TryWriteUInt16(SubmitFlags)
                    || !writer.TryWriteUInt16(ProfileFlags)
                    || !writer.TryWriteUInt16(LatencyBudgetMilliseconds)
                    || !writer.TryWriteUInt16(CadenceHintX100)
                    || !writer.TryWriteUInt32(DependencyFrameId)
                    || !writer.TryWriteUInt32(ProfileBlockBytes)
                    || !writer.TryWriteUInt32(PayloadDescriptorBytes)
                    || !writer.TryWriteUInt32(PayloadDataBytes)
                    || !writer.TryWriteUInt32(Reserved0))
                {
                    throw new InvalidOperationException("Failed to serialize FrameSubmitMessage metadata.");
                }
            }

            public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out LegacyWireMetadata metadata, out NnrpParseError error)
            {
                metadata = default;
                error = NnrpParseError.None;
                if (source.Length < MetadataLength)
                {
                    error = NnrpParseError.SourceTooShort;
                    return false;
                }

                var reader = new FixedBinaryReader(source);
                if (!reader.TryReadUInt16(out var profileId)
                    || !reader.TryReadByte(out var payloadKind)
                    || !reader.TryReadByte(out var frameClass)
                    || !reader.TryReadUInt16(out var submitFlags)
                    || !reader.TryReadUInt16(out var profileFlags)
                    || !reader.TryReadUInt16(out var latencyBudgetMilliseconds)
                    || !reader.TryReadUInt16(out var cadenceHintX100)
                    || !reader.TryReadUInt32(out var dependencyFrameId)
                    || !reader.TryReadUInt32(out var profileBlockBytes)
                    || !reader.TryReadUInt32(out var payloadDescriptorBytes)
                    || !reader.TryReadUInt32(out var payloadDataBytes)
                    || !reader.TryReadUInt32(out var reserved0))
                {
                    error = NnrpParseError.SourceTooShort;
                    return false;
                }

                if (strict && reserved0 != 0)
                {
                    error = NnrpParseError.NonZeroReservedField;
                    return false;
                }

                metadata = new LegacyWireMetadata(
                    profileId,
                    (PayloadKind)payloadKind,
                    (FrameClass)frameClass,
                    submitFlags,
                    profileFlags,
                    latencyBudgetMilliseconds,
                    cadenceHintX100,
                    dependencyFrameId,
                    profileBlockBytes,
                    payloadDescriptorBytes,
                    payloadDataBytes,
                    reserved0);
                return true;
            }
        }

        private static bool TryGetAlignedBodyLength(
            int submitBlockBytes,
            int cameraBytes,
            int tileIndexBytes,
            ReadOnlySpan<TensorSectionBlock> sections,
            out int bodyLength)
        {
            bodyLength = 0;
            if (!CheckedArithmetic.TryAdd(submitBlockBytes, cameraBytes, out var runningTotal))
            {
                return false;
            }

            runningTotal = BinaryAlignment.AlignUp(runningTotal, 8);
            if (!CheckedArithmetic.TryAdd(runningTotal, tileIndexBytes, out runningTotal))
            {
                return false;
            }

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
    }
}
