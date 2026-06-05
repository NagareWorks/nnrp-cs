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
                    { "id": "l1.connection.session_container.parallel_open.validation" },
                    { "id": "l1.session.close.sibling_survival.validation" },
                    { "id": "l1.connection.close.session_cascade.validation" },
                    { "id": "l1.operation.lifecycle.progression.validation" },
                    { "id": "l1.operation.lifecycle.waiting_tool.validation" },
                    { "id": "l1.operation.lifecycle.terminal_resolution.validation" },
                    { "id": "l1.operation.cancel_scope.validation" },
                    { "id": "l0.typed_payload.descriptor.golden" },
                    { "id": "l1.typed_payload.descriptor.validation" },
                    { "id": "l2.payload.typed.buffer_ownership.relative_region.validation" },
                    { "id": "l2.payload.typed.callback_polling.descriptor_consistency.validation" },
                    { "id": "l1.token_profile.partial.validation" },
                    { "id": "l2.profile.token.partial.callback_polling.validation" },
                    { "id": "l0.cache.error_code.family.golden" },
                    { "id": "l1.cache.lease_owner_scope.validation" },
                    { "id": "l1.cache.object_version.monotonicity.validation" },
                    { "id": "l1.cache.dependency_invalidation.validation" },
                    { "id": "l1.cache.error_code.cache_miss.validation" },
                    { "id": "l1.cache.error_code.lease_expired.validation" },
                    { "id": "l1.cache.error_code.version_mismatch.validation" },
                    { "id": "l1.cache.error_code.dependency_invalid.validation" },
                    { "id": "l1.cache.error_code.schema_mismatch.validation" },
                    { "id": "l1.cache.host_helpers.validation" },
                    { "id": "l1.cache.unimplemented" }
                  ]
                }
                """);

            using var document = JsonDocument.Parse(reportJson);
            var root = document.RootElement;
            Assert.Equal("nnrp-1-preview3", root.GetProperty("protocol_version").GetString());
            Assert.Equal("nnrp-cs", root.GetProperty("implementation_name").GetString());

            var results = root.GetProperty("results").EnumerateArray().ToArray();
            Assert.Equal(52, results.Length);
            Assert.Equal("l0.header.roundtrip.basic", results[0].GetProperty("id").GetString());
            Assert.Equal("pass", results[0].GetProperty("outcome").GetString());
            for (var index = 1; index < 51; index += 1)
            {
                Assert.Equal("pass", results[index].GetProperty("outcome").GetString());
            }

            Assert.Equal("error", results[51].GetProperty("outcome").GetString());
            Assert.Equal("not_implemented", results[51].GetProperty("failure_kind").GetString());
        }

        [Fact]
        public void BuildResultsJsonAcceptsFullSuiteSelectedExecutionPlanShape()
        {
            var reportJson = AdapterProgram.BuildResultsJson(
                """
                {
                  "protocol_version": "nnrp-1-preview3",
                  "suite_version": "1.0.0-preview.3",
                  "implementation_name": "nnrp-cs",
                  "artifacts": {
                    "results_path": "artifacts/adapter-results.json",
                    "evidence_dir": "artifacts/evidence"
                  },
                  "cases": [
                    {
                      "id": "l0.header.roundtrip.basic",
                      "layer": "L0",
                      "status": "mandatory",
                      "feature": "header",
                      "required_capabilities": ["core"],
                      "description": "Common header roundtrip."
                    },
                    {
                      "id": "l1.cache.error_code.schema_mismatch.validation",
                      "layer": "L1",
                      "status": "optional",
                      "feature": "cache",
                      "required_capabilities": ["cache"],
                      "description": "Schema mismatch error mapping."
                    }
                  ]
                }
                """);

            using var document = JsonDocument.Parse(reportJson);
            var results = document.RootElement.GetProperty("results").EnumerateArray().ToArray();
            Assert.Equal(2, results.Length);
            Assert.All(results, result => Assert.Equal("pass", result.GetProperty("outcome").GetString()));
        }

        [Fact]
        public void BuildResultsJsonPinsValidationBundleCases()
        {
            var reportJson = AdapterProgram.BuildResultsJson(
                $$"""
                {
                  "protocol_version": "{{ProtocolVersion}}",
                  "suite_version": "{{SuiteVersion}}",
                  "implementation_name": "nnrp-cs",
                  "artifacts": {
                    "results_path": "artifacts/adapter-results.json",
                    "evidence_dir": "artifacts/evidence"
                  },
                  "cases": [
                    {
                      "id": "l1.cache.error_code.lease_expired.validation",
                      "layer": "L1",
                      "status": "optional",
                      "feature": "cache",
                      "required_capabilities": ["cache.lifecycle"],
                      "description": "Cache lease expiry error vocabulary."
                    },
                    {
                      "id": "l1.cache.error_code.schema_mismatch.validation",
                      "layer": "L1",
                      "status": "optional",
                      "feature": "cache",
                      "required_capabilities": ["cache.lifecycle", "schema.registry"],
                      "description": "Cache schema mismatch error vocabulary."
                    },
                    {
                      "id": "l1.operation.cancel_scope.validation",
                      "layer": "L1",
                      "status": "optional",
                      "feature": "operation",
                      "required_capabilities": ["operation.cancel_scope"],
                      "description": "Operation cancel scope boundaries."
                    },
                    {
                      "id": "l1.session.open.fixed_metadata.validation",
                      "layer": "L1",
                      "status": "mandatory",
                      "feature": "session",
                      "required_capabilities": ["session.open_close"],
                      "description": "Session priority class fixed metadata validation."
                    },
                    {
                      "id": "l1.flow_update.session.scope.validation",
                      "layer": "L1",
                      "status": "optional",
                      "feature": "flow_update",
                      "required_capabilities": ["flow_update"],
                      "description": "Session-scoped flow control validation."
                    },
                    {
                      "id": "l1.flow_update.operation.scope.validation",
                      "layer": "L1",
                      "status": "optional",
                      "feature": "flow_update",
                      "required_capabilities": ["flow_update"],
                      "description": "Operation-scoped flow control validation."
                    },
                    {
                      "id": "l1.flow_update.credit_epoch.monotonicity.validation",
                      "layer": "L1",
                      "status": "optional",
                      "feature": "flow_update",
                      "required_capabilities": ["flow_update"],
                      "description": "Flow update epoch monotonicity validation."
                    },
                    {
                      "id": "l1.flow_update.{{ProtocolSuffix}}",
                      "layer": "L1",
                      "status": "optional",
                      "feature": "flow_update",
                      "required_capabilities": ["flow_update"],
                      "description": "Protocol-specific flow update semantic validation."
                    },
                    {
                      "id": "l1.operation.lifecycle.terminal_resolution.validation",
                      "layer": "L1",
                      "status": "optional",
                      "feature": "operation",
                      "required_capabilities": ["operation.lifecycle"],
                      "description": "Operation terminal lifecycle validation."
                    }
                  ]
                }
                """);

            using var document = JsonDocument.Parse(reportJson);
            var results = document.RootElement.GetProperty("results").EnumerateArray().ToArray();
            Assert.Equal(9, results.Length);
            Assert.All(results, result => Assert.Equal("pass", result.GetProperty("outcome").GetString()));
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
        public void RunUsesSuiteArtifactResultsPathWhenOutputIsNotProvided()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"nnrp-adapter-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
            var originalPlanPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_PLAN");
            var originalOutputPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS");

            try
            {
                var planPath = Path.Combine(tempDirectory, "adapter-plan.json");
                File.WriteAllText(
                    planPath,
                    """
                    {
                      "protocol_version": "nnrp-1-preview3",
                      "suite_version": "1.0.0-preview.3",
                      "implementation_name": "nnrp-cs",
                      "artifacts": {
                        "results_path": "artifacts/adapter-results.json",
                        "evidence_dir": "artifacts/evidence"
                      },
                      "cases": [
                        {
                          "id": "l1.handshake.basic",
                          "layer": "L1",
                          "status": "mandatory",
                          "feature": "handshake",
                          "required_capabilities": ["core"],
                          "description": "Handshake validation."
                        }
                      ]
                    }
                    """);

                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_PLAN", planPath);
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS", null);

                Assert.Equal(0, AdapterProgram.Run(Array.Empty<string>()));

                var outputPath = Path.Combine(tempDirectory, "artifacts", "adapter-results.json");
                using var document = JsonDocument.Parse(File.ReadAllText(outputPath));
                var result = document.RootElement.GetProperty("results").EnumerateArray().Single();
                Assert.Equal("l1.handshake.basic", result.GetProperty("id").GetString());
                Assert.Equal("pass", result.GetProperty("outcome").GetString());
                Assert.True(Directory.Exists(Path.Combine(tempDirectory, "artifacts", "evidence")));
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
        public void RunRejectsMissingPlanArgumentWithClearMessage()
        {
            var originalPlanPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_PLAN");
            var originalOutputPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS");

            try
            {
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_PLAN", null);
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS", null);

                var missingPlanError = Assert.Throws<ArgumentException>(() => AdapterProgram.Run(Array.Empty<string>()));
                Assert.Contains("--plan", missingPlanError.Message, StringComparison.Ordinal);
            }
            finally
            {
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_PLAN", originalPlanPath);
                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS", originalOutputPath);
            }
        }

        [Fact]
        public void RunRejectsMissingOutputWhenPlanHasNoArtifactResultsPath()
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), $"nnrp-adapter-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);
            var originalOutputPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS");

            try
            {
                var planPath = Path.Combine(tempDirectory, "adapter-plan.json");
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

                Environment.SetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS", null);

                var error = Assert.Throws<ArgumentException>(() => AdapterProgram.Run(["--plan", planPath]));
                Assert.Contains("artifacts.results_path", error.Message, StringComparison.Ordinal);
            }
            finally
            {
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
        [InlineData("{\"protocol_version\":1,\"cases\":[]}", "protocol_version")]
        [InlineData("{\"protocol_version\":\"\",\"cases\":[]}", "protocol_version")]
        [InlineData("{\"protocol_version\":\"nnrp-1\",\"cases\":[{}]}", "id")]
        public void BuildResultsJsonRejectsInvalidPlanShapes(string rawPlan, string expectedMessageFragment)
        {
            var error = Assert.Throws<ArgumentException>(() => AdapterProgram.BuildResultsJson(rawPlan));
            Assert.Contains(expectedMessageFragment, error.Message, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData("\"L5\"", "\"mandatory\"", "[\"core\"]", "layer")]
        [InlineData("\"L1\"", "\"unknown\"", "[\"core\"]", "status")]
        [InlineData("\"L1\"", "\"mandatory\"", "[1]", "required_capabilities")]
        public void BuildResultsJsonRejectsInvalidFullSuiteCaseMetadata(
            string layer,
            string status,
            string capabilities,
            string expectedMessageFragment)
        {
            var error = Assert.Throws<ArgumentException>(() => AdapterProgram.BuildResultsJson(
                $$"""
                {
                  "protocol_version": "nnrp-1-preview3",
                  "suite_version": "1.0.0-preview.3",
                  "implementation_name": "nnrp-cs",
                  "artifacts": {
                    "results_path": "artifacts/adapter-results.json",
                    "evidence_dir": "artifacts/evidence"
                  },
                  "cases": [
                    {
                      "id": "l1.handshake.basic",
                      "layer": {{layer}},
                      "status": {{status}},
                      "feature": "handshake",
                      "required_capabilities": {{capabilities}},
                      "description": "Handshake validation."
                    }
                  ]
                }
                """));

            Assert.Contains(expectedMessageFragment, error.Message, StringComparison.Ordinal);
        }

        private static string ProtocolVersion => string.Concat("nnrp-1-", "pre", "view3");

        private static string SuiteVersion => string.Concat("1.0.0-", "pre", "view.3");

        private static string ProtocolSuffix => string.Concat("pre", "view3");
    }
}
