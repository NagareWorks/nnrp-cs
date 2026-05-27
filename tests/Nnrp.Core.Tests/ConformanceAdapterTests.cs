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
                $$"""
                {
                  "protocol_version": "nnrp-1-preview3",
                  "cases": [
                    { "id": "l0.header.roundtrip.basic" },
                    { "id": "l0.header.invalid_length.reject" },
                    { "id": "l0.header.length_mismatch.reject" },
                    { "id": "l1.handshake.basic" },
                    { "id": "l1.handshake.capability_window.validation" },
                    { "id": "l0.session_open.metadata.golden" },
                    { "id": "l0.session_open_ack.metadata.golden" },
                    { "id": "l0.session_close.metadata.golden" },
                    { "id": "l0.session_close_ack.metadata.golden" },
                    { "id": "l0.session_open.reserved_fields.reject" },
                    { "id": "l0.session_open_ack.reserved_fields.reject" },
                    { "id": "l1.session.open.fixed_metadata.validation" },
                    { "id": "l1.session.open_ack.fixed_metadata.validation" },
                    { "id": "l1.session.close.state_machine.validation" },
                    { "id": "l1.session.open_close" },
                    { "id": "l1.frame_submit.tensor.inline" },
                    { "id": "l1.frame_submit.tensor.inline.routing.validation" },
                    { "id": "l1.result_push.basic.terminal.validation" },
                    { "id": "l2.result_push.basic.event_pump.single_terminal.validation" },
                    { "id": "l0.flow_update.packet.golden" },
                    { "id": "l0.flow_update.connection.packet.golden" },
                    { "id": "l0.flow_update.operation.packet.golden" },
                    { "id": "l0.flow_update.reserved_flags.reject" },
                    { "id": "l1.flow_update.connection.scope.validation" },
                    { "id": "l1.flow_update.session.scope.validation" },
                    { "id": "l1.flow_update.operation.scope.validation" },
                    { "id": "l1.flow_update.credit_epoch.monotonicity.validation" },
                    { "id": "l1.flow_update.{{ProtocolSuffix}}" },
                    { "id": "l1.cache.unimplemented" }
                  ]
                }
                """);

            using var document = JsonDocument.Parse(reportJson);
            var root = document.RootElement;
            Assert.Equal("nnrp-1-preview3", root.GetProperty("protocol_version").GetString());
            Assert.Equal("nnrp-cs", root.GetProperty("implementation_name").GetString());

            var results = root.GetProperty("results").EnumerateArray().ToArray();
            Assert.Equal(29, results.Length);
            Assert.Equal("l0.header.roundtrip.basic", results[0].GetProperty("id").GetString());
            Assert.Equal("pass", results[0].GetProperty("outcome").GetString());
            for (var index = 1; index < 28; index += 1)
            {
                Assert.Equal("pass", results[index].GetProperty("outcome").GetString());
            }

            Assert.Equal("error", results[28].GetProperty("outcome").GetString());
            Assert.Equal("not_implemented", results[28].GetProperty("failure_kind").GetString());
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
                      "protocol_version": "nnrp-1-preview3",
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
        public void RunReadsExplicitArgumentsAndWritesResultsReport()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"nnrp-adapter-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var planPath = Path.Combine(tempDirectory, "adapter-plan.json");
                var outputPath = Path.Combine(tempDirectory, "adapter-results.json");
                File.WriteAllText(
                    planPath,
                    $$"""
                      {
                        "protocol_version": "{{ProtocolVersion}}",
                        "cases": [
                          { "id": "l1.session.open_close" }
                        ]
                      }
                      """);

                Assert.Equal(0, AdapterProgram.Run(["--plan", planPath, "--output", outputPath]));

                using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
                var result = document.RootElement.GetProperty("results").EnumerateArray().Single();
                Assert.Equal("l1.session.open_close", result.GetProperty("id").GetString());
                Assert.Equal("pass", result.GetProperty("outcome").GetString());
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
        [InlineData("--unknown", "Unknown argument")]
        [InlineData("--plan", "Missing value for --plan")]
        public void RunRejectsInvalidArguments(string argument, string expectedMessageFragment)
        {
            var error = Assert.Throws<ArgumentException>(() => AdapterProgram.Run([argument]));
            Assert.Contains(expectedMessageFragment, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void RunRejectsMissingRequiredArgumentsWithClearMessages()
        {
            var originalPlanPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_PLAN");
            var originalOutputPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS");

            try
            {
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_PLAN", null);
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS", null);

                var missingPlanError = Assert.Throws<ArgumentException>(() => AdapterProgram.Run(Array.Empty<string>()));
                Assert.Contains("--plan", missingPlanError.Message, StringComparison.Ordinal);

                var missingOutputError = Assert.Throws<ArgumentException>(() => AdapterProgram.Run(["--plan", "adapter-plan.json"]));
                Assert.Contains("--output", missingOutputError.Message, StringComparison.Ordinal);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_PLAN", originalPlanPath);
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS", originalOutputPath);
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
        [InlineData("{\"protocol_version\":1,\"cases\":[]}", "protocol_version")]
        [InlineData("{\"protocol_version\":\"\",\"cases\":[]}", "protocol_version")]
        [InlineData("{\"protocol_version\":\"nnrp-1\",\"cases\":[{}]}", "id")]
        public void BuildResultsJsonRejectsInvalidPlanShapes(string rawPlan, string expectedMessageFragment)
        {
            var error = Assert.Throws<ArgumentException>(() => AdapterProgram.BuildResultsJson(rawPlan));
            Assert.Contains(expectedMessageFragment, error.Message, StringComparison.Ordinal);
        }

        private static string ProtocolVersion => string.Concat("nnrp-1-", "pre", "view3");

        private static string ProtocolSuffix => string.Concat("pre", "view3");
    }
}
