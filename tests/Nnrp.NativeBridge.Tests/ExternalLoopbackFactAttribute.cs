using System;
using System.IO;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    internal sealed class ExternalLoopbackFactAttribute : FactAttribute
    {
        internal const string EnableVariableName = "NNRP_RUN_EXTERNAL_LOOPBACK_SMOKE";
        internal const string ExternalAppRepoRootVariableName = "NNRP_EXTERNAL_APP_REPO_ROOT";

        public ExternalLoopbackFactAttribute()
        {
            if (!IsEnabled(Environment.GetEnvironmentVariable(EnableVariableName)))
            {
                Skip = $"Set {EnableVariableName}=1 to run the opt-in external loopback smoke test.";
                return;
            }

            if (!TryFindRepoRoot(out var repoRoot))
            {
                Skip = "Could not locate the nnrp-cs repository root for the external loopback smoke test.";
                return;
            }

            if (!TryFindExternalAppRepoRoot(out var externalAppRepoRoot))
            {
                Skip = $"Could not locate the external application checkout. Set {ExternalAppRepoRootVariableName} to its repository root.";
                return;
            }

            if (!File.Exists(Path.Combine(repoRoot, "scripts", "run_external_loopback_smoke.ps1")))
            {
                Skip = "run_external_loopback_smoke.ps1 is not available in this checkout.";
                return;
            }

            if (!File.Exists(Path.Combine(externalAppRepoRoot, "scripts", "run_local_dev.ps1")))
            {
                Skip = $"run_local_dev.ps1 was not found under {externalAppRepoRoot}.";
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

        internal static bool TryFindExternalAppRepoRoot(out string externalAppRepoRoot)
        {
            var configuredRoot = Environment.GetEnvironmentVariable(ExternalAppRepoRootVariableName);
            if (!string.IsNullOrWhiteSpace(configuredRoot) && Directory.Exists(configuredRoot))
            {
                externalAppRepoRoot = Path.GetFullPath(configuredRoot);
                return true;
            }

            externalAppRepoRoot = string.Empty;
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
