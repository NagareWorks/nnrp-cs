using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Nnrp.Client;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;
using Nnrp.Server;
using Nnrp.Transport.Ipc;
using Nnrp.Transport.Quic;
using Nnrp.Transport.Tcp;
using Nnrp.Transport.WebSocket;

namespace Nnrp.BenchmarkAdapter;

internal sealed class TransportLoopbackMeasurement
{
    public required double P50Microseconds { get; init; }

    public required double P95Microseconds { get; init; }

    public required double P99Microseconds { get; init; }

    public required double ThroughputOperationsPerSecond { get; init; }

    public required double AllocatedBytesPerOperation { get; init; }

    public required int PayloadBytes { get; init; }
}

internal enum TransportLoopbackOperation
{
    SubmitResult,
    RuntimeControl,
}

internal sealed class LatencySampleWindow
{
    private readonly List<double> samples;
    private int cursor;

    internal LatencySampleWindow(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        samples = new List<double>(capacity);
        Capacity = capacity;
    }

    internal int Capacity { get; }

    internal int Count => samples.Count;

    internal void Add(double latencyMicroseconds)
    {
        if (samples.Count < Capacity)
        {
            samples.Add(latencyMicroseconds);
            return;
        }

        samples[cursor] = latencyMicroseconds;
        cursor = (cursor + 1) % Capacity;
    }

    internal (double P50, double P95, double P99) Percentiles() =>
        Program.Percentiles(new List<double>(samples));
}

internal static class TransportLoopbackBenchmark
{
    private const int MaximumLatencySamples = 65_536;
    private const double WorkerFixedHeadroomSeconds = 60;
    private const double WorkerIterationHeadroomSeconds = 0.1;
    private const double MaximumWorkerIterationHeadroomSeconds = 300;
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    internal static TransportLoopbackMeasurement Run(
        TransportId transportId,
        string artifactPath,
        byte[] payload,
        int warmupIterations,
        double durationSeconds,
        int allocationIterations,
        TransportLoopbackOperation operation)
    {
        ValidateWorkloadArguments(warmupIterations, durationSeconds, allocationIterations);

        var payloadPath = Path.Combine(
            Path.GetTempPath(),
            $"nnrp-cs-transport-benchmark-{Guid.NewGuid():N}.payload");
        var outputPath = Path.Combine(
            Path.GetTempPath(),
            $"nnrp-cs-transport-benchmark-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllBytes(payloadPath, payload);
            using var process = StartWorker(
                transportId,
                artifactPath,
                payloadPath,
                warmupIterations,
                durationSeconds,
                allocationIterations,
                operation,
                outputPath);
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            var timeoutMilliseconds = CalculateWorkerTimeoutMilliseconds(
                durationSeconds,
                warmupIterations,
                allocationIterations);
            if (!process.WaitForExit(timeoutMilliseconds))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"{transportId} transport benchmark worker timed out.");
            }

            Task.WaitAll(standardOutput, standardError);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"{transportId} transport benchmark worker for '{Path.GetFullPath(artifactPath)}' "
                    + $"exited with code {process.ExitCode}. "
                    + standardError.Result.Trim());
            }

            return JsonSerializer.Deserialize<TransportLoopbackMeasurement>(File.ReadAllText(outputPath))
                ?? throw new InvalidOperationException(
                    $"{transportId} transport benchmark worker returned an empty result.");
        }
        finally
        {
            TryDeleteFile(payloadPath);
            TryDeleteFile(outputPath);
        }
    }

    internal static int RunWorker(string[] args)
    {
        if (args.Length != 9)
        {
            throw new ArgumentException("Transport benchmark worker received an invalid argument set.");
        }

        var transportId = Enum.Parse<TransportId>(args[0], ignoreCase: true);
        var artifactPath = args[1];
        var warmupIterations = int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
        var durationSeconds = double.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture);
        var allocationIterations = int.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture);
        var operation = Enum.Parse<TransportLoopbackOperation>(args[6], ignoreCase: true);
        var outputPath = args[7];
        var expectedMarker = args[8];
        if (expectedMarker != "nnrp-transport-worker-v2")
        {
            throw new ArgumentException("Transport benchmark worker marker is invalid.");
        }

        ValidateWorkloadArguments(warmupIterations, durationSeconds, allocationIterations);
        var payload = File.ReadAllBytes(args[2]);
        var measurement = RunDirect(
            transportId,
            artifactPath,
            payload,
            warmupIterations,
            durationSeconds,
            allocationIterations,
            operation);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(measurement));
        return 0;
    }

    internal static void ValidateWorkloadArguments(
        int warmupIterations,
        double durationSeconds,
        int allocationIterations)
    {
        if (warmupIterations < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(warmupIterations),
                warmupIterations,
                "Transport benchmark warmup iterations must be non-negative.");
        }

        if (!double.IsFinite(durationSeconds) || durationSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(durationSeconds),
                durationSeconds,
                "Transport benchmark duration must be a finite positive number.");
        }

        if (allocationIterations <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(allocationIterations),
                allocationIterations,
                "Transport benchmark allocation iterations must be positive.");
        }
    }

    private static TransportLoopbackMeasurement RunDirect(
        TransportId transportId,
        string artifactPath,
        byte[] payload,
        int warmupIterations,
        double durationSeconds,
        int allocationIterations,
        TransportLoopbackOperation operation)
    {
        using var loopback = LoopbackSession.OpenAsync(transportId, artifactPath, payload)
            .GetAwaiter()
            .GetResult();
        var completedRoundTrips = 0;
        Action roundTrip = operation switch
        {
            TransportLoopbackOperation.SubmitResult => loopback.SubmitResultRoundTrip,
            TransportLoopbackOperation.RuntimeControl => loopback.RuntimeControlRoundTrip,
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        try
        {
            for (var index = 0; index < warmupIterations; index += 1)
            {
                roundTrip();
                completedRoundTrips += 1;
            }

            var samples = new LatencySampleWindow(MaximumLatencySamples);
            long measuredRoundTrips = 0;
            var measuredStart = Stopwatch.GetTimestamp();
            var deadline = measuredStart + (long)(durationSeconds * Stopwatch.Frequency);
            while (Stopwatch.GetTimestamp() < deadline)
            {
                var start = Stopwatch.GetTimestamp();
                roundTrip();
                completedRoundTrips += 1;
                measuredRoundTrips += 1;
                var latencyMicroseconds =
                    ((Stopwatch.GetTimestamp() - start) * 1_000_000.0) / Stopwatch.Frequency;
                samples.Add(latencyMicroseconds);
            }

            if (samples.Count == 0)
            {
                throw new InvalidOperationException("Transport benchmark did not complete a request/result round trip.");
            }

            var measuredSeconds = (Stopwatch.GetTimestamp() - measuredStart) / (double)Stopwatch.Frequency;
            var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
            for (var index = 0; index < allocationIterations; index += 1)
            {
                roundTrip();
                completedRoundTrips += 1;
            }

            var allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
            var percentiles = samples.Percentiles();
            return new TransportLoopbackMeasurement
            {
                P50Microseconds = percentiles.P50,
                P95Microseconds = percentiles.P95,
                P99Microseconds = percentiles.P99,
                ThroughputOperationsPerSecond = measuredRoundTrips / measuredSeconds,
                AllocatedBytesPerOperation = allocatedBytes / (double)allocationIterations,
                PayloadBytes = payload.Length,
            };
        }
        catch (Exception error)
        {
            throw new InvalidOperationException(
                $"{transportId} transport benchmark failed after {completedRoundTrips} completed round trips.",
                error);
        }
    }

    internal static int CalculateWorkerTimeoutMilliseconds(
        double durationSeconds,
        int warmupIterations,
        int allocationIterations)
    {
        var iterationHeadroomSeconds = Math.Min(
            MaximumWorkerIterationHeadroomSeconds,
            ((double)warmupIterations + allocationIterations) * WorkerIterationHeadroomSeconds);
        var timeoutSeconds = durationSeconds + WorkerFixedHeadroomSeconds + iterationHeadroomSeconds;
        return checked((int)Math.Min(int.MaxValue, Math.Ceiling(timeoutSeconds * 1_000)));
    }

    private static Process StartWorker(
        TransportId transportId,
        string artifactPath,
        string payloadPath,
        int warmupIterations,
        double durationSeconds,
        int allocationIterations,
        TransportLoopbackOperation operation,
        string outputPath)
    {
        var assemblyPath = typeof(TransportLoopbackBenchmark).Assembly.Location;
        var startInfo = new ProcessStartInfo("dotnet");
        startInfo.ArgumentList.Add(assemblyPath);

        startInfo.ArgumentList.Add("--transport-worker");
        startInfo.ArgumentList.Add(transportId.ToString());
        startInfo.ArgumentList.Add(Path.GetFullPath(artifactPath));
        startInfo.ArgumentList.Add(payloadPath);
        startInfo.ArgumentList.Add(warmupIterations.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(durationSeconds.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(allocationIterations.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(operation.ToString());
        startInfo.ArgumentList.Add(outputPath);
        startInfo.ArgumentList.Add("nnrp-transport-worker-v2");
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Failed to start the {transportId} transport benchmark worker.");
        }

        return process;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    internal static ReadOnlyMemory<byte> RequireBodyTail(NnrpRuntimeEvent @event) =>
        @event.Tail.Match(
            static () => throw UnexpectedRuntimeControlTail(),
            static value => value,
            static _ => throw UnexpectedRuntimeControlTail(),
            static (_, _) => throw UnexpectedRuntimeControlTail());

    private static InvalidOperationException UnexpectedRuntimeControlTail() =>
        new("Transport benchmark runtime-control event must carry a body tail.");

    private sealed class LoopbackSession : IDisposable
    {
        private readonly string? ipcSocketPath;
        private readonly NnrpServer server;
        private readonly NnrpClient client;
        private readonly NnrpClientSession clientSession;
        private readonly NnrpServerSession serverSession;
        private readonly byte[] body;
        private readonly ResultPushMetadata resultMetadata;
        private ulong nextOperationId = 1;
        private uint nextFrameId = 1;
        private bool disposed;

        private LoopbackSession(
            string? ipcSocketPath,
            NnrpServer server,
            NnrpClient client,
            NnrpClientSession clientSession,
            NnrpServerSession serverSession,
            byte[] body)
        {
            this.ipcSocketPath = ipcSocketPath;
            this.server = server;
            this.client = client;
            this.clientSession = clientSession;
            this.serverSession = serverSession;
            this.body = body;
            resultMetadata = new ResultPushMetadata(
                ResultStatusCode.Success,
                ResultFlags.None,
                activeProfileId: 0,
                payloadKind: PayloadKind.TokenChunk,
                reserved0: 0,
                inferenceMilliseconds: 0,
                queueMilliseconds: 0,
                serverTotalMilliseconds: 0,
                reserved1: 0,
                profileBlockBytes: 0,
                payloadDescriptorBytes: 0,
                payloadDataBytes: checked((uint)body.Length));
        }

        internal static async Task<LoopbackSession> OpenAsync(
            TransportId transportId,
            string artifactPath,
            byte[] body)
        {
            var configuration = CreateConfiguration(transportId, artifactPath);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            NnrpServer? server = null;
            NnrpClient? client = null;
            NnrpClientSession? clientSession = null;
            NnrpServerSession? serverSession = null;
            try
            {
                server = await NnrpServer.ListenAsync(
                    new NnrpServerOptions(
                        configuration.Endpoint,
                        new Dictionary<TransportId, NnrpServerProviderRoute>
                        {
                            [transportId] = new NnrpServerProviderRoute
                            {
                                ProviderEndpoint = configuration.ProviderEndpoint,
                                Security = configuration.ServerSecurity,
                            },
                        },
                        ForcePolicy(transportId),
                        new[] { configuration.ProviderFactory() }),
                    timeout.Token);
                var boundEndpoint = server.BoundProviderEndpoints[transportId];
                var accepting = server.AcceptAsync(
                    new NnrpServerAcceptOptions(timeoutMilliseconds: 10_000),
                    timeout.Token).AsTask();
                client = await NnrpClient.ConnectAsync(
                    new NnrpClientOptions(
                        configuration.Endpoint,
                        new Dictionary<TransportId, NnrpClientProviderRoute>
                        {
                            [transportId] = new NnrpClientProviderRoute
                            {
                                ProviderEndpoint = boundEndpoint,
                                Security = configuration.ClientSecurity,
                            },
                        },
                        ForcePolicy(transportId),
                        new[] { configuration.ProviderFactory() }),
                    timeout.Token);
                clientSession = client.OpenSession();
                serverSession = await accepting;
                return new LoopbackSession(
                    configuration.IpcSocketPath,
                    server,
                    client,
                    clientSession,
                    serverSession,
                    body);
            }
            catch
            {
                if (serverSession != null)
                {
                    await serverSession.DisposeAsync();
                }

                if (clientSession != null)
                {
                    await clientSession.DisposeAsync();
                }

                if (client != null)
                {
                    await client.DisposeAsync();
                }

                if (server != null)
                {
                    await server.DisposeAsync();
                }

                DeleteIpcSocket(configuration.IpcSocketPath);
                throw;
            }
        }

        internal void SubmitResultRoundTrip()
        {
            SubmitResultRoundTripAsync().GetAwaiter().GetResult();
        }

        internal void RuntimeControlRoundTrip()
        {
            RuntimeControlRoundTripAsync().GetAwaiter().GetResult();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            try
            {
                CloseAsync().GetAwaiter().GetResult();
            }
            finally
            {
                DeleteIpcSocket(ipcSocketPath);
            }
        }

        private async Task SubmitResultRoundTripAsync()
        {
            var operationId = nextOperationId++;
            var request = NnrpSubmitRequest.CreateToken(new NnrpTokenSubmitInput(
                new NnrpSubmitIdentity(operationId, nextFrameId++, new NnrpSubmitHeaderContext()),
                new NnrpSubmitPolicy(),
                new[] { new NnrpTokenChunk(body) }));
            var receive = serverSession.ReceiveSubmitAsync().AsTask();
            var submittedOperationId = await clientSession.SubmitNoWaitAsync(request);
            var operation = await receive;
            if (submittedOperationId != operationId || operation.OperationId != operationId)
            {
                throw new InvalidOperationException("Transport benchmark submit operation identity mismatch.");
            }

            await operation.SendResultAsync(resultMetadata, body);
            var result = await clientSession.NextResultAsync();
            var resultBody = result.Event.Match(
                runtime => runtime.Tail.Match(
                    () => throw new InvalidOperationException("Transport benchmark result omitted its body."),
                    value => value,
                    _ => throw new InvalidOperationException("Transport benchmark result returned a diagnostic tail."),
                    (_, _) => throw new InvalidOperationException("Transport benchmark result returned a delta tail.")),
                _ => throw new InvalidOperationException("Transport benchmark result used lifecycle-only evidence."));
            if (result.OperationId != operationId || !resultBody.Span.SequenceEqual(body))
            {
                throw new InvalidOperationException("Transport benchmark result payload mismatch.");
            }
        }

        private async Task RuntimeControlRoundTripAsync()
        {
            var traceId = nextOperationId++;
            var request = new TraceContextMetadata(
                traceId,
                traceId * 2,
                0,
                1,
                0,
                checked((uint)body.Length));
            await clientSession.SendTraceContextAsync(request, body);
            var received = RuntimeEventOf(await serverSession.NextEventAsync());
            AssertTraceContext(received, request);

            var response = new TraceContextMetadata(
                traceId,
                traceId * 2 + 1,
                request.SpanId,
                2,
                0,
                checked((uint)body.Length));
            await serverSession.SendTraceContextAsync(response, body);
            var returned = RuntimeEventOf(await clientSession.NextEventAsync());
            AssertTraceContext(returned, response);
        }

        private void AssertTraceContext(NnrpRuntimeEvent @event, TraceContextMetadata expected)
        {
            if (@event.Header.MessageType != MessageType.TraceContext
                || @event.Metadata.Get<TraceContextMetadata>() != expected
                || !RequireBodyTail(@event).Span.SequenceEqual(body))
            {
                throw new InvalidOperationException("Transport benchmark runtime-control round trip mismatch.");
            }
        }

        private static NnrpRuntimeEvent RuntimeEventOf(NnrpClientEvent @event) =>
            @event.Match(
                runtime => runtime,
                _ => throw new InvalidOperationException("Transport benchmark expected a client runtime event."));

        private static NnrpRuntimeEvent RuntimeEventOf(NnrpServerEvent @event) =>
            @event.Match(
                _ => throw new InvalidOperationException("Transport benchmark expected a server runtime event."),
                runtime => runtime,
                _ => throw new InvalidOperationException("Transport benchmark expected a server runtime event."));

        private async Task CloseAsync()
        {
            Exception? firstError = null;
            try
            {
                using var closeTimeout = new CancellationTokenSource(CloseTimeout);
                var closingClient = clientSession.DisposeAsync().AsTask();
                var closeEvent = RuntimeEventOf(await serverSession.NextEventAsync(closeTimeout.Token));
                if (closeEvent.Header.MessageType != MessageType.SessionClose)
                {
                    throw new InvalidOperationException("Transport benchmark expected a SESSION_CLOSE event.");
                }

                await serverSession.DisposeAsync().AsTask().WaitAsync(closeTimeout.Token);
                await closingClient.WaitAsync(closeTimeout.Token);
            }
            catch (Exception error)
            {
                firstError = new InvalidOperationException("Failed to close the benchmark runtime sessions.", error);
            }

            try
            {
                await client.DisposeAsync().AsTask().WaitAsync(CloseTimeout);
            }
            catch (Exception error)
            {
                firstError ??= new InvalidOperationException("Failed to close the benchmark client.", error);
            }

            try
            {
                await server.DisposeAsync().AsTask().WaitAsync(CloseTimeout);
            }
            catch (Exception error)
            {
                firstError ??= new InvalidOperationException("Failed to close the benchmark server.", error);
            }

            if (firstError != null)
            {
                throw firstError;
            }
        }
    }

    private sealed record TransportConfiguration(
        Func<INnrpNativeTransportProvider> ProviderFactory,
        NnrpEndpoint Endpoint,
        NnrpProviderEndpoint ProviderEndpoint,
        NnrpTransportServerSecurity? ServerSecurity,
        NnrpTransportClientSecurity? ClientSecurity,
        string? IpcSocketPath);

    private static TransportConfiguration CreateConfiguration(TransportId transportId, string artifactPath)
    {
        return transportId switch
        {
            TransportId.Tcp => new TransportConfiguration(
                () => new NnrpNativeTcpTransportProvider(artifactPath),
                NnrpEndpoint.Parse("nnrp://127.0.0.1:0"),
                NnrpProviderEndpoint.Parse("tcp://127.0.0.1:0"),
                null,
                null,
                null),
            TransportId.Quic => CreateQuicConfiguration(artifactPath),
            TransportId.Ipc => CreateIpcConfiguration(artifactPath),
            TransportId.WebSocket => new TransportConfiguration(
                () => new NnrpNativeWebSocketTransportProvider(artifactPath),
                NnrpEndpoint.Parse("nnrp://localhost:0"),
                NnrpProviderEndpoint.Parse("ws://127.0.0.1:0/nnrp"),
                null,
                null,
                null),
            _ => throw new ArgumentOutOfRangeException(nameof(transportId)),
        };
    }

    private static TransportConfiguration CreateQuicConfiguration(string artifactPath)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        var alternativeName = new SubjectAlternativeNameBuilder();
        alternativeName.AddDnsName("localhost");
        request.CertificateExtensions.Add(alternativeName.Build());
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(24));
        var certificateDer = certificate.Export(X509ContentType.Cert);
        return new TransportConfiguration(
            () => new NnrpNativeQuicTransportProvider(artifactPath),
            NnrpEndpoint.Parse("nnrps://localhost:0"),
            NnrpProviderEndpoint.Parse("quic://127.0.0.1:0"),
            new NnrpTransportServerSecurity(certificateDer, rsa.ExportPkcs8PrivateKey()),
            new NnrpTransportClientSecurity("localhost", certificateDer),
            null);
    }

    private static TransportConfiguration CreateIpcConfiguration(string artifactPath)
    {
        var socketPath = OperatingSystem.IsWindows()
            ? null
            : Path.Combine(
                OperatingSystem.IsMacOS() ? "/tmp" : Path.GetTempPath(),
                $"nnrp-csb-{Guid.NewGuid():N}.sock");
        var providerEndpoint = OperatingSystem.IsWindows()
            ? NnrpProviderEndpoint.Parse($"npipe://nnrp-cs-benchmark-{Guid.NewGuid():N}")
            : NnrpProviderEndpoint.Parse($"unix://{socketPath!.Replace('\\', '/')}");
        return new TransportConfiguration(
            () => new NnrpNativeIpcTransportProvider(artifactPath),
            NnrpEndpoint.Parse("nnrp://localhost"),
            providerEndpoint,
            null,
            null,
            socketPath);
    }

    private static TransportPolicy ForcePolicy(TransportId transportId)
    {
        return transportId switch
        {
            TransportId.Tcp => TransportPolicy.ForceTcp,
            TransportId.Quic => TransportPolicy.ForceQuic,
            TransportId.Ipc => TransportPolicy.ForceIpc,
            TransportId.WebSocket => TransportPolicy.ForceWebSocket,
            _ => throw new ArgumentOutOfRangeException(nameof(transportId)),
        };
    }

    private static void DeleteIpcSocket(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            TryDeleteFile(path);
        }
    }
}
