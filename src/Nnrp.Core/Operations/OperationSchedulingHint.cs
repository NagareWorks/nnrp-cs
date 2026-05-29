using System;

namespace Nnrp.Core
{
    public readonly struct OperationSchedulingHint : IEquatable<OperationSchedulingHint>
    {
        public OperationSchedulingHint(
            ulong operationId,
            SessionPriorityClass priorityClass,
            uint deadlineWindowMilliseconds = 0)
        {
            if (operationId == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(operationId), "Operation id must be non-zero.");
            }

            if (!Enum.IsDefined(typeof(SessionPriorityClass), priorityClass))
            {
                throw new ArgumentOutOfRangeException(nameof(priorityClass));
            }

            OperationId = operationId;
            PriorityClass = priorityClass;
            DeadlineWindowMilliseconds = deadlineWindowMilliseconds;
        }

        public ulong OperationId { get; }

        public SessionPriorityClass PriorityClass { get; }

        public uint DeadlineWindowMilliseconds { get; }

        public bool Equals(OperationSchedulingHint other)
        {
            return OperationId == other.OperationId
                && PriorityClass == other.PriorityClass
                && DeadlineWindowMilliseconds == other.DeadlineWindowMilliseconds;
        }

        public override bool Equals(object obj)
        {
            return obj is OperationSchedulingHint other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = OperationId.GetHashCode();
                hash = (hash * 397) ^ PriorityClass.GetHashCode();
                hash = (hash * 397) ^ DeadlineWindowMilliseconds.GetHashCode();
                return hash;
            }
        }
    }
}
