using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;

namespace Nnrp.BenchmarkAdapter;

public static class Program
{
    private const string ResultsSchemaUrl = "https://raw.githubusercontent.com/NagareWorks/nnrp-conformance/main/schemas/benchmark-results.schema.json";
    private const string DefaultSkipMessage = "This benchmark scenario is not implemented in the current C# baseline runner.";
    private const string NativeArtifactPathEnvironmentVariable = "NNRP_BENCHMARK_NATIVE_ARTIFACT_PATH";
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static long nextNativeSessionHandleId;

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
            "metadata_encode_decode" => RunMetadataEncodeDecode(id, workload),
            "runtime_control_encode_decode" => RunRuntimeControlEncodeDecode(id, workload),
            "runtime_object_encode_decode" => RunRuntimeObjectEncodeDecode(id, workload),
            "cache_reference_encode_decode" => RunCacheReferenceEncodeDecode(id, workload),
            "submit_result_metadata_encode_decode" => RunSubmitResultMetadataEncodeDecode(id, workload),
            "typed_payload_pack_unpack" => RunTypedPayloadPackUnpack(id, workload),
            "payload_snapshot_copy" => RunPayloadSnapshotCopy(id, workload),
            "borrowed_buffer_view" => RunBorrowedBufferView(id, workload),
            "runtime_probe" => RunRuntimeProbe(id, workload),
            "session_lifecycle" => RunSessionLifecycle(id, workload),
            "submit_result_loop" => RunSubmitResultLoop(id, workload),
            "transport_loopback" => RunTransportLoopback(id, workload),
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

    private static BenchmarkScenarioResult RunMetadataEncodeDecode(string id, JsonElement workload)
    {
        var iterations = GetPositiveInt(workload, "iterations", 100_000);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", Math.Min(10_000, iterations));
        var clientHello = new ClientHelloMetadata(
            minVersionMajor: 1,
            maxVersionMajor: 1,
            supportedWireFormatBitmap: 1,
            supportedProfileBitmap: 1,
            supportedPayloadKindBitmap: (uint)PayloadKind.Tensor,
            supportedCodecBitmap: (uint)CodecId.Raw,
            supportedCompressionBitmap: (uint)CodecId.Raw,
            supportedDTypeBitmap: 1u << (int)DTypeId.UInt8,
            supportedLayoutBitmap: 1u << (int)TensorLayoutId.Nhwc,
            cacheDigestBitmap: 0,
            cacheObjectBitmap: 0,
            cacheNamespaceCount: 0,
            maxLaneCount: 2,
            maxCacheEntries: 0,
            maxCacheBytes: 0,
            targetCadenceX100: 6000,
            latencyBudgetMilliseconds: 16,
            qualityTier: 1,
            degradePolicy: 0,
            requestedSessionId: 41,
            authBytes: 0,
            controlExtensionBytes: 0);
        var serverAck = new ServerHelloAckMetadata(
            selectedVersionMajor: 1,
            selectedWireFormat: NnrpHeader.CurrentWireFormat,
            authStatus: 0,
            reserved0: 0,
            sessionId: 41,
            acceptedProfileBitmap: 1,
            acceptedPayloadKindBitmap: (uint)PayloadKind.Tensor,
            acceptedCodecBitmap: (uint)CodecId.Raw,
            acceptedCompressionBitmap: (uint)CodecId.Raw,
            acceptedDTypeBitmap: 1u << (int)DTypeId.UInt8,
            acceptedLayoutBitmap: 1u << (int)TensorLayoutId.Nhwc,
            cacheDigestBitmap: 0,
            cacheObjectBitmap: 0,
            maxCacheEntries: 0,
            maxCacheBytes: 0,
            maxLaneCount: 2,
            maxConcurrentFrames: 4,
            targetCadenceX100: 6000,
            latencyBudgetMilliseconds: 16,
            qualityTier: 1,
            degradePolicy: 0,
            maxBodyBytes: 1u << 20,
            tokenTtlMilliseconds: 30000,
            retryAfterMilliseconds: 0,
            controlExtensionBytes: 0,
            serverFlags: 0);
        var helloBuffer = new byte[ClientHelloMetadata.MetadataLength];
        var ackBuffer = new byte[ServerHelloAckMetadata.MetadataLength];

        void Operation()
        {
            clientHello.Write(helloBuffer);
            serverAck.Write(ackBuffer);
            if (!ClientHelloMetadata.TryParse(helloBuffer, out var decodedHello, out _)
                || !ServerHelloAckMetadata.TryParse(ackBuffer, out var decodedAck, out _)
                || !decodedHello.Equals(clientHello)
                || !decodedAck.Equals(serverAck))
            {
                throw new InvalidOperationException("Metadata benchmark roundtrip mismatch.");
            }
        }

        for (var index = 0; index < warmupIterations; index += 1)
        {
            Operation();
        }

        var samples = MeasureMicroseconds(Operation, iterations);
        return MeasuredLatencyResult(id, samples);
    }

    private static BenchmarkScenarioResult RunRuntimeControlEncodeDecode(string id, JsonElement workload)
    {
        var iterations = GetPositiveInt(workload, "iterations", 100_000);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", Math.Min(10_000, iterations));
        var diagnostic = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var metadata = new ControlRequestMetadata(
            OperationId: 41,
            ControlSequence: 7,
            ReasonCode: 2,
            SourceRole: RuntimeRole.Client,
            Flags: 0,
            DiagnosticBytes: (uint)diagnostic.Length);

        void Operation()
        {
            var encoded = NnrpRuntimeControl.Encode(MessageType.Cancel, metadata, diagnostic);
            var decoded = NnrpRuntimeControl.Decode(MessageType.Cancel, encoded);
            if (decoded.GetMetadata<ControlRequestMetadata>() != metadata
                || !decoded.Tail.Span.SequenceEqual(diagnostic))
            {
                throw new InvalidOperationException("Runtime-control benchmark roundtrip mismatch.");
            }
        }

        WarmUp(Operation, warmupIterations);
        return MeasuredLatencyResult(id, MeasureLatencyWithAllocations(Operation, iterations));
    }

    private static BenchmarkScenarioResult RunRuntimeObjectEncodeDecode(string id, JsonElement workload)
    {
        var iterations = GetPositiveInt(workload, "iterations", 100_000);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", Math.Min(10_000, iterations));
        var descriptorTail = new byte[] { 1, 2, 3, 4 };
        var referenceTail = new byte[] { 5, 6, 7, 8 };
        var releaseTail = new byte[] { 9, 10 };
        var deltaTail = new byte[] { 11, 12, 13, 14, 15, 16 };
        var descriptor = new ObjectDescriptorMetadata(
            ObjectId: 81,
            ObjectKind: RuntimeObjectKind.Tensor,
            ProducerRole: RuntimeRole.Client,
            ConsumerRole: RuntimeRole.Server,
            SessionId: 41,
            ByteSize: 4096,
            ComputeCostUnits: 5,
            MemoryLocationHint: MemoryLocationHint.HostMemory,
            OwnershipHint: OwnershipHint.TransferOnRef,
            LifetimeHintMs: 30_000,
            MetadataBytes: (uint)descriptorTail.Length);
        var reference = new ObjectReferenceMetadata(
            ObjectId: 81,
            OperationId: 42,
            ObjectVersion: 3,
            Offset: 0,
            Length: 4096,
            Flags: 0,
            MetadataBytes: (uint)referenceTail.Length);
        var release = new ObjectReleaseMetadata(
            ObjectId: 81,
            OperationId: 42,
            ReleaseReason: ObjectReleaseReason.Completed,
            SourceRole: RuntimeRole.Client,
            Flags: 0,
            DiagnosticBytes: (uint)releaseTail.Length);
        var delta = new ObjectDeltaMetadata(
            ObjectId: 81,
            DeltaSequence: 4,
            RegionOffset: 128,
            RegionBytes: 2,
            DeltaBytes: 4,
            Flags: 0,
            MetadataBytes: 2);

        void Operation()
        {
            AssertRuntimeObjectRoundtrip(MessageType.ObjectDeclare, descriptor, descriptorTail);
            AssertRuntimeObjectRoundtrip(MessageType.ObjectRef, reference, referenceTail);
            AssertRuntimeObjectRoundtrip(MessageType.ObjectRelease, release, releaseTail);
            AssertRuntimeObjectRoundtrip(MessageType.ObjectDelta, delta, deltaTail);
        }

        WarmUp(Operation, warmupIterations);
        return MeasuredLatencyResult(id, MeasureLatencyWithAllocations(Operation, iterations));
    }

    private static void AssertRuntimeObjectRoundtrip<TMetadata>(
        MessageType messageType,
        TMetadata metadata,
        ReadOnlySpan<byte> tail)
        where TMetadata : struct, IRuntimeObjectMetadata
    {
        var encoded = NnrpRuntimeObject.Encode(messageType, metadata, tail);
        var decoded = NnrpRuntimeObject.Decode(messageType, encoded);
        if (!EqualityComparer<TMetadata>.Default.Equals(decoded.GetMetadata<TMetadata>(), metadata)
            || !decoded.Tail.Span.SequenceEqual(tail))
        {
            throw new InvalidOperationException(messageType + " runtime-object benchmark roundtrip mismatch.");
        }
    }

    private static BenchmarkScenarioResult RunCacheReferenceEncodeDecode(string id, JsonElement workload)
    {
        var iterations = GetPositiveInt(workload, "iterations", 100_000);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", Math.Min(10_000, iterations));
        var referenceTail = new byte[] { 1, 2, 3, 4 };
        var missTail = new byte[] { 5, 6 };
        var reference = new CacheReferenceMetadata(
            CacheNamespace: 7,
            CacheKeyHi: 0x1122334455667788,
            CacheKeyLo: 0x8877665544332211,
            ProfileId: 1,
            ReuseScope: CacheReuseScope.Session,
            LeaseId: 9,
            ProducerTraceId: 10,
            ExpirationHintMs: 30_000,
            MetadataBytes: (uint)referenceTail.Length,
            Flags: 0);
        var miss = new CacheMissMetadata(
            CacheNamespace: 7,
            CacheKeyHi: 0x1122334455667788,
            CacheKeyLo: 0x8877665544332211,
            MissReason: CacheMissReason.NotFound,
            ProfileId: 1,
            DiagnosticBytes: (uint)missTail.Length);
        var invalidate = new CacheInvalidateMetadata(
            CacheInvalidateScope.ObjectKey,
            cacheNamespace: 7,
            cacheKeyHigh: 0x1122334455667788,
            cacheKeyLow: 0x8877665544332211,
            reasonCode: 3);

        void Operation()
        {
            AssertRuntimeObjectRoundtrip(MessageType.CacheReference, reference, referenceTail);
            AssertRuntimeObjectRoundtrip(MessageType.CacheMiss, miss, missTail);
            var encodedInvalidate = invalidate.ToArray();
            if (!CacheInvalidateMetadata.TryParse(encodedInvalidate, out var decodedInvalidate)
                || !decodedInvalidate.Equals(invalidate))
            {
                throw new InvalidOperationException("Cache-invalidate benchmark roundtrip mismatch.");
            }
        }

        WarmUp(Operation, warmupIterations);
        return MeasuredLatencyResult(id, MeasureLatencyWithAllocations(Operation, iterations));
    }

    private static BenchmarkScenarioResult RunSubmitResultMetadataEncodeDecode(string id, JsonElement workload)
    {
        var iterations = GetPositiveInt(workload, "iterations", 100_000);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", Math.Min(10_000, iterations));
        var (submitPacket, resultPacket) = BuildSubmitResultMessages();
        var submitHeader = submitPacket.Header;
        var submitMetadata = submitPacket.Metadata.ToArray();
        var resultHeader = resultPacket.Header;
        var resultMetadata = resultPacket.Metadata.ToArray();

        void Operation()
        {
            var submitHeaderBuffer = new byte[NnrpHeader.HeaderLength];
            var resultHeaderBuffer = new byte[NnrpHeader.HeaderLength];
            submitHeader.Write(submitHeaderBuffer);
            resultHeader.Write(resultHeaderBuffer);

            if (!NnrpHeader.TryParse(submitHeaderBuffer, NnrpHeaderParseOptions.Strict, out var decodedSubmitHeader, out _)
                || !NnrpHeader.TryParse(resultHeaderBuffer, NnrpHeaderParseOptions.Strict, out var decodedResultHeader, out _)
                || !FrameSubmitMetadata.TryParse(submitMetadata, strict: true, out _, out _)
                || !ResultPushMetadata.TryParse(resultMetadata, strict: true, out _, out _)
                || decodedSubmitHeader.MessageType != MessageType.FrameSubmit
                || decodedResultHeader.MessageType != MessageType.ResultPush)
            {
                throw new InvalidOperationException("Submit/result metadata benchmark roundtrip mismatch.");
            }
        }

        for (var index = 0; index < warmupIterations; index += 1)
        {
            Operation();
        }

        return MeasuredLatencyResult(id, MeasureMicroseconds(Operation, iterations));
    }

    private static BenchmarkScenarioResult RunTypedPayloadPackUnpack(string id, JsonElement workload)
    {
        var iterations = GetPositiveInt(workload, "iterations", 100_000);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", Math.Min(10_000, iterations));
        var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 41, frameId: 303);
        var tileIds = submit.TileIds.ToArray();
        var section = submit.Sections.Span[0];

        void Operation()
        {
            var tileIndex = TileIndexBlockCodec.Encode(tileIds, TileIndexMode.RawUInt16);
            var decodedTileIds = TileIndexBlockCodec.Decode(tileIndex, TileIndexMode.RawUInt16, tileIds.Length);
            if (decodedTileIds.Length != tileIds.Length)
            {
                throw new InvalidOperationException("Typed payload benchmark tile index mismatch.");
            }

            var sectionPayload = section.ToArray();
            if (!TensorSectionBlock.TryParse(sectionPayload, tileIds.Length, out _, out var sectionBytes, out _)
                || sectionBytes != sectionPayload.Length)
            {
                throw new InvalidOperationException("Typed payload benchmark tensor section mismatch.");
            }
        }

        for (var index = 0; index < warmupIterations; index += 1)
        {
            Operation();
        }

        return MeasuredLatencyResult(id, MeasureMicroseconds(Operation, iterations));
    }

    private static BenchmarkScenarioResult RunRuntimeProbe(string id, JsonElement workload)
    {
        var artifactPath = ResolveNativeArtifactPath();
        if (artifactPath == null)
        {
            return NativeUnavailableResult(id);
        }

        var iterations = GetPositiveInt(workload, "iterations", 100_000);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", Math.Min(10_000, iterations));

        using var entrypoints = NnrpNativeRuntimeEntrypoints.Load(artifactPath);

        void Operation()
        {
            var version = entrypoints.CurrentProtocolVersion();
            var capabilities = entrypoints.RuntimeCapabilities();
            if (version.Major != NnrpHeader.CurrentVersionMajor
                || (capabilities.TransportSlots & NnrpNativeArtifact.TransportSlotTcp) == 0)
            {
                throw new InvalidOperationException("Native runtime probe benchmark mismatch.");
            }
        }

        for (var index = 0; index < warmupIterations; index += 1)
        {
            Operation();
        }

        return MeasuredLatencyResult(id, MeasureMicroseconds(Operation, iterations));
    }

    private static BenchmarkScenarioResult RunPayloadSnapshotCopy(string id, JsonElement workload)
    {
        var iterations = GetPositiveInt(workload, "iterations", 100_000);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", Math.Min(10_000, iterations));
        var payloadBytes = GetPositiveInt(workload, "payload_bytes", 4_096);
        var payload = new byte[payloadBytes];
        Array.Fill(payload, (byte)'x');
        var payloadHandle = GCHandle.Alloc(payload, GCHandleType.Pinned);

        try
        {
            var ffiEvent = new NnrpEvent(
                kind: 6,
                messageType: (uint)MessageType.ResultPush,
                NnrpHandle.Invalid,
                NnrpHandle.Invalid,
                NnrpHandle.Invalid,
                frameId: 1,
                payloadOwner: NnrpHandle.Invalid,
                new NnrpBufferView(payloadHandle.AddrOfPinnedObject(), new UIntPtr((uint)payload.Length)),
                default);

            void Operation()
            {
                var snapshot = NnrpNativeRuntimeEvent.FromFfi(ffiEvent);
                if (snapshot.PayloadMemory.Length != payload.Length
                    || snapshot.PayloadSpan[0] != payload[0]
                    || snapshot.PayloadSpan[^1] != payload[^1])
                {
                    throw new InvalidOperationException("Managed payload snapshot benchmark copy mismatch.");
                }
            }

            for (var index = 0; index < warmupIterations; index += 1)
            {
                Operation();
            }

            return MeasuredLatencyResult(id, MeasureMicroseconds(Operation, iterations));
        }
        finally
        {
            payloadHandle.Free();
        }
    }

    private static BenchmarkScenarioResult RunBorrowedBufferView(string id, JsonElement workload)
    {
        var artifactPath = ResolveNativeArtifactPath();
        if (artifactPath == null)
        {
            return NativeUnavailableResult(id);
        }

        var iterations = GetPositiveInt(workload, "iterations", 100_000);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", Math.Min(10_000, iterations));
        var payloadBytes = GetPositiveInt(workload, "payload_bytes", 4_096);
        var payload = new byte[payloadBytes];
        Array.Fill(payload, (byte)'x');
        using var entrypoints = NnrpNativeRuntimeEntrypoints.Load(artifactPath);
        using var nativeBuffer = new NnrpNativeBuffers(entrypoints).AcquireCopy(payload);

        void Operation()
        {
            var view = nativeBuffer.BorrowView();
            if (view.Pointer == IntPtr.Zero || view.Length.ToUInt64() != (ulong)payload.Length)
            {
                throw new InvalidOperationException("Borrowed native buffer benchmark view mismatch.");
            }
        }

        for (var index = 0; index < warmupIterations; index += 1)
        {
            Operation();
        }

        return MeasuredLatencyResult(id, MeasureMicroseconds(Operation, iterations));
    }

    private static BenchmarkScenarioResult RunSessionLifecycle(string id, JsonElement workload)
    {
        var artifactPath = ResolveNativeArtifactPath();
        if (artifactPath == null)
        {
            return NativeUnavailableResult(id);
        }

        var iterations = GetPositiveInt(workload, "iterations", 100_000);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", Math.Min(10_000, iterations));
        var nextConnectionId = 1UL;
        var nextSessionId = 41U;

        void Operation()
        {
            using var host = OpenNativeSessionHost(
                artifactPath,
                NnrpNativeArtifact.TransportSlotTcp,
                nextConnectionId++,
                nextSessionId++);
            host.Close();
        }

        for (var index = 0; index < warmupIterations; index += 1)
        {
            Operation();
        }

        return MeasuredLatencyResult(id, MeasureMicroseconds(Operation, iterations));
    }

    private static BenchmarkScenarioResult RunSubmitResultLoop(string id, JsonElement workload)
    {
        var artifactPath = ResolveNativeArtifactPath();
        if (artifactPath == null)
        {
            return NativeUnavailableResult(id);
        }

        var durationSeconds = GetPositiveDouble(workload, "duration_seconds", 10.0);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", 1_000);
        var batchSize = GetPositiveInt(workload, "batch_size", 1024);
        var payloadBytes = GetPositiveInt(workload, "payload_bytes", 1024);
        var payload = new byte[payloadBytes];
        Array.Fill(payload, (byte)'x');
        var nextOperationId = 1UL;
        var nextFrameId = 1U;
        using var host = OpenNativeSessionHost(
            artifactPath,
            NnrpNativeArtifact.TransportSlotTcp,
            connectionId: 1,
            sessionId: 41);

        long Operation()
        {
            var completed = host.SubmitResultCompactBatch(
                nextOperationId,
                nextFrameId,
                frameIdStride: 1,
                payload,
                payload,
                maxEvents: batchSize * 2,
                iterations: batchSize);
            if (completed != (ulong)batchSize)
            {
                throw new InvalidOperationException("Native submit/result batch completed an unexpected number of operations.");
            }

            nextOperationId += completed;
            nextFrameId += (uint)completed;
            return (long)completed;
        }

        for (var index = 0; index < warmupIterations; index += 1)
        {
            Operation();
        }

        return MeasuredThroughputResult(id, MeasureThroughputItemsPerSecond(Operation, durationSeconds));
    }

    private static BenchmarkScenarioResult RunTransportLoopback(string id, JsonElement workload)
    {
        var durationSeconds = GetPositiveDouble(workload, "duration_seconds", 10.0);
        var warmupIterations = GetNonNegativeInt(workload, "warmup_iterations", 1_000);
        var batchSize = GetPositiveInt(workload, "batch_size", 1024);
        var payloadBytes = GetPositiveInt(workload, "payload_bytes", GetPositiveInt(workload, "probe_payload_bytes", 1024));
        var payload = new byte[payloadBytes];
        Array.Fill(payload, (byte)'x');
        var transportSlot = NativeTransportSlot(workload);
        var artifactPath = ResolveNativeArtifactPath(transportSlot);
        if (artifactPath == null)
        {
            return NativeUnavailableResult(id);
        }

        var nextOperationId = 1UL;
        var nextFrameId = 1U;
        using var host = OpenNativeSessionHost(
            artifactPath,
            transportSlot,
            connectionId: transportSlot == NnrpNativeArtifact.TransportSlotQuic ? 2UL : 1UL,
            sessionId: transportSlot == NnrpNativeArtifact.TransportSlotQuic ? 42U : 41U);

        long Operation()
        {
            var completed = host.SubmitResultCompactBatch(
                nextOperationId,
                nextFrameId,
                frameIdStride: 1,
                payload,
                payload,
                maxEvents: batchSize * 2,
                iterations: batchSize);
            if (completed != (ulong)batchSize)
            {
                throw new InvalidOperationException("Native transport benchmark completed an unexpected number of operations.");
            }

            nextOperationId += completed;
            nextFrameId += (uint)completed;
            return (long)completed;
        }

        for (var index = 0; index < warmupIterations; index += 1)
        {
            Operation();
        }

        return MeasuredThroughputResult(id, MeasureThroughputItemsPerSecond(Operation, durationSeconds));
    }

    private static (byte[] SubmitPacket, byte[] ResultPacket) BuildSubmitResultPackets()
    {
        var (submit, result) = BuildSubmitResultMessages();
        return (submit.ToArray(), result.ToArray());
    }

    private static (FrameSubmitMessage Submit, ResultPushMessage Result) BuildSubmitResultMessages()
    {
        var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 41, frameId: 303);
        var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(submit.TileIds.Span, TileIndexMode.RawUInt16);
        var resultMetadata = new ResultPushMetadata(
            ResultStatusCode.Success,
            ResultFlags.None,
            sectionCount: (ushort)submit.Sections.Length,
            tileCount: (ushort)submit.TileIds.Length,
            activeProfileId: 0,
            inferenceMilliseconds: 4,
            queueMilliseconds: 1,
            serverTotalMilliseconds: 5,
            tileBaseId: 0,
            tileIndexBytes: (uint)tileIndexBytes);
        var result = new ResultPushMessage(
            new NnrpHeader(
                NnrpHeader.CurrentVersionMajor,
                MessageType.ResultPush,
                HeaderFlags.None,
                ResultPushMetadata.MetadataLength,
                0,
                sessionId: 41,
                frameId: 303,
                viewId: 0,
                routeId: 0,
                traceId: 0),
            resultMetadata,
            submit.TileIds,
            submit.Sections);

        return (submit, result);
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

    private static void WarmUp(Action operation, int iterations)
    {
        for (var index = 0; index < iterations; index += 1)
        {
            operation();
        }
    }

    private static LatencyMeasurement MeasureLatencyWithAllocations(Action operation, int iterations)
    {
        var samples = MeasureMicroseconds(operation, iterations);
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < iterations; index += 1)
        {
            operation();
        }

        var allocatedBytes = checked(GC.GetAllocatedBytesForCurrentThread() - allocatedBefore);
        return new LatencyMeasurement(samples, (double)allocatedBytes / iterations);
    }

    private static double MeasureThroughputOpsPerSecond(Action operation, double durationSeconds)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(durationSeconds * Stopwatch.Frequency);
        var completed = 0L;
        while (Stopwatch.GetTimestamp() < deadline)
        {
            operation();
            completed += 1;
        }

        return completed / durationSeconds;
    }

    private static double MeasureThroughputItemsPerSecond(Func<long> operation, double durationSeconds)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(durationSeconds * Stopwatch.Frequency);
        var completed = 0L;
        while (Stopwatch.GetTimestamp() < deadline)
        {
            completed += operation();
        }

        return completed / durationSeconds;
    }

    private static string? ResolveNativeArtifactPath(uint? transportSlot = null)
    {
        if (transportSlot == NnrpNativeArtifact.TransportSlotTcp)
        {
            var tcpPath = Environment.GetEnvironmentVariable("NNRP_BENCHMARK_NATIVE_TCP_ARTIFACT_PATH");
            if (!string.IsNullOrWhiteSpace(tcpPath) && File.Exists(tcpPath))
            {
                return tcpPath;
            }
        }
        else if (transportSlot == NnrpNativeArtifact.TransportSlotQuic)
        {
            var quicPath = Environment.GetEnvironmentVariable("NNRP_BENCHMARK_NATIVE_QUIC_ARTIFACT_PATH");
            if (!string.IsNullOrWhiteSpace(quicPath) && File.Exists(quicPath))
            {
                return quicPath;
            }
        }

        foreach (var variable in new[]
        {
            NativeArtifactPathEnvironmentVariable,
            "NNRP_NATIVE_ARTIFACT_PATH",
            "NNRP_NATIVE_LIBRARY",
        })
        {
            var configured = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
            {
                return configured;
            }
        }

        try
        {
            var resolved = NnrpNativeArtifact.Resolve(
                Environment.GetEnvironmentVariable(NnrpNativeArtifact.ArtifactRootEnvironmentVariable));
            return File.Exists(resolved) ? resolved : null;
        }
        catch (NnrpNativeArtifactException)
        {
            return null;
        }
    }

    private static BenchmarkScenarioResult NativeUnavailableResult(string id)
    {
        return new BenchmarkScenarioResult
        {
            Id = id,
            Outcome = "skip",
            Message = "Native benchmark artifact is required; set NNRP_BENCHMARK_NATIVE_ARTIFACT_PATH or NNRP_NATIVE_ARTIFACT_ROOT.",
        };
    }

    private static NativeBenchmarkSessionHost OpenNativeSessionHost(
        string artifactPath,
        uint transportSlot,
        ulong connectionId,
        uint sessionId)
    {
        var transportScope = NnrpNativeArtifact.TransportScopeFromTransportId(transportSlot);
        var entrypoints = NnrpNativeRuntimeEntrypoints.Load(
            artifactPath,
            requiredTransportSlots: transportSlot,
            transportScope: transportScope);
        try
        {
            entrypoints.ConnectionBootstrap(
                new NnrpConnectionBootstrap(connectionId, 1, transportSlot),
                out var connectionHandle).ThrowIfError();
            var connection = new NnrpNativeRuntimeConnection(
                entrypoints,
                new NnrpConnectionHandle(connectionHandle));
            try
            {
                var session = connection.OpenSession(
                    sessionId,
                    AllocateNativeSessionHandleId(),
                    generation: 1,
                    profileId: 0,
                    SessionPriorityClass.Balanced,
                    schemaId: 0,
                    schemaVersion: 0,
                    defaultDeadlineMilliseconds: 500,
                    maxInFlightOperations: 4,
                    leaseTtlHintMilliseconds: 30_000,
                    allowResume: false,
                    resumeTokenBytes: 0,
                    Array.Empty<CacheObjectKind>());
                return new NativeBenchmarkSessionHost(entrypoints, connection, session);
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }
        catch
        {
            entrypoints.Dispose();
            throw;
        }
    }

    private static ulong AllocateNativeSessionHandleId()
    {
        var allocated = Interlocked.Increment(ref nextNativeSessionHandleId);
        if (allocated <= 0)
        {
            throw new InvalidOperationException("The benchmark native session handle allocator is exhausted.");
        }

        return checked((ulong)allocated);
    }

    private sealed class NativeBenchmarkSessionHost : IDisposable
    {
        private readonly NnrpNativeRuntimeEntrypoints entrypoints;
        private readonly NnrpNativeRuntimeConnection connection;
        private readonly NnrpNativeRuntimeSession session;
        private bool isClosed;

        internal NativeBenchmarkSessionHost(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpNativeRuntimeConnection connection,
            NnrpNativeRuntimeSession session)
        {
            this.entrypoints = entrypoints;
            this.connection = connection;
            this.session = session;
        }

        internal ulong SubmitResultCompactBatch(
            ulong operationIdStart,
            uint frameIdStart,
            uint frameIdStride,
            ReadOnlyMemory<byte> submitPayload,
            ReadOnlyMemory<byte> resultPayload,
            int maxEvents,
            int iterations)
        {
            return session.SubmitResultCompactBatch(
                operationIdStart,
                frameIdStart,
                frameIdStride,
                submitPayload,
                resultPayload,
                maxEvents,
                iterations);
        }

        internal void Close()
        {
            if (isClosed)
            {
                return;
            }

            try
            {
                session.Close();
            }
            finally
            {
                try
                {
                    connection.Dispose();
                }
                finally
                {
                    entrypoints.Dispose();
                    isClosed = true;
                }
            }
        }

        public void Dispose()
        {
            Close();
        }
    }

    private static uint NativeTransportSlot(JsonElement workload)
    {
        if (!workload.TryGetProperty("transport", out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return NnrpNativeArtifact.TransportSlotTcp;
        }

        var value = property.GetString();
        return string.Equals(value, "quic", StringComparison.OrdinalIgnoreCase)
            ? NnrpNativeArtifact.TransportSlotQuic
            : NnrpNativeArtifact.TransportSlotTcp;
    }

    private static BenchmarkScenarioResult MeasuredLatencyResult(string id, List<double> samples)
    {
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

    private static BenchmarkScenarioResult MeasuredLatencyResult(string id, LatencyMeasurement measurement)
    {
        return new BenchmarkScenarioResult
        {
            Id = id,
            Outcome = "measured",
            Metrics = new BenchmarkMetrics
            {
                P50Microseconds = Percentile(measurement.Samples, 50),
                P95Microseconds = Percentile(measurement.Samples, 95),
                P99Microseconds = Percentile(measurement.Samples, 99),
                GcAllocatedBytesPerOperation = measurement.AllocatedBytesPerOperation,
            },
        };
    }

    private static BenchmarkScenarioResult MeasuredThroughputResult(string id, double throughputOpsPerSecond)
    {
        return new BenchmarkScenarioResult
        {
            Id = id,
            Outcome = "measured",
            Metrics = new BenchmarkMetrics
            {
                ThroughputOpsPerSecond = throughputOpsPerSecond,
            },
        };
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

    private static double GetPositiveDouble(JsonElement element, string propertyName, double defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return defaultValue;
        }

        if (property.ValueKind != JsonValueKind.Number || !property.TryGetDouble(out var value) || value <= 0)
        {
            throw new ArgumentException($"Benchmark workload field '{propertyName}' must be a positive number.");
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

    private sealed record LatencyMeasurement(List<double> Samples, double AllocatedBytesPerOperation);

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

        [JsonPropertyName("throughput_ops_per_sec")]
        public double? ThroughputOpsPerSecond { get; init; }

        [JsonPropertyName("gc_alloc_bytes_per_op")]
        public double? GcAllocatedBytesPerOperation { get; init; }
    }
}
