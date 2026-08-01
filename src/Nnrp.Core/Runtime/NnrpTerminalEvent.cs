using System;
using Nnrp.Core;

namespace Nnrp.Runtime
{
    public enum NnrpTerminalEventKind : byte
    {
        Runtime = 0,
        Lifecycle = 1,
    }

    public sealed class NnrpOperationLifecycleEvent
    {
        public NnrpOperationLifecycleEvent(ulong operationId, NnrpOperationState state)
        {
            if (operationId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operationId));
            }

            if (!Enum.IsDefined(typeof(NnrpOperationState), state))
            {
                throw new ArgumentOutOfRangeException(nameof(state));
            }

            OperationId = operationId;
            State = state;
        }

        public ulong OperationId { get; }

        public NnrpOperationState State { get; }
    }

    public sealed class NnrpTerminalEvent
    {
        private readonly object value;
        private readonly ulong correlatedOperationId;
        private readonly NnrpResultTerminalState expectedTerminalState;

        private NnrpTerminalEvent(
            NnrpTerminalEventKind kind,
            object value,
            NnrpResultTerminalState expectedTerminalState,
            ulong correlatedOperationId)
        {
            Kind = kind;
            this.value = value;
            this.expectedTerminalState = expectedTerminalState;
            this.correlatedOperationId = correlatedOperationId;
        }

        public NnrpTerminalEventKind Kind { get; }

        public static NnrpTerminalEvent FromRuntime(NnrpRuntimeEvent @event)
        {
            if (@event == null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            var state = @event.Header.MessageType switch
            {
                MessageType.ResultPush
                    when @event.Metadata.Kind == NnrpRuntimeEventMetadataKind.ResultPush
                        && @event.Tail.Kind == NnrpRuntimeEventTailKind.Body =>
                    NnrpResultTerminalState.Success,
                MessageType.ResultDrop
                    when @event.Metadata.Kind == NnrpRuntimeEventMetadataKind.None
                        && @event.Tail.Kind == NnrpRuntimeEventTailKind.None =>
                    NnrpResultTerminalState.Dropped,
                MessageType.ResultDropReason
                    when @event.Metadata.Kind == NnrpRuntimeEventMetadataKind.ResultDropReason
                        && @event.Tail.Kind == NnrpRuntimeEventTailKind.Diagnostic =>
                    NnrpResultTerminalState.Dropped,
                _ => throw new ArgumentException(
                    "Runtime terminal evidence must be ResultPush, ResultDrop, or ResultDropReason with its frozen variants.",
                    nameof(@event)),
            };
            var operationId = @event.Metadata.Kind == NnrpRuntimeEventMetadataKind.ResultDropReason
                ? @event.Metadata.Get<ResultDropReasonMetadata>().OperationId
                : 0;
            return new NnrpTerminalEvent(NnrpTerminalEventKind.Runtime, @event, state, operationId);
        }

        public static NnrpTerminalEvent FromLifecycle(NnrpOperationLifecycleEvent @event)
        {
            if (@event == null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            var state = @event.State switch
            {
                NnrpOperationState.Completed => NnrpResultTerminalState.Success,
                NnrpOperationState.Cancelled => NnrpResultTerminalState.Cancelled,
                NnrpOperationState.Superseded => NnrpResultTerminalState.Dropped,
                NnrpOperationState.Failed => NnrpResultTerminalState.Error,
                _ => throw new ArgumentException(
                    "Lifecycle terminal evidence must carry Completed, Cancelled, Superseded, or Failed.",
                    nameof(@event)),
            };
            return new NnrpTerminalEvent(
                NnrpTerminalEventKind.Lifecycle,
                @event,
                state,
                @event.OperationId);
        }

        public TResult Match<TResult>(
            Func<NnrpRuntimeEvent, TResult> runtime,
            Func<NnrpOperationLifecycleEvent, TResult> lifecycle)
        {
            if (runtime == null)
            {
                throw new ArgumentNullException(nameof(runtime));
            }

            if (lifecycle == null)
            {
                throw new ArgumentNullException(nameof(lifecycle));
            }

            return Kind switch
            {
                NnrpTerminalEventKind.Runtime => runtime((NnrpRuntimeEvent)value),
                NnrpTerminalEventKind.Lifecycle => lifecycle((NnrpOperationLifecycleEvent)value),
                _ => throw new InvalidOperationException("Terminal event has an unknown variant."),
            };
        }

        internal NnrpResultTerminalState ExpectedTerminalState => expectedTerminalState;

        internal void ValidateResult(ulong operationId, NnrpResultTerminalState terminalState)
        {
            if (terminalState != expectedTerminalState)
            {
                throw new ArgumentException(
                    "Result terminal state does not match its terminal evidence.",
                    nameof(terminalState));
            }

            if (correlatedOperationId != 0 && correlatedOperationId != operationId)
            {
                throw new ArgumentException(
                    "Result operation identity does not match its terminal evidence.",
                    nameof(operationId));
            }
        }
    }
}
