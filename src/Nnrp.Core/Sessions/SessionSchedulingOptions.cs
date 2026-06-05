using System;

namespace Nnrp.Core
{
    public readonly struct SessionSchedulingOptions : IEquatable<SessionSchedulingOptions>
    {
        public const uint StandardDefaultDeadlineMilliseconds = 500;

        public const ushort StandardMaxInFlightOperations = 4;

        public SessionSchedulingOptions(
            SessionPriorityClass priorityClass,
            uint defaultDeadlineMilliseconds = 0,
            ushort maxInFlightOperations = 0)
        {
            if (!Enum.IsDefined(typeof(SessionPriorityClass), priorityClass))
            {
                throw new ArgumentOutOfRangeException(nameof(priorityClass));
            }

            PriorityClass = priorityClass;
            DefaultDeadlineMilliseconds = defaultDeadlineMilliseconds;
            MaxInFlightOperations = maxInFlightOperations;
        }

        public static SessionSchedulingOptions Default { get; } =
            new SessionSchedulingOptions(
                SessionPriorityClass.Balanced,
                StandardDefaultDeadlineMilliseconds,
                StandardMaxInFlightOperations);

        public SessionPriorityClass PriorityClass { get; }

        public uint DefaultDeadlineMilliseconds { get; }

        public ushort MaxInFlightOperations { get; }

        public SessionOpenMetadata CreateSessionOpenMetadata(
            uint requestedSessionId,
            ushort profileId,
            SessionFlags sessionFlags = SessionFlags.None,
            uint schemaId = 0,
            uint schemaVersion = 0,
            uint leaseTtlHintMilliseconds = 0,
            uint resumeTokenBytes = 0,
            uint authBytes = 0,
            uint sessionExtensionBytes = 0,
            ulong clientSessionTag = 0)
        {
            return new SessionOpenMetadata(
                requestedSessionId,
                profileId,
                PriorityClass,
                sessionFlags,
                schemaId,
                schemaVersion,
                DefaultDeadlineMilliseconds,
                MaxInFlightOperations,
                leaseTtlHintMilliseconds,
                resumeTokenBytes,
                authBytes,
                sessionExtensionBytes,
                clientSessionTag);
        }

        public bool Equals(SessionSchedulingOptions other)
        {
            return PriorityClass == other.PriorityClass
                && DefaultDeadlineMilliseconds == other.DefaultDeadlineMilliseconds
                && MaxInFlightOperations == other.MaxInFlightOperations;
        }

        public override bool Equals(object obj)
        {
            return obj is SessionSchedulingOptions other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = PriorityClass.GetHashCode();
                hash = (hash * 397) ^ DefaultDeadlineMilliseconds.GetHashCode();
                hash = (hash * 397) ^ MaxInFlightOperations.GetHashCode();
                return hash;
            }
        }
    }
}
