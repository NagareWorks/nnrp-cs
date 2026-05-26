using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nnrp.Core;

namespace Nnrp.ConformanceAdapter;

public static class Program
{
    private const string ResultsSchemaUrl = "https://raw.githubusercontent.com/NagareWorks/nnrp-conformance/main/schemas/adapter-case-results.schema.json";
    private const string DefaultImplementationName = "nnrp-cs";
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
        File.WriteAllText(
            options.OutputPath,
            reportJson + Environment.NewLine,
            Utf8WithoutBom);
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

    private static AdapterOptions ParseArguments(string[] args)
    {
        string? planPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_PLAN");
        string? outputPath = Environment.GetEnvironmentVariable("NNRP_CONFORMANCE_ADAPTER_RESULTS");

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
            throw new ArgumentException("--plan or NNRP_CONFORMANCE_ADAPTER_PLAN is required.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("--output or NNRP_CONFORMANCE_ADAPTER_RESULTS is required.");
        }

        return new AdapterOptions(planPath, outputPath);
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

    private static AdapterCaseResultsReport BuildResultsReport(string rawPlan)
    {
        using var document = JsonDocument.Parse(rawPlan);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Adapter execution plan must be a JSON object.");
        }

        var protocolVersion = GetRequiredString(root, "protocol_version");
        var cases = GetRequiredArray(root, "cases")
            .EnumerateArray()
            .Select(element =>
            {
                if (element.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("Adapter execution plan cases must be JSON objects.");
                }

                return RunCase(GetRequiredString(element, "id"));
            })
            .ToList();

        return new AdapterCaseResultsReport
        {
            Schema = ResultsSchemaUrl,
            ProtocolVersion = protocolVersion,
            ImplementationName = DefaultImplementationName,
            Results = cases,
        };
    }

    private static AdapterCaseResult RunCase(string caseId)
    {
        try
        {
            switch (caseId)
            {
                case "l0.header.roundtrip.basic":
                    RunHeaderRoundtrip();
                    return Pass(caseId, "Common header strict parse and re-emit roundtrip passed.");
                case "l0.header.invalid_length.reject":
                case "l0.header.length_mismatch.reject":
                    RunHeaderLengthReject();
                    return Pass(caseId, "Malformed common header lengths were rejected.");
                case "l1.handshake.basic":
                case "l1.handshake.capability_window.validation":
                    RunHandshakeBasic();
                    return Pass(caseId, "CLIENT_HELLO and SERVER_HELLO_ACK capability negotiation passed.");
                default:
                    return new AdapterCaseResult
                    {
                        Id = caseId,
                        Outcome = "error",
                        FailureKind = "not_implemented",
                        Message = "Case is outside the SDK-local adapter execution surface.",
                    };
            }
        }
        catch (InvalidOperationException ex)
        {
            return new AdapterCaseResult
            {
                Id = caseId,
                Outcome = "fail",
                FailureKind = "assertion_failed",
                Message = ex.Message,
            };
        }
        catch (Exception ex)
        {
            return new AdapterCaseResult
            {
                Id = caseId,
                Outcome = "error",
                FailureKind = ex.GetType().Name,
                Message = ex.Message,
            };
        }
    }

    private static AdapterCaseResult Pass(string caseId, string message)
    {
        return new AdapterCaseResult
        {
            Id = caseId,
            Outcome = "pass",
            Message = message,
        };
    }

    private static void RunHeaderRoundtrip()
    {
        var header = new NnrpHeader(
            versionMajor: NnrpHeader.CurrentVersionMajor,
            messageType: MessageType.FlowUpdate,
            flags: HeaderFlags.None,
            metaLength: 0,
            bodyLength: 0,
            sessionId: 42,
            frameId: 7,
            viewId: 0,
            routeId: 9,
            traceId: 0x1122334455667788);

        var bytes = header.ToArray();
        AssertTrue(
            NnrpHeader.TryParse(bytes, NnrpHeaderParseOptions.Strict, out var parsed, out var parseError),
            $"Common header strict parse failed: {parseError}.");
        AssertTrue(parsed.Equals(header), "Common header roundtrip changed fixed-width fields.");

        var reEmitted = parsed.ToArray();
        AssertTrue(bytes.AsSpan().SequenceEqual(reEmitted), "Common header re-emitted bytes changed.");
    }

    private static void RunHeaderLengthReject()
    {
        var lengthMismatchHeader = new NnrpHeader(
            versionMajor: NnrpHeader.CurrentVersionMajor,
            messageType: MessageType.Ping,
            flags: HeaderFlags.None,
            metaLength: 4,
            bodyLength: 0,
            sessionId: 0,
            frameId: 0,
            viewId: 0,
            routeId: 0,
            traceId: 0);
        var packet = new byte[NnrpHeader.HeaderLength + 2];
        lengthMismatchHeader.Write(packet);
        packet[NnrpHeader.HeaderLength] = 1;
        packet[NnrpHeader.HeaderLength + 1] = 2;

        AssertTrue(
            !NnrpFramedMessage.TryParse(packet, NnrpHeaderParseOptions.Strict, out _, out var mismatchError)
                && mismatchError == NnrpParseError.SourceTooShort,
            $"Declared metadata length mismatch was not rejected as SourceTooShort: {mismatchError}.");

        var invalidLengthHeader = new NnrpHeader(
            versionMajor: NnrpHeader.CurrentVersionMajor,
            messageType: MessageType.Ping,
            flags: HeaderFlags.None,
            metaLength: 0,
            bodyLength: 0,
            sessionId: 0,
            frameId: 0,
            viewId: 0,
            routeId: 0,
            traceId: 0,
            headerLength: 24);

        AssertTrue(
            !invalidLengthHeader.TryWrite(new byte[NnrpHeader.HeaderLength], out _),
            "Invalid common header length was accepted by the writer.");
    }

    private static void RunHandshakeBasic()
    {
        var clientMetadata = new ClientHelloMetadata(
            minVersionMajor: 1,
            maxVersionMajor: 1,
            supportedWireFormatBitmap: 1,
            supportedProfileBitmap: 1,
            supportedPayloadKindBitmap: (uint)PayloadKind.Tensor,
            supportedCodecBitmap: 3,
            supportedCompressionBitmap: 3,
            supportedDTypeBitmap: 0x21,
            supportedLayoutBitmap: 3,
            cacheDigestBitmap: 1,
            cacheObjectBitmap: 7,
            cacheNamespaceCount: 1,
            maxLaneCount: 2,
            maxCacheEntries: 512,
            maxCacheBytes: 16 * 1024 * 1024,
            targetCadenceX100: 6000,
            latencyBudgetMilliseconds: 100,
            qualityTier: 2,
            degradePolicy: 2,
            requestedSessionId: 0,
            authBytes: 0,
            controlExtensionBytes: 0);
        var clientHello = new ClientHelloMessage(
            new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ClientHello,
                flags: HeaderFlags.AckRequired,
                metaLength: ClientHelloMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: 0,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: 0x1122334455667788),
            clientMetadata,
            Array.Empty<byte>());

        AssertTrue(
            ClientHelloMessage.TryParse(clientHello.ToArray(), out var parsedHello, out var helloError),
            $"CLIENT_HELLO strict parse failed: {helloError}.");
        AssertTrue(parsedHello.Metadata.Equals(clientMetadata), "CLIENT_HELLO metadata roundtrip changed.");

        var ackMetadata = new ServerHelloAckMetadata(
            selectedVersionMajor: 1,
            selectedWireFormat: NnrpHeader.CurrentWireFormat,
            authStatus: 0,
            reserved0: 0,
            sessionId: 42,
            acceptedProfileBitmap: 1,
            acceptedPayloadKindBitmap: (uint)PayloadKind.Tensor,
            acceptedCodecBitmap: 3,
            acceptedCompressionBitmap: 3,
            acceptedDTypeBitmap: 1,
            acceptedLayoutBitmap: 1,
            cacheDigestBitmap: 1,
            cacheObjectBitmap: 7,
            maxCacheEntries: 512,
            maxCacheBytes: 16 * 1024 * 1024,
            maxLaneCount: 2,
            maxConcurrentFrames: 2,
            targetCadenceX100: 6000,
            latencyBudgetMilliseconds: 100,
            qualityTier: 2,
            degradePolicy: 2,
            maxBodyBytes: 32 * 1024 * 1024,
            tokenTtlMilliseconds: 300_000,
            retryAfterMilliseconds: 0,
            controlExtensionBytes: 0,
            serverFlags: 1);

        AssertHandshakeWindow(parsedHello.Metadata, ackMetadata);

        var ack = new ServerHelloAckMessage(
            new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ServerHelloAck,
                flags: HeaderFlags.None,
                metaLength: ServerHelloAckMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: ackMetadata.SessionId,
                frameId: 0,
                viewId: 0,
                routeId: 0,
                traceId: clientHello.Header.TraceId),
            ackMetadata);
        AssertTrue(
            ServerHelloAckMessage.TryParse(ack.ToArray(), out var parsedAck, out var ackError),
            $"SERVER_HELLO_ACK strict parse failed: {ackError}.");
        AssertTrue(parsedAck.Metadata.Equals(ackMetadata), "SERVER_HELLO_ACK metadata roundtrip changed.");
    }

    private static void AssertHandshakeWindow(ClientHelloMetadata hello, ServerHelloAckMetadata ack)
    {
        AssertTrue(hello.MinVersionMajor <= ack.SelectedVersionMajor
            && ack.SelectedVersionMajor <= hello.MaxVersionMajor, "Selected protocol version is outside the client window.");
        AssertTrue(ack.SelectedVersionMajor == NnrpHeader.CurrentVersionMajor, "Selected protocol version is not NNRP/1.");
        AssertTrue(ack.SelectedWireFormat == NnrpHeader.CurrentWireFormat, "Selected wire format is not the public NNRP/1.0 wire format.");
        AssertTrue((hello.SupportedWireFormatBitmap & (1u << (int)ack.SelectedWireFormat)) != 0, "Selected wire format is not supported by the client.");
        AssertSubset(ack.AcceptedProfileBitmap, hello.SupportedProfileBitmap, "profile");
        AssertSubset(ack.AcceptedPayloadKindBitmap, hello.SupportedPayloadKindBitmap, "payload kind");
        AssertSubset(ack.AcceptedCodecBitmap, hello.SupportedCodecBitmap, "codec");
        AssertSubset(ack.AcceptedCompressionBitmap, hello.SupportedCompressionBitmap, "compression");
        AssertSubset(ack.AcceptedDTypeBitmap, hello.SupportedDTypeBitmap, "dtype");
        AssertSubset(ack.AcceptedLayoutBitmap, hello.SupportedLayoutBitmap, "layout");
        AssertSubset(ack.CacheDigestBitmap, hello.CacheDigestBitmap, "cache digest");
        AssertSubset(ack.CacheObjectBitmap, hello.CacheObjectBitmap, "cache object");
        AssertTrue(ack.SessionId != 0, "SERVER_HELLO_ACK did not assign a session id.");
        AssertTrue(ack.MaxLaneCount <= hello.MaxLaneCount, "SERVER_HELLO_ACK widened max_lane_count beyond the client window.");
        AssertTrue(ack.MaxCacheEntries <= hello.MaxCacheEntries, "SERVER_HELLO_ACK widened max_cache_entries beyond the client window.");
        AssertTrue(ack.MaxCacheBytes <= hello.MaxCacheBytes, "SERVER_HELLO_ACK widened max_cache_bytes beyond the client window.");
        AssertTrue(ack.AuthStatus == 0, "SERVER_HELLO_ACK did not accept authentication.");
        AssertTrue(ack.Reserved0 == 0, "SERVER_HELLO_ACK reserved byte was non-zero.");
    }

    private static void AssertSubset(uint selected, uint supported, string fieldName)
    {
        AssertTrue((selected & ~supported) == 0, $"SERVER_HELLO_ACK selected unsupported {fieldName} bits.");
        AssertTrue(selected != 0, $"SERVER_HELLO_ACK selected no {fieldName} bits.");
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Adapter execution document field '{propertyName}' must be a string.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Adapter execution document field '{propertyName}' must not be empty.");
        }

        return value;
    }

    private static JsonElement GetRequiredArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"Adapter execution document field '{propertyName}' must be an array.");
        }

        return property;
    }

    private sealed record AdapterOptions(string PlanPath, string OutputPath);

    private sealed class AdapterCaseResultsReport
    {
        [JsonPropertyName("$schema")]
        public string Schema { get; init; } = string.Empty;

        [JsonPropertyName("protocol_version")]
        public string ProtocolVersion { get; init; } = string.Empty;

        [JsonPropertyName("implementation_name")]
        public string ImplementationName { get; init; } = string.Empty;

        [JsonPropertyName("results")]
        public List<AdapterCaseResult> Results { get; init; } = new();
    }

    private sealed class AdapterCaseResult
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("outcome")]
        public string Outcome { get; init; } = string.Empty;

        [JsonPropertyName("failure_kind")]
        public string? FailureKind { get; init; }

        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
