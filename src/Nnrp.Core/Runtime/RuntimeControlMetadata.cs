using System;

namespace Nnrp.Runtime
{
    /// <summary>Marker implemented by every frozen Preview4 runtime-control metadata value.</summary>
    public interface IRuntimeControlMetadata
    {
    }

    public enum NnrpResultDropReasonCode : ushort
    {
        None = 0,
        DeadlineExpired = 1,
        Superseded = 2,
        PeerCancelled = 3,
        Backpressure = 4,
        CapabilityMismatch = 5,
        BudgetExceeded = 6,
        ObjectInvalidated = 7,
        TransportClosed = 8,
        ConformanceInjection = 9,
    }

    /// <summary>Runtime endpoint roles carried by control metadata.</summary>
    public enum RuntimeRole : byte
    {
        Unspecified = 0,
        Client = 1,
        Server = 2,
        Runtime = 3,
        Subagent = 4,
        Tool = 5,
        Scheduler = 6,
        ConformanceRunner = 7,
    }

    /// <summary>Metadata shared by <c>Cancel</c> and <c>Abort</c>.</summary>
    public readonly record struct ControlRequestMetadata(
        ulong OperationId,
        ulong ControlSequence,
        ushort ReasonCode,
        RuntimeRole SourceRole,
        byte Flags,
        uint DiagnosticBytes) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>Metadata shared by priority, deadline, and expiration controls.</summary>
    public readonly record struct SchedulingMetadata(
        ulong OperationId,
        ulong ControlSequence,
        ushort PriorityClass,
        short PriorityDelta,
        ulong DeadlineUnixMs,
        uint Flags) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>Metadata for replacing one operation with another.</summary>
    public readonly record struct SupersedeMetadata(
        ulong OldOperationId,
        ulong NewOperationId,
        ulong ControlSequence,
        NnrpResultDropReasonCode DropReasonCode,
        ushort Flags,
        uint DiagnosticBytes) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>Compute, memory, bandwidth, and token budget metadata.</summary>
    public readonly record struct BudgetMetadata(
        ulong OperationId,
        ulong ComputeBudgetUnits,
        ulong MemoryBudgetBytes,
        ulong BandwidthBudgetBytes,
        uint TokenBudget,
        uint Flags) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 40;
    }

    /// <summary>Progress metadata with an optional declared body.</summary>
    public readonly record struct ProgressMetadata(
        ulong OperationId,
        ulong ProgressSequence,
        ushort StageCode,
        ushort PercentX100,
        ulong ObjectId,
        uint BodyBytes) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>Partial result metadata with an optional object reference.</summary>
    public readonly record struct PartialResultMetadata(
        ulong OperationId,
        ulong ResultSequence,
        ulong ObjectId,
        ulong DeltaSequence,
        uint BodyBytes,
        uint Flags) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 40;
    }

    /// <summary>Backpressure or credit-window metadata.</summary>
    public readonly record struct PressureMetadata(
        ulong ScopeId,
        ulong CreditWindow,
        ushort PressureLevel,
        ushort PressureReason,
        uint RetryAfterMs,
        uint Flags) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>Capability cost, preference, and limit metadata.</summary>
    public readonly record struct CapabilityMetadata(
        ushort ProfileId,
        ushort CapabilityCount,
        ushort CostModelId,
        ushort PreferenceRank,
        ulong LimitBytes,
        ulong LimitUnits,
        uint BodyBytes,
        uint Flags) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>Routing and execution placement metadata.</summary>
    public readonly record struct RouteHintMetadata(
        ulong OperationId,
        uint RouteId,
        ushort ExecutorClass,
        ushort AffinityClass,
        ulong DeadlineUnixMs,
        uint BodyBytes,
        uint Flags) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>End-to-end trace context metadata.</summary>
    public readonly record struct TraceContextMetadata(
        ulong TraceId,
        ulong SpanId,
        ulong ParentSpanId,
        ushort StageCode,
        ushort Flags,
        uint BodyBytes) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>Metadata explaining why a result was dropped.</summary>
    public readonly record struct ResultDropReasonMetadata(
        ulong OperationId,
        ulong ResultSequence,
        NnrpResultDropReasonCode DropReasonCode,
        RuntimeRole SourceRole,
        byte Flags,
        uint DiagnosticBytes) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>Recoverable error metadata preserving protocol error fields.</summary>
    public readonly record struct RecoverableErrorMetadata(
        uint ErrorCode,
        uint ErrorScope,
        ushort RecoveryAction,
        RuntimeRole SourceRole,
        byte Flags,
        uint RetryAfterMs,
        uint RelatedSessionId,
        uint RelatedFrameId,
        uint RelatedViewId,
        uint DiagnosticBytes) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>Retry delay and reason metadata.</summary>
    public readonly record struct RetryAfterMetadata(
        ulong ScopeId,
        ulong ControlSequence,
        uint RetryAfterMs,
        uint JitterMs,
        ushort ReasonCode,
        RuntimeRole SourceRole,
        byte Flags,
        uint DiagnosticBytes) : IRuntimeControlMetadata
    {
        public const int EncodedLength = 32;
    }

    /// <summary>A decoded metadata value and its declared tail bytes.</summary>
    public sealed class DecodedRuntimeControlMetadata
    {
        public DecodedRuntimeControlMetadata(IRuntimeControlMetadata metadata, ReadOnlyMemory<byte> tail)
        {
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Tail = tail;
        }

        public IRuntimeControlMetadata Metadata { get; }

        public ReadOnlyMemory<byte> Tail { get; }

        public T GetMetadata<T>()
            where T : struct, IRuntimeControlMetadata
        {
            if (Metadata is T typed)
            {
                return typed;
            }

            throw new InvalidOperationException(
                "Decoded runtime control metadata is " + Metadata.GetType().Name + ", not " + typeof(T).Name + ".");
        }
    }
}

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
