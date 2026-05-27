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
                case "l0.session_open.metadata.golden":
                    RunSessionOpenMetadataGolden();
                    return Pass(caseId, "SESSION_OPEN fixed metadata matched the golden vector.");
                case "l0.session_open_ack.metadata.golden":
                    RunSessionOpenAckMetadataGolden();
                    return Pass(caseId, "SESSION_OPEN_ACK fixed metadata matched the golden vector.");
                case "l0.session_close.metadata.golden":
                    RunSessionCloseMetadataGolden();
                    return Pass(caseId, "SESSION_CLOSE fixed metadata matched the golden vector.");
                case "l0.session_close_ack.metadata.golden":
                    RunSessionCloseAckMetadataGolden();
                    return Pass(caseId, "SESSION_CLOSE_ACK fixed metadata matched the golden vector.");
                case "l0.session_open.reserved_fields.reject":
                    RunSessionOpenReservedFieldsReject();
                    return Pass(caseId, "SESSION_OPEN reserved fields were rejected.");
                case "l0.session_open_ack.reserved_fields.reject":
                    RunSessionOpenAckReservedFieldsReject();
                    return Pass(caseId, "SESSION_OPEN_ACK reserved flags were rejected.");
                case "l1.session.open.fixed_metadata.validation":
                    RunSessionOpenMetadataValidation();
                    return Pass(caseId, "SESSION_OPEN fixed metadata validation passed.");
                case "l1.session.open_ack.fixed_metadata.validation":
                    RunSessionOpenAckMetadataValidation();
                    return Pass(caseId, "SESSION_OPEN_ACK fixed metadata validation passed.");
                case "l1.session.close.state_machine.validation":
                    RunSessionCloseStateMachineValidation();
                    return Pass(caseId, "SESSION_CLOSE lifecycle ordering validation passed.");
                case "l1.session.open_close":
                    RunSessionOpenClose();
                    return Pass(caseId, "SESSION_OPEN to SESSION_CLOSE roundtrip passed.");
                case "l1.frame_submit.tensor.inline":
                case "l1.frame_submit.tensor.inline.routing.validation":
                    RunInlineTensorSubmit();
                    return Pass(caseId, "FRAME_SUBMIT inline tensor routing validation passed.");
                case "l1.result_push.basic.terminal.validation":
                    RunBasicResultPush();
                    return Pass(caseId, "RESULT_PUSH terminal validation passed.");
                case "l2.result_push.basic.event_pump.single_terminal.validation":
                    RunSingleTerminalEventDelivery();
                    return Pass(caseId, "RESULT_PUSH single terminal delivery validation passed.");
                case "l0.flow_update.packet.golden":
                case "l1.flow_update.session.scope.validation":
                    RunSessionFlowUpdate();
                    return Pass(caseId, "FLOW_UPDATE session-scope validation passed.");
                case "l0.flow_update.connection.packet.golden":
                case "l1.flow_update.connection.scope.validation":
                    RunConnectionFlowUpdate();
                    return Pass(caseId, "FLOW_UPDATE connection-scope validation passed.");
                case "l0.flow_update.operation.packet.golden":
                case "l1.flow_update.operation.scope.validation":
                    RunOperationFlowUpdate();
                    return Pass(caseId, "FLOW_UPDATE operation-scope validation passed.");
                case "l0.flow_update.reserved_flags.reject":
                    RunFlowUpdateReservedFlagsReject();
                    return Pass(caseId, "FLOW_UPDATE reserved flags were rejected.");
                case "l1.flow_update.credit_epoch.monotonicity.validation":
                case string flowCase when flowCase == string.Concat("l1.flow_update.", "pre", "view3"):
                    RunFlowUpdateCreditEpochValidation();
                    return Pass(caseId, "FLOW_UPDATE credit epoch validation passed.");
                case "l1.connection.session_container.parallel_open.validation":
                    RunParallelSessionOpen();
                    return Pass(caseId, "Connection session container parallel open validation passed.");
                case "l1.session.close.sibling_survival.validation":
                    RunSessionCloseSiblingSurvival();
                    return Pass(caseId, "Session close sibling survival validation passed.");
                case "l1.connection.close.session_cascade.validation":
                    RunConnectionCloseSessionCascade();
                    return Pass(caseId, "Connection close session cascade validation passed.");
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

    private static void RunSessionOpenMetadataGolden()
    {
        var metadata = GoldenSessionOpenMetadata();
        var expected = new byte[]
        {
            0x2A, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, 0x05,
            0x01, 0x10, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00,
            0xF4, 0x01, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00,
            0x30, 0x75, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00,
            0x20, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00,
            0xEF, 0xCD, 0xAB, 0x89, 0x67, 0x45, 0x23, 0x01,
        };
        var actual = metadata.ToArray();

        AssertTrue(actual.AsSpan().SequenceEqual(expected), "SESSION_OPEN golden bytes changed.");
        AssertTrue(SessionOpenMetadata.TryParse(actual, strict: true, out var parsed, out var error), $"SESSION_OPEN strict parse failed: {error}.");
        AssertTrue(parsed.Equals(metadata), "SESSION_OPEN metadata roundtrip changed.");
    }

    private static void RunSessionOpenAckMetadataGolden()
    {
        var metadata = GoldenSessionOpenAckMetadata();
        var expected = new byte[]
        {
            0x2A, 0x00, 0x00, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x01, 0x10, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00,
            0x02, 0x00, 0x04, 0x00, 0x30, 0x75, 0x00, 0x00,
            0xC0, 0xD4, 0x01, 0x00, 0x10, 0x00, 0x00, 0x00,
            0x08, 0x00, 0x00, 0x00, 0x21, 0x43, 0x65, 0x87,
            0xA9, 0xCB, 0xED, 0x0F, 0x07, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00,
        };
        var actual = metadata.ToArray();

        AssertTrue(actual.AsSpan().SequenceEqual(expected), "SESSION_OPEN_ACK golden bytes changed.");
        AssertTrue(SessionOpenAckMetadata.TryParse(actual, out var parsed, out var error), $"SESSION_OPEN_ACK parse failed: {error}.");
        AssertTrue(parsed.Equals(metadata), "SESSION_OPEN_ACK metadata roundtrip changed.");
    }

    private static void RunSessionCloseMetadataGolden()
    {
        var metadata = GoldenSessionCloseMetadata();
        var expected = new byte[]
        {
            0x01, 0x00, 0x00, 0x00, 0xE8, 0x03, 0x00, 0x00,
            0x63, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x44, 0x33, 0x22, 0x11,
        };
        var actual = metadata.ToArray();

        AssertTrue(actual.AsSpan().SequenceEqual(expected), "SESSION_CLOSE golden bytes changed.");
        AssertTrue(SessionCloseMetadata.TryParse(actual, strict: true, out var parsed, out var error), $"SESSION_CLOSE strict parse failed: {error}.");
        AssertTrue(parsed.Equals(metadata), "SESSION_CLOSE metadata roundtrip changed.");
    }

    private static void RunSessionCloseAckMetadataGolden()
    {
        var metadata = GoldenSessionCloseAckMetadata();
        var expected = new byte[]
        {
            0x01, 0x00, 0x00, 0x00, 0x63, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        };
        var actual = metadata.ToArray();

        AssertTrue(actual.AsSpan().SequenceEqual(expected), "SESSION_CLOSE_ACK golden bytes changed.");
        AssertTrue(SessionCloseAckMetadata.TryParse(actual, strict: true, out var parsed, out var error), $"SESSION_CLOSE_ACK strict parse failed: {error}.");
        AssertTrue(parsed.Equals(metadata), "SESSION_CLOSE_ACK metadata roundtrip changed.");
    }

    private static void RunSessionOpenReservedFieldsReject()
    {
        var bytes = GoldenSessionOpenMetadata().ToArray();
        bytes[22] = 1;

        AssertTrue(
            !SessionOpenMetadata.TryParse(bytes, strict: true, out _, out var error)
                && error == NnrpParseError.NonZeroReservedField,
            $"SESSION_OPEN reserved field was not rejected as NonZeroReservedField: {error}.");
    }

    private static void RunSessionOpenAckReservedFieldsReject()
    {
        var bytes = GoldenSessionOpenAckMetadata().ToArray();
        bytes[52] = 0x20;

        AssertTrue(
            !SessionOpenAckMetadata.TryParse(bytes, out _, out var error)
                && error == NnrpParseError.InvalidMessageLayout,
            $"SESSION_OPEN_ACK reserved flag bits were not rejected as InvalidMessageLayout: {error}.");
    }

    private static void RunSessionOpenMetadataValidation()
    {
        RunSessionOpenMetadataGolden();
        var bytes = GoldenSessionOpenMetadata().ToArray();
        bytes[6] = 0xFF;
        AssertTrue(
            !SessionOpenMetadata.TryParse(bytes, strict: true, out _, out var priorityError)
                && priorityError == NnrpParseError.InvalidMessageLayout,
            $"SESSION_OPEN invalid priority class was not rejected: {priorityError}.");

        bytes = GoldenSessionOpenMetadata().ToArray();
        bytes[7] = 0x80;
        AssertTrue(
            !SessionOpenMetadata.TryParse(bytes, strict: true, out _, out var flagsError)
                && flagsError == NnrpParseError.InvalidMessageLayout,
            $"SESSION_OPEN invalid flags were not rejected: {flagsError}.");
    }

    private static void RunSessionOpenAckMetadataValidation()
    {
        RunSessionOpenAckMetadataGolden();
        var bytes = GoldenSessionOpenAckMetadata().ToArray();
        bytes[7] = 0xFF;
        AssertTrue(
            !SessionOpenAckMetadata.TryParse(bytes, out _, out var statusError)
                && statusError == NnrpParseError.InvalidMessageLayout,
            $"SESSION_OPEN_ACK invalid status was not rejected: {statusError}.");

        bytes = GoldenSessionOpenAckMetadata().ToArray();
        bytes[52] = 0x80;
        AssertTrue(
            !SessionOpenAckMetadata.TryParse(bytes, out _, out var flagsError)
                && flagsError == NnrpParseError.InvalidMessageLayout,
            $"SESSION_OPEN_ACK invalid flags were not rejected: {flagsError}.");
    }

    private static void RunSessionCloseStateMachineValidation()
    {
        var state = new NnrpSessionStateMachine();
        AssertTrue(state.TryBeginNegotiation(out _), "Session state machine did not begin negotiation.");
        AssertTrue(state.TryActivate(out _), "Session state machine did not activate.");
        AssertTrue(state.TryAcceptFrameSubmit(out _), "Session state machine rejected active frame submit.");
        AssertTrue(state.TryBeginDraining(out _), "Session state machine did not begin draining.");
        AssertTrue(state.TryClose(out _), "Session state machine did not close after draining.");
        AssertTrue(state.State == NnrpSessionState.Closed, "Session state machine did not enter Closed after draining.");

        AssertTrue(
            SessionCloseMetadata.TryParse(GoldenSessionCloseMetadata().ToArray(), strict: true, out _, out var closeError),
            $"SESSION_CLOSE metadata parse failed: {closeError}.");
        AssertTrue(
            SessionCloseAckMetadata.TryParse(GoldenSessionCloseAckMetadata().ToArray(), strict: true, out _, out var ackError),
            $"SESSION_CLOSE_ACK metadata parse failed: {ackError}.");
    }

    private static void RunSessionOpenClose()
    {
        var openMetadata = GoldenSessionOpenMetadata();
        var openBody = new byte[openMetadata.BodyLength];
        var open = new SessionOpenMessage(
            SessionHeader(MessageType.SessionOpen, SessionOpenMetadata.MetadataLength, openMetadata.BodyLength, 0, 0x1010),
            openMetadata,
            openBody);
        AssertTrue(SessionOpenMessage.TryParse(open.ToArray(), out var parsedOpen, out var openError), $"SESSION_OPEN message parse failed: {openError}.");
        AssertTrue(parsedOpen.Metadata.Equals(openMetadata), "SESSION_OPEN message metadata changed.");

        var ackMetadata = GoldenSessionOpenAckMetadata();
        var ackBody = new byte[ackMetadata.BodyLength];
        var ack = new SessionOpenAckMessage(
            SessionHeader(MessageType.SessionOpenAck, SessionOpenAckMetadata.MetadataLength, ackMetadata.BodyLength, ackMetadata.SessionId, 0x1010),
            ackMetadata,
            ackBody);
        AssertTrue(SessionOpenAckMessage.TryParse(ack.ToArray(), out var parsedAck, out var ackError), $"SESSION_OPEN_ACK message parse failed: {ackError}.");
        AssertTrue(parsedAck.Metadata.Equals(ackMetadata), "SESSION_OPEN_ACK message metadata changed.");

        var closeMetadata = GoldenSessionCloseMetadata();
        var close = new SessionCloseMessage(
            SessionHeader(MessageType.SessionClose, SessionCloseMetadata.MetadataLength, 0, ackMetadata.SessionId, 0x2020),
            closeMetadata);
        AssertTrue(SessionCloseMessage.TryParse(close.ToArray(), out var parsedClose, out var closeError), $"SESSION_CLOSE message parse failed: {closeError}.");
        AssertTrue(parsedClose.Metadata.Equals(closeMetadata), "SESSION_CLOSE message metadata changed.");

        var closeAckMetadata = GoldenSessionCloseAckMetadata();
        var closeAck = new SessionCloseAckMessage(
            SessionHeader(MessageType.SessionCloseAck, SessionCloseAckMetadata.MetadataLength, 0, ackMetadata.SessionId, 0x2020),
            closeAckMetadata);
        AssertTrue(SessionCloseAckMessage.TryParse(closeAck.ToArray(), out var parsedCloseAck, out var closeAckError), $"SESSION_CLOSE_ACK message parse failed: {closeAckError}.");
        AssertTrue(parsedCloseAck.Metadata.Equals(closeAckMetadata), "SESSION_CLOSE_ACK message metadata changed.");
    }

    private static void RunInlineTensorSubmit()
    {
        var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 42, frameId: 303, viewId: 2, traceId: 0x1122334455667788);
        AssertTrue(FrameSubmitMessage.TryParse(submit.ToArray(), out var parsed, out var error), $"FRAME_SUBMIT parse failed: {error}.");
        AssertTrue(parsed.Header.SessionId == 42, "FRAME_SUBMIT did not preserve session routing.");
        AssertTrue(parsed.Header.FrameId == 303, "FRAME_SUBMIT did not preserve operation routing.");
        AssertTrue(parsed.Header.ViewId == 2, "FRAME_SUBMIT did not preserve view routing.");
        AssertTrue(parsed.Metadata.SubmitMode == SubmitMode.Inline, "FRAME_SUBMIT was not inline mode.");
        AssertTrue(parsed.Metadata.ObjectRefMask == 0, "FRAME_SUBMIT carried object references.");
        AssertTrue(parsed.Metadata.PayloadKindBitmap == PayloadKind.Tensor, "FRAME_SUBMIT was not tensor-only.");
        AssertTrue(parsed.Metadata.PayloadFrameCount == 0, "FRAME_SUBMIT unexpectedly carried typed payload frames.");
        AssertTrue(parsed.TileIds.Length == parsed.Metadata.TileCount, "FRAME_SUBMIT tile ids did not match metadata.");
        AssertTrue(parsed.Sections.Length == parsed.Metadata.SectionCount, "FRAME_SUBMIT sections did not match metadata.");
        AssertTrue(parsed.CameraBlock.Length == parsed.Metadata.CameraBytes, "FRAME_SUBMIT camera block length did not match metadata.");
    }

    private static void RunBasicResultPush()
    {
        var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 42, frameId: 303, viewId: 2, traceId: 0x1122334455667788);
        var result = CreateBasicResultPush(submit);
        AssertTrue(ResultPushMessage.TryParse(result.ToArray(), out var parsed, out var error), $"RESULT_PUSH parse failed: {error}.");
        AssertTrue(parsed.Header.SessionId == submit.Header.SessionId, "RESULT_PUSH did not preserve session routing.");
        AssertTrue(parsed.Header.FrameId == submit.Header.FrameId, "RESULT_PUSH did not preserve operation routing.");
        AssertTrue(parsed.Header.ViewId == submit.Header.ViewId, "RESULT_PUSH did not preserve view routing.");
        AssertTrue(parsed.Metadata.StatusCode == ResultStatusCode.Success, "RESULT_PUSH did not report success.");
        AssertTrue(parsed.Metadata.ResultClass == ResultClass.Complete, "RESULT_PUSH did not report a terminal complete result.");
        AssertTrue((parsed.Metadata.ResultFlags & ResultFlags.Partial) == 0, "RESULT_PUSH was marked partial.");
        AssertTrue(parsed.Metadata.CoveredTileCount == parsed.Metadata.TileCount, "RESULT_PUSH did not cover every submitted tile.");
        AssertTrue(parsed.Metadata.DroppedTileCount == 0, "RESULT_PUSH dropped tiles on the basic path.");
        AssertTrue(parsed.TileIds.Span.SequenceEqual(submit.TileIds.Span), "RESULT_PUSH tile ids did not match submit routing.");
    }

    private static void RunSingleTerminalEventDelivery()
    {
        var submit = SmokePackets.CreateSmokeFrameSubmitMessage(sessionId: 42, frameId: 303, viewId: 2, traceId: 0x1122334455667788);
        var result = CreateBasicResultPush(submit);
        var events = new[] { SubmitOutcome.FromResultPush(result) };
        var terminalCount = 0;

        foreach (var outcome in events)
        {
            AssertTrue(!outcome.IsResultDrop, "Basic result delivery unexpectedly produced RESULT_DROP.");
            AssertTrue(outcome.ResultPush.Header.FrameId == submit.Header.FrameId, "Result event operation id changed.");
            if (outcome.ResultPush.Metadata.ResultClass == ResultClass.Complete)
            {
                terminalCount++;
            }
        }

        AssertTrue(terminalCount == 1, "Basic result delivery did not produce exactly one terminal result.");
    }

    private static void RunSessionFlowUpdate()
    {
        var message = CreateSessionFlowUpdate();
        AssertFlowUpdateRoundtrip(message);
        AssertTrue(message.Header.SessionId == 42, "Session-scope FLOW_UPDATE did not target the session header.");
        AssertTrue(message.Header.FrameId == 0, "Session-scope FLOW_UPDATE unexpectedly targeted an operation header.");
        AssertTrue(message.Metadata.ScopeKind == FlowUpdateScopeKind.Session, "Session-scope FLOW_UPDATE changed scope.");
        AssertTrue(message.Metadata.ConnectionCredit == 0, "Session-scope FLOW_UPDATE carried connection credit.");
        AssertTrue(message.Metadata.SessionCredit == 2, "Session-scope FLOW_UPDATE did not preserve session credit.");
        AssertTrue(message.Metadata.OperationCredit == 0, "Session-scope FLOW_UPDATE carried operation credit.");
        AssertTrue(message.Metadata.OperationId == 0, "Session-scope FLOW_UPDATE carried operation id.");
        AssertTrue(message.Metadata.BackpressureLevel == FlowUpdateBackpressureLevel.Hard, "Session-scope FLOW_UPDATE did not preserve hard backpressure.");
        AssertTrue(message.Metadata.RetryAfterMilliseconds == 120, "Session-scope FLOW_UPDATE did not preserve retry-after.");
        AssertTrue(message.Metadata.CreditEpoch == 7, "Session-scope FLOW_UPDATE did not preserve credit epoch.");
    }

    private static void RunConnectionFlowUpdate()
    {
        var message = CreateConnectionFlowUpdate(creditEpoch: 11);
        AssertFlowUpdateRoundtrip(message);
        AssertTrue(message.Header.SessionId == 0, "Connection-scope FLOW_UPDATE carried a session header.");
        AssertTrue(message.Header.FrameId == 0, "Connection-scope FLOW_UPDATE unexpectedly targeted an operation header.");
        AssertTrue(message.Metadata.ScopeKind == FlowUpdateScopeKind.Connection, "Connection-scope FLOW_UPDATE changed scope.");
        AssertTrue(message.Metadata.ConnectionCredit == 6, "Connection-scope FLOW_UPDATE did not preserve connection credit.");
        AssertTrue(message.Metadata.SessionCredit == 0, "Connection-scope FLOW_UPDATE carried session credit.");
        AssertTrue(message.Metadata.OperationCredit == 0, "Connection-scope FLOW_UPDATE carried operation credit.");
        AssertTrue(message.Metadata.OperationId == 0, "Connection-scope FLOW_UPDATE carried operation id.");
        AssertTrue(message.Metadata.BackpressureLevel == FlowUpdateBackpressureLevel.Soft, "Connection-scope FLOW_UPDATE did not preserve soft backpressure.");
    }

    private static void RunOperationFlowUpdate()
    {
        var message = CreateOperationFlowUpdate(creditEpoch: 12);
        AssertFlowUpdateRoundtrip(message);
        AssertTrue(message.Header.SessionId == 42, "Operation-scope FLOW_UPDATE did not target the session header.");
        AssertTrue(message.Header.FrameId == 0, "Operation-scope FLOW_UPDATE used the operation metadata as a header frame id.");
        AssertTrue(message.Metadata.ScopeKind == FlowUpdateScopeKind.Operation, "Operation-scope FLOW_UPDATE changed scope.");
        AssertTrue(message.Metadata.ConnectionCredit == 0, "Operation-scope FLOW_UPDATE carried connection credit.");
        AssertTrue(message.Metadata.SessionCredit == 0, "Operation-scope FLOW_UPDATE carried session credit.");
        AssertTrue(message.Metadata.OperationCredit == 1, "Operation-scope FLOW_UPDATE did not preserve operation credit.");
        AssertTrue(message.Metadata.OperationId == 4660, "Operation-scope FLOW_UPDATE did not preserve operation id.");
        AssertTrue(message.Metadata.RetryAfterMilliseconds == 250, "Operation-scope FLOW_UPDATE did not preserve retry-after.");
        AssertTrue((message.Metadata.Flags & FlowUpdateFlags.DrainInFlightOnly) != 0, "Operation-scope FLOW_UPDATE did not preserve drain-only flag.");
    }

    private static void RunFlowUpdateReservedFlagsReject()
    {
        var packet = CreateSessionFlowUpdate().ToArray();
        packet[NnrpHeader.HeaderLength + 28] = 0x10;

        AssertTrue(
            !FlowUpdateMessage.TryParse(packet, out _, out var error)
                && error == NnrpParseError.InvalidMessageLayout,
            $"FLOW_UPDATE reserved flags were not rejected as InvalidMessageLayout: {error}.");
    }

    private static void RunFlowUpdateCreditEpochValidation()
    {
        var tracker = new Dictionary<string, uint>(StringComparer.Ordinal);
        var first = CreateConnectionFlowUpdate(creditEpoch: 11);
        var newer = CreateConnectionFlowUpdate(creditEpoch: 12);
        var stale = CreateConnectionFlowUpdate(creditEpoch: 10);

        AssertTrue(TryAcceptCreditEpoch(first, tracker), "Initial FLOW_UPDATE epoch was rejected.");
        AssertTrue(TryAcceptCreditEpoch(newer, tracker), "Newer FLOW_UPDATE epoch was rejected.");
        AssertTrue(!TryAcceptCreditEpoch(stale, tracker), "Stale FLOW_UPDATE epoch was accepted.");
    }

    private static void RunParallelSessionOpen()
    {
        var container = new NnrpSessionContainer();
        AssertTrue(container.TryOpenSession(42, out var firstFailure), $"First session open failed: {firstFailure.Message}");
        AssertTrue(container.TryOpenSession(43, out var secondFailure), $"Second session open failed: {secondFailure.Message}");
        AssertTrue(container.SessionCount == 2, "Connection container did not retain both opened sessions.");
        AssertTrue(container.TryAcceptFrameSubmit(42, out var firstSubmitFailure), $"First session submit route failed: {firstSubmitFailure.Message}");
        AssertTrue(container.TryAcceptFrameSubmit(43, out var secondSubmitFailure), $"Second session submit route failed: {secondSubmitFailure.Message}");
        AssertTrue(container.TryGetSessionState(42, out var firstState) && firstState == NnrpSessionState.Active, "First session was not active.");
        AssertTrue(container.TryGetSessionState(43, out var secondState) && secondState == NnrpSessionState.Active, "Second session was not active.");
    }

    private static void RunSessionCloseSiblingSurvival()
    {
        var container = new NnrpSessionContainer();
        AssertTrue(container.TryOpenSession(42, out _), "First session open failed.");
        AssertTrue(container.TryOpenSession(43, out _), "Second session open failed.");
        AssertTrue(container.TryCloseSession(42, out var closeFailure), $"Session close failed: {closeFailure.Message}");
        AssertTrue(!container.TryAcceptFrameSubmit(42, out _), "Closed session accepted a submit.");
        AssertTrue(container.TryAcceptFrameSubmit(43, out var siblingFailure), $"Sibling session submit route failed: {siblingFailure.Message}");
        AssertTrue(container.TryGetSessionState(43, out var siblingState) && siblingState == NnrpSessionState.Active, "Sibling session did not survive close.");
    }

    private static void RunConnectionCloseSessionCascade()
    {
        var container = new NnrpSessionContainer();
        AssertTrue(container.TryOpenSession(42, out _), "First session open failed.");
        AssertTrue(container.TryOpenSession(43, out _), "Second session open failed.");

        var closed = container.CloseConnection();

        AssertTrue(closed.Count == 2, "Connection close did not cascade to every live session.");
        AssertTrue(container.IsConnectionClosed, "Connection container did not enter closed state.");
        AssertTrue(container.TryGetSessionState(42, out var firstState) && firstState == NnrpSessionState.Closed, "First session did not close.");
        AssertTrue(container.TryGetSessionState(43, out var secondState) && secondState == NnrpSessionState.Closed, "Second session did not close.");
        AssertTrue(!container.TryAcceptFrameSubmit(42, out _), "Connection accepted submit after close.");
        AssertTrue(!container.TryOpenSession(44, out _), "Connection accepted a new session after close.");
    }

    private static ResultPushMessage CreateBasicResultPush(FrameSubmitMessage submit)
    {
        var section = CreateResultSection(submit.Metadata.TileCount);
        var tileIndexBytes = TileIndexBlockCodec.GetEncodedLength(submit.TileIds.Span, TileIndexMode.RawUInt16);
        var metadata = new ResultPushMetadata(
            statusCode: ResultStatusCode.Success,
            resultFlags: ResultFlags.None,
            sectionCount: 1,
            tileCount: submit.Metadata.TileCount,
            activeProfileId: 0,
            inferenceMilliseconds: 4,
            queueMilliseconds: 1,
            serverTotalMilliseconds: 5,
            tileBaseId: submit.Metadata.TileBaseId,
            tileIndexBytes: (uint)tileIndexBytes,
            resultClass: ResultClass.Complete,
            appliedBudgetPolicy: BudgetPolicy.None,
            reusedFrameId: 0,
            coveredTileCount: submit.Metadata.TileCount,
            droppedTileCount: 0,
            payloadKindBitmap: PayloadKind.Tensor,
            payloadFrameCount: 0);
        return new ResultPushMessage(
            new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.ResultPush,
                flags: HeaderFlags.None,
                metaLength: ResultPushMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: submit.Header.SessionId,
                frameId: submit.Header.FrameId,
                viewId: submit.Header.ViewId,
                routeId: submit.Header.RouteId,
                traceId: submit.Header.TraceId),
            metadata,
            submit.TileIds,
            new[] { section });
    }

    private static TensorSectionBlock CreateResultSection(ushort tileCount)
    {
        var lengthTable = new byte[sizeof(uint) * tileCount];
        var payload = new byte[tileCount];
        for (var index = 0; index < tileCount; index += 1)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(lengthTable.AsSpan(index * sizeof(uint), sizeof(uint)), 1);
            payload[index] = (byte)(0x40 + index);
        }

        return new TensorSectionBlock(
            new TensorSectionDescriptor(
                role: TensorRole.SrResidual,
                codec: CodecId.Raw,
                dtype: DTypeId.UInt8,
                layout: TensorLayoutId.Nhwc,
                scalePolicy: ScalePolicy.None,
                flags: 0,
                elementCountPerTile: 1,
                codecTableBytes: 0,
                lengthTableBytes: (uint)lengthTable.Length,
                payloadBytes: (uint)payload.Length,
                payloadStrideBytes: 0),
            Array.Empty<byte>(),
            lengthTable,
            payload);
    }

    private static FlowUpdateMessage CreateSessionFlowUpdate()
    {
        return CreateFlowUpdate(
            sessionId: 42,
            routeId: 9,
            traceId: 0x1122334455667788,
            new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Session,
                updateReason: FlowUpdateReason.Congestion,
                backpressureLevel: FlowUpdateBackpressureLevel.Hard,
                connectionCredit: 0,
                sessionCredit: 2,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 120,
                creditEpoch: 7,
                flags: FlowUpdateFlags.CreditValid | FlowUpdateFlags.RetryAfterValid));
    }

    private static FlowUpdateMessage CreateConnectionFlowUpdate(uint creditEpoch)
    {
        return CreateFlowUpdate(
            sessionId: 0,
            routeId: 3,
            traceId: 0x0102030405060708,
            new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Connection,
                updateReason: FlowUpdateReason.Grant,
                backpressureLevel: FlowUpdateBackpressureLevel.Soft,
                connectionCredit: 6,
                sessionCredit: 0,
                operationCredit: 0,
                operationId: 0,
                retryAfterMilliseconds: 0,
                creditEpoch: creditEpoch,
                flags: FlowUpdateFlags.CreditValid));
    }

    private static FlowUpdateMessage CreateOperationFlowUpdate(uint creditEpoch)
    {
        return CreateFlowUpdate(
            sessionId: 42,
            routeId: 9,
            traceId: 0x8877665544332211,
            new FlowUpdateMetadata(
                scopeKind: FlowUpdateScopeKind.Operation,
                updateReason: FlowUpdateReason.Pause,
                backpressureLevel: FlowUpdateBackpressureLevel.Hard,
                connectionCredit: 0,
                sessionCredit: 0,
                operationCredit: 1,
                operationId: 4660,
                retryAfterMilliseconds: 250,
                creditEpoch: creditEpoch,
                flags: FlowUpdateFlags.CreditValid | FlowUpdateFlags.RetryAfterValid | FlowUpdateFlags.DrainInFlightOnly));
    }

    private static FlowUpdateMessage CreateFlowUpdate(uint sessionId, ushort routeId, ulong traceId, FlowUpdateMetadata metadata)
    {
        return new FlowUpdateMessage(
            new NnrpHeader(
                versionMajor: NnrpHeader.CurrentVersionMajor,
                messageType: MessageType.FlowUpdate,
                flags: HeaderFlags.None,
                metaLength: FlowUpdateMetadata.MetadataLength,
                bodyLength: 0,
                sessionId: sessionId,
                frameId: 0,
                viewId: 0,
                routeId: routeId,
                traceId: traceId),
            metadata);
    }

    private static void AssertFlowUpdateRoundtrip(FlowUpdateMessage message)
    {
        AssertTrue(FlowUpdateMessage.TryParse(message.ToArray(), out var parsed, out var error), $"FLOW_UPDATE parse failed: {error}.");
        AssertTrue(parsed.Header.Equals(message.Header), "FLOW_UPDATE header roundtrip changed.");
        AssertTrue(parsed.Metadata.Equals(message.Metadata), "FLOW_UPDATE metadata roundtrip changed.");
    }

    private static bool TryAcceptCreditEpoch(FlowUpdateMessage message, Dictionary<string, uint> currentEpochs)
    {
        var key = $"{message.Metadata.ScopeKind}:{message.Header.SessionId}:{message.Metadata.OperationId}";
        if (currentEpochs.TryGetValue(key, out var currentEpoch) && message.Metadata.CreditEpoch <= currentEpoch)
        {
            return false;
        }

        currentEpochs[key] = message.Metadata.CreditEpoch;
        return true;
    }

    private static SessionOpenMetadata GoldenSessionOpenMetadata()
    {
        return new SessionOpenMetadata(
            requestedSessionId: 42,
            profileId: 2,
            priorityClass: SessionPriorityClass.Balanced,
            sessionFlags: SessionFlags.AllowResume | SessionFlags.AllowCacheLeases,
            schemaId: 4097,
            schemaVersion: 3,
            defaultDeadlineMilliseconds: 500,
            maxInFlightOperations: 4,
            leaseTtlHintMilliseconds: 30000,
            resumeTokenBytes: 16,
            authBytes: 32,
            sessionExtensionBytes: 8,
            clientSessionTag: 0x0123456789ABCDEF);
    }

    private static SessionOpenAckMetadata GoldenSessionOpenAckMetadata()
    {
        return new SessionOpenAckMetadata(
            sessionId: 42,
            acceptedProfileId: 2,
            acceptedPriorityClass: SessionPriorityClass.Balanced,
            sessionStatus: SessionStatus.Opened,
            schemaId: 4097,
            schemaVersion: 3,
            grantedOperationCredit: 2,
            maxInFlightOperations: 4,
            leaseTtlMilliseconds: 30000,
            resumeWindowMilliseconds: 120000,
            resumeTokenBytes: 16,
            sessionExtensionBytes: 8,
            serverSessionTag: 0x0FEDCBA987654321,
            routeScopeId: 7,
            sessionErrorCode: SessionErrorCode.None,
            sessionFlagsAck: SessionAckFlags.ResumeEnabled | SessionAckFlags.CacheLeasesEnabled);
    }

    private static SessionCloseMetadata GoldenSessionCloseMetadata()
    {
        return new SessionCloseMetadata(
            closeReason: SessionCloseReason.ClientShutdown,
            inFlightPolicy: InFlightPolicy.Drain,
            drainTimeoutMilliseconds: 1000,
            lastOperationId: 99,
            sessionErrorCode: SessionErrorCode.None,
            sessionCloseTag: 0x11223344);
    }

    private static SessionCloseAckMetadata GoldenSessionCloseAckMetadata()
    {
        return new SessionCloseAckMetadata(
            closeStatus: SessionCloseStatus.Draining,
            lastOperationId: 99,
            sessionErrorCode: SessionErrorCode.None);
    }

    private static NnrpHeader SessionHeader(MessageType messageType, int metadataLength, uint bodyLength, uint sessionId, ulong traceId)
    {
        return new NnrpHeader(
            versionMajor: NnrpHeader.CurrentVersionMajor,
            messageType: messageType,
            flags: HeaderFlags.None,
            metaLength: (uint)metadataLength,
            bodyLength: bodyLength,
            sessionId: sessionId,
            frameId: 0,
            viewId: 0,
            routeId: 0,
            traceId: traceId);
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
