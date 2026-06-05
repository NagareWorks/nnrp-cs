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
            uint tileIndexBytes)
            : this(
                sourceWidth,
                sourceHeight,
                tileWidth,
                tileHeight,
                tileCount,
                sectionCount,
                frameClass,
                inputProfile,
                tileIndexMode,
                reserved0: 0,
                latencyBudgetMilliseconds,
                targetFpsTimes100,
                retryOfFrame,
                tileBaseId,
                cameraBytes,
                tileIndexBytes,
                reserved1: 0,
                reserved2: 0,
                submitMode: SubmitMode.Inline,
                budgetPolicy: BudgetPolicy.None,
                lossTolerancePolicy: 0xFF,
                reserved3: 0,
                objectRefMask: 0,
                dependencyFrameId: retryOfFrame,
                payloadKindBitmap: PayloadKind.Tensor,
                payloadFrameCount: 0,
                reserved4: 0)
        {
        }

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
            byte reserved0,
            ushort latencyBudgetMilliseconds,
            ushort targetFpsTimes100,
            uint retryOfFrame,
            uint tileBaseId,
            uint cameraBytes,
            uint tileIndexBytes,
            ulong reserved1,
            ulong reserved2,
            SubmitMode submitMode,
            BudgetPolicy budgetPolicy,
            byte lossTolerancePolicy,
            byte reserved3,
            uint objectRefMask,
            uint dependencyFrameId,
            PayloadKind payloadKindBitmap,
            ushort payloadFrameCount,
            ushort reserved4)
        {
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            TileCount = tileCount;
            SectionCount = sectionCount;
            FrameClass = frameClass;
            InputProfile = inputProfile;
            TileIndexMode = tileIndexMode;
            Reserved0 = reserved0;
            LatencyBudgetMilliseconds = latencyBudgetMilliseconds;
            TargetFpsTimes100 = targetFpsTimes100;
            RetryOfFrame = retryOfFrame;
            TileBaseId = tileBaseId;
            CameraBytes = cameraBytes;
            TileIndexBytes = tileIndexBytes;
            Reserved1 = reserved1;
            Reserved2 = reserved2;
            SubmitMode = submitMode;
            BudgetPolicy = budgetPolicy;
            LossTolerancePolicy = lossTolerancePolicy;
            Reserved3 = reserved3;
            ObjectRefMask = objectRefMask;
            DependencyFrameId = dependencyFrameId;
            PayloadKindBitmap = payloadKindBitmap;
            PayloadFrameCount = payloadFrameCount;
            Reserved4 = reserved4;
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

        public byte Reserved0 { get; }

        public ushort LatencyBudgetMilliseconds { get; }

        public ushort TargetFpsTimes100 { get; }

        public uint RetryOfFrame { get; }

        public uint TileBaseId { get; }

        public uint CameraBytes { get; }

        public uint TileIndexBytes { get; }

        public ulong Reserved1 { get; }

        public ulong Reserved2 { get; }

        public SubmitMode SubmitMode { get; }

        public BudgetPolicy BudgetPolicy { get; }

        public byte LossTolerancePolicy { get; }

        public byte Reserved3 { get; }

        public uint ObjectRefMask { get; }

        public uint DependencyFrameId { get; }

        public PayloadKind PayloadKindBitmap { get; }

        public ushort PayloadFrameCount { get; }

        public ushort Reserved4 { get; }

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
                || !writer.TryWriteByte(Reserved0)
                || !writer.TryWriteUInt16(LatencyBudgetMilliseconds)
                || !writer.TryWriteUInt16(TargetFpsTimes100)
                || !writer.TryWriteUInt32(RetryOfFrame)
                || !writer.TryWriteUInt32(TileBaseId)
                || !writer.TryWriteUInt32(CameraBytes)
                || !writer.TryWriteUInt32(TileIndexBytes)
                || !writer.TryWriteUInt64(Reserved1)
                || !writer.TryWriteUInt64(Reserved2)
                || !writer.TryWriteByte((byte)SubmitMode)
                || !writer.TryWriteByte((byte)BudgetPolicy)
                || !writer.TryWriteByte(LossTolerancePolicy)
                || !writer.TryWriteByte(Reserved3)
                || !writer.TryWriteUInt32(ObjectRefMask)
                || !writer.TryWriteUInt32(DependencyFrameId)
                || !writer.TryWriteUInt32((uint)PayloadKindBitmap)
                || !writer.TryWriteUInt16(PayloadFrameCount)
                || !writer.TryWriteUInt16(Reserved4))
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
                || !reader.TryReadUInt64(out var reserved1)
                || !reader.TryReadUInt64(out var reserved2)
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
                reserved0,
                latencyBudgetMilliseconds,
                targetFpsTimes100,
                retryOfFrame,
                tileBaseId,
                cameraBytes,
                tileIndexBytes,
                reserved1,
                reserved2,
                (SubmitMode)submitMode,
                (BudgetPolicy)budgetPolicy,
                lossTolerancePolicy,
                reserved3,
                objectRefMask,
                dependencyFrameId,
                (PayloadKind)payloadKindBitmap,
                payloadFrameCount,
                reserved4);
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
                && Reserved0 == other.Reserved0
                && LatencyBudgetMilliseconds == other.LatencyBudgetMilliseconds
                && TargetFpsTimes100 == other.TargetFpsTimes100
                && RetryOfFrame == other.RetryOfFrame
                && TileBaseId == other.TileBaseId
                && CameraBytes == other.CameraBytes
                && TileIndexBytes == other.TileIndexBytes
                && Reserved1 == other.Reserved1
                && Reserved2 == other.Reserved2
                && SubmitMode == other.SubmitMode
                && BudgetPolicy == other.BudgetPolicy
                && LossTolerancePolicy == other.LossTolerancePolicy
                && Reserved3 == other.Reserved3
                && ObjectRefMask == other.ObjectRefMask
                && DependencyFrameId == other.DependencyFrameId
                && PayloadKindBitmap == other.PayloadKindBitmap
                && PayloadFrameCount == other.PayloadFrameCount
                && Reserved4 == other.Reserved4;
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
                hash = (hash * 397) ^ Reserved0.GetHashCode();
                hash = (hash * 397) ^ LatencyBudgetMilliseconds.GetHashCode();
                hash = (hash * 397) ^ TargetFpsTimes100.GetHashCode();
                hash = (hash * 397) ^ RetryOfFrame.GetHashCode();
                hash = (hash * 397) ^ TileBaseId.GetHashCode();
                hash = (hash * 397) ^ CameraBytes.GetHashCode();
                hash = (hash * 397) ^ TileIndexBytes.GetHashCode();
                hash = (hash * 397) ^ Reserved1.GetHashCode();
                hash = (hash * 397) ^ Reserved2.GetHashCode();
                hash = (hash * 397) ^ SubmitMode.GetHashCode();
                hash = (hash * 397) ^ BudgetPolicy.GetHashCode();
                hash = (hash * 397) ^ LossTolerancePolicy.GetHashCode();
                hash = (hash * 397) ^ Reserved3.GetHashCode();
                hash = (hash * 397) ^ ObjectRefMask.GetHashCode();
                hash = (hash * 397) ^ DependencyFrameId.GetHashCode();
                hash = (hash * 397) ^ PayloadKindBitmap.GetHashCode();
                hash = (hash * 397) ^ PayloadFrameCount.GetHashCode();
                hash = (hash * 397) ^ Reserved4.GetHashCode();
                return hash;
            }
        }
    }
}
