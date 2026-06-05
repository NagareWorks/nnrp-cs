using System;

namespace Nnrp.Core
{
    public readonly struct FrameSubmitMessage
    {
        public const int MetadataLength = FrameSubmitMetadata.MetadataLength;

        /// <summary>
        /// Creates a frame submit message and canonicalizes reserved metadata fields to zero
        /// so the serialized metadata remains valid under strict round-trip parsing.
        /// </summary>
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
            if (!TryGetAlignedBodyLength(
                    cameraBlock.Length,
                    tileIndexLength,
                    sections.Span,
                    out var bodyLength)
                || header.BodyLength != (uint)bodyLength)
            {
                throw new ArgumentException("Header body length must match the computed FrameSubmit body length.", nameof(header));
            }

            if (!TryValidateMetadataContract(
                    metadata,
                    cameraBlock.Length,
                    tileIds.Length,
                    sections.Length,
                    tileIndexLength,
                    out var validationError))
            {
                throw new ArgumentException(validationError, nameof(metadata));
            }

            Header = header;
            Metadata = NormalizeMetadata(metadata, cameraBlock.Length, tileIds.Length, sections.Length, tileIndexLength);
            TensorSubmitBlock = CreateTensorSubmitBlock(Metadata);
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
            var metadataBytes = Metadata.ToArray();
            var tileIndexBlock = TileIndexBlockCodec.Encode(TileIds.Span, Metadata.TileIndexMode, Metadata.TileBaseId);
            var body = new byte[checked((int)Header.BodyLength)];

            var offset = 0;
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

        public static uint ComputeBodyLength(int cameraBytes, int tileIndexBytes, ReadOnlySpan<TensorSectionBlock> sections)
        {
            if (cameraBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(cameraBytes), cameraBytes, "cameraBytes must be non-negative.");
            }

            if (tileIndexBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tileIndexBytes), tileIndexBytes, "tileIndexBytes must be non-negative.");
            }

            if (!TryComputeBodyLength(cameraBytes, tileIndexBytes, sections, out var bodyLength))
            {
                throw new OverflowException("FrameSubmitMessage body length exceeds Int32.MaxValue.");
            }

            return bodyLength;
        }

        public static bool TryComputeBodyLength(int cameraBytes, int tileIndexBytes, ReadOnlySpan<TensorSectionBlock> sections, out uint bodyLength)
        {
            bodyLength = 0;
            if (cameraBytes < 0 || tileIndexBytes < 0)
            {
                return false;
            }

            if (!TryGetAlignedBodyLength(cameraBytes, tileIndexBytes, sections, out var alignedBodyLength))
            {
                return false;
            }

            bodyLength = (uint)alignedBodyLength;
            return true;
        }

        public static bool TryParseMetadata(ReadOnlySpan<byte> source, out FrameSubmitMetadata metadata)
        {
            return TryParseMetadata(source, strict: false, out metadata, out _);
        }

        public static bool TryParseMetadata(ReadOnlySpan<byte> source, bool strict, out FrameSubmitMetadata metadata, out NnrpParseError error)
        {
            return FrameSubmitMetadata.TryParse(source, strict, out metadata, out error);
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

            if (!FrameSubmitMetadata.TryParse(framed.Metadata.Span, strict: true, out var metadata, out error))
            {
                return false;
            }

            if (!TryValidateMetadataContract(
                    metadata,
                    expectedCameraBytes: null,
                    expectedTileCount: null,
                    expectedSectionCount: null,
                    expectedTileIndexBytes: null,
                    out _))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (metadata.CameraBytes > int.MaxValue || metadata.TileIndexBytes > int.MaxValue)
            {
                error = NnrpParseError.MessageTooLarge;
                return false;
            }

            var cameraBytes = (int)metadata.CameraBytes;
            var tileIndexBytes = (int)metadata.TileIndexBytes;

            if (framed.Body.Length < cameraBytes)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var cameraBlock = framed.Body.Slice(0, cameraBytes);
            var cursor = cameraBytes;

            if (!TryValidateZeroPadding(framed.Body, cameraBytes, out cursor, out error))
            {
                return false;
            }

            if (framed.Body.Length < cursor || framed.Body.Length - cursor < tileIndexBytes)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var tileIndexBlock = framed.Body.Slice(cursor, tileIndexBytes);
            var tileIds = metadata.TileCount == 0 ? Array.Empty<ushort>() : new ushort[metadata.TileCount];
            if (!TileIndexBlockCodec.TryDecode(
                    tileIndexBlock.Span,
                    metadata.TileIndexMode,
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

            if (!CheckedArithmetic.TryAdd(cursor, tileIndexBytes, out var nextOffset))
            {
                error = NnrpParseError.MessageTooLarge;
                return false;
            }

            cursor = nextOffset;
            if (metadata.SectionCount > 0
                && !TryValidateZeroPadding(framed.Body, nextOffset, out cursor, out error))
            {
                return false;
            }

            var sections = metadata.SectionCount == 0 ? Array.Empty<TensorSectionBlock>() : new TensorSectionBlock[metadata.SectionCount];
            TensorRole? previousRole = null;
            for (var index = 0; index < sections.Length; index++)
            {
                if (framed.Body.Length < cursor)
                {
                    error = NnrpParseError.SourceTooShort;
                    return false;
                }

                if (!TensorSectionBlock.TryParse(framed.Body.Slice(cursor), metadata.TileCount, out var section, out var sectionBytes, out error))
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
                    if (!TryValidateZeroPadding(framed.Body, nextOffset, out cursor, out error))
                    {
                        return false;
                    }
                }
                else
                {
                    cursor = nextOffset;
                }
            }

            if (cursor != framed.Body.Length)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (!TryGetAlignedBodyLength(cameraBytes, tileIndexBytes, sections, out var expectedBodyLength)
                || framed.Header.BodyLength != (uint)expectedBodyLength)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            message = new FrameSubmitMessage(
                framed.Header,
                metadata,
                cameraBlock,
                tileIds,
                sections);
            error = NnrpParseError.None;
            return true;
        }

        private static FrameSubmitMetadata NormalizeMetadata(
            FrameSubmitMetadata metadata,
            int cameraBytes,
            int tileCount,
            int sectionCount,
            int tileIndexBytes)
        {
            // Canonicalize reserved fields so constructor output survives strict parser round-trips.
            return new FrameSubmitMetadata(
                sourceWidth: metadata.SourceWidth,
                sourceHeight: metadata.SourceHeight,
                tileWidth: metadata.TileWidth,
                tileHeight: metadata.TileHeight,
                tileCount: checked((ushort)tileCount),
                sectionCount: checked((ushort)sectionCount),
                frameClass: metadata.FrameClass,
                inputProfile: metadata.InputProfile,
                tileIndexMode: metadata.TileIndexMode,
                reserved0: 0,
                latencyBudgetMilliseconds: metadata.LatencyBudgetMilliseconds,
                targetFpsTimes100: metadata.TargetFpsTimes100,
                retryOfFrame: metadata.RetryOfFrame,
                tileBaseId: metadata.TileBaseId,
                cameraBytes: checked((uint)cameraBytes),
                tileIndexBytes: checked((uint)tileIndexBytes),
                reserved1: 0,
                reserved2: 0,
                submitMode: metadata.SubmitMode,
                budgetPolicy: metadata.BudgetPolicy,
                lossTolerancePolicy: metadata.LossTolerancePolicy,
                reserved3: 0,
                objectRefMask: metadata.ObjectRefMask,
                dependencyFrameId: metadata.DependencyFrameId,
                payloadKindBitmap: metadata.PayloadKindBitmap,
                payloadFrameCount: metadata.PayloadFrameCount,
                reserved4: 0);
        }

        private static TensorSubmitBlock CreateTensorSubmitBlock(FrameSubmitMetadata metadata)
        {
            return new TensorSubmitBlock(
                metadata.SourceWidth,
                metadata.SourceHeight,
                metadata.TileWidth,
                metadata.TileHeight,
                metadata.TileCount,
                metadata.SectionCount,
                metadata.TileIndexMode,
                tensorFlags: 0,
                reserved0: 0,
                metadata.TileBaseId,
                metadata.CameraBytes,
                metadata.TileIndexBytes);
        }

        private static bool TryValidateMetadataContract(
            FrameSubmitMetadata metadata,
            int? expectedCameraBytes,
            int? expectedTileCount,
            int? expectedSectionCount,
            int? expectedTileIndexBytes,
            out string validationError)
        {
            validationError = string.Empty;

            if (metadata.SubmitMode != SubmitMode.Inline)
            {
                validationError = "FrameSubmitMessage only supports inline submit mode.";
                return false;
            }

            if (!SubmitObjectReferenceMask.TryValidateForSubmitMode(metadata.SubmitMode, metadata.ObjectRefMask, out _))
            {
                validationError = "FrameSubmitMessage does not support submit object references.";
                return false;
            }

            if (metadata.PayloadKindBitmap != PayloadKind.Tensor || metadata.PayloadFrameCount != 0)
            {
                validationError = "FrameSubmitMessage only supports inline tensor payloads.";
                return false;
            }

            if (expectedCameraBytes.HasValue && metadata.CameraBytes != (uint)expectedCameraBytes.Value)
            {
                validationError = "Camera block length must match metadata.CameraBytes.";
                return false;
            }

            if (expectedTileCount.HasValue && metadata.TileCount != expectedTileCount.Value)
            {
                validationError = "Tile id count must match metadata.TileCount.";
                return false;
            }

            if (expectedSectionCount.HasValue && metadata.SectionCount != expectedSectionCount.Value)
            {
                validationError = "Section count must match metadata.SectionCount.";
                return false;
            }

            if (expectedTileIndexBytes.HasValue && metadata.TileIndexBytes != (uint)expectedTileIndexBytes.Value)
            {
                validationError = "Tile index block length must match metadata.TileIndexBytes.";
                return false;
            }

            return true;
        }

        private static bool TryGetAlignedBodyLength(
            int cameraBytes,
            int tileIndexBytes,
            ReadOnlySpan<TensorSectionBlock> sections,
            out int bodyLength)
        {
            bodyLength = BinaryAlignment.AlignUp(cameraBytes, 8);
            if (!CheckedArithmetic.TryAdd(bodyLength, tileIndexBytes, out bodyLength))
            {
                return false;
            }

            foreach (var section in sections)
            {
                bodyLength = BinaryAlignment.AlignUp(bodyLength, 8);
                if (!CheckedArithmetic.TryAdd(bodyLength, section.TotalLength, out bodyLength))
                {
                    return false;
                }
            }

            return true;
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
