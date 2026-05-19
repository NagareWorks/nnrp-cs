using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class ExternalLoopbackSmokeTests
    {
        private const string KeepLogsVariableName = "NNRP_KEEP_LOOPBACK_LOGS";
        private const string SkipStagePackageVariableName = "NNRP_SKIP_STAGE_PACKAGE";
        private const string TimeoutSecondsVariableName = "NNRP_QUIC_SMOKE_TIMEOUT_SECONDS";

        [ExternalLoopbackFact]
        public Task LoopbackSmokePassesAgainstConfiguredExternalApplication()
        {
            Assert.True(ExternalLoopbackFactAttribute.TryFindRepoRoot(out var repoRoot), "Could not locate the nnrp-cs repository root.");
            Assert.True(ExternalLoopbackFactAttribute.TryFindExternalAppRepoRoot(out var externalAppRepoRoot), "Could not locate the external application repository root.");

            var scriptPath = Path.Combine(repoRoot, "scripts", "run_external_loopback_smoke.ps1");
            var startInfo = new ProcessStartInfo("pwsh")
            {
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-ExecutionPolicy");
            startInfo.ArgumentList.Add("Bypass");
            startInfo.ArgumentList.Add("-File");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.ArgumentList.Add("-ExternalAppRepoRoot");
            startInfo.ArgumentList.Add(externalAppRepoRoot);
            startInfo.ArgumentList.Add("-UseAutoTransport");

            if (ExternalLoopbackFactAttribute.IsEnabled(Environment.GetEnvironmentVariable(SkipStagePackageVariableName)))
            {
                startInfo.ArgumentList.Add("-SkipStagePackage");
            }

            if (ExternalLoopbackFactAttribute.IsEnabled(Environment.GetEnvironmentVariable(KeepLogsVariableName)))
            {
                startInfo.ArgumentList.Add("-KeepLogs");
            }

            using var process = new Process { StartInfo = startInfo };
            Assert.True(process.Start(), "Failed to start the external loopback smoke process.");

            var timeout = GetTimeout();
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                TryKill(process);
                Assert.Fail($"External loopback smoke timed out after {timeout.TotalSeconds:F0} seconds.");
            }

            Assert.True(
                process.ExitCode == 0,
                BuildFailureMessage(process.ExitCode, scriptPath));
            return Task.CompletedTask;
        }

        private static TimeSpan GetTimeout()
        {
            var rawTimeout = Environment.GetEnvironmentVariable(TimeoutSecondsVariableName);
            if (int.TryParse(rawTimeout, out var seconds) && seconds > 0)
            {
                return TimeSpan.FromSeconds(seconds);
            }

            return TimeSpan.FromMinutes(3);
        }

        private static void TryKill(Process process)
        {
            if (process.HasExited)
            {
                return;
            }

            process.Kill(entireProcessTree: true);
        }

        private static string BuildFailureMessage(int exitCode, string scriptPath)
        {
            return $"External loopback smoke exited with code {exitCode}. Re-run {scriptPath} manually for detailed logs.";
        }
    }
}
