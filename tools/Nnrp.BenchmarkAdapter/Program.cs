using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nnrp.Core;

namespace Nnrp.BenchmarkAdapter;

public static class Program
{
    private const string ResultsSchemaUrl = "https://raw.githubusercontent.com/NagareWorks/nnrp-conformance/main/schemas/benchmark-results.schema.json";
    private const string DefaultSkipMessage = "This benchmark scenario is not implemented in the current C# baseline runner.";
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private static int Main(string[] args)
    {
        return Run(args);
    }

    public static int Run(string[] args)
    {
        var options = ParseArguments(args);
        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        if (!File.Exists(options.PlanPath))
        {
            throw new ArgumentException($"Plan file does not exist: {options.PlanPath}");
        }

        var reportJson = BuildResultsJson(File.ReadAllText(options.PlanPath, Utf8WithoutBom));
        File.WriteAllText(options.OutputPath, reportJson + Environment.NewLine, Utf8WithoutBom);
        return 0;
    }

    public static string BuildResultsJson(string rawPlan)
    {
        var report = BuildResultsReport(rawPlan);
        return JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
    }

    private static BenchmarkOptions ParseArguments(string[] args)
    {
        string? planPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_BENCHMARK_PLAN");
        string? outputPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_BENCHMARK_RESULTS");

        for (var index = 0; index < args.Length; index += 1)
        {
            switch (args[index])
            {
                case "--plan":
                    planPath = RequireValue(args, ref index, "--plan");
                    break;
                case "--output":
                    outputPath = RequireValue(args, ref index, "--output");
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        if (string.IsNullOrWhiteSpace(planPath))
        {
            throw new ArgumentException("--plan or NNRP_CONFORMANCE_BENCHMARK_PLAN is required.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("--output or NNRP_CONFORMANCE_BENCHMARK_RESULTS is required.");
        }

        return new BenchmarkOptions(planPath, outputPath);
    }

    private static string RequireValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {optionName}.");
        }

        index += 1;
        return args[index];
    }

    private static BenchmarkResultsReport BuildResultsReport(string rawPlan)
    {
        using var document = JsonDocument.Parse(rawPlan);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Benchmark execution plan must be a JSON object.");
        }

        var protocolVersion = GetRequiredString(root, "protocol_version");
        var scenarios = GetRequiredArray(root, "scenarios");
        var results = scenarios
            .EnumerateArray()
            .Select(RunScenario)
            .ToList();
        var implementationName = GetRequiredString(root, "implementation_name");

        return new BenchmarkResultsReport
        {
            Schema = ResultsSchemaUrl,
            ProtocolVersion = protocolVersion,
            ImplementationName = implementationName,
            Environment = BuildEnvironment(),
            Results = results,
        };
    }

    private static BenchmarkScenarioResult RunScenario(JsonElement scenario)
    {
        if (scenario.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Benchmark execution plan scenarios must be JSON objects.");
        }

        var id = GetRequiredString(scenario, "id");
        var workload = GetRequiredObject(scenario, "workload");
        var operation = GetRequiredString(workload, "operation");
        return operation switch
        {
            "header_encode_decode" => RunHeaderEncodeDecode(id, workload),
            _ => new BenchmarkScenarioResult
            {
                Id = id,
                Outcome = "skip",
                Message = DefaultSkipMessage,
            },
        };
    }

    private static BenchmarkScenarioResult RunHeaderEncodeDecode(string id, JsonElement workload)
    {
        var iterations = GetPositiveInt(workload, "iterations", 100_000);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", Math.Min(10_000, iterations));
        var header = new NnrpHeader(
            versionMajor: NnrpHeader.CurrentVersionMajor,
            messageType: MessageType.Ping,
            flags: HeaderFlags.CanDrop,
            metaLength: 0,
            bodyLength: 0,
            sessionId: 7,
            frameId: 11,
            viewId: 13,
            routeId: 17,
            traceId: 19);
        var buffer = new byte[NnrpHeader.HeaderLength];

        void Operation()
        {
            header.Write(buffer);
            if (!NnrpHeader.TryParse(buffer, NnrpHeaderParseOptions.Strict, out var decoded, out _)
                || !decoded.Equals(header))
            {
                throw new InvalidOperationException("Header benchmark roundtrip mismatch.");
            }
        }

        for (var index = 0; index < warmupIterations; index += 1)
        {
            Operation();
        }

        var samples = MeasureMicroseconds(Operation, iterations);
        return new BenchmarkScenarioResult
        {
            Id = id,
            Outcome = "measured",
            Metrics = new BenchmarkMetrics
            {
                P50Microseconds = Percentile(samples, 50),
                P95Microseconds = Percentile(samples, 95),
                P99Microseconds = Percentile(samples, 99),
            },
        };
    }

    private static List<double> MeasureMicroseconds(Action operation, int iterations)
    {
        var samples = new List<double>(iterations);
        for (var index = 0; index < iterations; index += 1)
        {
            var start = Stopwatch.GetTimestamp();
            operation();
            var elapsedTicks = Stopwatch.GetTimestamp() - start;
            samples.Add((elapsedTicks * 1_000_000.0) / Stopwatch.Frequency);
        }

        return samples;
    }

    private static double Percentile(List<double> samples, int percentile)
    {
        if (samples.Count == 0)
        {
            throw new ArgumentException("Benchmark samples must not be empty.", nameof(samples));
        }

        samples.Sort();
        if (percentile == 50)
        {
            var middle = samples.Count / 2;
            return samples.Count % 2 == 0 ? (samples[middle - 1] + samples[middle]) / 2 : samples[middle];
        }

        var rank = (int)Math.Round((percentile / 100.0) * (samples.Count - 1), MidpointRounding.AwayFromZero);
        return samples[rank];
    }

    private static BenchmarkEnvironment BuildEnvironment()
    {
        return new BenchmarkEnvironment
        {
            HostRuntime = RuntimeInformation.FrameworkDescription,
            Os = OperatingSystem.IsWindows()
                ? "windows"
                : OperatingSystem.IsMacOS()
                    ? "macos"
                    : OperatingSystem.IsLinux()
                        ? "linux"
                        : RuntimeInformation.OSDescription.ToLowerInvariant(),
            Arch = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
        };
    }

    private static int GetPositiveInt(JsonElement element, string propertyName, int defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value) || value <= 0)
        {
            throw new ArgumentException($"Benchmark workload field '{propertyName}' must be a positive integer.");
        }

        return value;
    }

    private static int GetNonNegativeInt(JsonElement element, string propertyName, int defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt32(out var value) || value < 0)
        {
            throw new ArgumentException($"Benchmark workload field '{propertyName}' must be a non-negative integer.");
        }

        return value;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Benchmark execution document field '{propertyName}' must be a string.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Benchmark execution document field '{propertyName}' must not be empty.");
        }

        return value;
    }

    private static JsonElement GetRequiredArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"Benchmark execution document field '{propertyName}' must be an array.");
        }

        return property;
    }

    private static JsonElement GetRequiredObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException($"Benchmark execution document field '{propertyName}' must be an object.");
        }

        return property;
    }

    private sealed record BenchmarkOptions(string PlanPath, string OutputPath);

    private sealed class BenchmarkResultsReport
    {
        [JsonPropertyName("$schema")]
        public string Schema { get; init; } = string.Empty;

        [JsonPropertyName("protocol_version")]
        public string ProtocolVersion { get; init; } = string.Empty;

        [JsonPropertyName("implementation_name")]
        public string ImplementationName { get; init; } = string.Empty;

        [JsonPropertyName("environment")]
        public BenchmarkEnvironment Environment { get; init; } = new();

        [JsonPropertyName("results")]
        public List<BenchmarkScenarioResult> Results { get; init; } = new();
    }

    private sealed class BenchmarkEnvironment
    {
        [JsonPropertyName("host_runtime")]
        public string? HostRuntime { get; init; }

        [JsonPropertyName("os")]
        public string Os { get; init; } = string.Empty;

        [JsonPropertyName("arch")]
        public string Arch { get; init; } = string.Empty;
    }

    private sealed class BenchmarkScenarioResult
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("outcome")]
        public string Outcome { get; init; } = string.Empty;

        [JsonPropertyName("metrics")]
        public BenchmarkMetrics? Metrics { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }

    private sealed class BenchmarkMetrics
    {
        [JsonPropertyName("p50_us")]
        public double? P50Microseconds { get; init; }

        [JsonPropertyName("p95_us")]
        public double? P95Microseconds { get; init; }

        [JsonPropertyName("p99_us")]
        public double? P99Microseconds { get; init; }
    }
}
