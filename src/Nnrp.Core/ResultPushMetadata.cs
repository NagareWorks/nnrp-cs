using System;

namespace Nnrp.Core
{
    public readonly struct ResultPushMetadata : IEquatable<ResultPushMetadata>
    {
        public const int MetadataLength = 64;
        public const int CurrentMetadataLength = MetadataLength;

        private readonly ushort sectionCount;
        private readonly ushort tileCount;
        private readonly TileIndexMode tileIndexMode;
        private readonly uint tileBaseId;
        private readonly uint tileIndexBytes;

        private ResultPushMetadata(
            ResultStatusCode statusCode,
            ResultFlags resultFlags,
            ushort activeProfileId,
            PayloadKind payloadKind,
            byte reserved0,
            ushort inferenceMilliseconds,
            ushort queueMilliseconds,
            ushort serverTotalMilliseconds,
            ushort reserved1,
            uint profileBlockBytes,
            uint payloadDescriptorBytes,
            uint payloadDataBytes,
            uint reserved2,
            ResultClass resultClass,
            BudgetPolicy appliedBudgetPolicy,
            uint reusedFrameId,
            ushort coveredTileCount,
            ushort droppedTileCount,
            PayloadKind payloadKindBitmap,
            ushort payloadFrameCount,
            ushort sectionCount,
            ushort tileCount,
            TileIndexMode tileIndexMode,
            uint tileBaseId,
            uint tileIndexBytes)
        {
            StatusCode = statusCode;
            ResultFlags = resultFlags;
            ActiveProfileId = activeProfileId;
            PayloadKind = payloadKind;
            Reserved0 = reserved0;
            InferenceMilliseconds = inferenceMilliseconds;
            QueueMilliseconds = queueMilliseconds;
            ServerTotalMilliseconds = serverTotalMilliseconds;
            Reserved1 = reserved1;
            ProfileBlockBytes = profileBlockBytes;
            PayloadDescriptorBytes = payloadDescriptorBytes;
            PayloadDataBytes = payloadDataBytes;
            Reserved2 = reserved2;
            ResultClass = resultClass;
            AppliedBudgetPolicy = appliedBudgetPolicy;
            ReusedFrameId = reusedFrameId;
            CoveredTileCount = coveredTileCount;
            DroppedTileCount = droppedTileCount;
            PayloadKindBitmap = payloadKindBitmap;
            PayloadFrameCount = payloadFrameCount;
            this.sectionCount = sectionCount;
            this.tileCount = tileCount;
            this.tileIndexMode = tileIndexMode;
            this.tileBaseId = tileBaseId;
            this.tileIndexBytes = tileIndexBytes;
        }

        public ResultPushMetadata(
            ResultStatusCode statusCode,
            ResultFlags resultFlags,
            ushort activeProfileId,
            PayloadKind payloadKind,
            byte reserved0,
            ushort inferenceMilliseconds,
            ushort queueMilliseconds,
            ushort serverTotalMilliseconds,
            ushort reserved1,
            uint profileBlockBytes,
            uint payloadDescriptorBytes,
            uint payloadDataBytes,
            uint reserved2 = 0)
            : this(
                statusCode,
                resultFlags,
                activeProfileId,
                payloadKind,
                reserved0,
                inferenceMilliseconds,
                queueMilliseconds,
                serverTotalMilliseconds,
                reserved1,
                profileBlockBytes,
                payloadDescriptorBytes,
                payloadDataBytes,
                reserved2,
                ResultClass.Complete,
                BudgetPolicy.None,
                0,
                0,
                0,
                payloadKind,
                0,
                0,
                0,
                TileIndexMode.DenseRange,
                0,
                0)
        {
        }

        public ResultPushMetadata(
            ResultStatusCode statusCode,
            ResultFlags resultFlags,
            ushort sectionCount,
            ushort tileCount,
            ushort activeProfileId,
            ushort inferenceMilliseconds,
            ushort queueMilliseconds,
            ushort serverTotalMilliseconds,
            uint tileBaseId,
            uint tileIndexBytes,
            ResultClass resultClass = ResultClass.Complete,
            BudgetPolicy appliedBudgetPolicy = BudgetPolicy.None,
            uint reusedFrameId = 0,
            ushort coveredTileCount = 0,
            ushort droppedTileCount = 0,
            PayloadKind payloadKindBitmap = PayloadKind.Tensor,
            ushort payloadFrameCount = 0)
            : this(
                statusCode,
                resultFlags,
                activeProfileId,
                PayloadKind.Tensor,
                0,
                inferenceMilliseconds,
                queueMilliseconds,
                serverTotalMilliseconds,
                0,
                (uint)(TensorResultBlock.BlockLength + tileIndexBytes),
                0,
                0,
                0,
                resultClass,
                appliedBudgetPolicy,
                reusedFrameId,
                NormalizeCoveredTileCount(resultClass, resultFlags, tileCount, coveredTileCount, droppedTileCount, payloadKindBitmap),
                droppedTileCount,
                payloadKindBitmap,
                payloadFrameCount,
                sectionCount,
                tileCount,
                InferTileIndexMode(tileCount, tileIndexBytes),
                tileBaseId,
                tileIndexBytes)
        {
        }

        private static ushort NormalizeCoveredTileCount(
            ResultClass resultClass,
            ResultFlags resultFlags,
            ushort tileCount,
            ushort coveredTileCount,
            ushort droppedTileCount,
            PayloadKind payloadKindBitmap)
        {
            if ((payloadKindBitmap & PayloadKind.Tensor) != 0
                && tileCount != 0
                && coveredTileCount == 0
                && droppedTileCount == 0
                && resultClass != ResultClass.Partial
                && (resultFlags & ResultFlags.Partial) == 0)
            {
                return tileCount;
            }

            return coveredTileCount;
        }

        public ResultStatusCode StatusCode { get; }

        public ResultFlags ResultFlags { get; }

        public ushort ActiveProfileId { get; }

        public PayloadKind PayloadKind { get; }

        public byte Reserved0 { get; }

        public ushort InferenceMilliseconds { get; }

        public ushort QueueMilliseconds { get; }

        public ushort ServerTotalMilliseconds { get; }

        public ushort Reserved1 { get; }

        public uint ProfileBlockBytes { get; }

        public uint PayloadDescriptorBytes { get; }

        public uint PayloadDataBytes { get; }

        public uint Reserved2 { get; }

        public ResultClass ResultClass { get; }

        public BudgetPolicy AppliedBudgetPolicy { get; }

        public uint ReusedFrameId { get; }

        public ushort CoveredTileCount { get; }

        public ushort DroppedTileCount { get; }

        public PayloadKind PayloadKindBitmap { get; }

        public ushort PayloadFrameCount { get; }

        public ushort SectionCount => sectionCount;

        public ushort TileCount => tileCount;

        public TileIndexMode TileIndexMode => tileIndexMode;

        public uint TileBaseId => tileBaseId;

        public uint TileIndexBytes => tileIndexBytes;

        internal ResultPushMetadata WithTensorResultLayout(TensorResultBlock resultBlock, uint payloadDescriptorBytes, uint payloadDataBytes)
        {
            return new ResultPushMetadata(
                StatusCode,
                ResultFlags,
                ActiveProfileId,
                PayloadKind,
                Reserved0,
                InferenceMilliseconds,
                QueueMilliseconds,
                ServerTotalMilliseconds,
                Reserved1,
                (uint)(TensorResultBlock.BlockLength + resultBlock.TileIndexBytes),
                payloadDescriptorBytes,
                payloadDataBytes,
                Reserved2,
                ResultClass,
                AppliedBudgetPolicy,
                ReusedFrameId,
                CoveredTileCount,
                DroppedTileCount,
                PayloadKindBitmap,
                PayloadFrameCount,
                resultBlock.SectionCount,
                resultBlock.TileCount,
                resultBlock.TileIndexMode,
                resultBlock.TileBaseId,
                resultBlock.TileIndexBytes);
        }

        public void Write(Span<byte> destination)
        {
            if (!TryWrite(destination, out _))
            {
                throw new ArgumentException($"Destination must be at least {MetadataLength} bytes.", nameof(destination));
            }
        }

        public bool TryWrite(Span<byte> destination, out int bytesWritten)
        {
            bytesWritten = 0;
            if (destination.Length < MetadataLength)
            {
                return false;
            }

            var writer = new FixedBinaryWriter(destination);
            if (!writer.TryWriteUInt16((ushort)StatusCode)
                || !writer.TryWriteUInt16((ushort)ResultFlags)
                || !writer.TryWriteUInt16(SectionCount)
                || !writer.TryWriteUInt16(TileCount)
                || !writer.TryWriteUInt16(ActiveProfileId)
                || !writer.TryWriteUInt16(0)
                || !writer.TryWriteUInt16(InferenceMilliseconds)
                || !writer.TryWriteUInt16(QueueMilliseconds)
                || !writer.TryWriteUInt16(ServerTotalMilliseconds)
                || !writer.TryWriteUInt16(0)
                || !writer.TryWriteUInt32(TileBaseId)
                || !writer.TryWriteUInt32(TileIndexBytes)
                || !writer.TryWriteUInt64(0)
                || !writer.TryWriteUInt64(0)
                || !writer.TryWriteByte((byte)ResultClass)
                || !writer.TryWriteByte((byte)AppliedBudgetPolicy)
                || !writer.TryWriteUInt16(0)
                || !writer.TryWriteUInt32(ReusedFrameId)
                || !writer.TryWriteUInt16(CoveredTileCount)
                || !writer.TryWriteUInt16(DroppedTileCount)
                || !writer.TryWriteUInt32((uint)PayloadKindBitmap)
                || !writer.TryWriteUInt16(PayloadFrameCount)
                || !writer.TryWriteUInt16(0))
            {
                return false;
            }

            bytesWritten = writer.Offset;
            return bytesWritten == MetadataLength;
        }

        public byte[] ToArray()
        {
            var payload = new byte[MetadataLength];
            Write(payload);
            return payload;
        }

        public static bool TryParse(ReadOnlySpan<byte> source, out ResultPushMetadata metadata)
        {
            return TryParse(source, strict: false, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out ResultPushMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt16(out var statusCode)
                || !reader.TryReadUInt16(out var resultFlags)
                || !reader.TryReadUInt16(out var sectionCount)
                || !reader.TryReadUInt16(out var tileCount)
                || !reader.TryReadUInt16(out var activeProfileId)
                || !reader.TryReadUInt16(out var reserved0)
                || !reader.TryReadUInt16(out var inferenceMilliseconds)
                || !reader.TryReadUInt16(out var queueMilliseconds)
                || !reader.TryReadUInt16(out var serverTotalMilliseconds)
                || !reader.TryReadUInt16(out var reserved1)
                || !reader.TryReadUInt32(out var tileBaseId)
                || !reader.TryReadUInt32(out var tileIndexBytes)
                || !reader.TryReadUInt64(out var reserved2)
                || !reader.TryReadUInt64(out var reserved3)
                || !reader.TryReadByte(out var rawResultClass)
                || !reader.TryReadByte(out var rawAppliedBudgetPolicy)
                || !reader.TryReadUInt16(out var reserved4)
                || !reader.TryReadUInt32(out var reusedFrameId)
                || !reader.TryReadUInt16(out var coveredTileCount)
                || !reader.TryReadUInt16(out var droppedTileCount)
                || !reader.TryReadUInt32(out var rawPayloadKindBitmap)
                || !reader.TryReadUInt16(out var payloadFrameCount)
                || !reader.TryReadUInt16(out var reserved5))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && (reserved0 != 0 || reserved1 != 0 || reserved2 != 0 || reserved3 != 0 || reserved4 != 0 || reserved5 != 0))
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            if (!PayloadKindValidator.IsDefinedBitmap((PayloadKind)rawPayloadKindBitmap))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var payloadKindBitmap = (PayloadKind)rawPayloadKindBitmap;
            if ((payloadKindBitmap & PayloadKind.Tensor) == 0)
            {
                if (sectionCount != 0
                    || tileCount != 0
                    || tileBaseId != 0
                    || tileIndexBytes != 0
                    || coveredTileCount != 0
                    || droppedTileCount != 0)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }
            }

            var parsedMetadata = new ResultPushMetadata(
                (ResultStatusCode)statusCode,
                (ResultFlags)resultFlags,
                sectionCount,
                tileCount,
                activeProfileId,
                inferenceMilliseconds,
                queueMilliseconds,
                serverTotalMilliseconds,
                tileBaseId,
                tileIndexBytes,
                (ResultClass)rawResultClass,
                (BudgetPolicy)rawAppliedBudgetPolicy,
                reusedFrameId,
                coveredTileCount,
                droppedTileCount,
                payloadKindBitmap,
                payloadFrameCount);

            if (strict
                && !TryValidateCoverageContract(parsedMetadata, tileCount, out error))
            {
                return false;
            }

            metadata = parsedMetadata;
            return true;
        }

        internal static bool TryValidateCoverageContract(
            ResultPushMetadata metadata,
            int tileCount,
            out NnrpParseError error)
        {
            error = NnrpParseError.None;

            if ((metadata.PayloadKindBitmap & PayloadKind.Tensor) == 0)
            {
                return true;
            }

            if (metadata.CoveredTileCount > tileCount
                || metadata.DroppedTileCount > tileCount
                || metadata.CoveredTileCount + metadata.DroppedTileCount != tileCount)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if ((metadata.ResultClass == ResultClass.Partial || (metadata.ResultFlags & ResultFlags.Partial) != 0)
                && metadata.DroppedTileCount == 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (metadata.ResultClass == ResultClass.StaleReuse)
            {
                if (metadata.ReusedFrameId == 0)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }
            }
            else if (metadata.ReusedFrameId != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        public bool Equals(ResultPushMetadata other)
        {
            return StatusCode == other.StatusCode
                && ResultFlags == other.ResultFlags
                && ActiveProfileId == other.ActiveProfileId
                && PayloadKind == other.PayloadKind
                && Reserved0 == other.Reserved0
                && InferenceMilliseconds == other.InferenceMilliseconds
                && QueueMilliseconds == other.QueueMilliseconds
                && ServerTotalMilliseconds == other.ServerTotalMilliseconds
                && Reserved1 == other.Reserved1
                && ProfileBlockBytes == other.ProfileBlockBytes
                && PayloadDescriptorBytes == other.PayloadDescriptorBytes
                && PayloadDataBytes == other.PayloadDataBytes
                && Reserved2 == other.Reserved2
                && ResultClass == other.ResultClass
                && AppliedBudgetPolicy == other.AppliedBudgetPolicy
                && ReusedFrameId == other.ReusedFrameId
                && CoveredTileCount == other.CoveredTileCount
                && DroppedTileCount == other.DroppedTileCount
                && PayloadKindBitmap == other.PayloadKindBitmap
                && PayloadFrameCount == other.PayloadFrameCount
                && SectionCount == other.SectionCount
                && TileCount == other.TileCount
                && TileIndexMode == other.TileIndexMode
                && TileBaseId == other.TileBaseId
                && TileIndexBytes == other.TileIndexBytes;
        }

        public override bool Equals(object obj)
        {
            return obj is ResultPushMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StatusCode.GetHashCode();
                hash = (hash * 397) ^ ResultFlags.GetHashCode();
                hash = (hash * 397) ^ ActiveProfileId.GetHashCode();
                hash = (hash * 397) ^ PayloadKind.GetHashCode();
                hash = (hash * 397) ^ Reserved0.GetHashCode();
                hash = (hash * 397) ^ InferenceMilliseconds.GetHashCode();
                hash = (hash * 397) ^ QueueMilliseconds.GetHashCode();
                hash = (hash * 397) ^ ServerTotalMilliseconds.GetHashCode();
                hash = (hash * 397) ^ Reserved1.GetHashCode();
                hash = (hash * 397) ^ ProfileBlockBytes.GetHashCode();
                hash = (hash * 397) ^ PayloadDescriptorBytes.GetHashCode();
                hash = (hash * 397) ^ PayloadDataBytes.GetHashCode();
                hash = (hash * 397) ^ Reserved2.GetHashCode();
                hash = (hash * 397) ^ ResultClass.GetHashCode();
                hash = (hash * 397) ^ AppliedBudgetPolicy.GetHashCode();
                hash = (hash * 397) ^ ReusedFrameId.GetHashCode();
                hash = (hash * 397) ^ CoveredTileCount.GetHashCode();
                hash = (hash * 397) ^ DroppedTileCount.GetHashCode();
                hash = (hash * 397) ^ PayloadKindBitmap.GetHashCode();
                hash = (hash * 397) ^ PayloadFrameCount.GetHashCode();
                hash = (hash * 397) ^ SectionCount.GetHashCode();
                hash = (hash * 397) ^ TileCount.GetHashCode();
                hash = (hash * 397) ^ TileIndexMode.GetHashCode();
                hash = (hash * 397) ^ TileBaseId.GetHashCode();
                hash = (hash * 397) ^ TileIndexBytes.GetHashCode();
                return hash;
            }
        }

        private static TileIndexMode InferTileIndexMode(ushort tileCount, uint tileIndexBytes)
        {
            if (tileIndexBytes == 0)
            {
                return TileIndexMode.DenseRange;
            }

            return tileIndexBytes == (uint)(tileCount * sizeof(ushort))
                ? TileIndexMode.RawUInt16
                : TileIndexMode.DenseRange;
        }
    }
}
