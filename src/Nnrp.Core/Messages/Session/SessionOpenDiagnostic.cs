using System;

namespace Nnrp.Core
{
    public readonly struct SessionOpenDiagnostic : IEquatable<SessionOpenDiagnostic>
    {
        private const SessionAckFlags AllowedFlags = SessionAckFlags.ResumeEnabled
            | SessionAckFlags.BackgroundResultsEnabled
            | SessionAckFlags.CacheLeasesEnabled
            | SessionAckFlags.SchemaOverrideEnabled
            | SessionAckFlags.PriorityDowngraded;

        public SessionOpenDiagnostic(
            SessionStatus sessionStatus,
            SessionErrorCode sessionErrorCode,
            SessionAckFlags sessionFlagsAck,
            SessionPriorityClass acceptedPriorityClass,
            bool hasRequestedPriority,
            SessionPriorityClass requestedPriorityClass,
            uint sessionId,
            uint routeScopeId)
        {
            if (!Enum.IsDefined(typeof(SessionStatus), sessionStatus))
            {
                throw new ArgumentOutOfRangeException(nameof(sessionStatus));
            }

            if (!Enum.IsDefined(typeof(SessionErrorCode), sessionErrorCode))
            {
                throw new ArgumentOutOfRangeException(nameof(sessionErrorCode));
            }

            if (!Enum.IsDefined(typeof(SessionPriorityClass), acceptedPriorityClass))
            {
                throw new ArgumentOutOfRangeException(nameof(acceptedPriorityClass));
            }

            if (((uint)sessionFlagsAck & ~(uint)AllowedFlags) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sessionFlagsAck));
            }

            if (hasRequestedPriority && !Enum.IsDefined(typeof(SessionPriorityClass), requestedPriorityClass))
            {
                throw new ArgumentOutOfRangeException(nameof(requestedPriorityClass));
            }

            SessionStatus = sessionStatus;
            SessionErrorCode = sessionErrorCode;
            SessionFlagsAck = sessionFlagsAck;
            AcceptedPriorityClass = acceptedPriorityClass;
            HasRequestedPriority = hasRequestedPriority;
            RequestedPriorityClass = requestedPriorityClass;
            SessionId = sessionId;
            RouteScopeId = routeScopeId;
        }

        public SessionStatus SessionStatus { get; }

        public SessionErrorCode SessionErrorCode { get; }

        public SessionAckFlags SessionFlagsAck { get; }

        public SessionPriorityClass AcceptedPriorityClass { get; }

        public bool HasRequestedPriority { get; }

        public SessionPriorityClass RequestedPriorityClass { get; }

        public uint SessionId { get; }

        public uint RouteScopeId { get; }

        public bool IsPriorityDowngraded => (SessionFlagsAck & SessionAckFlags.PriorityDowngraded) != 0;

        public bool ShouldRetryLater => SessionStatus == SessionStatus.RetryLater;

        public bool IsRejected => SessionStatus == SessionStatus.Rejected;

        public bool HasSessionError => SessionErrorCode != SessionErrorCode.None;

        public bool HasDiagnostic => IsPriorityDowngraded
            || ShouldRetryLater
            || IsRejected
            || HasSessionError;

        public static SessionOpenDiagnostic FromAck(SessionOpenAckMetadata metadata)
        {
            return new SessionOpenDiagnostic(
                metadata.SessionStatus,
                metadata.SessionErrorCode,
                metadata.SessionFlagsAck,
                metadata.AcceptedPriorityClass,
                hasRequestedPriority: false,
                requestedPriorityClass: default,
                metadata.SessionId,
                metadata.RouteScopeId);
        }

        public static SessionOpenDiagnostic FromAck(SessionOpenMetadata request, SessionOpenAckMetadata metadata)
        {
            return new SessionOpenDiagnostic(
                metadata.SessionStatus,
                metadata.SessionErrorCode,
                metadata.SessionFlagsAck,
                metadata.AcceptedPriorityClass,
                hasRequestedPriority: true,
                request.PriorityClass,
                metadata.SessionId,
                metadata.RouteScopeId);
        }

        public bool Equals(SessionOpenDiagnostic other)
        {
            return SessionStatus == other.SessionStatus
                && SessionErrorCode == other.SessionErrorCode
                && SessionFlagsAck == other.SessionFlagsAck
                && AcceptedPriorityClass == other.AcceptedPriorityClass
                && HasRequestedPriority == other.HasRequestedPriority
                && RequestedPriorityClass == other.RequestedPriorityClass
                && SessionId == other.SessionId
                && RouteScopeId == other.RouteScopeId;
        }

        public override bool Equals(object obj)
        {
            return obj is SessionOpenDiagnostic other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = SessionStatus.GetHashCode();
                hash = (hash * 397) ^ SessionErrorCode.GetHashCode();
                hash = (hash * 397) ^ SessionFlagsAck.GetHashCode();
                hash = (hash * 397) ^ AcceptedPriorityClass.GetHashCode();
                hash = (hash * 397) ^ HasRequestedPriority.GetHashCode();
                hash = (hash * 397) ^ RequestedPriorityClass.GetHashCode();
                hash = (hash * 397) ^ SessionId.GetHashCode();
                hash = (hash * 397) ^ RouteScopeId.GetHashCode();
                return hash;
            }
        }
    }
}
