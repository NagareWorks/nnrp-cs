using System;

namespace Nnrp.Runtime
{
    public enum NnrpClientEventKind : byte
    {
        Runtime = 0,
        Lifecycle = 1,
    }

    public sealed class NnrpClientEvent
    {
        private readonly object value;

        private NnrpClientEvent(NnrpClientEventKind kind, object value)
        {
            Kind = kind;
            this.value = value;
        }

        public NnrpClientEventKind Kind { get; }

        public static NnrpClientEvent FromRuntime(NnrpRuntimeEvent @event)
        {
            return new NnrpClientEvent(
                NnrpClientEventKind.Runtime,
                @event ?? throw new ArgumentNullException(nameof(@event)));
        }

        public static NnrpClientEvent FromLifecycle(NnrpOperationLifecycleEvent @event)
        {
            return new NnrpClientEvent(
                NnrpClientEventKind.Lifecycle,
                @event ?? throw new ArgumentNullException(nameof(@event)));
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
                NnrpClientEventKind.Runtime => runtime((NnrpRuntimeEvent)value),
                NnrpClientEventKind.Lifecycle => lifecycle((NnrpOperationLifecycleEvent)value),
                _ => throw new InvalidOperationException("Client event has an unknown variant."),
            };
        }
    }
}
