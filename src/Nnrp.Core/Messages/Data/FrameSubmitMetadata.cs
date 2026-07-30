using System;

namespace Nnrp.Core
{
    public readonly struct FrameSubmitMetadata : IEquatable<FrameSubmitMetadata>
    {
        public const int MetadataLength = 72;

        public FrameSubmitMetadata(
            ushort sourceWidth,
            ushort sourceHeight,
            ushort tileWidth,
            ushort tileHeight,
            ushort tileCount,
            ushort sectionCount,
            FrameClass frameClass,
            InputProfile inputProfile,
            TileIndexMode tileIndexMode,
            ushort latencyBudgetMilliseconds,
            ushort targetFpsTimes100,
            uint retryOfFrame,
            uint tileBaseId,
            uint cameraBytes,
            uint tileIndexBytes,
            ulong operationId,
            SubmitMode submitMode,
            BudgetPolicy budgetPolicy,
            LossTolerancePolicy lossTolerancePolicy,
            uint objectRefMask,
            uint dependencyFrameId,
            PayloadKind payloadKindBitmap,
            ushort payloadFrameCount)
        {
            if (operationId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operationId));
            }

            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            TileCount = tileCount;
            SectionCount = sectionCount;
            FrameClass = frameClass;
            InputProfile = inputProfile;
            TileIndexMode = tileIndexMode;
            LatencyBudgetMilliseconds = latencyBudgetMilliseconds;
            TargetFpsTimes100 = targetFpsTimes100;
            RetryOfFrame = retryOfFrame;
            TileBaseId = tileBaseId;
            CameraBytes = cameraBytes;
            TileIndexBytes = tileIndexBytes;
            OperationId = operationId;
            SubmitMode = submitMode;
            BudgetPolicy = budgetPolicy;
            LossTolerancePolicy = lossTolerancePolicy;
            ObjectRefMask = objectRefMask;
            DependencyFrameId = dependencyFrameId;
            PayloadKindBitmap = payloadKindBitmap;
            PayloadFrameCount = payloadFrameCount;
        }

        public ushort SourceWidth { get; }

        public ushort SourceHeight { get; }

        public ushort TileWidth { get; }

        public ushort TileHeight { get; }

        public ushort TileCount { get; }

        public ushort SectionCount { get; }

        public FrameClass FrameClass { get; }

        public InputProfile InputProfile { get; }

        public TileIndexMode TileIndexMode { get; }

        public ushort LatencyBudgetMilliseconds { get; }

        public ushort TargetFpsTimes100 { get; }

        public uint RetryOfFrame { get; }

        public uint TileBaseId { get; }

        public uint CameraBytes { get; }

        public uint TileIndexBytes { get; }

        public ulong OperationId { get; }

        public SubmitMode SubmitMode { get; }

        public BudgetPolicy BudgetPolicy { get; }

        public LossTolerancePolicy LossTolerancePolicy { get; }

        public uint ObjectRefMask { get; }

        public uint DependencyFrameId { get; }

        public PayloadKind PayloadKindBitmap { get; }

        public ushort PayloadFrameCount { get; }

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
            if (!writer.TryWriteUInt16(SourceWidth)
                || !writer.TryWriteUInt16(SourceHeight)
                || !writer.TryWriteUInt16(TileWidth)
                || !writer.TryWriteUInt16(TileHeight)
                || !writer.TryWriteUInt16(TileCount)
                || !writer.TryWriteUInt16(SectionCount)
                || !writer.TryWriteByte((byte)FrameClass)
                || !writer.TryWriteByte((byte)InputProfile)
                || !writer.TryWriteByte((byte)TileIndexMode)
                || !writer.TryWriteByte(0)
                || !writer.TryWriteUInt16(LatencyBudgetMilliseconds)
                || !writer.TryWriteUInt16(TargetFpsTimes100)
                || !writer.TryWriteUInt32(RetryOfFrame)
                || !writer.TryWriteUInt32(TileBaseId)
                || !writer.TryWriteUInt32(CameraBytes)
                || !writer.TryWriteUInt32(TileIndexBytes)
                || !writer.TryWriteUInt32(0)
                || !writer.TryWriteUInt64(OperationId)
                || !writer.TryWriteUInt32(0)
                || !writer.TryWriteByte((byte)SubmitMode)
                || !writer.TryWriteByte((byte)BudgetPolicy)
                || !writer.TryWriteByte((byte)LossTolerancePolicy)
                || !writer.TryWriteByte(0)
                || !writer.TryWriteUInt32(ObjectRefMask)
                || !writer.TryWriteUInt32(DependencyFrameId)
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

        public static bool TryParse(ReadOnlySpan<byte> source, out FrameSubmitMetadata metadata)
        {
            return TryParse(source, strict: false, out metadata, out _);
        }

        public static bool TryParse(ReadOnlySpan<byte> source, bool strict, out FrameSubmitMetadata metadata, out NnrpParseError error)
        {
            metadata = default;
            error = NnrpParseError.None;
            if (source.Length < MetadataLength)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var reader = new FixedBinaryReader(source);
            if (!reader.TryReadUInt16(out var sourceWidth)
                || !reader.TryReadUInt16(out var sourceHeight)
                || !reader.TryReadUInt16(out var tileWidth)
                || !reader.TryReadUInt16(out var tileHeight)
                || !reader.TryReadUInt16(out var tileCount)
                || !reader.TryReadUInt16(out var sectionCount)
                || !reader.TryReadByte(out var frameClass)
                || !reader.TryReadByte(out var inputProfile)
                || !reader.TryReadByte(out var tileIndexMode)
                || !reader.TryReadByte(out var reserved0)
                || !reader.TryReadUInt16(out var latencyBudgetMilliseconds)
                || !reader.TryReadUInt16(out var targetFpsTimes100)
                || !reader.TryReadUInt32(out var retryOfFrame)
                || !reader.TryReadUInt32(out var tileBaseId)
                || !reader.TryReadUInt32(out var cameraBytes)
                || !reader.TryReadUInt32(out var tileIndexBytes)
                || !reader.TryReadUInt32(out var reserved1)
                || !reader.TryReadUInt64(out var operationId)
                || !reader.TryReadUInt32(out var reserved2)
                || !reader.TryReadByte(out var submitMode)
                || !reader.TryReadByte(out var budgetPolicy)
                || !reader.TryReadByte(out var lossTolerancePolicy)
                || !reader.TryReadByte(out var reserved3)
                || !reader.TryReadUInt32(out var objectRefMask)
                || !reader.TryReadUInt32(out var dependencyFrameId)
                || !reader.TryReadUInt32(out var payloadKindBitmap)
                || !reader.TryReadUInt16(out var payloadFrameCount)
                || !reader.TryReadUInt16(out var reserved4))
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            if (strict && (reserved0 != 0 || reserved1 != 0 || reserved2 != 0 || reserved3 != 0 || reserved4 != 0))
            {
                error = NnrpParseError.NonZeroReservedField;
                return false;
            }

            if (operationId == 0
                || !Enum.IsDefined(typeof(LossTolerancePolicy), (LossTolerancePolicy)lossTolerancePolicy))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (strict
                && !SubmitObjectReferenceMask.TryValidateForSubmitMode((SubmitMode)submitMode, objectRefMask, out error))
            {
                return false;
            }

            metadata = new FrameSubmitMetadata(
                sourceWidth,
                sourceHeight,
                tileWidth,
                tileHeight,
                tileCount,
                sectionCount,
                (FrameClass)frameClass,
                (InputProfile)inputProfile,
                (TileIndexMode)tileIndexMode,
                latencyBudgetMilliseconds,
                targetFpsTimes100,
                retryOfFrame,
                tileBaseId,
                cameraBytes,
                tileIndexBytes,
                operationId,
                (SubmitMode)submitMode,
                (BudgetPolicy)budgetPolicy,
                (LossTolerancePolicy)lossTolerancePolicy,
                objectRefMask,
                dependencyFrameId,
                (PayloadKind)payloadKindBitmap,
                payloadFrameCount);
            return true;
        }

        public bool Equals(FrameSubmitMetadata other)
        {
            return SourceWidth == other.SourceWidth
                && SourceHeight == other.SourceHeight
                && TileWidth == other.TileWidth
                && TileHeight == other.TileHeight
                && TileCount == other.TileCount
                && SectionCount == other.SectionCount
                && FrameClass == other.FrameClass
                && InputProfile == other.InputProfile
                && TileIndexMode == other.TileIndexMode
                && LatencyBudgetMilliseconds == other.LatencyBudgetMilliseconds
                && TargetFpsTimes100 == other.TargetFpsTimes100
                && RetryOfFrame == other.RetryOfFrame
                && TileBaseId == other.TileBaseId
                && CameraBytes == other.CameraBytes
                && TileIndexBytes == other.TileIndexBytes
                && OperationId == other.OperationId
                && SubmitMode == other.SubmitMode
                && BudgetPolicy == other.BudgetPolicy
                && LossTolerancePolicy == other.LossTolerancePolicy
                && ObjectRefMask == other.ObjectRefMask
                && DependencyFrameId == other.DependencyFrameId
                && PayloadKindBitmap == other.PayloadKindBitmap
                && PayloadFrameCount == other.PayloadFrameCount;
        }

        public override bool Equals(object obj)
        {
            return obj is FrameSubmitMetadata other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SourceWidth.GetHashCode();
                hash = (hash * 397) ^ SourceHeight.GetHashCode();
                hash = (hash * 397) ^ TileWidth.GetHashCode();
                hash = (hash * 397) ^ TileHeight.GetHashCode();
                hash = (hash * 397) ^ TileCount.GetHashCode();
                hash = (hash * 397) ^ SectionCount.GetHashCode();
                hash = (hash * 397) ^ FrameClass.GetHashCode();
                hash = (hash * 397) ^ InputProfile.GetHashCode();
                hash = (hash * 397) ^ TileIndexMode.GetHashCode();
                hash = (hash * 397) ^ LatencyBudgetMilliseconds.GetHashCode();
                hash = (hash * 397) ^ TargetFpsTimes100.GetHashCode();
                hash = (hash * 397) ^ RetryOfFrame.GetHashCode();
                hash = (hash * 397) ^ TileBaseId.GetHashCode();
                hash = (hash * 397) ^ CameraBytes.GetHashCode();
                hash = (hash * 397) ^ TileIndexBytes.GetHashCode();
                hash = (hash * 397) ^ OperationId.GetHashCode();
                hash = (hash * 397) ^ SubmitMode.GetHashCode();
                hash = (hash * 397) ^ BudgetPolicy.GetHashCode();
                hash = (hash * 397) ^ LossTolerancePolicy.GetHashCode();
                hash = (hash * 397) ^ ObjectRefMask.GetHashCode();
                hash = (hash * 397) ^ DependencyFrameId.GetHashCode();
                hash = (hash * 397) ^ PayloadKindBitmap.GetHashCode();
                hash = (hash * 397) ^ PayloadFrameCount.GetHashCode();
                return hash;
            }
        }
    }
}
