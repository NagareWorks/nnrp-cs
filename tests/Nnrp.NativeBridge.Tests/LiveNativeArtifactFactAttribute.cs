using System;
using System.IO;
using Nnrp.NativeBridge;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    internal sealed class LiveNativeArtifactFactAttribute : FactAttribute
    {
        internal const string EnableVariableName = "NNRP_RUN_LIVE_NATIVE_ARTIFACT_SMOKE";
        internal const string ArtifactPathVariableName = "NNRP_NATIVE_ARTIFACT_PATH";

        public LiveNativeArtifactFactAttribute()
        {
            if (!ExternalLoopbackFactAttribute.IsEnabled(Environment.GetEnvironmentVariable(EnableVariableName)))
            {
                Skip = $"Set {EnableVariableName}=1 to run the opt-in live native artifact smoke test.";
                return;
            }

            if (!TryResolveArtifact(out _, out var reason))
            {
                Skip = reason;
            }
        }

        internal static bool TryResolveArtifact(out string artifactPath, out string reason)
        {
            var configuredPath = Environment.GetEnvironmentVariable(ArtifactPathVariableName);
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                artifactPath = Path.GetFullPath(configuredPath);
                if (File.Exists(artifactPath))
                {
                    reason = string.Empty;
                    return true;
                }

                reason = $"Configured native artifact was not found: {artifactPath}.";
                return false;
            }

            try
            {
                artifactPath = NnrpNativeArtifact.Resolve();
                reason = string.Empty;
                return true;
            }
            catch (NnrpNativeArtifactException error)
            {
                artifactPath = string.Empty;
                reason = error.Message;
                return false;
            }
        }
    }
}
