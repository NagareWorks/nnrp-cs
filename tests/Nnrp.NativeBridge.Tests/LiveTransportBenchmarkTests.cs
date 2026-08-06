using System.Linq;
using System.Text.Json;
using BenchmarkProgram = Nnrp.BenchmarkAdapter.Program;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class LiveTransportBenchmarkTests
    {
        [LiveNativeArtifactFact]
        public void EveryTransportBenchmarkUsesARealProviderRoundTrip()
        {
            using var report = JsonDocument.Parse(BenchmarkProgram.BuildResultsJson(Plan));
            var results = report.RootElement.GetProperty("results").EnumerateArray().ToArray();
            Assert.Equal(4, results.Length);
            foreach (var result in results)
            {
                Assert.Equal("measured", result.GetProperty("outcome").GetString());
                var metrics = result.GetProperty("metrics");
                Assert.True(metrics.GetProperty("throughput_ops_per_sec").GetDouble() > 0);
                Assert.True(metrics.GetProperty("p50_us").GetDouble() >= 0);
                Assert.True(metrics.GetProperty("p95_us").GetDouble() >= 0);
                Assert.True(metrics.GetProperty("p99_us").GetDouble() >= 0);
                Assert.True(metrics.GetProperty("gc_alloc_bytes_per_op").GetDouble() >= 0);
                var expectedPayloadBytes = result.GetProperty("id").GetString() == "tcp" ? 65_536 : 128;
                Assert.Equal(expectedPayloadBytes, metrics.GetProperty("payload_bytes").GetInt32());
            }
        }

        private const string Plan = """
            {
              "protocol_version": "nnrp-1",
              "implementation_name": "nnrp-cs",
              "scenarios": [
                {
                  "id": "tcp",
                  "workload": {
                    "operation": "transport_loopback",
                    "transport": "tcp",
                    "payload_bytes": 65536,
                    "duration_seconds": 0.01,
                    "warmup_iterations": 1,
                    "allocation_iterations": 1
                  }
                },
                {
                  "id": "quic",
                  "workload": {
                    "operation": "transport_loopback",
                    "transport": "quic",
                    "payload_bytes": 128,
                    "duration_seconds": 0.01,
                    "warmup_iterations": 1,
                    "allocation_iterations": 1
                  }
                },
                {
                  "id": "ipc",
                  "workload": {
                    "operation": "transport_loopback",
                    "transport": "ipc",
                    "payload_bytes": 128,
                    "duration_seconds": 0.01,
                    "warmup_iterations": 1,
                    "allocation_iterations": 1
                  }
                },
                {
                  "id": "websocket",
                  "workload": {
                    "operation": "transport_loopback",
                    "transport": "websocket",
                    "payload_bytes": 128,
                    "duration_seconds": 0.01,
                    "warmup_iterations": 1,
                    "allocation_iterations": 1
                  }
                }
              ]
            }
            """;
    }
}
