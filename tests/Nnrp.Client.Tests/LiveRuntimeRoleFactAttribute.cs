using System;
using System.IO;
using Xunit;

namespace Nnrp.Client.Tests
{
    internal sealed class LiveRuntimeRoleFactAttribute : FactAttribute
    {
        internal const string ArtifactPathVariableName = "NNRP_NATIVE_IPC_ARTIFACT_PATH";

        public LiveRuntimeRoleFactAttribute()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("NNRP_RUN_LIVE_NATIVE_TRANSPORT_TESTS"),
                    "1",
                    StringComparison.Ordinal))
            {
                Skip = "Set NNRP_RUN_LIVE_NATIVE_TRANSPORT_TESTS=1 to run the Rust-backed role E2E.";
                return;
            }

            var artifactPath = Environment.GetEnvironmentVariable(ArtifactPathVariableName);
            if (string.IsNullOrWhiteSpace(artifactPath) || !File.Exists(artifactPath))
            {
                Skip = $"{ArtifactPathVariableName} must point to the IPC native artifact.";
            }
        }
    }
}
