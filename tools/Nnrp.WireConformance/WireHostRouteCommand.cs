using System.Text.Json;

namespace Nnrp.WireConformance;

internal static class WireHostRouteCommand
{
    internal const string ProtocolVersion = "nnrp-1-preview4";
    internal const string ResultSchema =
        "https://github.com/NagareWorks/nnrp-conformance/schemas/wire-conformance-case-results.schema.json";

    internal static async Task<int> RunAsync(
        ReadOnlyMemory<string> args,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);
        try
        {
            WireHostRouteCommandOptions options = Parse(args.Span);
            WireHostRouteScenario scenario = LoadScenario(options.ScenarioPath);
            WireHostRouteCaseResult result;
            try
            {
                WireHostRouteScenario resolved = LoadScenario(options.ResolvedScenarioPath);
                result = await new WireHostRouteDriver().RunAsync(
                    scenario,
                    resolved,
                    options,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                error.WriteLine(exception);
                result = Failed(scenario.Id, exception.Message);
            }

            WriteJsonAtomically(
                options.OutputPath,
                new WireHostRouteResultReport(
                    ResultSchema,
                    ProtocolVersion,
                    options.SuiteVersion,
                    options.TargetName,
                    [result]),
                WireHostRouteJsonContext.Default.WireHostRouteResultReport);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            error.WriteLine("Host-route target was cancelled.");
            return 130;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or JsonException)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }

    internal static WireHostRouteCommandOptions Parse(ReadOnlySpan<string> args)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            if (!option.StartsWith("--", StringComparison.Ordinal)
                || index + 1 >= args.Length
                || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for option: {option}");
            }

            if (!values.TryAdd(option, args[++index]))
            {
                throw new ArgumentException($"Duplicate option: {option}");
            }
        }

        string Require(string name) => values.Remove(name, out string? value)
            && !string.IsNullOrWhiteSpace(value)
                ? value
                : throw new ArgumentException($"Missing required option: {name}");

        WireHostRouteCommandOptions options = new(
            Path.GetFullPath(Require("--scenario")),
            Path.GetFullPath(Require("--resolved-scenario")),
            Path.GetFullPath(Require("--output")),
            Path.GetFullPath(Require("--ready-output")),
            Path.GetFullPath(Require("--artifacts")),
            Require("--suite-version"),
            Require("--target-name"));
        if (values.Count != 0)
        {
            throw new ArgumentException($"Unknown option: {values.Keys.Order().First()}");
        }

        return options;
    }

    internal static WireHostRouteCaseResult Passed(
        string id,
        string terminal,
        WireHostRouteEvidence evidence,
        string? message = null) =>
        new(id, "passed", terminal, [], evidence, message, []);

    internal static WireHostRouteCaseResult Failed(string id, string message) =>
        new(id, "failed", "error", [], null, message, []);

    internal static void WriteJsonAtomically<T>(
        string path,
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, $"{JsonSerializer.Serialize(value, typeInfo)}{Environment.NewLine}");
            File.Move(temporaryPath, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static WireHostRouteScenario LoadScenario(string path) =>
        JsonSerializer.Deserialize(
            File.ReadAllText(path),
            WireHostRouteJsonContext.Default.WireHostRouteScenario)
        ?? throw new JsonException($"Host-route scenario is empty: {path}");
}

internal sealed record WireHostRouteCommandOptions(
    string ScenarioPath,
    string ResolvedScenarioPath,
    string OutputPath,
    string ReadyOutputPath,
    string ArtifactsPath,
    string SuiteVersion,
    string TargetName);
