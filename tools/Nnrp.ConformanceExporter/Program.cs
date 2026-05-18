using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nnrp.Core;

namespace Nnrp.ConformanceExporter;

internal static class Program
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private static int Main(string[] args)
    {
        var options = ParseArguments(args);
        var manifest = BuildManifest(options.ProtocolVersion);
        var outputDirectory = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
        File.WriteAllText(options.OutputPath, json + Environment.NewLine, Utf8WithoutBom);
        return 0;
    }

    private static ExportOptions ParseArguments(string[] args)
    {
        string? protocolVersion = null;
        string? outputPath = null;

        for (var index = 0; index < args.Length; index += 1)
        {
            switch (args[index])
            {
                case "--protocol-version":
                    protocolVersion = RequireValue(args, ref index, "--protocol-version");
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

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("--output is required.");
        }

        return new ExportOptions(protocolVersion, outputPath);
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

    private static ConformanceVectorManifest BuildManifest(string protocolVersion)
    {
        if (!string.Equals(protocolVersion, "nnrp-1-preview2", StringComparison.Ordinal))
        {
            throw new ArgumentException($"Unsupported protocol version for C# conformance export: {protocolVersion}");
        }

        return new ConformanceVectorManifest
        {
            ProtocolVersion = protocolVersion,
            Generator = "nnrp-cs",
            Vectors = new List<ConformanceVectorEntry>
            {
                CreateVector(
                    "current.header.frame_submit_ack_required_keyframe",
                    "header",
                    "Preview2 common header golden vector for a FRAME_SUBMIT keyframe with ACK_REQUIRED.",
                    new NnrpHeader(
                        versionMajor: NnrpHeader.CurrentVersionMajor,
                        wireFormat: NnrpHeader.CurrentWireFormat,
                        messageType: MessageType.FrameSubmit,
                        flags: HeaderFlags.AckRequired | HeaderFlags.Keyframe,
                        metaLength: 48,
                        bodyLength: 4096,
                        sessionId: 7,
                        frameId: 11,
                        viewId: 2,
                        routeId: 0,
                        traceId: 123456789).ToArray()),
                CreateVector(
                    "current.metadata.client_hello",
                    "metadata",
                    "Preview2 CLIENT_HELLO fixed metadata golden vector.",
                    new ClientHelloMetadata(
                        minVersionMajor: 1,
                        maxVersionMajor: 1,
                        supportedWireFormatBitmap: 1,
                        supportedProfileBitmap: 1,
                        supportedPayloadKindBitmap: (uint)PayloadKind.Tensor,
                        supportedCodecBitmap: 7,
                        supportedCompressionBitmap: 3,
                        supportedDTypeBitmap: 31,
                        supportedLayoutBitmap: 3,
                        cacheDigestBitmap: 1,
                        cacheObjectBitmap: 7,
                        cacheNamespaceCount: 4,
                        maxLaneCount: 2,
                        maxCacheEntries: 256,
                        maxCacheBytes: 8388608,
                        targetCadenceX100: 6000,
                        latencyBudgetMilliseconds: 100,
                        qualityTier: 2,
                        degradePolicy: 2,
                        requestedSessionId: 0,
                        authBytes: 96,
                        controlExtensionBytes: 0).ToArray()),
                CreateVector(
                    "current.metadata.session_patch_ack",
                    "metadata",
                    "Preview2 SESSION_PATCH_ACK fixed metadata golden vector.",
                    new SessionPatchAckMetadata(
                        ackStatus: SessionPatchAckStatus.PartiallyApplied,
                        rejectReason: SessionPatchRejectReason.UnsupportedStrategy,
                        appliedPatchMask: SessionPatchField.TargetCadence | SessionPatchField.QualityTier | SessionPatchField.ActiveLaneMask,
                        rejectedPatchMask: SessionPatchField.PreferredCodec,
                        retryAfterMilliseconds: 0,
                        effectiveProfileId: 1,
                        effectiveTargetCadenceX100: 9000,
                        effectiveQualityTier: 2,
                        effectiveDegradePolicy: 2,
                        effectiveLaneMask: 3,
                        preferredCodecBitmap: 1,
                        preferredCompressionBitmap: 3,
                        profilePatchAckBytes: 0,
                        reserved0: 0).ToArray()),
                CreateVector(
                    "current.packet.flow_update",
                    "packet",
                    "Preview2 FLOW_UPDATE packet golden vector.",
                    new FlowUpdateMessage(
                        new NnrpHeader(
                            versionMajor: NnrpHeader.CurrentVersionMajor,
                            wireFormat: NnrpHeader.CurrentWireFormat,
                            messageType: MessageType.FlowUpdate,
                            flags: HeaderFlags.None,
                            metaLength: FlowUpdateMetadata.MetadataLength,
                            bodyLength: 0,
                            sessionId: 21,
                            frameId: 0,
                            viewId: 0,
                            routeId: 6,
                            traceId: 13),
                        new FlowUpdateMetadata(
                            scopeKind: FlowUpdateScopeKind.Session,
                            updateReason: FlowUpdateReason.Congestion,
                            backpressureLevel: FlowUpdateBackpressureLevel.Hard,
                            connectionCredit: 0,
                            sessionCredit: 1,
                            operationCredit: 0,
                            operationId: 0,
                            retryAfterMilliseconds: 40,
                            creditEpoch: 5,
                            flags: FlowUpdateFlags.CreditValid | FlowUpdateFlags.RetryAfterValid)).ToArray()),
                CreateVector(
                    "current.packet.result_hint",
                    "packet",
                    "Preview2 RESULT_HINT packet golden vector.",
                    new ResultHintMessage(
                        new NnrpHeader(
                            versionMajor: NnrpHeader.CurrentVersionMajor,
                            wireFormat: NnrpHeader.CurrentWireFormat,
                            messageType: MessageType.ResultHint,
                            flags: HeaderFlags.None,
                            metaLength: ResultHintMetadata.MetadataLength,
                            bodyLength: 0,
                            sessionId: 21,
                            frameId: 303,
                            viewId: 0,
                            routeId: 7,
                            traceId: 14),
                        new ResultHintMetadata(
                            appliedBudgetPolicy: ResultHintBudgetPolicy.StaleReuse,
                            congestionState: ResultHintCongestionState.Saturated,
                            reason: ResultHintReason.BudgetExceeded,
                            retryAfterMilliseconds: 60)).ToArray()),
                CreateVector(
                    "current.metadata.frame_submit",
                    "metadata",
                    "Preview2 FRAME_SUBMIT fixed metadata golden vector for mixed submit mode.",
                    new FrameSubmitMetadata(
                        sourceWidth: 640,
                        sourceHeight: 360,
                        tileWidth: 32,
                        tileHeight: 32,
                        tileCount: 84,
                        sectionCount: 2,
                        frameClass: FrameClass.Delta,
                        inputProfile: InputProfile.DenseLumaFrame,
                        tileIndexMode: TileIndexMode.DenseRange,
                        reserved0: 0,
                        latencyBudgetMilliseconds: 100,
                        targetFpsTimes100: 6000,
                        retryOfFrame: 7,
                        tileBaseId: 0,
                        cameraBytes: 192,
                        tileIndexBytes: 0,
                        reserved1: 0,
                        reserved2: 0,
                        submitMode: SubmitMode.Mixed,
                        budgetPolicy: BudgetPolicy.AllowPartial | BudgetPolicy.AllowDegraded,
                        lossTolerancePolicy: 0xFF,
                        reserved3: 0,
                        objectRefMask: 3,
                        dependencyFrameId: 41,
                        payloadKindBitmap: PayloadKind.Tensor | PayloadKind.StructuredEvent,
                        payloadFrameCount: 2,
                        reserved4: 0).ToArray()),
                CreateVector(
                    "current.metadata.result_push",
                    "metadata",
                    "Preview2 RESULT_PUSH fixed metadata golden vector for partial stale-reuse results.",
                    new ResultPushMetadata(
                        statusCode: ResultStatusCode.Success,
                        resultFlags: ResultFlags.Partial,
                        sectionCount: 1,
                        tileCount: 84,
                        activeProfileId: 2,
                        inferenceMilliseconds: 843,
                        queueMilliseconds: 2,
                        serverTotalMilliseconds: 846,
                        tileBaseId: 0,
                        tileIndexBytes: 16,
                        resultClass: ResultClass.Partial,
                        appliedBudgetPolicy: BudgetPolicy.AllowPartial,
                        reusedFrameId: 41,
                        coveredTileCount: 53,
                        droppedTileCount: 31,
                        payloadKindBitmap: PayloadKind.Tensor | PayloadKind.TokenChunk,
                        payloadFrameCount: 3).ToArray()),
                CreateVector(
                    "current.body_region.prelude",
                    "body_region",
                    "Preview2 body-region prelude golden vector.",
                    new BodyRegionPrelude(
                        inlineObjectBytes: 24,
                        objectReferenceBytes: 16,
                        typedPayloadDescriptorBytes: 16,
                        typedPayloadFrameBytes: 14,
                        extensionDescriptorBytes: 16,
                        extensionPayloadBytes: 5,
                        bodyFlags: 0,
                        reserved: 0).ToArray()),
                CreateVector(
                    "current.object_reference.tile_index_block",
                    "object_reference",
                    "Preview2 object-reference block golden vector for a tile-index cache object.",
                    new ObjectReferenceBlock(
                        objectKind: CacheObjectKind.TileIndexBlock,
                        referenceFlags: 0,
                        cacheNamespace: 7,
                        cacheKeyHigh: 0x11223344,
                        cacheKeyLow: 0x55667788).ToArray()),
                CreateVector(
                    "current.typed_payload.descriptor",
                    "typed_payload_descriptor",
                    "Preview2 typed-payload descriptor golden vector.",
                    new TypedPayloadDescriptor(
                        payloadKind: PayloadKind.StructuredEvent,
                        descriptorFlags: 0,
                        profileId: 3,
                        payloadOffset: 4,
                        payloadLength: 7,
                        reserved: 0).ToArray()),
                CreateVector(
                    "current.typed_payload.frame_descriptor_region",
                    "typed_payload_descriptor_region",
                    "Preview2 typed-payload descriptor region golden vector for token/audio/video/event frames.",
                    Concat(
                        new TypedPayloadDescriptor(PayloadKind.TokenChunk, 0, 1, 0, 3, 0).ToArray(),
                        new TypedPayloadDescriptor(PayloadKind.AudioChunk, 0, 2, 3, 2, 0).ToArray(),
                        new TypedPayloadDescriptor(PayloadKind.VideoChunk, 0, 3, 5, 5, 0).ToArray(),
                        new TypedPayloadDescriptor(PayloadKind.StructuredEvent, 0, 4, 10, 3, 0).ToArray())),
                CreateVector(
                    "current.typed_payload.frame_region",
                    "typed_payload_frame_region",
                    "Preview2 typed-payload frame region golden vector for token/audio/video/event frames.",
                    Encoding.UTF8.GetBytes("tokauvideoevt")),
            },
        };
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

    private sealed record ExportOptions(string ProtocolVersion, string OutputPath);

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
