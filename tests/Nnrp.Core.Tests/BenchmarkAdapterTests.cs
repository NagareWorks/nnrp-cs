using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Nnrp.BenchmarkAdapter;
using Nnrp.Core;
using Nnrp.Runtime;
using BenchmarkProgram = Nnrp.BenchmarkAdapter.Program;
using Xunit;

namespace Nnrp.Core.Tests
{
    public sealed class BenchmarkAdapterTests
    {
        [Fact]
        public void LatencySampleWindowKeepsTheMostRecentBoundedSampleSet()
        {
            var samples = new LatencySampleWindow(3);

            samples.Add(1);
            samples.Add(2);
            samples.Add(3);
            samples.Add(4);

            Assert.Equal(3, samples.Count);
            Assert.Equal((3.0, 4.0, 4.0), samples.Percentiles());

            samples.Add(5);

            Assert.Equal((4.0, 5.0, 5.0), samples.Percentiles());
        }

        [Fact]
        public void TransportWorkerTimeoutIncludesBoundedIterationHeadroom()
        {
            Assert.Equal(
                180_000,
                TransportLoopbackBenchmark.CalculateWorkerTimeoutMilliseconds(
                    durationSeconds: 10,
                    warmupIterations: 1_000,
                    allocationIterations: 100));
            Assert.Equal(
                370_000,
                TransportLoopbackBenchmark.CalculateWorkerTimeoutMilliseconds(
                    durationSeconds: 10,
                    warmupIterations: int.MaxValue,
                    allocationIterations: int.MaxValue));
        }

        [Fact]
        public void RuntimeControlBenchmarkRejectsNonBodyTails()
        {
            var body = new byte[] { 1, 2, 3 };
            var trace = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.TraceContext),
                NnrpRuntimeControl.Encode(
                    MessageType.TraceContext,
                    new TraceContextMetadata(1, 2, 0, 1, 0, 3),
                    body));
            var diagnostic = NnrpRuntimeEvent.Decode(
                new RuntimeFrameHeader(MessageType.ResultDropReason),
                NnrpRuntimeControl.Encode(
                    MessageType.ResultDropReason,
                    new ResultDropReasonMetadata(
                        1,
                        2,
                        NnrpResultDropReasonCode.Backpressure,
                        RuntimeRole.Server,
                        0,
                        3),
                    body));

            Assert.Equal(body, TransportLoopbackBenchmark.RequireBodyTail(trace).ToArray());
            var error = Assert.Throws<InvalidOperationException>(() =>
                TransportLoopbackBenchmark.RequireBodyTail(diagnostic));
            Assert.Equal(
                "Transport benchmark runtime-control event must carry a body tail.",
                error.Message);
        }

        [Theory]
        [InlineData("-1", "1", "1", "warmupIterations")]
        [InlineData("0", "0", "1", "durationSeconds")]
        [InlineData("0", "NaN", "1", "durationSeconds")]
        [InlineData("0", "1", "0", "allocationIterations")]
        public void TransportWorkerRejectsInvalidWorkloadArgumentsBeforeReadingPayload(
            string warmupIterations,
            string durationSeconds,
            string allocationIterations,
            string expectedParameter)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
                TransportLoopbackBenchmark.RunWorker(
                [
                    "Tcp",
                    "missing-artifact",
                    "missing-payload",
                    warmupIterations,
                    durationSeconds,
                    allocationIterations,
                    "SubmitResult",
                    "unused-output",
                    "nnrp-transport-worker-v2",
                ]));

            Assert.Equal(expectedParameter, error.ParamName);
        }

        [Fact]
        public void BuildResultsJsonMeasuresConfiguredScenarios()
        {
            var reportJson = BenchmarkProgram.BuildResultsJson(SamplePlanJson);

            using var document = JsonDocument.Parse(reportJson);
            var root = document.RootElement;
            Assert.Equal("nnrp-1", root.GetProperty("protocol_version").GetString());
            Assert.Equal("nnrp-cs", root.GetProperty("implementation_name").GetString());
            Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("environment").GetProperty("os").GetString()));

            var results = root.GetProperty("results").EnumerateArray().ToArray();
            Assert.Equal(17, results.Length);

            var headerResult = results.Single(result => result.GetProperty("id").GetString() == "l4.header.encode_decode.latency");
            Assert.Equal("measured", headerResult.GetProperty("outcome").GetString());
            Assert.True(headerResult.GetProperty("metrics").GetProperty("p50_us").GetDouble() >= 0);
            Assert.True(headerResult.GetProperty("metrics").GetProperty("p95_us").GetDouble() >= 0);
            Assert.True(headerResult.GetProperty("metrics").GetProperty("p99_us").GetDouble() >= 0);

            var submitResult = results.Single(result => result.GetProperty("id").GetString() == "l4.submit_result.inline_tensor.throughput");
            Assert.Equal("skip", submitResult.GetProperty("outcome").GetString());
            Assert.Contains("Native benchmark artifact", submitResult.GetProperty("message").GetString(), StringComparison.Ordinal);

            AssertMeasured(results, "l4.metadata.session_open_ack.latency");
            AssertMeasuredWithAllocations(results, "l4.runtime_control.encode_decode.latency");
            AssertMeasuredWithAllocations(results, "l4.runtime_object.encode_decode.latency");
            AssertMeasuredWithAllocations(results, "l4.cache_reference.encode_decode.latency");
            AssertMeasured(results, "l4.metadata.submit_result.latency");
            AssertMeasured(results, "l4.typed_payload.tensor_pack_unpack.latency");
            AssertNativeSkipped(results, "l4.native.payload_snapshot_copy.latency");
            AssertNativeSkipped(results, "l4.native.borrowed_buffer_view.latency");
            AssertNativeSkipped(results, "l4.native.runtime_control.roundtrip.latency");
            AssertNativeSkipped(results, "l4.runtime.probe.latency");
            AssertNativeSkipped(results, "l4.session.lifecycle.latency");
            AssertNativeSkipped(results, "l4.transport.tcp.loopback.throughput");
            AssertNativeSkipped(results, "l4.transport.quic.loopback.throughput");
            AssertNativeSkipped(results, "l4.transport.ipc.loopback.throughput");
            AssertNativeSkipped(results, "l4.transport.websocket.loopback.throughput");
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
                Assert.Equal("nnrp-1", document.RootElement.GetProperty("protocol_version").GetString());
                Assert.Equal(17, document.RootElement.GetProperty("results").GetArrayLength());
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

        private static void AssertMeasured(JsonElement[] results, string id)
        {
            var result = results.Single(entry => entry.GetProperty("id").GetString() == id);
            Assert.Equal("measured", result.GetProperty("outcome").GetString());
        }

        private static void AssertMeasuredWithAllocations(JsonElement[] results, string id)
        {
            var result = results.Single(entry => entry.GetProperty("id").GetString() == id);
            Assert.Equal("measured", result.GetProperty("outcome").GetString());
            Assert.True(result.GetProperty("metrics").GetProperty("gc_alloc_bytes_per_op").GetDouble() >= 0);
        }

        private static void AssertNativeSkipped(JsonElement[] results, string id)
        {
            var result = results.Single(entry => entry.GetProperty("id").GetString() == id);
            Assert.Equal("skip", result.GetProperty("outcome").GetString());
            Assert.Contains("Native benchmark artifact", result.GetProperty("message").GetString(), StringComparison.Ordinal);
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
        [InlineData("{\"protocol_version\":\"nnrp-1\"}", "must be an array")]
        [InlineData("{\"protocol_version\":\"nnrp-1\",\"scenarios\":[\"bad\"]}", "scenarios must be JSON objects")]
        public void BuildResultsJsonRejectsInvalidPlanShapes(string rawPlan, string expectedMessageFragment)
        {
            var error = Assert.Throws<ArgumentException>(() => BenchmarkProgram.BuildResultsJson(rawPlan));
            Assert.Contains(expectedMessageFragment, error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildResultsJsonReportsUnknownTransportValue()
        {
            const string plan = """
                {
                  "protocol_version": "nnrp-1",
                  "implementation_name": "nnrp-cs",
                  "scenarios": [
                    {
                      "id": "invalid-transport",
                      "workload": {
                        "operation": "transport_loopback",
                        "transport": "sctp"
                      }
                    }
                  ]
                }
                """;

            var error = Assert.Throws<ArgumentException>(() => BenchmarkProgram.BuildResultsJson(plan));

            Assert.Contains("sctp", error.Message, StringComparison.Ordinal);
            Assert.Contains("tcp, quic, ipc, or websocket", error.Message, StringComparison.Ordinal);
        }

        private const string SamplePlanJson = """
            {
              "protocol_version": "nnrp-1",
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
                    "duration_seconds": 0.01,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.metadata.session_open_ack.latency",
                  "workload": {
                    "operation": "metadata_encode_decode",
                    "payload": "session_open_ack",
                    "iterations": 3,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.runtime_control.encode_decode.latency",
                  "workload": {
                    "operation": "runtime_control_encode_decode",
                    "payload": "cancel_with_diagnostic",
                    "iterations": 3,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.runtime_object.encode_decode.latency",
                  "workload": {
                    "operation": "runtime_object_encode_decode",
                    "payload": "object_lifecycle_and_delta",
                    "iterations": 3,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.cache_reference.encode_decode.latency",
                  "workload": {
                    "operation": "cache_reference_encode_decode",
                    "payload": "cache_reference_miss_invalidate",
                    "iterations": 3,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.metadata.submit_result.latency",
                  "workload": {
                    "operation": "submit_result_metadata_encode_decode",
                    "payload": "frame_submit_result_push",
                    "iterations": 3,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.typed_payload.tensor_pack_unpack.latency",
                  "workload": {
                    "operation": "typed_payload_pack_unpack",
                    "payload": "tensor_descriptor_plus_payload",
                    "iterations": 3,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.native.payload_snapshot_copy.latency",
                  "workload": {
                    "operation": "payload_snapshot_copy",
                    "payload": "runtime_event_4k",
                    "payload_bytes": 4096,
                    "iterations": 3,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.native.borrowed_buffer_view.latency",
                  "workload": {
                    "operation": "borrowed_buffer_view",
                    "payload": "native_buffer_4k",
                    "payload_bytes": 4096,
                    "iterations": 3,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.runtime.probe.latency",
                  "workload": {
                    "operation": "runtime_probe",
                    "payload": "version_capability_query",
                    "iterations": 3,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.native.runtime_control.roundtrip.latency",
                  "workload": {
                    "operation": "native_runtime_control_roundtrip",
                    "payload": "trace_context_64b",
                    "transport": "ipc",
                    "payload_bytes": 64,
                    "duration_seconds": 0.01,
                    "warmup_iterations": 1,
                    "allocation_iterations": 1
                  }
                },
                {
                  "id": "l4.session.lifecycle.latency",
                  "workload": {
                    "operation": "session_lifecycle",
                    "payload": "open_close_loop",
                    "iterations": 3,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.transport.tcp.loopback.throughput",
                  "workload": {
                    "operation": "transport_loopback",
                    "payload": "request_result_stream",
                    "transport": "tcp",
                    "duration_seconds": 0.01,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.transport.quic.loopback.throughput",
                  "workload": {
                    "operation": "transport_loopback",
                    "payload": "request_result_stream",
                    "transport": "quic",
                    "duration_seconds": 0.01,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.transport.ipc.loopback.throughput",
                  "workload": {
                    "operation": "transport_loopback",
                    "transport": "ipc",
                    "duration_seconds": 0.01,
                    "warmup_iterations": 1
                  }
                },
                {
                  "id": "l4.transport.websocket.loopback.throughput",
                  "workload": {
                    "operation": "transport_loopback",
                    "transport": "websocket",
                    "duration_seconds": 0.01,
                    "warmup_iterations": 1
                  }
                }
              ]
            }
            """;
    }
}
