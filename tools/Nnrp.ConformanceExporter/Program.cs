using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nnrp.Core;

namespace Nnrp.ConformanceExporter;

internal static class Program
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly IReadOnlyDictionary<string, string> VectorKindByRecipeType = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["header"] = "header",
        ["client_hello_metadata"] = "metadata",
        ["session_patch_ack_metadata"] = "metadata",
        ["flow_update_packet"] = "packet",
        ["result_hint_packet"] = "packet",
        ["frame_submit_metadata"] = "metadata",
        ["result_push_metadata"] = "metadata",
        ["body_region_prelude"] = "body_region",
        ["object_reference_block"] = "object_reference",
        ["typed_payload_descriptor"] = "typed_payload_descriptor",
        ["typed_payload_descriptor_region"] = "typed_payload_descriptor_region",
        ["typed_payload_frame_region"] = "typed_payload_frame_region",
    };
    private static readonly IReadOnlyDictionary<string, byte> LossTolerancePolicyValues = new Dictionary<string, byte>(StringComparer.Ordinal)
    {
        ["strict"] = 0,
        ["best_effort"] = 1,
        ["low_latency"] = 2,
        ["fire_and_forget"] = 3,
        ["inherit_session"] = 0xFF,
    };

    private static int Main(string[] args)
    {
        var options = ParseArguments(args);
        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var json = BuildManifestJson(options.ProtocolVersion, options.RecipeManifestPath);
        File.WriteAllText(options.OutputPath, json + Environment.NewLine, Utf8WithoutBom);
        return 0;
    }

    internal static string BuildManifestJson(string protocolVersion, string recipeManifestPath)
    {
        var manifest = BuildManifest(protocolVersion, recipeManifestPath);
        return JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
    }

    private static ExportOptions ParseArguments(string[] args)
    {
        string? protocolVersion = null;
        string? recipeManifestPath = null;
        string? outputPath = null;

        for (var index = 0; index < args.Length; index += 1)
        {
            switch (args[index])
            {
                case "--protocol-version":
                    protocolVersion = RequireValue(args, ref index, "--protocol-version");
                    break;
                case "--recipe-manifest":
                    recipeManifestPath = RequireValue(args, ref index, "--recipe-manifest");
                    break;
                case "--output":
                    outputPath = RequireValue(args, ref index, "--output");
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[index]}");
            }
        }

        if (string.IsNullOrWhiteSpace(protocolVersion))
        {
            throw new ArgumentException("--protocol-version is required.");
        }

        if (string.IsNullOrWhiteSpace(recipeManifestPath))
        {
            throw new ArgumentException("--recipe-manifest is required.");
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("--output is required.");
        }

        return new ExportOptions(protocolVersion, recipeManifestPath, outputPath);
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

    private static ConformanceVectorManifest BuildManifest(string protocolVersion, string recipeManifestPath)
    {
        if (!string.Equals(protocolVersion, "nnrp-1-preview2", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unsupported protocol version for C# conformance export: {protocolVersion}");
        }

        if (!File.Exists(recipeManifestPath))
        {
            throw new ArgumentException($"Recipe manifest path does not exist: {recipeManifestPath}");
        }

        using var recipeDocument = JsonDocument.Parse(File.ReadAllText(recipeManifestPath, Utf8WithoutBom));
        var root = recipeDocument.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Semantic vector recipe manifest must be a JSON object.");
        }

        var manifestProtocolVersion = GetRequiredString(root, "protocol_version");
        if (!string.Equals(manifestProtocolVersion, protocolVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Semantic vector recipe manifest protocol version does not match requested export: '{manifestProtocolVersion}' != '{protocolVersion}'.");
        }

        var vectors = new List<ConformanceVectorEntry>();
        foreach (var recipe in GetRequiredArray(root, "vectors").EnumerateArray())
        {
            vectors.Add(BuildVectorEntry(recipe));
        }

        return new ConformanceVectorManifest
        {
            ProtocolVersion = protocolVersion,
            Generator = "nnrp-cs",
            Vectors = vectors,
        };
    }

    private static ConformanceVectorEntry BuildVectorEntry(JsonElement recipe)
    {
        var recipeType = GetRequiredString(recipe, "recipe_type");
        if (!VectorKindByRecipeType.TryGetValue(recipeType, out var kind))
        {
            throw new ArgumentException($"Unsupported semantic vector recipe type: {recipeType}");
        }

        var description = recipe.TryGetProperty("description", out var descriptionElement)
            ? descriptionElement.GetString()
            : null;

        return CreateVector(
            GetRequiredString(recipe, "name"),
            kind,
            description ?? string.Empty,
            BuildVectorPayload(recipeType, recipe));
    }

    private static byte[] BuildVectorPayload(string recipeType, JsonElement recipe)
    {
        switch (recipeType)
        {
            case "header":
                return new NnrpHeader(
                    versionMajor: checked((byte)GetRequiredInt(recipe, "version_major")),
                    wireFormat: checked((byte)GetRequiredInt(recipe, "wire_format")),
                    messageType: ParseSnakeCaseEnum<MessageType>(GetRequiredString(recipe, "message_type")),
                    flags: ParseFlags<HeaderFlags>(GetRequiredStringArray(recipe, "flags")),
                    metaLength: checked((uint)GetRequiredInt(recipe, "meta_len")),
                    bodyLength: checked((uint)GetRequiredInt(recipe, "body_len")),
                    sessionId: checked((uint)GetRequiredInt(recipe, "session_id")),
                    frameId: checked((uint)GetRequiredInt(recipe, "frame_id")),
                    viewId: checked((ushort)GetRequiredInt(recipe, "view_id")),
                    routeId: checked((ushort)GetRequiredInt(recipe, "route_id")),
                    traceId: checked((ulong)GetRequiredLong(recipe, "trace_id"))).ToArray();

            case "client_hello_metadata":
                return new ClientHelloMetadata(
                    minVersionMajor: checked((byte)GetRequiredInt(recipe, "min_version_major")),
                    maxVersionMajor: checked((byte)GetRequiredInt(recipe, "max_version_major")),
                    supportedWireFormatBitmap: checked((uint)GetRequiredInt(recipe, "supported_wire_format_bitmap")),
                    supportedProfileBitmap: checked((uint)GetRequiredInt(recipe, "supported_profile_bitmap")),
                    supportedPayloadKindBitmap: Convert.ToUInt32(ParseFlags<PayloadKind>(GetRequiredStringArray(recipe, "supported_payload_kind_bitmap"))),
                    supportedCodecBitmap: checked((uint)GetRequiredInt(recipe, "supported_codec_bitmap")),
                    supportedCompressionBitmap: checked((uint)GetRequiredInt(recipe, "supported_compression_bitmap")),
                    supportedDTypeBitmap: checked((uint)GetRequiredInt(recipe, "supported_dtype_bitmap")),
                    supportedLayoutBitmap: checked((uint)GetRequiredInt(recipe, "supported_layout_bitmap")),
                    cacheDigestBitmap: checked((uint)GetRequiredInt(recipe, "cache_digest_bitmap")),
                    cacheObjectBitmap: checked((uint)GetRequiredInt(recipe, "cache_object_bitmap")),
                    cacheNamespaceCount: checked((ushort)GetRequiredInt(recipe, "cache_namespace_count")),
                    maxLaneCount: checked((ushort)GetRequiredInt(recipe, "max_lane_count")),
                    maxCacheEntries: checked((uint)GetRequiredInt(recipe, "max_cache_entries")),
                    maxCacheBytes: checked((uint)GetRequiredLong(recipe, "max_cache_bytes")),
                    targetCadenceX100: checked((uint)GetRequiredInt(recipe, "target_cadence_x100")),
                    latencyBudgetMilliseconds: checked((uint)GetRequiredInt(recipe, "latency_budget_ms")),
                    qualityTier: checked((uint)GetRequiredInt(recipe, "quality_tier")),
                    degradePolicy: checked((uint)GetRequiredInt(recipe, "degrade_policy")),
                    requestedSessionId: checked((uint)GetRequiredInt(recipe, "requested_session_id")),
                    authBytes: checked((uint)GetRequiredInt(recipe, "auth_bytes")),
                    controlExtensionBytes: checked((uint)GetRequiredInt(recipe, "control_extension_bytes"))).ToArray();

            case "session_patch_ack_metadata":
                return new SessionPatchAckMetadata(
                    ackStatus: ParseSnakeCaseEnum<SessionPatchAckStatus>(GetRequiredString(recipe, "ack_status")),
                    rejectReason: ParseSnakeCaseEnum<SessionPatchRejectReason>(GetRequiredString(recipe, "reject_reason")),
                    appliedPatchMask: ParseFlags<SessionPatchField>(GetRequiredStringArray(recipe, "applied_patch_mask")),
                    rejectedPatchMask: ParseFlags<SessionPatchField>(GetRequiredStringArray(recipe, "rejected_patch_mask")),
                    retryAfterMilliseconds: checked((uint)GetRequiredInt(recipe, "retry_after_ms")),
                    effectiveProfileId: checked((ushort)GetRequiredInt(recipe, "effective_profile_id")),
                    effectiveTargetCadenceX100: checked((uint)GetRequiredInt(recipe, "effective_target_cadence_x100")),
                    effectiveQualityTier: checked((ushort)GetRequiredInt(recipe, "effective_quality_tier")),
                    effectiveDegradePolicy: checked((ushort)GetRequiredInt(recipe, "effective_degrade_policy")),
                    effectiveLaneMask: checked((ulong)GetRequiredLong(recipe, "effective_lane_mask")),
                    preferredCodecBitmap: checked((uint)GetRequiredInt(recipe, "effective_codec_bitmap")),
                    preferredCompressionBitmap: checked((uint)GetRequiredInt(recipe, "effective_compression_bitmap")),
                    profilePatchAckBytes: checked((uint)GetRequiredInt(recipe, "profile_patch_ack_bytes")),
                    reserved0: checked((ushort)GetRequiredInt(recipe, "reserved0"))).ToArray();

            case "flow_update_packet":
                return new FlowUpdateMessage(
                    new NnrpHeader(
                        versionMajor: checked((byte)GetRequiredInt(recipe, "version_major")),
                        wireFormat: checked((byte)GetRequiredInt(recipe, "wire_format")),
                        messageType: MessageType.FlowUpdate,
                        flags: ParseFlags<HeaderFlags>(GetRequiredStringArray(recipe, "flags")),
                        metaLength: FlowUpdateMetadata.MetadataLength,
                        bodyLength: 0,
                        sessionId: checked((uint)GetRequiredInt(recipe, "session_id")),
                        frameId: 0,
                        viewId: 0,
                        routeId: checked((ushort)GetRequiredInt(recipe, "route_id")),
                        traceId: checked((ulong)GetRequiredLong(recipe, "trace_id"))),
                    new FlowUpdateMetadata(
                        scopeKind: ParseSnakeCaseEnum<FlowUpdateScopeKind>(GetRequiredString(recipe, "scope_kind")),
                        updateReason: ParseSnakeCaseEnum<FlowUpdateReason>(GetRequiredString(recipe, "update_reason")),
                        backpressureLevel: ParseSnakeCaseEnum<FlowUpdateBackpressureLevel>(GetRequiredString(recipe, "backpressure_level")),
                        connectionCredit: checked((ushort)GetRequiredInt(recipe, "connection_credit")),
                        sessionCredit: checked((ushort)GetRequiredInt(recipe, "session_credit")),
                        operationCredit: checked((ushort)GetRequiredInt(recipe, "operation_credit")),
                        operationId: checked((ulong)GetRequiredLong(recipe, "operation_id")),
                        retryAfterMilliseconds: checked((uint)GetRequiredInt(recipe, "retry_after_ms")),
                        creditEpoch: checked((uint)GetRequiredInt(recipe, "credit_epoch")),
                        flags: ParseFlags<FlowUpdateFlags>(GetRequiredStringArray(recipe, "flow_update_flags")))).ToArray();

            case "result_hint_packet":
                return new ResultHintMessage(
                    new NnrpHeader(
                        versionMajor: checked((byte)GetRequiredInt(recipe, "version_major")),
                        wireFormat: checked((byte)GetRequiredInt(recipe, "wire_format")),
                        messageType: MessageType.ResultHint,
                        flags: ParseFlags<HeaderFlags>(GetRequiredStringArray(recipe, "flags")),
                        metaLength: ResultHintMetadata.MetadataLength,
                        bodyLength: 0,
                        sessionId: checked((uint)GetRequiredInt(recipe, "session_id")),
                        frameId: checked((uint)GetRequiredInt(recipe, "frame_id")),
                        viewId: 0,
                        routeId: checked((ushort)GetRequiredInt(recipe, "route_id")),
                        traceId: checked((ulong)GetRequiredLong(recipe, "trace_id"))),
                    new ResultHintMetadata(
                        appliedBudgetPolicy: ParseSnakeCaseEnum<ResultHintBudgetPolicy>(GetRequiredString(recipe, "applied_budget_policy")),
                        congestionState: ParseSnakeCaseEnum<ResultHintCongestionState>(GetRequiredString(recipe, "congestion_state")),
                        reason: ParseSnakeCaseEnum<ResultHintReason>(GetRequiredString(recipe, "reason")),
                        retryAfterMilliseconds: checked((uint)GetRequiredInt(recipe, "retry_after_ms")))).ToArray();

            case "frame_submit_metadata":
                return new FrameSubmitMetadata(
                    sourceWidth: checked((ushort)GetRequiredInt(recipe, "src_width")),
                    sourceHeight: checked((ushort)GetRequiredInt(recipe, "src_height")),
                    tileWidth: checked((ushort)GetRequiredInt(recipe, "tile_width")),
                    tileHeight: checked((ushort)GetRequiredInt(recipe, "tile_height")),
                    tileCount: checked((ushort)GetRequiredInt(recipe, "tile_count")),
                    sectionCount: checked((ushort)GetRequiredInt(recipe, "section_count")),
                    frameClass: (FrameClass)GetRequiredInt(recipe, "frame_class"),
                    inputProfile: ParseSnakeCaseEnum<InputProfile>(GetRequiredString(recipe, "input_profile")),
                    tileIndexMode: ParseSnakeCaseEnum<TileIndexMode>(GetRequiredString(recipe, "tile_index_mode")),
                    reserved0: checked((byte)GetRequiredInt(recipe, "reserved0")),
                    latencyBudgetMilliseconds: checked((ushort)GetRequiredInt(recipe, "latency_budget_ms")),
                    targetFpsTimes100: checked((ushort)GetRequiredInt(recipe, "target_fps_x100")),
                    retryOfFrame: checked((uint)GetRequiredInt(recipe, "retry_of_frame")),
                    tileBaseId: checked((uint)GetRequiredInt(recipe, "tile_base_id")),
                    cameraBytes: checked((uint)GetRequiredInt(recipe, "camera_bytes")),
                    tileIndexBytes: checked((uint)GetRequiredInt(recipe, "tile_index_bytes")),
                    reserved1: 0,
                    reserved2: 0,
                    submitMode: ParseSnakeCaseEnum<SubmitMode>(GetRequiredString(recipe, "submit_mode")),
                    budgetPolicy: ParseFlags<BudgetPolicy>(GetRequiredStringArray(recipe, "budget_policy")),
                    lossTolerancePolicy: ParseLossTolerancePolicy(GetRequiredString(recipe, "loss_tolerance_policy")),
                    reserved3: 0,
                    objectRefMask: checked((uint)GetRequiredInt(recipe, "object_ref_mask")),
                    dependencyFrameId: checked((uint)GetRequiredInt(recipe, "dependency_frame_id")),
                    payloadKindBitmap: ParseFlags<PayloadKind>(GetRequiredStringArray(recipe, "payload_kind_bitmap")),
                    payloadFrameCount: checked((ushort)GetRequiredInt(recipe, "payload_frame_count")),
                    reserved4: 0).ToArray();

            case "result_push_metadata":
                return new ResultPushMetadata(
                    statusCode: (ResultStatusCode)GetRequiredInt(recipe, "status_code"),
                    resultFlags: ParseFlags<ResultFlags>(GetRequiredStringArray(recipe, "result_flags")),
                    sectionCount: checked((ushort)GetRequiredInt(recipe, "section_count")),
                    tileCount: checked((ushort)GetRequiredInt(recipe, "tile_count")),
                    activeProfileId: checked((ushort)GetRequiredInt(recipe, "active_profile_id")),
                    inferenceMilliseconds: checked((ushort)GetRequiredInt(recipe, "inference_ms")),
                    queueMilliseconds: checked((ushort)GetRequiredInt(recipe, "queue_ms")),
                    serverTotalMilliseconds: checked((ushort)GetRequiredInt(recipe, "server_total_ms")),
                    tileBaseId: checked((uint)GetRequiredInt(recipe, "tile_base_id")),
                    tileIndexBytes: checked((ushort)GetRequiredInt(recipe, "tile_index_bytes")),
                    resultClass: ParseSnakeCaseEnum<ResultClass>(GetRequiredString(recipe, "result_class")),
                    appliedBudgetPolicy: ParseFlags<BudgetPolicy>(GetRequiredStringArray(recipe, "applied_budget_policy")),
                    reusedFrameId: checked((uint)GetRequiredInt(recipe, "reused_frame_id")),
                    coveredTileCount: checked((ushort)GetRequiredInt(recipe, "covered_tile_count")),
                    droppedTileCount: checked((ushort)GetRequiredInt(recipe, "dropped_tile_count")),
                    payloadKindBitmap: ParseFlags<PayloadKind>(GetRequiredStringArray(recipe, "payload_kind_bitmap")),
                    payloadFrameCount: checked((ushort)GetRequiredInt(recipe, "payload_frame_count"))).ToArray();

            case "body_region_prelude":
                return new BodyRegionPrelude(
                    inlineObjectBytes: checked((uint)GetRequiredInt(recipe, "inline_object_bytes")),
                    objectReferenceBytes: checked((uint)GetRequiredInt(recipe, "object_reference_bytes")),
                    typedPayloadDescriptorBytes: checked((uint)GetRequiredInt(recipe, "typed_payload_descriptor_bytes")),
                    typedPayloadFrameBytes: checked((uint)GetRequiredInt(recipe, "typed_payload_frame_bytes")),
                    extensionDescriptorBytes: checked((uint)GetRequiredInt(recipe, "extension_descriptor_bytes")),
                    extensionPayloadBytes: checked((uint)GetRequiredInt(recipe, "extension_payload_bytes")),
                    bodyFlags: 0,
                    reserved: 0).ToArray();

            case "object_reference_block":
                return new ObjectReferenceBlock(
                    objectKind: ParseSnakeCaseEnum<CacheObjectKind>(GetRequiredString(recipe, "object_kind")),
                    referenceFlags: checked((byte)GetRequiredInt(recipe, "ref_flags")),
                    cacheNamespace: checked((ushort)GetRequiredInt(recipe, "cache_namespace")),
                    cacheKeyHigh: checked((uint)GetRequiredLong(recipe, "cache_key_hi")),
                    cacheKeyLow: checked((uint)GetRequiredLong(recipe, "cache_key_lo"))).ToArray();

            case "typed_payload_descriptor":
                return new TypedPayloadDescriptor(
                    payloadKind: ParseSnakeCaseEnum<PayloadKind>(GetRequiredString(recipe, "payload_kind")),
                    descriptorFlags: checked((byte)GetRequiredInt(recipe, "descriptor_flags")),
                    profileId: checked((ushort)GetRequiredInt(recipe, "profile_id")),
                    payloadOffset: checked((uint)GetRequiredInt(recipe, "payload_offset")),
                    payloadLength: checked((uint)GetRequiredInt(recipe, "payload_length")),
                    reserved: 0).ToArray();

            case "typed_payload_descriptor_region":
            case "typed_payload_frame_region":
                var regions = BuildTypedPayloadRegions(recipe);
                return recipeType == "typed_payload_descriptor_region" ? regions.DescriptorRegion : regions.FrameRegion;

            default:
                throw new ArgumentException($"Unsupported semantic vector recipe type: {recipeType}");
        }
    }

    private static (byte[] DescriptorRegion, byte[] FrameRegion) BuildTypedPayloadRegions(JsonElement recipe)
    {
        var descriptorBlocks = new List<byte[]>();
        var payloadBlocks = new List<byte[]>();
        uint payloadOffset = 0;

        foreach (var frameRecipe in GetRequiredArray(recipe, "frames").EnumerateArray())
        {
            var payload = GetTypedPayloadBytes(frameRecipe);
            descriptorBlocks.Add(
                new TypedPayloadDescriptor(
                    payloadKind: ParseSnakeCaseEnum<PayloadKind>(GetRequiredString(frameRecipe, "payload_kind")),
                    descriptorFlags: checked((byte)GetOptionalInt(frameRecipe, "descriptor_flags", 0)),
                    profileId: checked((ushort)GetRequiredInt(frameRecipe, "profile_id")),
                    payloadOffset: payloadOffset,
                    payloadLength: checked((uint)payload.Length),
                    reserved: 0).ToArray());
            payloadBlocks.Add(payload);
            payloadOffset = checked(payloadOffset + (uint)payload.Length);
        }

        return (Concat(descriptorBlocks.ToArray()), Concat(payloadBlocks.ToArray()));
    }

    private static byte[] GetTypedPayloadBytes(JsonElement frameRecipe)
    {
        if (frameRecipe.TryGetProperty("payload_utf8", out var payloadUtf8))
        {
            var text = payloadUtf8.GetString();
            if (text is null)
            {
                throw new ArgumentException("Semantic vector frame payload_utf8 must be a string.");
            }

            return Utf8WithoutBom.GetBytes(text);
        }

        if (frameRecipe.TryGetProperty("payload_hex", out var payloadHex))
        {
            var hex = payloadHex.GetString();
            if (hex is null)
            {
                throw new ArgumentException("Semantic vector frame payload_hex must be a string.");
            }

            return Convert.FromHexString(hex);
        }

        throw new ArgumentException("Semantic vector frame recipe must define payload_utf8 or payload_hex.");
    }

    private static JsonElement GetRequiredArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"Semantic vector recipe field '{propertyName}' must be an array.");
        }

        return value;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException($"Semantic vector recipe field '{propertyName}' must be a string.");
        }

        return value.GetString()!;
    }

    private static string[] GetRequiredStringArray(JsonElement element, string propertyName)
    {
        return GetRequiredArray(element, propertyName)
            .EnumerateArray()
            .Select(static item => item.ValueKind == JsonValueKind.String
                ? item.GetString()!
                : throw new ArgumentException("Semantic vector recipe string array contains a non-string entry."))
            .ToArray();
    }

    private static int GetRequiredInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || !value.TryGetInt32(out var parsed))
        {
            throw new ArgumentException($"Semantic vector recipe field '{propertyName}' must be an integer.");
        }

        return parsed;
    }

    private static long GetRequiredLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || !value.TryGetInt64(out var parsed))
        {
            throw new ArgumentException($"Semantic vector recipe field '{propertyName}' must be an integer.");
        }

        return parsed;
    }

    private static int GetOptionalInt(JsonElement element, string propertyName, int defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return defaultValue;
        }

        if (!value.TryGetInt32(out var parsed))
        {
            throw new ArgumentException($"Semantic vector recipe field '{propertyName}' must be an integer when present.");
        }

        return parsed;
    }

    private static TEnum ParseSnakeCaseEnum<TEnum>(string value)
        where TEnum : struct, Enum
    {
        return Enum.Parse<TEnum>(SnakeToPascalCase(value), ignoreCase: false);
    }

    private static TEnum ParseFlags<TEnum>(IEnumerable<string> values)
        where TEnum : struct, Enum
    {
        ulong rawValue = 0;
        foreach (var value in values)
        {
            rawValue |= Convert.ToUInt64(ParseSnakeCaseEnum<TEnum>(value));
        }

        return (TEnum)Enum.ToObject(typeof(TEnum), rawValue);
    }

    private static byte ParseLossTolerancePolicy(string value)
    {
        if (!LossTolerancePolicyValues.TryGetValue(value, out var parsed))
        {
            throw new ArgumentException($"Unsupported loss tolerance policy in semantic vector recipe: {value}");
        }

        return parsed;
    }

    private static string SnakeToPascalCase(string value)
    {
        return string.Concat(
            value.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(static part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static byte[] Concat(params byte[][] blocks)
    {
        var totalLength = blocks.Sum(static block => block.Length);
        var payload = new byte[totalLength];
        var cursor = 0;
        foreach (var block in blocks)
        {
            Buffer.BlockCopy(block, 0, payload, cursor, block.Length);
            cursor += block.Length;
        }

        return payload;
    }

    private static ConformanceVectorEntry CreateVector(string name, string kind, string description, byte[] payload)
    {
        return new ConformanceVectorEntry
        {
            Name = name,
            Kind = kind,
            Description = description,
            Hex = Convert.ToHexString(payload).ToLowerInvariant(),
            Bytes = payload.Length,
        };
    }

    private sealed record ExportOptions(string ProtocolVersion, string RecipeManifestPath, string OutputPath);

    private sealed class ConformanceVectorManifest
    {
        [JsonPropertyName("protocol_version")]
        public string ProtocolVersion { get; set; } = string.Empty;

        [JsonPropertyName("generator")]
        public string? Generator { get; set; }

        [JsonPropertyName("vectors")]
        public List<ConformanceVectorEntry> Vectors { get; set; } = new();
    }

    private sealed class ConformanceVectorEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("kind")]
        public string Kind { get; set; } = string.Empty;

        [JsonPropertyName("hex")]
        public string Hex { get; set; } = string.Empty;

        [JsonPropertyName("bytes")]
        public int Bytes { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
