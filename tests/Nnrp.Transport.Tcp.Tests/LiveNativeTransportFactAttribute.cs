using System;
using Xunit;

namespace Nnrp.Transport.Tcp.Tests
{
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class LiveNativeTransportFactAttribute : FactAttribute
    {
        internal const string EnableVariableName = "NNRP_RUN_LIVE_NATIVE_TRANSPORT_TESTS";
        internal const string ArtifactPathVariableName = "NNRP_NATIVE_TCP_ARTIFACT_PATH";

        public LiveNativeTransportFactAttribute()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(EnableVariableName),
                    "1",
                    StringComparison.Ordinal))
            {
                Skip = $"Set {EnableVariableName}=1 to run native TCP provider tests.";
            }
        }
    }
}
