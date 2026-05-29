using System;
using System.Buffers.Binary;

namespace Nnrp.Core
{
    public readonly struct ResultPacketSummary
    {
        public ResultPacketSummary(string messageType, uint frameId, uint sessionId, ushort tileCount)
        {
            MessageType = messageType ?? string.Empty;
            FrameId = frameId;
            SessionId = sessionId;
            TileCount = tileCount;
        }

        public string MessageType { get; }

        public uint FrameId { get; }

        public uint SessionId { get; }

        public ushort TileCount { get; }
    }

    public static class SmokePackets
    {
        private static readonly byte[] DefaultCameraBlock = BuildDefaultCameraBlock();
        private static readonly ushort[] DefaultTileIds = { 0, 1, 2 };

        public static FrameSubmitMessage CreateSmokeFrameSubmitMessage(uint sessionId, uint frameId, ushort viewId = 0, ulong traceId = 0)
        {
            var section = CreateLumaHintSection();
            var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(DefaultTileIds, TileIndexMode.RawUInt16);
            var metadata = new FrameSubmitMetadata(
                sourceWidth: 64,
                sourceHeight: 64,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: (ushort)DefaultTileIds.Length,
                sectionCount: 1,
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.ChangedTilesLuma,
                tileIndexMode: TileIndexMode.RawUInt16,
                latencyBudgetMilliseconds: 16,
                targetFpsTimes100: 0,
                retryOfFrame: 0,
                tileBaseId: 0,
                cameraBytes: (uint)DefaultCameraBlock.Length,
                tileIndexBytes: (uint)tileIndexBytes);
            var bodyLength = FrameSubmitMessage.ComputeBodyLength(DefaultCameraBlock.Length, tileIndexBytes, new[] { section });
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMessage.MetadataLength,
                bodyLength: bodyLength,
                sessionId: sessionId,
                frameId: frameId,
                viewId: viewId,
                routeId: 0,
                traceId: traceId);
            return new FrameSubmitMessage(header, metadata, DefaultCameraBlock, DefaultTileIds, new[] { section });
        }

        public static byte[] BuildSmokeFrameSubmitPacket(uint sessionId, uint frameId, ushort viewId = 0, ulong traceId = 0)
        {
            var section = CreateLumaHintSection();
            var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(DefaultTileIds, TileIndexMode.RawUInt16);
            var metadata = new FrameSubmitMetadata(
                sourceWidth: 64,
                sourceHeight: 64,
                tileWidth: 32,
                tileHeight: 32,
                tileCount: (ushort)DefaultTileIds.Length,
                sectionCount: 1,
                frameClass: FrameClass.Keyframe,
                inputProfile: InputProfile.ChangedTilesLuma,
                tileIndexMode: TileIndexMode.RawUInt16,
                reserved0: 0,
                latencyBudgetMilliseconds: 16,
                targetFpsTimes100: 0,
                retryOfFrame: 0,
                tileBaseId: 0,
                cameraBytes: (uint)DefaultCameraBlock.Length,
                tileIndexBytes: (uint)tileIndexBytes,
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Inline,
                budgetPolicy: BudgetPolicy.None,
                lossTolerancePolicy: 0xFF,
                reserved3: 0,
                objectRefMask: 0,
                dependencyFrameId: 0,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0,
                reserved4: 0);
            var bodyLength = FrameSubmitMessage.ComputeBodyLength(DefaultCameraBlock.Length, tileIndexBytes, new[] { section });
            var header = new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.FrameSubmit,
                flags: HeaderFlags.None,
                metaLength: FrameSubmitMetadata.MetadataLength,
                bodyLength: bodyLength,
                sessionId: sessionId,
                frameId: frameId,
                viewId: viewId,
                routeId: 0,
                traceId: traceId);

            var tileIndexBlock = TileIndexBlockCodec.Encode(DefaultTileIds, TileIndexMode.RawUInt16, tileBaseId: 0);
            var body = new byte[bodyLength];
            var offset = 0;

            DefaultCameraBlock.CopyTo(body.AsSpan(offset, DefaultCameraBlock.Length));
            offset += DefaultCameraBlock.Length;
            offset = BinaryAlignment.AlignUp(offset, 8);

            tileIndexBlock.CopyTo(body.AsSpan(offset, tileIndexBlock.Length));
            offset += tileIndexBlock.Length;
            offset = BinaryAlignment.AlignUp(offset, 8);

            if (!section.TryCopyTo(body.AsSpan(offset), out var written) || written != section.TotalLength)
            {
                throw new InvalidOperationException("Failed to serialize smoke tensor section.");
            }

            return new NnrpFramedMessage(header, metadata.ToArray(), body).ToArray();
        }

        public static ResultPacketSummary DescribeResultPacket(byte[] packetBytes)
        {
            if (packetBytes == null || packetBytes.Length == 0)
            {
                throw new ArgumentException("Result packet must not be empty.", nameof(packetBytes));
            }

            if (!ResultPushMessage.TryParse(packetBytes, out var message, out var error))
            {
                throw new InvalidOperationException($"Failed to parse RESULT_PUSH packet: {error}.");
            }

            return new ResultPacketSummary(
                message.Header.MessageType.ToString(),
                message.Header.FrameId,
                message.Header.SessionId,
                message.Metadata.TileCount);
        }

        private static TensorSectionBlock CreateLumaHintSection()
        {
            var payload = new byte[32 * 32 * DefaultTileIds.Length];
            FillTilePayload(payload, 0, 32 * 32, 7);
            FillTilePayload(payload, 32 * 32, 32 * 32, 9);
            FillTilePayload(payload, 2 * 32 * 32, 32 * 32, 11);

            var lengthTable = new byte[sizeof(uint) * DefaultTileIds.Length];
            for (var index = 0; index < DefaultTileIds.Length; index++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(lengthTable.AsSpan(index * sizeof(uint), sizeof(uint)), 32u * 32u);
            }

            return new TensorSectionBlock(
                new TensorSectionDescriptor(
                    role: (TensorRole)5,
                    codec: CodecId.Raw,
                    dtype: DTypeId.UInt8,
                    layout: TensorLayoutId.Nhwc,
                    scalePolicy: ScalePolicy.None,
                    flags: 0,
                    elementCountPerTile: 0,
                    codecTableBytes: 0,
                    lengthTableBytes: (uint)lengthTable.Length,
                    payloadBytes: (uint)payload.Length,
                    payloadStrideBytes: 0),
                Array.Empty<byte>(),
                lengthTable,
                payload);
        }

        private static byte[] BuildDefaultCameraBlock()
        {
            var payload = new byte[22];
            payload[0] = (byte)'N';
            payload[1] = (byte)'R';
            payload[2] = (byte)'C';
            payload[3] = (byte)'M';
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(4, 2), 1);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(6, 2), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(8, 2), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(10, 2), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(12, 2), 0);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(14, 4), BitConverter.SingleToInt32Bits(0.0f));
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(18, 4), BitConverter.SingleToInt32Bits(0.0f));
            return payload;
        }

        private static void FillTilePayload(byte[] destination, int offset, int length, byte value)
        {
            for (var index = 0; index < length; index++)
            {
                destination[offset + index] = value;
            }
        }

    }
}
