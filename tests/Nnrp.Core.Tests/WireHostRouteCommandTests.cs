using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.WireConformance;
using Xunit;

namespace Nnrp.Core.Tests;

public sealed class WireHostRouteCommandTests
{
    [Fact]
    public void ParseOwnsTheFrozenHostRouteCliContract()
    {
        using TemporaryDirectory temporary = new();
        string[] args = Arguments(temporary.Path);

        WireHostRouteCommandOptions options = WireHostRouteCommand.Parse(args);

        Assert.Equal(Path.Combine(temporary.Path, "scenario.json"), options.ScenarioPath);
        Assert.Equal(Path.Combine(temporary.Path, "resolved.json"), options.ResolvedScenarioPath);
        Assert.Equal(Path.Combine(temporary.Path, "result.json"), options.OutputPath);
        Assert.Equal(Path.Combine(temporary.Path, "ready.json"), options.ReadyOutputPath);
        Assert.Equal(Path.Combine(temporary.Path, "artifacts"), options.ArtifactsPath);
        Assert.Equal("0.1.0", options.SuiteVersion);
        Assert.Equal("nnrp-cs", options.TargetName);

        Assert.Throws<ArgumentException>(() => WireHostRouteCommand.Parse([]));
        Assert.Throws<ArgumentException>(() => WireHostRouteCommand.Parse(["--scenario"]));
        Assert.Throws<ArgumentException>(() => WireHostRouteCommand.Parse(
            [.. args, "--other", "value"]));
        Assert.Throws<ArgumentException>(() => WireHostRouteCommand.Parse(
            [.. args, "--scenario", "duplicate.json"]));
    }

    [Fact]
    public async Task CommandReportsDriverFailuresAsOneSchemaShapedCase()
    {
        using TemporaryDirectory temporary = new();
        string scenarioPath = Path.Combine(temporary.Path, "scenario.json");
        string resolvedPath = Path.Combine(temporary.Path, "resolved.json");
        string scenario = """
            {
              "id": "wire.host-route.invalid-role",
              "host_route": {
                "role": "invalid",
                "platform": "native",
                "application_endpoint": "nnrp://host-route.test",
                "routes": [{
                  "transport": "tcp",
                  "provider_id": "nnrp.transport.tcp.native",
                  "locator": "suite://allocate/tcp/test",
                  "security": {"mode": "plain", "credential_owner": "none"}
                }]
              }
            }
            """;
        await File.WriteAllTextAsync(scenarioPath, scenario);
        await File.WriteAllTextAsync(resolvedPath, scenario.Replace(
            "suite://allocate/tcp/test",
            "tcp://127.0.0.1:1",
            StringComparison.Ordinal));

        StringWriter error = new();
        int exitCode = await WireHostRouteCommand.RunAsync(
            Arguments(temporary.Path),
            error,
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        using JsonDocument report = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(temporary.Path, "result.json")));
        JsonElement root = report.RootElement;
        Assert.Equal(WireHostRouteCommand.ProtocolVersion, root.GetProperty("protocol_version").GetString());
        Assert.Equal("0.1.0", root.GetProperty("suite_version").GetString());
        Assert.Equal("nnrp-cs", root.GetProperty("target_name").GetString());
        JsonElement result = Assert.Single(root.GetProperty("results").EnumerateArray());
        Assert.Equal("wire.host-route.invalid-role", result.GetProperty("id").GetString());
        Assert.Equal("failed", result.GetProperty("outcome").GetString());
        Assert.Equal("error", result.GetProperty("terminal").GetString());
        Assert.False(result.TryGetProperty("route_evidence", out _));
        Assert.Contains("Unsupported host-route role", result.GetProperty("message").GetString());
        Assert.Contains("Unsupported host-route role", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TopLevelCommandRecognizesTheRunnerInvocationWithoutASubcommand()
    {
        using TemporaryDirectory temporary = new();
        StringWriter error = new();

        int result = await WireConformanceCommand.RunAsync(
            Arguments(temporary.Path),
            TextWriter.Null,
            error,
            CancellationToken.None);

        Assert.Equal(2, result);
        Assert.Contains("scenario.json", error.ToString(), StringComparison.Ordinal);
    }

    private static string[] Arguments(string root) =>
    [
        "--scenario", Path.Combine(root, "scenario.json"),
        "--resolved-scenario", Path.Combine(root, "resolved.json"),
        "--output", Path.Combine(root, "result.json"),
        "--ready-output", Path.Combine(root, "ready.json"),
        "--artifacts", Path.Combine(root, "artifacts"),
        "--suite-version", "0.1.0",
        "--target-name", "nnrp-cs",
    ];

    private sealed class TemporaryDirectory : IDisposable
    {
        internal TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"nnrp-cs-host-route-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        internal string Path { get; }

        public void Dispose() => Directory.Delete(Path, true);
    }
}
