using Nnrp.Core;
using Nnrp.NativeBridge;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class LiveNativeResumeSmokeTests
    {
        private const uint SessionRecoveryOutcomeResumed = 2;

        [LiveNativeArtifactFact]
        public void ResumeSessionPassesAgainstConfiguredNativeArtifact()
        {
            Assert.True(
                LiveNativeArtifactFactAttribute.TryResolveArtifact(out var artifactPath, out var reason),
                reason);

            using var entrypoints = NnrpNativeRuntimeEntrypoints.Load(artifactPath);
            var client = new NnrpNativeRuntimeClient(entrypoints);
            using var connection = client.Connect(101, 1, NnrpNativeArtifact.TransportSlotTcp);
            NnrpNativeRuntimeSession? opened = null;
            NnrpNativeRuntimeSession? resumed = null;

            try
            {
                opened = connection.OpenSession(
                    requestedSessionId: 201,
                    generation: 1,
                    profileId: SchemaDescriptorHeader.ProfileToken,
                    schemaId: SchemaDescriptorHeader.TokenDeltaSchemaId,
                    schemaVersion: SchemaDescriptorHeader.TokenDeltaSchemaVersion);
                resumed = connection.ResumeSession(
                    requestedSessionId: 202,
                    generation: 2,
                    profileId: SchemaDescriptorHeader.ProfileToken,
                    schemaId: SchemaDescriptorHeader.TokenDeltaSchemaId,
                    schemaVersion: SchemaDescriptorHeader.TokenDeltaSchemaVersion,
                    resumeTokenBytes: 16,
                    recoveryOutcome: out var recoveryOutcome);

                Assert.Equal((ulong)101, connection.Handle.Handle.Id);
                Assert.Equal((ulong)201, opened.Handle.Handle.Id);
                Assert.Equal((uint)1, opened.Handle.Handle.Generation);
                Assert.Equal((ulong)202, resumed.Handle.Handle.Id);
                Assert.Equal((uint)2, resumed.Handle.Handle.Generation);
                Assert.Equal(SessionRecoveryOutcomeResumed, recoveryOutcome.OutcomeCode);
            }
            finally
            {
                if (resumed != null && !resumed.IsClosed)
                {
                    resumed.Close();
                }

                if (opened != null && !opened.IsClosed)
                {
                    opened.Close();
                }
            }
        }
    }
}
