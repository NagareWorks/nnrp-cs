using System;
using Nnrp.Core;

namespace Nnrp.Runtime
{
    public enum NnrpRuntimeEventMetadataKind
    {
        None = 0,
        FrameSubmit,
        ResultPush,
        ResultHint,
        ControlRequest,
        Scheduling,
        Supersede,
        Budget,
        Progress,
        PartialResult,
        Pressure,
        Capability,
        RouteHint,
        TraceContext,
        ResultDropReason,
        RecoverableError,
        RetryAfter,
        FlowUpdate,
        ObjectDescriptor,
        ObjectReference,
        ObjectRelease,
        ObjectDelta,
        CacheReference,
        CacheMiss,
        CacheInvalidate,
        SessionClose,
    }

    public sealed class NnrpRuntimeEventMetadata
    {
        private readonly object? value;

        private NnrpRuntimeEventMetadata(NnrpRuntimeEventMetadataKind kind, object? value)
        {
            Kind = kind;
            this.value = value;
        }

        public NnrpRuntimeEventMetadataKind Kind { get; }

        public static NnrpRuntimeEventMetadata None { get; } =
            new NnrpRuntimeEventMetadata(NnrpRuntimeEventMetadataKind.None, null);

        public T Get<T>()
            where T : struct
        {
            if (value is T typed)
            {
                return typed;
            }

            throw new InvalidOperationException(
                "Runtime event metadata is " + Kind + ", not " + typeof(T).Name + ".");
        }

        internal static NnrpRuntimeEventMetadata Create<T>(NnrpRuntimeEventMetadataKind kind, T value)
            where T : struct
        {
            if (kind == NnrpRuntimeEventMetadataKind.None)
            {
                throw new ArgumentOutOfRangeException(nameof(kind));
            }

            return new NnrpRuntimeEventMetadata(kind, value);
        }
    }

    public enum NnrpRuntimeEventTailKind
    {
        None = 0,
        Body,
        Diagnostic,
        MetadataBodyAndDelta,
    }

    public sealed class NnrpRuntimeEventTail
    {
        private readonly byte[] first;
        private readonly byte[] second;

        private NnrpRuntimeEventTail(
            NnrpRuntimeEventTailKind kind,
            byte[] first,
            byte[] second)
        {
            Kind = kind;
            this.first = first;
            this.second = second;
        }

        public NnrpRuntimeEventTailKind Kind { get; }

        public static NnrpRuntimeEventTail None { get; } =
            new NnrpRuntimeEventTail(
                NnrpRuntimeEventTailKind.None,
                Array.Empty<byte>(),
                Array.Empty<byte>());

        public TResult Match<TResult>(
            Func<TResult> none,
            Func<ReadOnlyMemory<byte>, TResult> body,
            Func<ReadOnlyMemory<byte>, TResult> diagnostic,
            Func<ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, TResult> metadataBodyAndDelta)
        {
            if (none == null)
            {
                throw new ArgumentNullException(nameof(none));
            }

            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (diagnostic == null)
            {
                throw new ArgumentNullException(nameof(diagnostic));
            }

            if (metadataBodyAndDelta == null)
            {
                throw new ArgumentNullException(nameof(metadataBodyAndDelta));
            }

            return Kind switch
            {
                NnrpRuntimeEventTailKind.None => none(),
                NnrpRuntimeEventTailKind.Body => body(first),
                NnrpRuntimeEventTailKind.Diagnostic => diagnostic(first),
                NnrpRuntimeEventTailKind.MetadataBodyAndDelta => metadataBodyAndDelta(first, second),
                _ => throw new InvalidOperationException("Runtime event tail has an unknown variant."),
            };
        }

        internal static NnrpRuntimeEventTail FromBody(ReadOnlySpan<byte> body)
        {
            return new NnrpRuntimeEventTail(
                NnrpRuntimeEventTailKind.Body,
                body.ToArray(),
                Array.Empty<byte>());
        }

        internal static NnrpRuntimeEventTail FromDiagnostic(ReadOnlySpan<byte> diagnostic)
        {
            return new NnrpRuntimeEventTail(
                NnrpRuntimeEventTailKind.Diagnostic,
                diagnostic.ToArray(),
                Array.Empty<byte>());
        }

        internal static NnrpRuntimeEventTail FromDelta(ReadOnlySpan<byte> metadataBody, ReadOnlySpan<byte> delta)
        {
            return new NnrpRuntimeEventTail(
                NnrpRuntimeEventTailKind.MetadataBodyAndDelta,
                metadataBody.ToArray(),
                delta.ToArray());
        }
    }

    public sealed class NnrpRuntimeEvent
    {
        private delegate T SpanParser<T>(ReadOnlySpan<byte> source)
            where T : struct;

        private delegate NnrpRuntimeEventTail TailFactory(ReadOnlySpan<byte> source);

        private NnrpRuntimeEvent(
            RuntimeFrameHeader header,
            NnrpRuntimeEventMetadata metadata,
            NnrpRuntimeEventTail tail)
        {
            Header = header;
            Metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
            Tail = tail ?? throw new ArgumentNullException(nameof(tail));
        }

        public RuntimeFrameHeader Header { get; }

        public NnrpRuntimeEventMetadata Metadata { get; }

        public NnrpRuntimeEventTail Tail { get; }

        public static NnrpRuntimeEvent Decode(RuntimeFrameHeader header, ReadOnlySpan<byte> payload)
        {
            ValidateHeader(header);
            switch (header.MessageType)
            {
                case MessageType.FrameSubmit:
                    return DecodeFixed(
                        header,
                        payload,
                        FrameSubmitMetadata.MetadataLength,
                        NnrpRuntimeEventMetadataKind.FrameSubmit,
                        ParseFrameSubmit,
                        NnrpRuntimeEventTail.FromBody);
                case MessageType.FrameCancel:
                case MessageType.ResultDrop:
                    RequireEmpty(payload, header.MessageType);
                    return new NnrpRuntimeEvent(header, NnrpRuntimeEventMetadata.None, NnrpRuntimeEventTail.None);
                case MessageType.ResultPush:
                    return DecodeFixed(
                        header,
                        payload,
                        ResultPushMetadata.MetadataLength,
                        NnrpRuntimeEventMetadataKind.ResultPush,
                        ParseResultPush,
                        NnrpRuntimeEventTail.FromBody);
                case MessageType.ResultHint:
                    return DecodeNoTail(
                        header,
                        payload,
                        ResultHintMetadata.MetadataLength,
                        NnrpRuntimeEventMetadataKind.ResultHint,
                        ParseResultHint);
                case MessageType.FlowUpdate:
                    return DecodeNoTail(
                        header,
                        payload,
                        FlowUpdateMetadata.MetadataLength,
                        NnrpRuntimeEventMetadataKind.FlowUpdate,
                        ParseFlowUpdate);
                case MessageType.CacheInvalidate:
                    return DecodeNoTail(
                        header,
                        payload,
                        CacheInvalidateMetadata.MetadataLength,
                        NnrpRuntimeEventMetadataKind.CacheInvalidate,
                        ParseCacheInvalidate);
                case MessageType.SessionClose:
                    return DecodeNoTail(
                        header,
                        payload,
                        SessionCloseMetadata.MetadataLength,
                        NnrpRuntimeEventMetadataKind.SessionClose,
                        ParseSessionClose);
                case MessageType.Cancel:
                case MessageType.Abort:
                case MessageType.PriorityUpdate:
                case MessageType.Deadline:
                case MessageType.ExpireAt:
                case MessageType.Supersede:
                case MessageType.BudgetUpdate:
                case MessageType.Progress:
                case MessageType.PartialResult:
                case MessageType.Backpressure:
                case MessageType.CreditUpdate:
                case MessageType.CapabilityNegotiation:
                case MessageType.DegradeProfile:
                case MessageType.RouteHint:
                case MessageType.ExecutionHint:
                case MessageType.TraceContext:
                case MessageType.ResultDropReason:
                case MessageType.ErrorRecoverable:
                case MessageType.RetryAfter:
                    return DecodeControl(header, payload);
                case MessageType.ObjectDeclare:
                case MessageType.ObjectRef:
                case MessageType.ObjectRelease:
                case MessageType.ObjectPatch:
                case MessageType.ObjectDelta:
                case MessageType.CacheReference:
                case MessageType.CacheMiss:
                    return DecodeObject(header, payload);
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(header),
                        header.MessageType,
                        "Message type is not delivered through a Preview4 runtime event pump.");
            }
        }

        private static NnrpRuntimeEvent DecodeControl(RuntimeFrameHeader header, ReadOnlySpan<byte> payload)
        {
            var decoded = NnrpRuntimeControl.Decode(header.MessageType, payload);
            var kind = header.MessageType switch
            {
                MessageType.Cancel or MessageType.Abort => NnrpRuntimeEventMetadataKind.ControlRequest,
                MessageType.PriorityUpdate or MessageType.Deadline or MessageType.ExpireAt => NnrpRuntimeEventMetadataKind.Scheduling,
                MessageType.Supersede => NnrpRuntimeEventMetadataKind.Supersede,
                MessageType.BudgetUpdate => NnrpRuntimeEventMetadataKind.Budget,
                MessageType.Progress => NnrpRuntimeEventMetadataKind.Progress,
                MessageType.PartialResult => NnrpRuntimeEventMetadataKind.PartialResult,
                MessageType.Backpressure or MessageType.CreditUpdate => NnrpRuntimeEventMetadataKind.Pressure,
                MessageType.CapabilityNegotiation or MessageType.DegradeProfile => NnrpRuntimeEventMetadataKind.Capability,
                MessageType.RouteHint or MessageType.ExecutionHint => NnrpRuntimeEventMetadataKind.RouteHint,
                MessageType.TraceContext => NnrpRuntimeEventMetadataKind.TraceContext,
                MessageType.ResultDropReason => NnrpRuntimeEventMetadataKind.ResultDropReason,
                MessageType.ErrorRecoverable => NnrpRuntimeEventMetadataKind.RecoverableError,
                MessageType.RetryAfter => NnrpRuntimeEventMetadataKind.RetryAfter,
                _ => throw new InvalidOperationException(),
            };
            var metadata = CreateControlMetadata(kind, decoded.Metadata);
            var tail = header.MessageType switch
            {
                MessageType.Cancel or MessageType.Abort or MessageType.Supersede
                    or MessageType.ResultDropReason or MessageType.ErrorRecoverable or MessageType.RetryAfter =>
                    NnrpRuntimeEventTail.FromDiagnostic(decoded.Tail.Span),
                MessageType.Progress or MessageType.PartialResult or MessageType.CapabilityNegotiation
                    or MessageType.DegradeProfile or MessageType.RouteHint or MessageType.ExecutionHint
                    or MessageType.TraceContext => NnrpRuntimeEventTail.FromBody(decoded.Tail.Span),
                _ => NnrpRuntimeEventTail.None,
            };
            return new NnrpRuntimeEvent(header, metadata, tail);
        }

        private static NnrpRuntimeEvent DecodeObject(RuntimeFrameHeader header, ReadOnlySpan<byte> payload)
        {
            var decoded = NnrpRuntimeObject.Decode(header.MessageType, payload);
            var kind = header.MessageType switch
            {
                MessageType.ObjectDeclare => NnrpRuntimeEventMetadataKind.ObjectDescriptor,
                MessageType.ObjectRef => NnrpRuntimeEventMetadataKind.ObjectReference,
                MessageType.ObjectRelease => NnrpRuntimeEventMetadataKind.ObjectRelease,
                MessageType.ObjectPatch or MessageType.ObjectDelta => NnrpRuntimeEventMetadataKind.ObjectDelta,
                MessageType.CacheReference => NnrpRuntimeEventMetadataKind.CacheReference,
                MessageType.CacheMiss => NnrpRuntimeEventMetadataKind.CacheMiss,
                _ => throw new InvalidOperationException(),
            };
            var metadata = CreateObjectMetadata(kind, decoded.Metadata);
            if (decoded.Metadata is ObjectDeltaMetadata delta)
            {
                var metadataBytes = checked((int)delta.MetadataBytes);
                return new NnrpRuntimeEvent(
                    header,
                    metadata,
                    NnrpRuntimeEventTail.FromDelta(
                        decoded.Tail.Span.Slice(0, metadataBytes),
                        decoded.Tail.Span.Slice(metadataBytes)));
            }

            var tail = header.MessageType is MessageType.ObjectRelease or MessageType.CacheMiss
                ? NnrpRuntimeEventTail.FromDiagnostic(decoded.Tail.Span)
                : NnrpRuntimeEventTail.FromBody(decoded.Tail.Span);
            return new NnrpRuntimeEvent(header, metadata, tail);
        }

        private static NnrpRuntimeEventMetadata CreateControlMetadata(
            NnrpRuntimeEventMetadataKind kind,
            IRuntimeControlMetadata metadata)
        {
            return metadata switch
            {
                ControlRequestMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                SchedulingMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                SupersedeMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                BudgetMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                ProgressMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                PartialResultMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                PressureMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                CapabilityMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                RouteHintMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                TraceContextMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                ResultDropReasonMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                RecoverableErrorMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                RetryAfterMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                _ => throw new InvalidOperationException("Unknown runtime-control metadata type."),
            };
        }

        private static NnrpRuntimeEventMetadata CreateObjectMetadata(
            NnrpRuntimeEventMetadataKind kind,
            IRuntimeObjectMetadata metadata)
        {
            return metadata switch
            {
                ObjectDescriptorMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                ObjectReferenceMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                ObjectReleaseMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                ObjectDeltaMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                CacheReferenceMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                CacheMissMetadata value => NnrpRuntimeEventMetadata.Create(kind, value),
                _ => throw new InvalidOperationException("Unknown runtime-object metadata type."),
            };
        }

        private static NnrpRuntimeEvent DecodeNoTail<T>(
            RuntimeFrameHeader header,
            ReadOnlySpan<byte> payload,
            int metadataLength,
            NnrpRuntimeEventMetadataKind kind,
            SpanParser<T> parser)
            where T : struct
        {
            if (payload.Length != metadataLength)
            {
                throw new ArgumentException("Runtime event payload has an invalid metadata length.", nameof(payload));
            }

            return new NnrpRuntimeEvent(
                header,
                NnrpRuntimeEventMetadata.Create(kind, parser(payload)),
                NnrpRuntimeEventTail.None);
        }

        private static NnrpRuntimeEvent DecodeFixed<T>(
            RuntimeFrameHeader header,
            ReadOnlySpan<byte> payload,
            int metadataLength,
            NnrpRuntimeEventMetadataKind kind,
            SpanParser<T> parser,
            TailFactory tailFactory)
            where T : struct
        {
            if (payload.Length < metadataLength)
            {
                throw new ArgumentException("Runtime event payload is shorter than its metadata layout.", nameof(payload));
            }

            return new NnrpRuntimeEvent(
                header,
                NnrpRuntimeEventMetadata.Create(kind, parser(payload.Slice(0, metadataLength))),
                tailFactory(payload.Slice(metadataLength)));
        }

        private static FrameSubmitMetadata ParseFrameSubmit(ReadOnlySpan<byte> source)
        {
            if (!FrameSubmitMetadata.TryParse(source, true, out var value, out var error))
            {
                throw new ArgumentException("Invalid FRAME_SUBMIT metadata: " + error + ".", nameof(source));
            }

            return value;
        }

        private static ResultPushMetadata ParseResultPush(ReadOnlySpan<byte> source)
        {
            if (!ResultPushMetadata.TryParse(source, true, out var value, out var error))
            {
                throw new ArgumentException("Invalid RESULT_PUSH metadata: " + error + ".", nameof(source));
            }

            return value;
        }

        private static ResultHintMetadata ParseResultHint(ReadOnlySpan<byte> source)
        {
            if (!ResultHintMetadata.TryParse(source, true, out var value, out var error))
            {
                throw new ArgumentException("Invalid RESULT_HINT metadata: " + error + ".", nameof(source));
            }

            return value;
        }

        private static FlowUpdateMetadata ParseFlowUpdate(ReadOnlySpan<byte> source)
        {
            if (!FlowUpdateMetadata.TryParse(source, true, out var value, out var error))
            {
                throw new ArgumentException("Invalid FLOW_UPDATE metadata: " + error + ".", nameof(source));
            }

            return value;
        }

        private static CacheInvalidateMetadata ParseCacheInvalidate(ReadOnlySpan<byte> source)
        {
            if (!CacheInvalidateMetadata.TryParse(source, out var value, out var error))
            {
                throw new ArgumentException("Invalid CACHE_INVALIDATE metadata: " + error + ".", nameof(source));
            }

            return value;
        }

        private static SessionCloseMetadata ParseSessionClose(ReadOnlySpan<byte> source)
        {
            if (!SessionCloseMetadata.TryParse(source, true, out var value, out var error))
            {
                throw new ArgumentException("Invalid SESSION_CLOSE metadata: " + error + ".", nameof(source));
            }

            return value;
        }

        private static void RequireEmpty(ReadOnlySpan<byte> payload, MessageType messageType)
        {
            if (!payload.IsEmpty)
            {
                throw new ArgumentException(messageType + " runtime events must not carry a payload.", nameof(payload));
            }
        }

        private static void ValidateHeader(RuntimeFrameHeader header)
        {
            if (header.VersionMajor != NnrpHeader.CurrentVersionMajor
                || header.WireFormat != NnrpHeader.CurrentWireFormat)
            {
                throw new ArgumentException("Runtime event header does not use the current NNRP/1 wire format.", nameof(header));
            }
        }
    }
}
