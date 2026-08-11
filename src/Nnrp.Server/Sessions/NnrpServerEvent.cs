using System;
using Nnrp.Runtime;

namespace Nnrp.Server
{
    public enum NnrpServerEventKind : byte
    {
        Submit = 0,
        Runtime = 1,
        Lifecycle = 2,
    }

    public sealed class NnrpServerEvent
    {
        private readonly object value;

        private NnrpServerEvent(NnrpServerEventKind kind, object value)
        {
            Kind = kind;
            this.value = value;
        }

        public NnrpServerEventKind Kind { get; }

        public static NnrpServerEvent FromSubmit(NnrpServerOperation operation)
        {
            return new NnrpServerEvent(
                NnrpServerEventKind.Submit,
                operation ?? throw new ArgumentNullException(nameof(operation)));
        }

        public static NnrpServerEvent FromRuntime(NnrpRuntimeEvent @event)
        {
            return new NnrpServerEvent(
                NnrpServerEventKind.Runtime,
                @event ?? throw new ArgumentNullException(nameof(@event)));
        }

        public static NnrpServerEvent FromLifecycle(NnrpOperationLifecycleEvent @event)
        {
            return new NnrpServerEvent(
                NnrpServerEventKind.Lifecycle,
                @event ?? throw new ArgumentNullException(nameof(@event)));
        }

        public TResult Match<TResult>(
            Func<NnrpServerOperation, TResult> submit,
            Func<NnrpRuntimeEvent, TResult> runtime,
            Func<NnrpOperationLifecycleEvent, TResult> lifecycle)
        {
            if (submit == null)
            {
                throw new ArgumentNullException(nameof(submit));
            }

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
                NnrpServerEventKind.Submit => submit((NnrpServerOperation)value),
                NnrpServerEventKind.Runtime => runtime((NnrpRuntimeEvent)value),
                NnrpServerEventKind.Lifecycle => lifecycle((NnrpOperationLifecycleEvent)value),
                _ => throw new InvalidOperationException("Server event has an unknown variant."),
            };
        }
    }
}
