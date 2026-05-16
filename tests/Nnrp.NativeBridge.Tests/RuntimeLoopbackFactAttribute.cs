using System;
using System.IO;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    internal sealed class RuntimeLoopbackFactAttribute : FactAttribute
    {
        internal const string EnableVariableName = "NNRP_RUN_QUIC_RUNTIME_SMOKE";
        internal const string RuntimeRepoRootVariableName = "NNRP_RUNTIME_REPO_ROOT";

        public RuntimeLoopbackFactAttribute()
        {
            if (!IsEnabled(Environment.GetEnvironmentVariable(EnableVariableName)))
            {
                Skip = $"Set {EnableVariableName}=1 to run the QUIC runtime loopback smoke test.";
                return;
            }

            if (!TryFindRepoRoot(out var repoRoot))
            {
                Skip = "Could not locate the nnrp-cs repository root for the QUIC runtime loopback smoke test.";
                return;
            }

            if (!TryFindRuntimeRepoRoot(repoRoot, out var runtimeRepoRoot))
            {
                Skip = $"Could not locate the neural-render-runtime checkout. Set {RuntimeRepoRootVariableName} to its repository root.";
                return;
            }

            if (!File.Exists(Path.Combine(repoRoot, "scripts", "run_runtime_loopback_smoke.ps1")))
            {
                Skip = "run_runtime_loopback_smoke.ps1 is not available in this checkout.";
                return;
            }

            if (!File.Exists(Path.Combine(runtimeRepoRoot, "scripts", "run_local_dev.ps1")))
            {
                Skip = $"run_local_dev.ps1 was not found under {runtimeRepoRoot}.";
            }
        }

        internal static bool TryFindRepoRoot(out string repoRoot)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Nnrp.sln")))
                {
                    repoRoot = directory.FullName;
                    return true;
                }

                directory = directory.Parent;
            }

            repoRoot = string.Empty;
            return false;
        }

        internal static bool TryFindRuntimeRepoRoot(string repoRoot, out string runtimeRepoRoot)
        {
            var configuredRoot = Environment.GetEnvironmentVariable(RuntimeRepoRootVariableName);
            if (!string.IsNullOrWhiteSpace(configuredRoot) && Directory.Exists(configuredRoot))
            {
                runtimeRepoRoot = Path.GetFullPath(configuredRoot);
                return true;
            }

            var siblingRoot = Path.GetFullPath(Path.Combine(repoRoot, "..", "neural-render-runtime"));
            if (Directory.Exists(siblingRoot))
            {
                runtimeRepoRoot = siblingRoot;
                return true;
            }

            runtimeRepoRoot = string.Empty;
            return false;
        }

        internal static bool IsEnabled(string? value)
        {
            return string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
