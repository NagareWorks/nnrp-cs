using System.Text.Json;

namespace Nnrp.WireConformance;

public static class WireTargetManifestCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        try
        {
            if (args.Length == 0 || args[0] is "--help" or "-h")
            {
                WriteHelp(output);
                return 0;
            }

            if (!string.Equals(args[0], "manifest", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown command: {args[0]}");
            }

            WireTargetCommandOptions options = ParseManifestArguments(args.AsSpan(1));
            WireTargetManifestBuilder builder = new(WireTargetSupport.Compiled);
            WireTargetManifest manifest = builder.Build(
                options.TargetName,
                options.SuiteVersion,
                options.Modes,
                AttachSecurity(options.Transports, options.SecurityByTransport),
                options.Capabilities,
                options.MaxFrameBytes,
                options.MaxInFlight);
            WireTargetManifestBuilder.Write(options.OutputPath, manifest);
            return 0;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException)
        {
            error.WriteLine(exception.Message);
            return 2;
        }
    }

    private static WireTargetCommandOptions ParseManifestArguments(ReadOnlySpan<string> args)
    {
        string targetName = "nnrp-cs";
        string suiteVersion = "0.1.0";
        string? outputPath = null;
        int maxFrameBytes = WireTargetManifestBuilder.DefaultMaxFrameBytes;
        int maxInFlight = WireTargetManifestBuilder.DefaultMaxInFlight;
        List<string> modes = [];
        List<WireTargetTransport> transports = [];
        List<string> capabilities = [];
        Dictionary<string, WireTargetTransportSecurity> security = new(StringComparer.Ordinal);

        for (int index = 0; index < args.Length; index++)
        {
            string option = args[index];
            string value = ReadValue(args, ref index, option);
            switch (option)
            {
                case "--target-name":
                    targetName = value;
                    break;
                case "--suite-version":
                    suiteVersion = value;
                    break;
                case "--mode":
                    modes.Add(value);
                    break;
                case "--transport":
                    transports.Add(ParseTransport(value));
                    break;
                case "--transport-security":
                    (string name, WireTargetTransportSecurity transportSecurity) = ParseSecurity(value);
                    if (!security.TryAdd(name, transportSecurity))
                    {
                        throw new ArgumentException($"Duplicate transport security: {name}");
                    }

                    break;
                case "--capability":
                    capabilities.Add(value);
                    break;
                case "--max-frame-bytes":
                    maxFrameBytes = ParsePositiveInteger(value, option);
                    break;
                case "--max-in-flight":
                    maxInFlight = ParsePositiveInteger(value, option);
                    break;
                case "--output":
                    outputPath = value;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {option}");
            }
        }

        if (outputPath is null)
        {
            throw new ArgumentException("Missing required option: --output");
        }

        return new WireTargetCommandOptions(
            targetName,
            suiteVersion,
            modes,
            transports,
            security,
            capabilities,
            maxFrameBytes,
            maxInFlight,
            outputPath);
    }

    private static string ReadValue(ReadOnlySpan<string> args, ref int index, string option)
    {
        if (!option.StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Expected an option, received: {option}");
        }

        int valueIndex = index + 1;
        if (valueIndex >= args.Length || args[valueIndex].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Missing value for option: {option}");
        }

        index = valueIndex;
        return args[valueIndex];
    }

    private static WireTargetTransport ParseTransport(string value)
    {
        int separator = value.IndexOf('=');
        if (separator <= 0)
        {
            throw new ArgumentException("Transport must use name=endpoint form.");
        }

        string name = value[..separator].Trim().ToLowerInvariant();
        string endpoint = value[(separator + 1)..].Trim();
        if (endpoint.Length == 0)
        {
            throw new ArgumentException("Transport endpoint must be non-empty.");
        }

        bool tls = name == "quic" || endpoint.StartsWith("wss://", StringComparison.OrdinalIgnoreCase);
        return new WireTargetTransport(name, endpoint, tls);
    }

    private static (string Name, WireTargetTransportSecurity Security) ParseSecurity(string value)
    {
        using JsonDocument document = JsonDocument.Parse(value);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Transport security must be a JSON object.");
        }

        HashSet<string> expected =
        [
            "transport",
            "server_name",
            "trusted_certificate_der_path",
            "certificate_der_path",
            "private_key_pkcs8_der_path",
        ];
        foreach (JsonProperty property in root.EnumerateObject())
        {
            if (!expected.Remove(property.Name))
            {
                throw new ArgumentException($"Transport security contains unknown field: {property.Name}");
            }
        }

        if (expected.Count != 0)
        {
            throw new ArgumentException($"Transport security is missing fields: {string.Join(", ", expected.Order())}");
        }

        string name = ReadRequiredString(root, "transport").Trim().ToLowerInvariant();
        return (
            name,
            new WireTargetTransportSecurity(
                ReadRequiredString(root, "server_name"),
                ReadRequiredString(root, "trusted_certificate_der_path"),
                ReadRequiredString(root, "certificate_der_path"),
                ReadRequiredString(root, "private_key_pkcs8_der_path")));
    }

    private static string ReadRequiredString(JsonElement root, string name)
    {
        JsonElement value = root.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new ArgumentException($"Transport security field must be a non-empty string: {name}");
        }

        return value.GetString()!;
    }

    private static IEnumerable<WireTargetTransport> AttachSecurity(
        IEnumerable<WireTargetTransport> transports,
        IReadOnlyDictionary<string, WireTargetTransportSecurity> security)
    {
        List<WireTargetTransport> result = [];
        HashSet<string> declared = new(StringComparer.Ordinal);
        foreach (WireTargetTransport transport in transports)
        {
            declared.Add(transport.Name);
            result.Add(transport with { Security = security.GetValueOrDefault(transport.Name) });
        }

        string[] unknown = security.Keys.Where(name => !declared.Contains(name)).Order().ToArray();
        if (unknown.Length != 0)
        {
            throw new ArgumentException($"Transport security references undeclared transport: {string.Join(", ", unknown)}");
        }

        return result;
    }

    private static int ParsePositiveInteger(string value, string option)
    {
        if (!int.TryParse(value, out int result) || result <= 0)
        {
            throw new ArgumentException($"{option} must be a positive integer.");
        }

        return result;
    }

    private static void WriteHelp(TextWriter output)
    {
        output.WriteLine("Usage: Nnrp.WireConformance manifest [options]");
        output.WriteLine("  --target-name NAME");
        output.WriteLine("  --suite-version VERSION");
        output.WriteLine("  --mode suite_as_client|suite_as_server");
        output.WriteLine("  --transport name=endpoint");
        output.WriteLine("  --transport-security JSON");
        output.WriteLine("  --capability TOKEN");
        output.WriteLine("  --max-frame-bytes COUNT");
        output.WriteLine("  --max-in-flight COUNT");
        output.WriteLine("  --output PATH");
    }

    private sealed record WireTargetCommandOptions(
        string TargetName,
        string SuiteVersion,
        IReadOnlyList<string> Modes,
        IReadOnlyList<WireTargetTransport> Transports,
        IReadOnlyDictionary<string, WireTargetTransportSecurity> SecurityByTransport,
        IReadOnlyList<string> Capabilities,
        int MaxFrameBytes,
        int MaxInFlight,
        string OutputPath);
}
