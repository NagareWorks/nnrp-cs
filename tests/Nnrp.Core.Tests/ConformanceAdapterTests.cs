using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AdapterProgram = Nnrp.ConformanceAdapter.Program;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class ConformanceAdapterTests
    {
        [Fact]
        public void BuildResultsJsonExecutesSupportedCases()
        {
            var reportJson = AdapterProgram.BuildResultsJson(
                """
                {
                  "protocol_version": "nnrp-1",
                  "cases": [
                    { "id": "l1.handshake.basic" },
                    { "id": "l1.session.open_close" },
                    { "id": "l1.cache.unimplemented" }
                  ]
                }
                """);

            using var document = JsonDocument.Parse(reportJson);
            var root = document.RootElement;
            Assert.Equal("nnrp-1", root.GetProperty("protocol_version").GetString());
            Assert.Equal("nnrp-cs", root.GetProperty("implementation_name").GetString());

            var results = root.GetProperty("results").EnumerateArray().ToArray();
            Assert.Equal(3, results.Length);
            Assert.Equal("l1.handshake.basic", results[0].GetProperty("id").GetString());
            Assert.Equal("pass", results[0].GetProperty("outcome").GetString());
            Assert.Equal("pass", results[1].GetProperty("outcome").GetString());
            Assert.Equal("skip", results[2].GetProperty("outcome").GetString());
        }

        [Fact]
        public void RunReadsPathsFromEnvironmentAndWritesResultsReport()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"nnrp-adapter-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
            var originalPlanPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_PLAN");
            var originalOutputPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS");

            try
            {
                var planPath = Path.Combine(tempDirectory, "adapter-plan.json");
                var outputPath = Path.Combine(tempDirectory, "artifacts", "adapter-results.json");
                File.WriteAllText(
                    planPath,
                    """
                    {
                      "protocol_version": "nnrp-1",
                      "cases": [
                        { "id": "l1.handshake.basic" }
                      ]
                    }
                    """);

                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_PLAN", planPath);
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS", outputPath);

                Assert.Equal(0, AdapterProgram.Run(Array.Empty<string>()));

                using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
                var result = document.RootElement.GetProperty("results").EnumerateArray().Single();
                Assert.Equal("l1.handshake.basic", result.GetProperty("id").GetString());
                Assert.Equal("pass", result.GetProperty("outcome").GetString());
            }
            finally
            {
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_PLAN", originalPlanPath);
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS", originalOutputPath);
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
        }

        [Fact]
        public void RunRejectsMissingPlanPathWithClearMessage()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"nnrp-adapter-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var missingPlanPath = Path.Combine(tempDirectory, "missing-plan.json");
                var outputPath = Path.Combine(tempDirectory, "artifacts", "adapter-results.json");

                var error = Assert.Throws<ArgumentException>(() =>
                    AdapterProgram.Run(["--plan", missingPlanPath, "--output", outputPath]));

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
        [InlineData("{\"protocol_version\":\"nnrp-1\"}", "must be an array")]
        [InlineData("{\"protocol_version\":\"nnrp-1\",\"cases\":[\"bad\"]}", "cases must be JSON objects")]
        public void BuildResultsJsonRejectsInvalidPlanShapes(string rawPlan, string expectedMessageFragment)
        {
            var error = Assert.Throws<ArgumentException>(() => AdapterProgram.BuildResultsJson(rawPlan));
            Assert.Contains(expectedMessageFragment, error.Message, StringComparison.Ordinal);
        }
    }
}
