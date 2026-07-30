using System;
using System.Collections.Generic;
using Nnrp.Core;

namespace Nnrp.Client
{
    public readonly struct NnrpSubmitHeaderContext
    {
        public NnrpSubmitHeaderContext(
            HeaderFlags flags = HeaderFlags.None,
            ushort viewId = 0,
            ushort routeId = 0,
            ulong traceId = 0)
        {
            Flags = flags;
            ViewId = viewId;
            RouteId = routeId;
            TraceId = traceId;
        }

        public HeaderFlags Flags { get; }

        public ushort ViewId { get; }

        public ushort RouteId { get; }

        public ulong TraceId { get; }
    }

    public readonly struct NnrpSubmitIdentity
    {
        public NnrpSubmitIdentity(
            ulong operationId,
            uint frameId,
            NnrpSubmitHeaderContext header = default)
        {
            if (operationId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operationId));
            }

            if (frameId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frameId));
            }

            OperationId = operationId;
            FrameId = frameId;
            Header = header;
        }

        public ulong OperationId { get; }

        public uint FrameId { get; }

        public NnrpSubmitHeaderContext Header { get; }
    }

    public readonly struct NnrpSubmitPolicy
    {
        public NnrpSubmitPolicy(
            byte frameClass = 0,
            ushort latencyBudgetMilliseconds = 0,
            ushort targetFpsTimes100 = 0,
            uint retryOfFrame = 0,
            BudgetPolicy budgetPolicy = BudgetPolicy.None,
            LossTolerancePolicy lossTolerancePolicy = LossTolerancePolicy.InheritSession,
            uint dependencyFrameId = 0)
        {
            FrameClass = frameClass;
            LatencyBudgetMilliseconds = latencyBudgetMilliseconds;
            TargetFpsTimes100 = targetFpsTimes100;
            RetryOfFrame = retryOfFrame;
            BudgetPolicy = budgetPolicy;
            LossTolerancePolicy = lossTolerancePolicy;
            DependencyFrameId = dependencyFrameId;
        }

        public byte FrameClass { get; }

        public ushort LatencyBudgetMilliseconds { get; }

        public ushort TargetFpsTimes100 { get; }

        public uint RetryOfFrame { get; }

        public BudgetPolicy BudgetPolicy { get; }

        public LossTolerancePolicy LossTolerancePolicy { get; }

        public uint DependencyFrameId { get; }
    }

    public readonly struct NnrpSubmitObjectReferences
    {
        public NnrpSubmitObjectReferences(
            ObjectReferenceBlock? camera = null,
            ObjectReferenceBlock? tileIndex = null,
            ObjectReferenceBlock? tensorSectionTable = null)
        {
            ValidateSlot(camera, CacheObjectKind.CameraBlock, nameof(camera));
            ValidateSlot(tileIndex, CacheObjectKind.TileIndexBlock, nameof(tileIndex));
            ValidateSlot(tensorSectionTable, CacheObjectKind.TensorSectionTable, nameof(tensorSectionTable));
            Camera = camera;
            TileIndex = tileIndex;
            TensorSectionTable = tensorSectionTable;
        }

        public ObjectReferenceBlock? Camera { get; }

        public ObjectReferenceBlock? TileIndex { get; }

        public ObjectReferenceBlock? TensorSectionTable { get; }

        private static void ValidateSlot(ObjectReferenceBlock? value, CacheObjectKind kind, string parameterName)
        {
            if (value.HasValue && value.Value.ObjectKind != kind)
            {
                throw new ArgumentException($"The reference must have object kind {kind}.", parameterName);
            }
        }
    }

    public readonly struct NnrpTensorSection
    {
        public NnrpTensorSection(
            ushort roleId,
            byte defaultCodecId,
            byte dtypeId,
            byte layoutId,
            ReadOnlyMemory<ReadOnlyMemory<byte>> tilePayloads,
            byte scalePolicy = 0,
            uint elementCountPerTile = 0,
            ReadOnlyMemory<byte> codecIds = default,
            uint payloadStrideBytes = 0)
        {
            RoleId = roleId;
            DefaultCodecId = defaultCodecId;
            DTypeId = dtypeId;
            LayoutId = layoutId;
            ScalePolicy = scalePolicy;
            ElementCountPerTile = elementCountPerTile;
            TilePayloads = tilePayloads;
            CodecIds = codecIds;
            PayloadStrideBytes = payloadStrideBytes;
        }

        public ushort RoleId { get; }

        public byte DefaultCodecId { get; }

        public byte DTypeId { get; }

        public byte LayoutId { get; }

        public byte ScalePolicy { get; }

        public uint ElementCountPerTile { get; }

        public ReadOnlyMemory<ReadOnlyMemory<byte>> TilePayloads { get; }

        public ReadOnlyMemory<byte> CodecIds { get; }

        public uint PayloadStrideBytes { get; }
    }

    public readonly struct NnrpTensorSubmitInput
    {
        public NnrpTensorSubmitInput(
            NnrpSubmitIdentity identity,
            NnrpSubmitPolicy policy,
            ushort sourceWidth,
            ushort sourceHeight,
            ushort tileWidth,
            ushort tileHeight,
            ReadOnlyMemory<ushort> tileIds,
            ReadOnlyMemory<NnrpTensorSection> sections,
            InputProfile inputProfile,
            TileIndexMode tileIndexMode,
            ReadOnlyMemory<byte> cameraBlock = default,
            uint tileBaseId = 0,
            NnrpSubmitObjectReferences references = default)
        {
            Identity = identity;
            Policy = policy;
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            TileIds = tileIds;
            Sections = sections;
            CameraBlock = cameraBlock;
            InputProfile = inputProfile;
            TileIndexMode = tileIndexMode;
            TileBaseId = tileBaseId;
            References = references;
        }

        public NnrpSubmitIdentity Identity { get; }
        public NnrpSubmitPolicy Policy { get; }
        public ushort SourceWidth { get; }
        public ushort SourceHeight { get; }
        public ushort TileWidth { get; }
        public ushort TileHeight { get; }
        public ReadOnlyMemory<ushort> TileIds { get; }
        public ReadOnlyMemory<NnrpTensorSection> Sections { get; }
        public ReadOnlyMemory<byte> CameraBlock { get; }
        public InputProfile InputProfile { get; }
        public TileIndexMode TileIndexMode { get; }
        public uint TileBaseId { get; }
        public NnrpSubmitObjectReferences References { get; }
    }

    public readonly struct NnrpTokenChunk
    {
        public NnrpTokenChunk(
            ReadOnlyMemory<byte> payload,
            TypedPayloadDescriptorFlags descriptorFlags = TypedPayloadDescriptorFlags.Partial)
        {
            Payload = payload;
            DescriptorFlags = descriptorFlags;
        }

        public ReadOnlyMemory<byte> Payload { get; }
        public TypedPayloadDescriptorFlags DescriptorFlags { get; }
    }

    public readonly struct NnrpTokenSubmitInput
    {
        public NnrpTokenSubmitInput(
            NnrpSubmitIdentity identity,
            NnrpSubmitPolicy policy,
            ReadOnlyMemory<NnrpTokenChunk> chunks)
        {
            Identity = identity;
            Policy = policy;
            Chunks = chunks;
        }

        public NnrpSubmitIdentity Identity { get; }
        public NnrpSubmitPolicy Policy { get; }
        public ReadOnlyMemory<NnrpTokenChunk> Chunks { get; }
    }

    public readonly struct NnrpTypedPayloadInputFrame
    {
        public NnrpTypedPayloadInputFrame(
            ushort profileId,
            PayloadKind payloadKind,
            ReadOnlyMemory<byte> payload,
            TypedPayloadDescriptorFlags descriptorFlags = TypedPayloadDescriptorFlags.None,
            uint schemaId = 0,
            uint schemaVersion = 0,
            ushort streamSemantics = TypedPayloadDescriptor.StreamSemanticsDefault)
        {
            ProfileId = profileId;
            PayloadKind = payloadKind;
            Payload = payload;
            DescriptorFlags = descriptorFlags;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
            StreamSemantics = streamSemantics;
        }

        public ushort ProfileId { get; }
        public PayloadKind PayloadKind { get; }
        public TypedPayloadDescriptorFlags DescriptorFlags { get; }
        public uint SchemaId { get; }
        public uint SchemaVersion { get; }
        public ushort StreamSemantics { get; }
        public ReadOnlyMemory<byte> Payload { get; }
    }

    public readonly struct NnrpTypedPayloadSubmitInput
    {
        public NnrpTypedPayloadSubmitInput(
            NnrpSubmitIdentity identity,
            NnrpSubmitPolicy policy,
            ReadOnlyMemory<NnrpTypedPayloadInputFrame> frames)
        {
            Identity = identity;
            Policy = policy;
            Frames = frames;
        }

        public NnrpSubmitIdentity Identity { get; }
        public NnrpSubmitPolicy Policy { get; }
        public ReadOnlyMemory<NnrpTypedPayloadInputFrame> Frames { get; }
    }

    public readonly struct NnrpSubmitRequest
    {
        private const ushort MixedCodecFlag = 0x0001;
        private const ushort FixedStrideFlag = 0x0002;

        private NnrpSubmitRequest(
            NnrpSubmitIdentity identity,
            FrameSubmitMetadata metadata,
            byte[] body)
        {
            OperationId = identity.OperationId;
            FrameId = identity.FrameId;
            Header = identity.Header;
            Metadata = metadata;
            Body = body;
        }

        public ulong OperationId { get; }
        public uint FrameId { get; }
        public NnrpSubmitHeaderContext Header { get; }
        public FrameSubmitMetadata Metadata { get; }
        public ReadOnlyMemory<byte> Body { get; }

        public static NnrpSubmitRequest CreateTensor(NnrpTensorSubmitInput input)
        {
            ValidateIdentity(input.Identity);
            var tileIds = input.TileIds.ToArray();
            var sections = input.Sections.ToArray();
            var tileIndexPayload = TileIndexBlockCodec.Encode(tileIds, input.TileIndexMode, input.TileBaseId);
            var sectionTablePayload = BuildTensorSectionTable(sections, tileIds.Length);

            var inlineBlocks = new List<byte[]>(3);
            if (!input.References.Camera.HasValue && !input.CameraBlock.IsEmpty)
            {
                inlineBlocks.Add(BodyCodec.BuildInlineObjectBlock(CacheObjectKind.CameraBlock, input.CameraBlock.ToArray()));
            }

            if (!input.References.TileIndex.HasValue && tileIds.Length != 0)
            {
                inlineBlocks.Add(BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TileIndexBlock, tileIndexPayload));
            }

            if (!input.References.TensorSectionTable.HasValue && sections.Length != 0)
            {
                inlineBlocks.Add(BodyCodec.BuildInlineObjectBlock(CacheObjectKind.TensorSectionTable, sectionTablePayload));
            }

            var references = BuildReferences(input.References, out var objectRefMask);
            var hasInline = inlineBlocks.Count != 0;
            var hasReferences = references.Length != 0;
            var submitMode = hasInline && hasReferences
                ? SubmitMode.Mixed
                : hasReferences ? SubmitMode.Reference : SubmitMode.Inline;
            if (!SubmitObjectReferenceMask.TryValidateForSubmitMode(submitMode, objectRefMask, out var referenceError))
            {
                throw new ArgumentException($"Invalid tensor object references: {referenceError}.", nameof(input));
            }

            var body = BodyCodec.Pack(
                inlineObjectRegion: BodyCodec.PackInlineObjectRegion(inlineBlocks.ToArray()),
                objectReferenceRegion: BodyCodec.PackObjectReferenceRegion(references));
            var metadata = new FrameSubmitMetadata(
                input.SourceWidth,
                input.SourceHeight,
                input.TileWidth,
                input.TileHeight,
                checked((ushort)tileIds.Length),
                checked((ushort)sections.Length),
                (FrameClass)input.Policy.FrameClass,
                input.InputProfile,
                input.TileIndexMode,
                input.Policy.LatencyBudgetMilliseconds,
                input.Policy.TargetFpsTimes100,
                input.Policy.RetryOfFrame,
                input.TileBaseId,
                input.References.Camera.HasValue ? 0 : checked((uint)input.CameraBlock.Length),
                input.References.TileIndex.HasValue ? 0 : checked((uint)tileIndexPayload.Length),
                input.Identity.OperationId,
                submitMode,
                input.Policy.BudgetPolicy,
                input.Policy.LossTolerancePolicy,
                objectRefMask,
                input.Policy.DependencyFrameId,
                PayloadKind.Tensor,
                payloadFrameCount: 0);
            return new NnrpSubmitRequest(input.Identity, metadata, body);
        }

        public static NnrpSubmitRequest CreateToken(NnrpTokenSubmitInput input)
        {
            var chunks = input.Chunks.Span;
            if (chunks.Length == 0)
            {
                throw new ArgumentException("Token submit requires at least one chunk.", nameof(input));
            }

            var frames = new NnrpTypedPayloadInputFrame[chunks.Length];
            for (var index = 0; index < chunks.Length; index++)
            {
                frames[index] = new NnrpTypedPayloadInputFrame(
                    TypedPayloadDescriptor.ProfileToken,
                    PayloadKind.TokenChunk,
                    chunks[index].Payload,
                    chunks[index].DescriptorFlags,
                    TypedPayloadDescriptor.TokenDeltaSchemaId,
                    TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                    TypedPayloadDescriptor.StreamSemanticsAppend);
            }

            return CreateTypedPayload(new NnrpTypedPayloadSubmitInput(input.Identity, input.Policy, frames));
        }

        public static NnrpSubmitRequest CreateTypedPayload(NnrpTypedPayloadSubmitInput input)
        {
            ValidateIdentity(input.Identity);
            var frames = input.Frames.Span;
            if (frames.Length == 0)
            {
                throw new ArgumentException("Typed payload submit requires at least one frame.", nameof(input));
            }

            var descriptors = new TypedPayloadDescriptor[frames.Length];
            var payloadLength = 0;
            var payloadKindBitmap = PayloadKind.None;
            for (var index = 0; index < frames.Length; index++)
            {
                var frame = frames[index];
                descriptors[index] = new TypedPayloadDescriptor(
                    frame.PayloadKind,
                    frame.ProfileId,
                    (byte)frame.DescriptorFlags,
                    frame.SchemaId,
                    frame.SchemaVersion,
                    frame.StreamSemantics,
                    checked((uint)payloadLength),
                    checked((uint)frame.Payload.Length));
                payloadLength = checked(payloadLength + frame.Payload.Length);
                payloadKindBitmap |= frame.PayloadKind;
            }

            var payloadRegion = new byte[payloadLength];
            var payloadCursor = 0;
            for (var index = 0; index < frames.Length; index++)
            {
                frames[index].Payload.Span.CopyTo(payloadRegion.AsSpan(payloadCursor));
                payloadCursor += frames[index].Payload.Length;
            }

            if (!TypedPayloadRegionValidator.TryValidateTypedPayloadDescriptors(
                    payloadKindBitmap,
                    checked((ushort)frames.Length),
                    descriptors,
                    payloadRegion,
                    out var payloadError))
            {
                throw new ArgumentException($"Invalid typed payload submit: {payloadError}.", nameof(input));
            }

            var descriptorRegion = new byte[checked(descriptors.Length * TypedPayloadDescriptor.DescriptorLength)];
            for (var index = 0; index < descriptors.Length; index++)
            {
                descriptors[index].Write(descriptorRegion.AsSpan(index * TypedPayloadDescriptor.DescriptorLength));
            }

            var body = BodyCodec.Pack(
                typedPayloadDescriptorRegion: descriptorRegion,
                typedPayloadFrameRegion: payloadRegion);
            var metadata = new FrameSubmitMetadata(
                sourceWidth: 0,
                sourceHeight: 0,
                tileWidth: 0,
                tileHeight: 0,
                tileCount: 0,
                sectionCount: 0,
                frameClass: (FrameClass)input.Policy.FrameClass,
                inputProfile: InputProfile.Unspecified,
                tileIndexMode: TileIndexMode.RawUInt16,
                latencyBudgetMilliseconds: input.Policy.LatencyBudgetMilliseconds,
                targetFpsTimes100: input.Policy.TargetFpsTimes100,
                retryOfFrame: input.Policy.RetryOfFrame,
                tileBaseId: 0,
                cameraBytes: 0,
                tileIndexBytes: 0,
                operationId: input.Identity.OperationId,
                submitMode: SubmitMode.Inline,
                budgetPolicy: input.Policy.BudgetPolicy,
                lossTolerancePolicy: input.Policy.LossTolerancePolicy,
                objectRefMask: 0,
                dependencyFrameId: input.Policy.DependencyFrameId,
                payloadKindBitmap: payloadKindBitmap,
                payloadFrameCount: checked((ushort)frames.Length));
            return new NnrpSubmitRequest(input.Identity, metadata, body);
        }

        public byte[] EncodePayload()
        {
            var payload = new byte[checked(FrameSubmitMetadata.MetadataLength + Body.Length)];
            Metadata.Write(payload);
            Body.Span.CopyTo(payload.AsSpan(FrameSubmitMetadata.MetadataLength));
            return payload;
        }

        private static void ValidateIdentity(NnrpSubmitIdentity identity)
        {
            if (identity.OperationId == 0 || identity.FrameId == 0)
            {
                throw new ArgumentException("Submit operation and frame identifiers must be non-zero.", nameof(identity));
            }
        }

        private static ObjectReferenceBlock[] BuildReferences(
            NnrpSubmitObjectReferences references,
            out uint mask)
        {
            var blocks = new List<ObjectReferenceBlock>(3);
            mask = 0;
            AppendReference(references.Camera, CacheObjectKind.CameraBlock, 0, blocks, ref mask);
            AppendReference(references.TileIndex, CacheObjectKind.TileIndexBlock, 1, blocks, ref mask);
            AppendReference(references.TensorSectionTable, CacheObjectKind.TensorSectionTable, 2, blocks, ref mask);
            return blocks.ToArray();
        }

        private static void AppendReference(
            ObjectReferenceBlock? reference,
            CacheObjectKind expectedKind,
            int maskBit,
            List<ObjectReferenceBlock> blocks,
            ref uint mask)
        {
            if (!reference.HasValue)
            {
                return;
            }

            if (reference.Value.ObjectKind != expectedKind)
            {
                throw new ArgumentException($"Reference slot requires {expectedKind}.", nameof(reference));
            }

            blocks.Add(reference.Value);
            mask |= 1u << maskBit;
        }

        private static byte[] BuildTensorSectionTable(NnrpTensorSection[] sections, int tileCount)
        {
            var encodedSections = new byte[sections.Length][];
            ushort? previousRole = null;
            var totalLength = 0;
            for (var index = 0; index < sections.Length; index++)
            {
                var section = sections[index];
                if (previousRole.HasValue && section.RoleId <= previousRole.Value)
                {
                    throw new ArgumentException("Tensor sections must be strictly ordered by role ID.", nameof(sections));
                }

                previousRole = section.RoleId;
                encodedSections[index] = EncodeTensorSection(section, tileCount);
                totalLength = checked(BinaryAlignment.AlignUp(totalLength, 8) + encodedSections[index].Length);
            }

            var payload = new byte[totalLength];
            var cursor = 0;
            for (var index = 0; index < encodedSections.Length; index++)
            {
                cursor = BinaryAlignment.AlignUp(cursor, 8);
                encodedSections[index].CopyTo(payload, cursor);
                cursor += encodedSections[index].Length;
            }

            return payload;
        }

        private static byte[] EncodeTensorSection(NnrpTensorSection section, int tileCount)
        {
            var tilePayloads = section.TilePayloads.Span;
            var codecIds = section.CodecIds.Span;
            if (tilePayloads.Length != tileCount)
            {
                throw new ArgumentException("Tensor section payload count must match tile count.", nameof(section));
            }

            if (codecIds.Length != 0 && codecIds.Length != tileCount)
            {
                throw new ArgumentException("Tensor codec ID count must match tile count.", nameof(section));
            }

            var mixedCodec = false;
            for (var index = 0; index < codecIds.Length; index++)
            {
                mixedCodec |= codecIds[index] != section.DefaultCodecId;
            }

            var codecTable = mixedCodec ? codecIds.ToArray() : Array.Empty<byte>();
            var lengthTable = new byte[checked(tileCount * sizeof(uint))];
            var payloadLength = 0;
            for (var index = 0; index < tilePayloads.Length; index++)
            {
                var tileLength = tilePayloads[index].Length;
                if (section.PayloadStrideBytes != 0 && tileLength > section.PayloadStrideBytes)
                {
                    throw new ArgumentException("Tensor tile payload exceeds the fixed stride.", nameof(section));
                }

                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(
                    lengthTable.AsSpan(index * sizeof(uint), sizeof(uint)),
                    checked((uint)tileLength));
                payloadLength = checked(payloadLength + (section.PayloadStrideBytes == 0
                    ? tileLength
                    : checked((int)section.PayloadStrideBytes)));
            }

            var tensorPayload = new byte[payloadLength];
            var payloadCursor = 0;
            for (var index = 0; index < tilePayloads.Length; index++)
            {
                tilePayloads[index].Span.CopyTo(tensorPayload.AsSpan(payloadCursor));
                payloadCursor += section.PayloadStrideBytes == 0
                    ? tilePayloads[index].Length
                    : checked((int)section.PayloadStrideBytes);
            }

            var flags = (ushort)((mixedCodec ? MixedCodecFlag : 0)
                | (section.PayloadStrideBytes != 0 ? FixedStrideFlag : 0));
            var descriptor = new TensorSectionDescriptor(
                (TensorRole)section.RoleId,
                (CodecId)section.DefaultCodecId,
                (DTypeId)section.DTypeId,
                (TensorLayoutId)section.LayoutId,
                (ScalePolicy)section.ScalePolicy,
                flags,
                section.ElementCountPerTile,
                checked((uint)codecTable.Length),
                checked((uint)lengthTable.Length),
                checked((uint)tensorPayload.Length),
                section.PayloadStrideBytes);
            return new TensorSectionBlock(descriptor, codecTable, lengthTable, tensorPayload).ToArray();
        }
    }
}
