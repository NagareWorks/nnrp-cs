using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using BenchmarkProgram = Nnrp.BenchmarkAdapter.Program;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class BenchmarkAdapterTests
    {
        [Fact]
        public void BuildResultsJsonMeasuresHeaderScenarioAndSkipsUnimplementedScenarios()
        {
            var reportJson = BenchmarkProgram.BuildResultsJson(SamplePlanJson);

            using var document = JsonDocument.Parse(reportJson);
            var root = document.RootElement;
            Assert.Equal("nnrp-1-preview3", root.GetProperty("protocol_version").GetString());
            Assert.Equal("nnrp-cs", root.GetProperty("implementation_name").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("environment").GetProperty("os").GetString()));

            var results = root.GetProperty("results").EnumerateArray().ToArray();
            Assert.Equal(2, results.Length);

            var headerResult = results.Single(result => result.GetProperty("id").GetString() == "l4.header.encode_decode.latency");
            Assert.Equal("measured", headerResult.GetProperty("outcome").GetString());
            Assert.True(headerResult.GetProperty("metrics").GetProperty("p50_us").GetDouble() >= 0);
            Assert.True(headerResult.GetProperty("metrics").GetProperty("p95_us").GetDouble() >= 0);
            Assert.True(headerResult.GetProperty("metrics").GetProperty("p99_us").GetDouble() >= 0);

            var submitResult = results.Single(result => result.GetProperty("id").GetString() == "l4.submit_result.inline_tensor.throughput");
            Assert.Equal("skip", submitResult.GetProperty("outcome").GetString());
            Assert.Contains("not implemented", submitResult.GetProperty("message").GetString(), StringComparison.Ordinal);
        }

        [Fact]
        public void RunReadsPathsFromEnvironmentAndWritesResultsReport()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"nnrp-benchmark-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
            var originalPlanPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_BENCHMARK_PLAN");
            var originalOutputPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_BENCHMARK_RESULTS");

            try
            {
                var planPath = Path.Combine(tempDirectory, "benchmark-plan.json");
                var outputPath = Path.Combine(tempDirectory, "artifacts", "benchmark-results.json");
                File.WriteAllText(planPath, SamplePlanJson);

                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_BENCHMARK_PLAN", planPath);
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_BENCHMARK_RESULTS", outputPath);

                Assert.Equal(0, BenchmarkProgram.Run(Array.Empty<string>()));

                using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
                Assert.Equal("nnrp-1-preview3", document.RootElement.GetProperty("protocol_version").GetString());
                Assert.Equal(2, document.RootElement.GetProperty("results").GetArrayLength());
            }
            finally
            {
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_BENCHMARK_PLAN", originalPlanPath);
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_BENCHMARK_RESULTS", originalOutputPath);
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
        }

        [Fact]
        public void RunRejectsMissingPlanPathWithClearMessage()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"nnrp-benchmark-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var missingPlanPath = Path.Combine(tempDirectory, "missing-plan.json");
                var outputPath = Path.Combine(tempDirectory, "artifacts", "benchmark-results.json");

                var error = Assert.Throws<ArgumentException>(() =>
                    BenchmarkProgram.Run(["--plan", missingPlanPath, "--output", outputPath]));

                Assert.Contains("Plan file does not exist", error.Message, StringComparison.Ordinal);
                Assert.Contains(missingPlanPath, error.Message, StringComparison.Ordinal);
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
        }

        [Theory]
        [InlineData("[]", "JSON object")]
        [InlineData("{\"protocol_version\":\"nnrp-1-preview3\"}", "must be an array")]
        [InlineData("{\"protocol_version\":\"nnrp-1-preview3\",\"scenarios\":[\"bad\"]}", "scenarios must be JSON objects")]
        public void BuildResultsJsonRejectsInvalidPlanShapes(string rawPlan, string expectedMessageFragment)
        {
            var error = Assert.Throws<ArgumentException>(() => BenchmarkProgram.BuildResultsJson(rawPlan));
            Assert.Contains(expectedMessageFragment, error.Message, StringComparison.Ordinal);
        }

        private const string SamplePlanJson = """
            {
              "protocol_version": "nnrp-1-preview3",
              "implementation_name": "nnrp-cs",
              "scenarios": [
                {
                  "id": "l4.header.encode_decode.latency",
                  "workload": {
                    "operation": "header_encode_decode",
                    "payload": "l0_header",
                    "iterations": 3,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.submit_result.inline_tensor.throughput",
                  "workload": {
                    "operation": "submit_result_loop",
                    "payload": "inline_tensor_4k",
                    "duration_seconds": 1
                  }
                }
              ]
            }
            """;
    }
}
