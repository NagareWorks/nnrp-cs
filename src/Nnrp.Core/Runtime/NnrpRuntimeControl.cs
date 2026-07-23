using System;
using System.Buffers.Binary;
using Nnrp.Core;

namespace Nnrp.Runtime
{
    /// <summary>Encodes and decodes frozen Preview4 runtime-control metadata.</summary>
    public static class NnrpRuntimeControl
    {
        public static byte[] Encode(
            MessageType messageType,
            IRuntimeControlMetadata metadata,
            ReadOnlySpan<byte> tail = default)
        {
            if (metadata == null)
            {
                throw new ArgumentNullException(nameof(metadata));
            }

            var fixedLength = GetFixedLength(messageType);
            var declaredTailLength = GetDeclaredTailLength(messageType, metadata);
            ValidateTailLength(declaredTailLength, tail.Length);

            var encoded = new byte[checked(fixedLength + tail.Length)];
            WriteMetadata(messageType, metadata, encoded.AsSpan(0, fixedLength));
            tail.CopyTo(encoded.AsSpan(fixedLength));
            return encoded;
        }

        public static DecodedRuntimeControlMetadata Decode(MessageType messageType, ReadOnlySpan<byte> payload)
        {
            var fixedLength = GetFixedLength(messageType);
            if (payload.Length < fixedLength)
            {
                throw new ArgumentException(
                    "Runtime control payload is shorter than the frozen metadata layout.",
                    nameof(payload));
            }

            var metadata = ReadMetadata(messageType, payload.Slice(0, fixedLength));
            var tail = payload.Slice(fixedLength).ToArray();
            ValidateTailLength(GetDeclaredTailLength(messageType, metadata), tail.Length);
            return new DecodedRuntimeControlMetadata(metadata, tail);
        }

        private static int GetFixedLength(MessageType messageType)
        {
            switch (messageType)
            {
                case MessageType.Cancel:
                case MessageType.Abort:
                    return ControlRequestMetadata.EncodedLength;
                case MessageType.PriorityUpdate:
                case MessageType.Deadline:
                case MessageType.ExpireAt:
                    return SchedulingMetadata.EncodedLength;
                case MessageType.Supersede:
                    return SupersedeMetadata.EncodedLength;
                case MessageType.BudgetUpdate:
                    return BudgetMetadata.EncodedLength;
                case MessageType.Progress:
                    return ProgressMetadata.EncodedLength;
                case MessageType.PartialResult:
                    return PartialResultMetadata.EncodedLength;
                case MessageType.Backpressure:
                case MessageType.CreditUpdate:
                    return PressureMetadata.EncodedLength;
                case MessageType.CapabilityNegotiation:
                case MessageType.DegradeProfile:
                    return CapabilityMetadata.EncodedLength;
                case MessageType.RouteHint:
                case MessageType.ExecutionHint:
                    return RouteHintMetadata.EncodedLength;
                case MessageType.TraceContext:
                    return TraceContextMetadata.EncodedLength;
                case MessageType.ResultDropReason:
                    return ResultDropReasonMetadata.EncodedLength;
                case MessageType.ErrorRecoverable:
                    return RecoverableErrorMetadata.EncodedLength;
                case MessageType.RetryAfter:
                    return RetryAfterMetadata.EncodedLength;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(messageType),
                        messageType,
                        "Message type does not select a runtime-control metadata layout.");
            }
        }

        private static uint GetDeclaredTailLength(MessageType messageType, IRuntimeControlMetadata metadata)
        {
            switch (messageType)
            {
                case MessageType.Cancel:
                case MessageType.Abort:
                    return RequireType<ControlRequestMetadata>(metadata, messageType).DiagnosticBytes;
                case MessageType.PriorityUpdate:
                case MessageType.Deadline:
                case MessageType.ExpireAt:
                    RequireType<SchedulingMetadata>(metadata, messageType);
                    return 0;
                case MessageType.Supersede:
                    return RequireType<SupersedeMetadata>(metadata, messageType).DiagnosticBytes;
                case MessageType.BudgetUpdate:
                    RequireType<BudgetMetadata>(metadata, messageType);
                    return 0;
                case MessageType.Progress:
                    return RequireType<ProgressMetadata>(metadata, messageType).BodyBytes;
                case MessageType.PartialResult:
                    return RequireType<PartialResultMetadata>(metadata, messageType).BodyBytes;
                case MessageType.Backpressure:
                case MessageType.CreditUpdate:
                    RequireType<PressureMetadata>(metadata, messageType);
                    return 0;
                case MessageType.CapabilityNegotiation:
                case MessageType.DegradeProfile:
                    return RequireType<CapabilityMetadata>(metadata, messageType).BodyBytes;
                case MessageType.RouteHint:
                case MessageType.ExecutionHint:
                    return RequireType<RouteHintMetadata>(metadata, messageType).BodyBytes;
                case MessageType.TraceContext:
                    return RequireType<TraceContextMetadata>(metadata, messageType).BodyBytes;
                case MessageType.ResultDropReason:
                    return RequireType<ResultDropReasonMetadata>(metadata, messageType).DiagnosticBytes;
                case MessageType.ErrorRecoverable:
                    return RequireType<RecoverableErrorMetadata>(metadata, messageType).DiagnosticBytes;
                case MessageType.RetryAfter:
                    return RequireType<RetryAfterMetadata>(metadata, messageType).DiagnosticBytes;
                default:
                    GetFixedLength(messageType);
                    return 0;
            }
        }

        private static void WriteMetadata(
            MessageType messageType,
            IRuntimeControlMetadata metadata,
            Span<byte> destination)
        {
            destination.Clear();
            switch (messageType)
            {
                case MessageType.Cancel:
                case MessageType.Abort:
                    WriteControlRequest(RequireType<ControlRequestMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.PriorityUpdate:
                case MessageType.Deadline:
                case MessageType.ExpireAt:
                    WriteScheduling(RequireType<SchedulingMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.Supersede:
                    WriteSupersede(RequireType<SupersedeMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.BudgetUpdate:
                    WriteBudget(RequireType<BudgetMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.Progress:
                    WriteProgress(RequireType<ProgressMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.PartialResult:
                    WritePartialResult(RequireType<PartialResultMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.Backpressure:
                case MessageType.CreditUpdate:
                    WritePressure(RequireType<PressureMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.CapabilityNegotiation:
                case MessageType.DegradeProfile:
                    WriteCapability(RequireType<CapabilityMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.RouteHint:
                case MessageType.ExecutionHint:
                    WriteRouteHint(RequireType<RouteHintMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.TraceContext:
                    WriteTraceContext(RequireType<TraceContextMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.ResultDropReason:
                    WriteResultDropReason(RequireType<ResultDropReasonMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.ErrorRecoverable:
                    WriteRecoverableError(RequireType<RecoverableErrorMetadata>(metadata, messageType), destination);
                    return;
                case MessageType.RetryAfter:
                    WriteRetryAfter(RequireType<RetryAfterMetadata>(metadata, messageType), destination);
                    return;
                default:
                    GetFixedLength(messageType);
                    return;
            }
        }

        private static IRuntimeControlMetadata ReadMetadata(MessageType messageType, ReadOnlySpan<byte> source)
        {
            switch (messageType)
            {
                case MessageType.Cancel:
                case MessageType.Abort:
                    return ReadControlRequest(source);
                case MessageType.PriorityUpdate:
                case MessageType.Deadline:
                case MessageType.ExpireAt:
                    return ReadScheduling(source);
                case MessageType.Supersede:
                    return ReadSupersede(source);
                case MessageType.BudgetUpdate:
                    return ReadBudget(source);
                case MessageType.Progress:
                    return ReadProgress(source);
                case MessageType.PartialResult:
                    return ReadPartialResult(source);
                case MessageType.Backpressure:
                case MessageType.CreditUpdate:
                    return ReadPressure(source);
                case MessageType.CapabilityNegotiation:
                case MessageType.DegradeProfile:
                    return ReadCapability(source);
                case MessageType.RouteHint:
                case MessageType.ExecutionHint:
                    return ReadRouteHint(source);
                case MessageType.TraceContext:
                    return ReadTraceContext(source);
                case MessageType.ResultDropReason:
                    return ReadResultDropReason(source);
                case MessageType.ErrorRecoverable:
                    return ReadRecoverableError(source);
                case MessageType.RetryAfter:
                    return ReadRetryAfter(source);
                default:
                    GetFixedLength(messageType);
                    throw new InvalidOperationException();
            }
        }

        private static void WriteControlRequest(ControlRequestMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x03, "control_request.flags");
            WriteUInt64(destination, 0, value.OperationId);
            WriteUInt64(destination, 8, value.ControlSequence);
            WriteUInt16(destination, 16, value.ReasonCode);
            destination[18] = (byte)value.SourceRole;
            destination[19] = value.Flags;
            WriteUInt32(destination, 20, value.DiagnosticBytes);
        }

        private static ControlRequestMetadata ReadControlRequest(ReadOnlySpan<byte> source)
        {
            RequireZero(ReadUInt64(source, 24), "control_request.reserved");
            var value = new ControlRequestMetadata(
                ReadUInt64(source, 0),
                ReadUInt64(source, 8),
                ReadUInt16(source, 16),
                (RuntimeRole)source[18],
                source[19],
                ReadUInt32(source, 20));
            RequireMask(value.Flags, 0x03, "control_request.flags");
            return value;
        }

        private static void WriteScheduling(SchedulingMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x00000003, "scheduling.flags");
            WriteUInt64(destination, 0, value.OperationId);
            WriteUInt64(destination, 8, value.ControlSequence);
            WriteUInt16(destination, 16, value.PriorityClass);
            WriteUInt16(destination, 18, unchecked((ushort)value.PriorityDelta));
            WriteUInt64(destination, 20, value.DeadlineUnixMs);
            WriteUInt32(destination, 28, value.Flags);
        }

        private static SchedulingMetadata ReadScheduling(ReadOnlySpan<byte> source)
        {
            var value = new SchedulingMetadata(
                ReadUInt64(source, 0),
                ReadUInt64(source, 8),
                ReadUInt16(source, 16),
                unchecked((short)ReadUInt16(source, 18)),
                ReadUInt64(source, 20),
                ReadUInt32(source, 28));
            RequireMask(value.Flags, 0x00000003, "scheduling.flags");
            return value;
        }

        private static void WriteSupersede(SupersedeMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x0001, "supersede.flags");
            WriteUInt64(destination, 0, value.OldOperationId);
            WriteUInt64(destination, 8, value.NewOperationId);
            WriteUInt64(destination, 16, value.ControlSequence);
            WriteUInt16(destination, 24, value.DropReasonCode);
            WriteUInt16(destination, 26, value.Flags);
            WriteUInt32(destination, 28, value.DiagnosticBytes);
        }

        private static SupersedeMetadata ReadSupersede(ReadOnlySpan<byte> source)
        {
            var value = new SupersedeMetadata(
                ReadUInt64(source, 0),
                ReadUInt64(source, 8),
                ReadUInt64(source, 16),
                ReadUInt16(source, 24),
                ReadUInt16(source, 26),
                ReadUInt32(source, 28));
            RequireMask(value.Flags, 0x0001, "supersede.flags");
            return value;
        }

        private static void WriteBudget(BudgetMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x00000003, "budget.flags");
            WriteUInt64(destination, 0, value.OperationId);
            WriteUInt64(destination, 8, value.ComputeBudgetUnits);
            WriteUInt64(destination, 16, value.MemoryBudgetBytes);
            WriteUInt64(destination, 24, value.BandwidthBudgetBytes);
            WriteUInt32(destination, 32, value.TokenBudget);
            WriteUInt32(destination, 36, value.Flags);
        }

        private static BudgetMetadata ReadBudget(ReadOnlySpan<byte> source)
        {
            var value = new BudgetMetadata(
                ReadUInt64(source, 0),
                ReadUInt64(source, 8),
                ReadUInt64(source, 16),
                ReadUInt64(source, 24),
                ReadUInt32(source, 32),
                ReadUInt32(source, 36));
            RequireMask(value.Flags, 0x00000003, "budget.flags");
            return value;
        }

        private static void WriteProgress(ProgressMetadata value, Span<byte> destination)
        {
            if (value.PercentX100 > 10000)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "progress.percent_x100 exceeds 10000.");
            }

            WriteUInt64(destination, 0, value.OperationId);
            WriteUInt64(destination, 8, value.ProgressSequence);
            WriteUInt16(destination, 16, value.StageCode);
            WriteUInt16(destination, 18, value.PercentX100);
            WriteUInt64(destination, 20, value.ObjectId);
            WriteUInt32(destination, 28, value.BodyBytes);
        }

        private static ProgressMetadata ReadProgress(ReadOnlySpan<byte> source)
        {
            var value = new ProgressMetadata(
                ReadUInt64(source, 0),
                ReadUInt64(source, 8),
                ReadUInt16(source, 16),
                ReadUInt16(source, 18),
                ReadUInt64(source, 20),
                ReadUInt32(source, 28));
            if (value.PercentX100 > 10000)
            {
                throw new ArgumentException("progress.percent_x100 exceeds 10000.", nameof(source));
            }

            return value;
        }

        private static void WritePartialResult(PartialResultMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x00000003, "partial_result.flags");
            WriteUInt64(destination, 0, value.OperationId);
            WriteUInt64(destination, 8, value.ResultSequence);
            WriteUInt64(destination, 16, value.ObjectId);
            WriteUInt64(destination, 24, value.DeltaSequence);
            WriteUInt32(destination, 32, value.BodyBytes);
            WriteUInt32(destination, 36, value.Flags);
        }

        private static PartialResultMetadata ReadPartialResult(ReadOnlySpan<byte> source)
        {
            var value = new PartialResultMetadata(
                ReadUInt64(source, 0),
                ReadUInt64(source, 8),
                ReadUInt64(source, 16),
                ReadUInt64(source, 24),
                ReadUInt32(source, 32),
                ReadUInt32(source, 36));
            RequireMask(value.Flags, 0x00000003, "partial_result.flags");
            return value;
        }

        private static void WritePressure(PressureMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x00000003, "pressure.flags");
            WriteUInt64(destination, 0, value.ScopeId);
            WriteUInt64(destination, 8, value.CreditWindow);
            WriteUInt16(destination, 16, value.PressureLevel);
            WriteUInt16(destination, 18, value.PressureReason);
            WriteUInt32(destination, 20, value.RetryAfterMs);
            WriteUInt32(destination, 24, value.Flags);
        }

        private static PressureMetadata ReadPressure(ReadOnlySpan<byte> source)
        {
            RequireZero(ReadUInt32(source, 28), "pressure.reserved");
            var value = new PressureMetadata(
                ReadUInt64(source, 0),
                ReadUInt64(source, 8),
                ReadUInt16(source, 16),
                ReadUInt16(source, 18),
                ReadUInt32(source, 20),
                ReadUInt32(source, 24));
            RequireMask(value.Flags, 0x00000003, "pressure.flags");
            return value;
        }

        private static void WriteCapability(CapabilityMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x00000003, "capability.flags");
            WriteUInt16(destination, 0, value.ProfileId);
            WriteUInt16(destination, 2, value.CapabilityCount);
            WriteUInt16(destination, 4, value.CostModelId);
            WriteUInt16(destination, 6, value.PreferenceRank);
            WriteUInt64(destination, 8, value.LimitBytes);
            WriteUInt64(destination, 16, value.LimitUnits);
            WriteUInt32(destination, 24, value.BodyBytes);
            WriteUInt32(destination, 28, value.Flags);
        }

        private static CapabilityMetadata ReadCapability(ReadOnlySpan<byte> source)
        {
            var value = new CapabilityMetadata(
                ReadUInt16(source, 0),
                ReadUInt16(source, 2),
                ReadUInt16(source, 4),
                ReadUInt16(source, 6),
                ReadUInt64(source, 8),
                ReadUInt64(source, 16),
                ReadUInt32(source, 24),
                ReadUInt32(source, 28));
            RequireMask(value.Flags, 0x00000003, "capability.flags");
            return value;
        }

        private static void WriteRouteHint(RouteHintMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x00000003, "route_hint.flags");
            WriteUInt64(destination, 0, value.OperationId);
            WriteUInt32(destination, 8, value.RouteId);
            WriteUInt16(destination, 12, value.ExecutorClass);
            WriteUInt16(destination, 14, value.AffinityClass);
            WriteUInt64(destination, 16, value.DeadlineUnixMs);
            WriteUInt32(destination, 24, value.BodyBytes);
            WriteUInt32(destination, 28, value.Flags);
        }

        private static RouteHintMetadata ReadRouteHint(ReadOnlySpan<byte> source)
        {
            var value = new RouteHintMetadata(
                ReadUInt64(source, 0),
                ReadUInt32(source, 8),
                ReadUInt16(source, 12),
                ReadUInt16(source, 14),
                ReadUInt64(source, 16),
                ReadUInt32(source, 24),
                ReadUInt32(source, 28));
            RequireMask(value.Flags, 0x00000003, "route_hint.flags");
            return value;
        }

        private static void WriteTraceContext(TraceContextMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x0003, "trace_context.flags");
            WriteUInt64(destination, 0, value.TraceId);
            WriteUInt64(destination, 8, value.SpanId);
            WriteUInt64(destination, 16, value.ParentSpanId);
            WriteUInt16(destination, 24, value.StageCode);
            WriteUInt16(destination, 26, value.Flags);
            WriteUInt32(destination, 28, value.BodyBytes);
        }

        private static TraceContextMetadata ReadTraceContext(ReadOnlySpan<byte> source)
        {
            var value = new TraceContextMetadata(
                ReadUInt64(source, 0),
                ReadUInt64(source, 8),
                ReadUInt64(source, 16),
                ReadUInt16(source, 24),
                ReadUInt16(source, 26),
                ReadUInt32(source, 28));
            RequireMask(value.Flags, 0x0003, "trace_context.flags");
            return value;
        }

        private static void WriteResultDropReason(ResultDropReasonMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x03, "result_drop_reason.flags");
            WriteUInt64(destination, 0, value.OperationId);
            WriteUInt64(destination, 8, value.ResultSequence);
            WriteUInt16(destination, 16, value.DropReasonCode);
            destination[18] = (byte)value.SourceRole;
            destination[19] = value.Flags;
            WriteUInt32(destination, 20, value.DiagnosticBytes);
        }

        private static ResultDropReasonMetadata ReadResultDropReason(ReadOnlySpan<byte> source)
        {
            RequireZero(ReadUInt64(source, 24), "result_drop_reason.reserved");
            var value = new ResultDropReasonMetadata(
                ReadUInt64(source, 0),
                ReadUInt64(source, 8),
                ReadUInt16(source, 16),
                (RuntimeRole)source[18],
                source[19],
                ReadUInt32(source, 20));
            RequireMask(value.Flags, 0x03, "result_drop_reason.flags");
            return value;
        }

        private static void WriteRecoverableError(RecoverableErrorMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x03, "recoverable_error.flags");
            WriteUInt32(destination, 0, value.ErrorCode);
            WriteUInt32(destination, 4, value.ErrorScope);
            WriteUInt16(destination, 8, value.RecoveryAction);
            destination[10] = (byte)value.SourceRole;
            destination[11] = value.Flags;
            WriteUInt32(destination, 12, value.RetryAfterMs);
            WriteUInt32(destination, 16, value.RelatedSessionId);
            WriteUInt32(destination, 20, value.RelatedFrameId);
            WriteUInt32(destination, 24, value.RelatedViewId);
            WriteUInt32(destination, 28, value.DiagnosticBytes);
        }

        private static RecoverableErrorMetadata ReadRecoverableError(ReadOnlySpan<byte> source)
        {
            var value = new RecoverableErrorMetadata(
                ReadUInt32(source, 0),
                ReadUInt32(source, 4),
                ReadUInt16(source, 8),
                (RuntimeRole)source[10],
                source[11],
                ReadUInt32(source, 12),
                ReadUInt32(source, 16),
                ReadUInt32(source, 20),
                ReadUInt32(source, 24),
                ReadUInt32(source, 28));
            RequireMask(value.Flags, 0x03, "recoverable_error.flags");
            return value;
        }

        private static void WriteRetryAfter(RetryAfterMetadata value, Span<byte> destination)
        {
            RequireMask(value.Flags, 0x03, "retry_after.flags");
            WriteUInt64(destination, 0, value.ScopeId);
            WriteUInt64(destination, 8, value.ControlSequence);
            WriteUInt32(destination, 16, value.RetryAfterMs);
            WriteUInt32(destination, 20, value.JitterMs);
            WriteUInt16(destination, 24, value.ReasonCode);
            destination[26] = (byte)value.SourceRole;
            destination[27] = value.Flags;
            WriteUInt32(destination, 28, value.DiagnosticBytes);
        }

        private static RetryAfterMetadata ReadRetryAfter(ReadOnlySpan<byte> source)
        {
            var value = new RetryAfterMetadata(
                ReadUInt64(source, 0),
                ReadUInt64(source, 8),
                ReadUInt32(source, 16),
                ReadUInt32(source, 20),
                ReadUInt16(source, 24),
                (RuntimeRole)source[26],
                source[27],
                ReadUInt32(source, 28));
            RequireMask(value.Flags, 0x03, "retry_after.flags");
            return value;
        }

        private static T RequireType<T>(IRuntimeControlMetadata metadata, MessageType messageType)
            where T : struct, IRuntimeControlMetadata
        {
            if (metadata is T typed)
            {
                return typed;
            }

            throw new ArgumentException(
                messageType + " requires " + typeof(T).Name + ", not " + metadata.GetType().Name + ".",
                nameof(metadata));
        }

        private static void ValidateTailLength(uint declaredLength, int actualLength)
        {
            if (declaredLength != (uint)actualLength)
            {
                throw new ArgumentException(
                    "Runtime control tail length " + actualLength + " does not match declared length " + declaredLength + ".");
            }
        }

        private static void RequireMask(uint value, uint mask, string name)
        {
            if ((value & ~mask) != 0)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " sets reserved bits.");
            }
        }

        private static void RequireZero(ulong value, string name)
        {
            if (value != 0)
            {
                throw new ArgumentException(name + " must be zero.", name);
            }
        }

        private static ushort ReadUInt16(ReadOnlySpan<byte> source, int offset) =>
            BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, sizeof(ushort)));

        private static uint ReadUInt32(ReadOnlySpan<byte> source, int offset) =>
            BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, sizeof(uint)));

        private static ulong ReadUInt64(ReadOnlySpan<byte> source, int offset) =>
            BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset, sizeof(ulong)));

        private static void WriteUInt16(Span<byte> destination, int offset, ushort value) =>
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), value);

        private static void WriteUInt32(Span<byte> destination, int offset, uint value) =>
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);

        private static void WriteUInt64(Span<byte> destination, int offset, ulong value) =>
            BinaryPrimitives.WriteUInt64LittleEndian(destination.Slice(offset, sizeof(ulong)), value);
    }
}
