using System;
using Nnrp.Core;

namespace Nnrp.NativeBridge.Tests
{
    internal static class NnrpNativeRuntimeConnectionTestExtensions
    {
        internal static NnrpNativeRuntimeSession OpenSession(
            this NnrpNativeRuntimeConnection connection,
            uint sessionId,
            uint generation,
            ushort profileId,
            uint schemaId,
            uint schemaVersion)
        {
            return connection.OpenSession(
                sessionId,
                sessionId,
                generation,
                profileId,
                SessionPriorityClass.Balanced,
                schemaId,
                schemaVersion,
                defaultDeadlineMilliseconds: 500,
                maxInFlightOperations: 4,
                leaseTtlHintMilliseconds: 30_000,
                allowResume: false,
                resumeTokenBytes: 0,
                Array.Empty<CacheObjectKind>());
        }

        internal static NnrpNativeRuntimeSession ResumeSession(
            this NnrpNativeRuntimeConnection connection,
            uint sessionId,
            uint generation,
            ushort profileId,
            uint schemaId,
            uint schemaVersion,
            uint resumeTokenBytes,
            out NnrpSessionRecoveryOutcome recoveryOutcome)
        {
            return connection.ResumeSession(
                sessionId,
                sessionId,
                generation,
                profileId,
                SessionPriorityClass.Balanced,
                schemaId,
                schemaVersion,
                defaultDeadlineMilliseconds: 500,
                maxInFlightOperations: 4,
                leaseTtlHintMilliseconds: 30_000,
                resumeTokenBytes,
                Array.Empty<CacheObjectKind>(),
                new byte[resumeTokenBytes],
                out recoveryOutcome);
        }
    }
}
