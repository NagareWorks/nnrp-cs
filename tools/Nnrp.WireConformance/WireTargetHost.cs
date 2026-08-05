using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Nnrp.Client;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;

namespace Nnrp.WireConformance;

internal sealed class WireTargetHost(IWireTargetSdk sdk)
{
    private const string SuiteVersion = "0.1.0";
    private const ulong CacheKeyHigh = 1_234_605_616_436_508_552;
    private const ulong CacheKeyLow = 11_072_869_122_414_935_808;
    private const ulong ProgressOperationId = 301;
    private const int ConnectAttempts = 100;
    private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly byte[] RequestBody = Encoding.UTF8.GetBytes("wire-external-request");
    private static readonly byte[] ResponseBody = Encoding.UTF8.GetBytes("wire-external-result");
    private static readonly byte[] TraceBody = Encoding.UTF8.GetBytes("trace");

    internal async Task RunAsync(
        WireTargetHostOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        string manifestPath = Path.GetFullPath(options.ManifestPath);
        string manifestDirectory = Path.GetDirectoryName(manifestPath)
            ?? throw new ArgumentException("Manifest path must have a parent directory.", nameof(options));
        Directory.CreateDirectory(manifestDirectory);

        string artifactRoot = options.ArtifactRoot ?? NnrpNativeArtifact.DefaultArtifactRoot;
        sdk.ValidateArtifacts(artifactRoot);
        WireTargetSecurity security = CreateSecurity(manifestDirectory);
        string tcpAddress = ReserveTcpAddress();
        string quicAddress = ReserveUdpAddress();
        string websocketEndpoint = $"wss://localhost:{PortOf(ReserveTcpAddress())}/nnrp";
        string ipcEndpoint = CreateIpcEndpoint(manifestDirectory);

        IWireTargetServer? tcpServer = null;
        IWireTargetServer? quicServer = null;
        IWireTargetServer? ipcServer = null;
        try
        {
            tcpServer = await ListenAsync(
                TransportId.Tcp,
                $"nnrp://{tcpAddress}/session/default",
                $"tcp://{tcpAddress}",
                null,
                cancellationToken).ConfigureAwait(false);
            quicServer = await ListenAsync(
                TransportId.Quic,
                $"nnrps://localhost:{PortOf(quicAddress)}/session/default",
                $"quic://{quicAddress}",
                security.Server,
                cancellationToken).ConfigureAwait(false);
            ipcServer = await ListenAsync(
                TransportId.Ipc,
                "nnrp://localhost/session/default",
                ipcEndpoint,
                null,
                cancellationToken).ConfigureAwait(false);

            Task<IWireTargetServerSession> tcpAccept = AcceptAsync(tcpServer, cancellationToken);
            Task<IWireTargetServerSession> quicAccept = AcceptAsync(quicServer, cancellationToken);
            Task<IWireTargetServerSession> ipcAccept = AcceptAsync(ipcServer, cancellationToken);

            string boundTcpAddress = WithoutScheme(tcpServer.BoundProviderEndpoint, "tcp");
            string boundQuicAddress = WithoutScheme(quicServer.BoundProviderEndpoint, "quic");
            string boundIpcEndpoint = ipcServer.BoundProviderEndpoint.ToString();
            WriteManifestAtomically(
                manifestPath,
                boundTcpAddress,
                boundQuicAddress,
                boundIpcEndpoint,
                websocketEndpoint);

            await HandleCancelAsync(await tcpAccept.ConfigureAwait(false), cancellationToken)
                .ConfigureAwait(false);
            await tcpServer.DisposeAsync().ConfigureAwait(false);
            tcpServer = null;

            await HandlePriorityAsync(await quicAccept.ConfigureAwait(false), cancellationToken)
                .ConfigureAwait(false);
            await HandleProgressClientAsync(
                TransportId.Tcp,
                NnrpEndpoint.Parse($"nnrp://{boundTcpAddress}/session/default"),
                NnrpProviderEndpoint.Parse($"tcp://{boundTcpAddress}"),
                null,
                cancellationToken).ConfigureAwait(false);

            await using (IWireTargetServerSession cacheSession =
                await quicServer.AcceptAsync(cancellationToken).ConfigureAwait(false))
            {
                await HandleCacheAsync(cacheSession, cancellationToken).ConfigureAwait(false);
            }

            await quicServer.DisposeAsync().ConfigureAwait(false);
            quicServer = null;

            await HandleCancelAsync(await ipcAccept.ConfigureAwait(false), cancellationToken)
                .ConfigureAwait(false);
            await ipcServer.DisposeAsync().ConfigureAwait(false);
            ipcServer = null;

            await HandleProgressClientAsync(
                TransportId.WebSocket,
                NnrpEndpoint.Parse(
                    $"nnrps://localhost:{new Uri(websocketEndpoint).Port}/session/default"),
                NnrpProviderEndpoint.Parse(websocketEndpoint),
                security.Client,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await DisposeIgnoringFailureAsync(tcpServer).ConfigureAwait(false);
            await DisposeIgnoringFailureAsync(quicServer).ConfigureAwait(false);
            await DisposeIgnoringFailureAsync(ipcServer).ConfigureAwait(false);
            CleanupIpcEndpoint(ipcEndpoint);
        }
    }

    internal static async Task HandleCancelAsync(
        IWireTargetServerSession session,
        CancellationToken cancellationToken)
    {
        await using (session.ConfigureAwait(false))
        {
            IWireTargetOperation operation = await session.ReceiveSubmitAsync(cancellationToken)
                .ConfigureAwait(false);
            WireTargetReceivedEvent cancel = Expect(
                await session.NextEventAsync(cancellationToken).ConfigureAwait(false),
                MessageType.Cancel);
            ControlRequestMetadata metadata = cancel.GetMetadata<ControlRequestMetadata>();
            if (metadata.OperationId != operation.OperationId)
            {
                throw new InvalidOperationException("Cancel targeted another operation.");
            }

            await session.SendTraceContextAsync(
                new TraceContextMetadata(0x1234, 0x5678, 0, 1, 0, (uint)TraceBody.Length),
                TraceBody,
                cancellationToken).ConfigureAwait(false);
            await operation.SendResultDropAsync(
                new ResultDropReasonMetadata(
                    operation.OperationId,
                    1,
                    NnrpResultDropReasonCode.PeerCancelled,
                    RuntimeRole.Server,
                    0,
                    0),
                ReadOnlyMemory<byte>.Empty,
                cancellationToken).ConfigureAwait(false);
            _ = Expect(
                await session.NextEventAsync(cancellationToken).ConfigureAwait(false),
                MessageType.SessionClose);
        }
    }

    internal static async Task HandlePriorityAsync(
        IWireTargetServerSession session,
        CancellationToken cancellationToken)
    {
        await using (session.ConfigureAwait(false))
        {
            IWireTargetOperation operation = await session.ReceiveSubmitAsync(cancellationToken)
                .ConfigureAwait(false);
            SchedulingMetadata priority = Expect(
                await session.NextEventAsync(cancellationToken).ConfigureAwait(false),
                MessageType.PriorityUpdate).GetMetadata<SchedulingMetadata>();
            SchedulingMetadata expiry = Expect(
                await session.NextEventAsync(cancellationToken).ConfigureAwait(false),
                MessageType.ExpireAt).GetMetadata<SchedulingMetadata>();
            if (priority.OperationId != operation.OperationId
                || expiry.OperationId != operation.OperationId
                || expiry.DeadlineUnixMs != 1)
            {
                throw new InvalidOperationException(
                    "Priority/deadline metadata does not match the submitted operation.");
            }

            await operation.SendResultDropAsync(
                new ResultDropReasonMetadata(
                    operation.OperationId,
                    1,
                    NnrpResultDropReasonCode.DeadlineExpired,
                    RuntimeRole.Server,
                    0,
                    0),
                ReadOnlyMemory<byte>.Empty,
                cancellationToken).ConfigureAwait(false);
            _ = Expect(
                await session.NextEventAsync(cancellationToken).ConfigureAwait(false),
                MessageType.SessionClose);
        }
    }

    internal static async Task HandleCacheAsync(
        IWireTargetServerSession session,
        CancellationToken cancellationToken)
    {
        IWireTargetOperation operation = await session.ReceiveSubmitAsync(cancellationToken)
            .ConfigureAwait(false);
        _ = Expect(
            await session.NextEventAsync(cancellationToken).ConfigureAwait(false),
            MessageType.CapabilityNegotiation).GetMetadata<CapabilityMetadata>();
        _ = Expect(
            await session.NextEventAsync(cancellationToken).ConfigureAwait(false),
            MessageType.RouteHint).GetMetadata<RouteHintMetadata>();
        CacheReferenceMetadata cache = Expect(
            await session.NextEventAsync(cancellationToken).ConfigureAwait(false),
            MessageType.CacheReference).GetMetadata<CacheReferenceMetadata>();
        if (cache.CacheKeyHi != CacheKeyHigh || cache.CacheKeyLo != CacheKeyLow)
        {
            throw new InvalidOperationException("Cache reference used an unexpected identity.");
        }

        await session.ReportCacheMissAsync(
            new CacheMissMetadata(
                1,
                CacheKeyHigh,
                CacheKeyLow,
                CacheMissReason.NotFound,
                TypedPayloadProfileId.Token.Value,
                0),
            ReadOnlyMemory<byte>.Empty,
            cancellationToken).ConfigureAwait(false);
        await operation.SendResultAsync(
            new ResultPushMetadata(
                ResultStatusCode.Success,
                ResultFlags.None,
                TypedPayloadProfileId.Token.Value,
                PayloadKind.TokenChunk,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                (uint)ResponseBody.Length),
            ResponseBody,
            cancellationToken).ConfigureAwait(false);
        _ = Expect(
            await session.NextEventAsync(cancellationToken).ConfigureAwait(false),
            MessageType.SessionClose);
    }

    internal async Task HandleProgressClientAsync(
        TransportId transportId,
        NnrpEndpoint endpoint,
        NnrpProviderEndpoint providerEndpoint,
        NnrpTransportClientSecurity? security,
        CancellationToken cancellationToken)
    {
        await using IWireTargetClient client = await ConnectWithRetryAsync(
            transportId,
            endpoint,
            providerEndpoint,
            security,
            cancellationToken).ConfigureAwait(false);
        await using IWireTargetClientSession session = client.OpenSession();
        ulong operationId = await session.SubmitNoWaitAsync(
            NnrpSubmitRequest.CreateToken(new NnrpTokenSubmitInput(
                new NnrpSubmitIdentity(
                    ProgressOperationId,
                    (uint)ProgressOperationId,
                    new NnrpSubmitHeaderContext()),
                new NnrpSubmitPolicy(),
                new[] { new NnrpTokenChunk(RequestBody) })),
            cancellationToken).ConfigureAwait(false);
        if (operationId != ProgressOperationId)
        {
            throw new InvalidOperationException("Progress target submission changed operation identity.");
        }

        ProgressMetadata progress = Expect(
            await session.NextEventAsync(cancellationToken).ConfigureAwait(false),
            MessageType.Progress).GetMetadata<ProgressMetadata>();
        PressureMetadata pressure = Expect(
            await session.NextEventAsync(cancellationToken).ConfigureAwait(false),
            MessageType.CreditUpdate).GetMetadata<PressureMetadata>();
        PartialResultMetadata partial = Expect(
            await session.NextEventAsync(cancellationToken).ConfigureAwait(false),
            MessageType.PartialResult).GetMetadata<PartialResultMetadata>();
        WireTargetTerminalResult result = await session.NextResultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (progress.OperationId != ProgressOperationId
            || pressure.CreditWindow != 1
            || partial.OperationId != ProgressOperationId
            || result.OperationId != ProgressOperationId
            || result.TerminalState != NnrpResultTerminalState.Success
            || result.Event.MessageType != MessageType.ResultPush
            || !result.Event.Body.Span.SequenceEqual(ResponseBody))
        {
            throw new InvalidOperationException(
                "Progress/backpressure target observed an unexpected response sequence.");
        }
    }

    private async Task<IWireTargetClient> ConnectWithRetryAsync(
        TransportId transportId,
        NnrpEndpoint endpoint,
        NnrpProviderEndpoint providerEndpoint,
        NnrpTransportClientSecurity? security,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        for (int attempt = 0; attempt < ConnectAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await sdk.ConnectAsync(
                    transportId,
                    endpoint,
                    providerEndpoint,
                    security,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception error) when (IsTransientConnectFailure(error))
            {
                lastError = error;
                await Task.Delay(ConnectRetryDelay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException(
            "Wire target could not connect to the suite listener.",
            lastError);
    }

    private static bool IsTransientConnectFailure(Exception error) => error switch
    {
        IOException => true,
        SocketException => true,
        NnrpNativeWouldBlockException { Status.ErrorFamily: NnrpErrorFamily.Transport } => true,
        NnrpNativeInternalException
        {
            Status.ErrorFamily: NnrpErrorFamily.Transport,
            Status.DetailCode: 106,
        } => true,
        _ => false,
    };

    private async ValueTask<IWireTargetServer> ListenAsync(
        TransportId transportId,
        string endpoint,
        string providerEndpoint,
        NnrpTransportServerSecurity? security,
        CancellationToken cancellationToken) =>
        await sdk.ListenAsync(
            transportId,
            NnrpEndpoint.Parse(endpoint),
            NnrpProviderEndpoint.Parse(providerEndpoint),
            security,
            cancellationToken).ConfigureAwait(false);

    private static async Task<IWireTargetServerSession> AcceptAsync(
        IWireTargetServer server,
        CancellationToken cancellationToken) =>
        await server.AcceptAsync(cancellationToken).ConfigureAwait(false);

    private static WireTargetReceivedEvent Expect(
        WireTargetReceivedEvent received,
        MessageType expected)
    {
        if (received.MessageType != expected)
        {
            throw new InvalidOperationException(
                $"Wire target expected {expected}, received {received.MessageType}.");
        }

        return received;
    }

    private static WireTargetSecurity CreateSecurity(string manifestDirectory)
    {
        string certificateDirectory = Path.Combine(manifestDirectory, "certs");
        Directory.CreateDirectory(certificateDirectory);
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        SubjectAlternativeNameBuilder alternativeName = new();
        alternativeName.AddDnsName("localhost");
        request.CertificateExtensions.Add(alternativeName.Build());
        using X509Certificate2 certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
        byte[] certificateDer = certificate.Export(X509ContentType.Cert);
        byte[] privateKeyDer = rsa.ExportPkcs8PrivateKey();
        File.WriteAllBytes(Path.Combine(certificateDirectory, "server.der"), certificateDer);
        File.WriteAllBytes(Path.Combine(certificateDirectory, "server-key.der"), privateKeyDer);
        return new WireTargetSecurity(
            new NnrpTransportClientSecurity("localhost", certificateDer),
            new NnrpTransportServerSecurity(certificateDer, privateKeyDer));
    }

    private static void WriteManifestAtomically(
        string manifestPath,
        string tcpEndpoint,
        string quicEndpoint,
        string ipcEndpoint,
        string websocketEndpoint)
    {
        WireTargetTransportSecurity tls = new(
            "localhost",
            "certs/server.der",
            "certs/server.der",
            "certs/server-key.der");
        WireTargetManifest manifest = new WireTargetManifestBuilder(WireTargetSupport.Compiled).Build(
            "nnrp-cs-preview4-native",
            SuiteVersion,
            [WireTargetModes.SuiteAsClient, WireTargetModes.SuiteAsServer, WireTargetModes.SuiteAsProxy],
            [
                new WireTargetTransport("tcp", tcpEndpoint),
                new WireTargetTransport("quic", quicEndpoint, true, tls),
                new WireTargetTransport("ipc", ipcEndpoint),
                new WireTargetTransport("websocket", websocketEndpoint, true, tls),
            ],
            [
                NnrpPreview4CapabilityTokens.ControlCancelAbort,
                NnrpPreview4CapabilityTokens.ControlPriorityUpdate,
                NnrpPreview4CapabilityTokens.ControlDeadlineExpire,
                NnrpPreview4CapabilityTokens.ControlProgressPartial,
                NnrpPreview4CapabilityTokens.ControlCreditBackpressure,
                NnrpPreview4CapabilityTokens.ControlCapabilityCosts,
                NnrpPreview4CapabilityTokens.ControlRouteExecutionHint,
                NnrpPreview4CapabilityTokens.CacheReference,
                NnrpPreview4CapabilityTokens.ControlTraceContext,
                NnrpPreview4CapabilityTokens.ControlResultDropReason,
                NnrpPreview4CapabilityTokens.ControlDegradeProfile,
                NnrpPreview4CapabilityTokens.ControlBudgetUpdate,
                NnrpPreview4CapabilityTokens.ObjectLifecycle,
            ]);
        WireTargetManifestBuilder.ValidateReleaseTarget(manifest);
        string temporaryPath = $"{manifestPath}.tmp";
        WireTargetManifestBuilder.Write(temporaryPath, manifest);
        File.Move(temporaryPath, manifestPath, true);
    }

    private static string ReserveTcpAddress()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            return ((IPEndPoint)listener.LocalEndpoint).ToString();
        }
        finally
        {
            listener.Stop();
        }
    }

    private static string ReserveUdpAddress()
    {
        using UdpClient client = new(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)client.Client.LocalEndPoint!).ToString();
    }

    private static int PortOf(string address) => IPEndPoint.Parse(address).Port;

    private static string CreateIpcEndpoint(string manifestDirectory)
    {
        if (OperatingSystem.IsWindows())
        {
            return $"npipe://nnrp-cs-wire-{Environment.ProcessId}-{Guid.NewGuid():N}";
        }

        string socketPath = Path.Combine(
            manifestDirectory,
            $"nnrp-cs-wire-{Environment.ProcessId}.sock").Replace('\\', '/');
        return $"unix://{socketPath}";
    }

    private static string WithoutScheme(NnrpProviderEndpoint endpoint, string scheme)
    {
        string value = endpoint.ToString();
        string prefix = $"{scheme}://";
        return value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? value[prefix.Length..]
            : throw new InvalidOperationException(
                $"Bound {scheme} endpoint used an unexpected scheme: {value}");
    }

    private static void CleanupIpcEndpoint(string endpoint)
    {
        const string unixPrefix = "unix://";
        if (!endpoint.StartsWith(unixPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string path = endpoint[unixPrefix.Length..];
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static async ValueTask DisposeIgnoringFailureAsync(IAsyncDisposable? resource)
    {
        if (resource is null)
        {
            return;
        }

        try
        {
            await resource.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Preserve the first scenario failure while still releasing native listeners.
        }
    }

    private sealed record WireTargetSecurity(
        NnrpTransportClientSecurity Client,
        NnrpTransportServerSecurity Server);
}
