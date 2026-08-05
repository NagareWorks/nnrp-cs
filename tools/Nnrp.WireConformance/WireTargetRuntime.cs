using Nnrp.Client;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;
using Nnrp.Server;

namespace Nnrp.WireConformance;

internal interface IWireTargetSdk
{
    void ValidateArtifacts(string artifactRoot);

    ValueTask<IWireTargetServer> ListenAsync(
        TransportId transportId,
        NnrpEndpoint endpoint,
        NnrpProviderEndpoint providerEndpoint,
        NnrpTransportServerSecurity? security,
        CancellationToken cancellationToken);

    ValueTask<IWireTargetClient> ConnectAsync(
        TransportId transportId,
        NnrpEndpoint endpoint,
        NnrpProviderEndpoint providerEndpoint,
        NnrpTransportClientSecurity? security,
        CancellationToken cancellationToken);
}

internal interface IWireTargetServer : IAsyncDisposable
{
    NnrpProviderEndpoint BoundProviderEndpoint { get; }

    ValueTask<IWireTargetServerSession> AcceptAsync(CancellationToken cancellationToken);
}

internal interface IWireTargetServerSession : IAsyncDisposable
{
    ValueTask<IWireTargetOperation> ReceiveSubmitAsync(CancellationToken cancellationToken);

    ValueTask<WireTargetReceivedEvent> NextEventAsync(CancellationToken cancellationToken);

    ValueTask SendTraceContextAsync(
        TraceContextMetadata metadata,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken);

    ValueTask ReportCacheMissAsync(
        CacheMissMetadata metadata,
        ReadOnlyMemory<byte> diagnostic,
        CancellationToken cancellationToken);
}

internal interface IWireTargetOperation
{
    ulong OperationId { get; }

    uint FrameId { get; }

    ValueTask SendResultAsync(
        ResultPushMetadata metadata,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken);

    ValueTask SendResultDropAsync(
        ResultDropReasonMetadata metadata,
        ReadOnlyMemory<byte> diagnostic,
        CancellationToken cancellationToken);
}

internal interface IWireTargetClient : IAsyncDisposable
{
    IWireTargetClientSession OpenSession();
}

internal interface IWireTargetClientSession : IAsyncDisposable
{
    ValueTask<ulong> SubmitNoWaitAsync(
        NnrpSubmitRequest request,
        CancellationToken cancellationToken);

    ValueTask<WireTargetReceivedEvent> NextEventAsync(CancellationToken cancellationToken);

    ValueTask<WireTargetTerminalResult> NextResultAsync(CancellationToken cancellationToken);
}

internal sealed class WireTargetReceivedEvent
{
    private readonly NnrpRuntimeEventMetadata? runtimeMetadata;
    private readonly object? testMetadata;

    internal WireTargetReceivedEvent(
        MessageType messageType,
        object? metadata = null,
        ReadOnlyMemory<byte> body = default)
    {
        MessageType = messageType;
        testMetadata = metadata;
        Body = body;
    }

    private WireTargetReceivedEvent(NnrpRuntimeEvent runtimeEvent)
    {
        MessageType = runtimeEvent.Header.MessageType;
        runtimeMetadata = runtimeEvent.Metadata;
        Body = runtimeEvent.Tail.Match(
            () => ReadOnlyMemory<byte>.Empty,
            body => body,
            diagnostic => diagnostic,
            (_, _) => throw new InvalidOperationException(
                "Wire target scenarios do not accept delta tails."));
    }

    internal MessageType MessageType { get; }

    internal ReadOnlyMemory<byte> Body { get; }

    internal T GetMetadata<T>()
        where T : struct
    {
        if (runtimeMetadata is not null)
        {
            return runtimeMetadata.Get<T>();
        }

        return testMetadata is T value
            ? value
            : throw new InvalidOperationException(
                $"Wire target event metadata is not {typeof(T).Name}.");
    }

    internal static WireTargetReceivedEvent FromRuntime(NnrpRuntimeEvent runtimeEvent) =>
        new(runtimeEvent ?? throw new ArgumentNullException(nameof(runtimeEvent)));
}

internal sealed record WireTargetTerminalResult(
    ulong OperationId,
    NnrpResultTerminalState TerminalState,
    WireTargetReceivedEvent Event);

internal sealed class NnrpWireTargetSdk : IWireTargetSdk
{
    private static readonly (string Scope, uint Slot)[] RequiredArtifacts =
    [
        ("tcp", NnrpNativeArtifact.TransportSlotTcp),
        ("quic", NnrpNativeArtifact.TransportSlotQuic),
        ("ipc", NnrpNativeArtifact.TransportSlotIpc),
        ("websocket", NnrpNativeArtifact.TransportSlotWebSocket),
    ];

    public void ValidateArtifacts(string artifactRoot)
    {
        foreach ((string scope, uint slot) in RequiredArtifacts)
        {
            string artifactPath = NnrpNativeArtifact.ResolveTransport(scope, artifactRoot);
            _ = NnrpNativeArtifact.Probe(
                artifactPath,
                requiredTransportSlots: slot);
        }
    }

    public async ValueTask<IWireTargetServer> ListenAsync(
        TransportId transportId,
        NnrpEndpoint endpoint,
        NnrpProviderEndpoint providerEndpoint,
        NnrpTransportServerSecurity? security,
        CancellationToken cancellationToken)
    {
        NnrpServer server = await NnrpServer.ListenAsync(
            new NnrpServerOptions(
                endpoint,
                new Dictionary<TransportId, NnrpServerProviderRoute>
                {
                    [transportId] = new NnrpServerProviderRoute
                    {
                        ProviderEndpoint = providerEndpoint,
                        Security = security,
                    },
                },
                ForcePolicy(transportId)),
            cancellationToken).ConfigureAwait(false);
        return new NnrpWireTargetServer(
            server,
            server.BoundProviderEndpoints[transportId]);
    }

    public async ValueTask<IWireTargetClient> ConnectAsync(
        TransportId transportId,
        NnrpEndpoint endpoint,
        NnrpProviderEndpoint providerEndpoint,
        NnrpTransportClientSecurity? security,
        CancellationToken cancellationToken)
    {
        NnrpClient client = await NnrpClient.ConnectAsync(
            new NnrpClientOptions(
                endpoint,
                new Dictionary<TransportId, NnrpClientProviderRoute>
                {
                    [transportId] = new NnrpClientProviderRoute
                    {
                        ProviderEndpoint = providerEndpoint,
                        Security = security,
                    },
                },
                ForcePolicy(transportId)),
            cancellationToken).ConfigureAwait(false);
        return new NnrpWireTargetClient(client);
    }

    private static TransportPolicy ForcePolicy(TransportId transportId) => transportId switch
    {
        TransportId.Tcp => TransportPolicy.ForceTcp,
        TransportId.Quic => TransportPolicy.ForceQuic,
        TransportId.Ipc => TransportPolicy.ForceIpc,
        TransportId.WebSocket => TransportPolicy.ForceWebSocket,
        _ => throw new ArgumentOutOfRangeException(nameof(transportId)),
    };
}

internal sealed class NnrpWireTargetServer(
    NnrpServer server,
    NnrpProviderEndpoint boundProviderEndpoint) : IWireTargetServer
{
    public NnrpProviderEndpoint BoundProviderEndpoint { get; } = boundProviderEndpoint;

    public async ValueTask<IWireTargetServerSession> AcceptAsync(CancellationToken cancellationToken)
    {
        NnrpServerSession session = await server.AcceptAsync(
            new NnrpServerAcceptOptions(15_000),
            cancellationToken).ConfigureAwait(false);
        return new NnrpWireTargetServerSession(session);
    }

    public ValueTask DisposeAsync() => server.DisposeAsync();
}

internal sealed class NnrpWireTargetServerSession(NnrpServerSession session) : IWireTargetServerSession
{
    public async ValueTask<IWireTargetOperation> ReceiveSubmitAsync(CancellationToken cancellationToken) =>
        new NnrpWireTargetOperation(
            await session.ReceiveSubmitAsync(cancellationToken).ConfigureAwait(false));

    public async ValueTask<WireTargetReceivedEvent> NextEventAsync(CancellationToken cancellationToken) =>
        WireTargetReceivedEvent.FromRuntime(
            await session.NextEventAsync(cancellationToken).ConfigureAwait(false));

    public ValueTask SendTraceContextAsync(
        TraceContextMetadata metadata,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken) =>
        session.SendTraceContextAsync(metadata, body, cancellationToken);

    public ValueTask ReportCacheMissAsync(
        CacheMissMetadata metadata,
        ReadOnlyMemory<byte> diagnostic,
        CancellationToken cancellationToken) =>
        session.ReportCacheMissAsync(metadata, diagnostic, cancellationToken);

    public ValueTask DisposeAsync() => session.DisposeAsync();
}

internal sealed class NnrpWireTargetOperation(NnrpServerOperation operation) : IWireTargetOperation
{
    public ulong OperationId => operation.OperationId;

    public uint FrameId => operation.FrameId;

    public ValueTask SendResultAsync(
        ResultPushMetadata metadata,
        ReadOnlyMemory<byte> body,
        CancellationToken cancellationToken) =>
        operation.SendResultAsync(metadata, body, cancellationToken);

    public ValueTask SendResultDropAsync(
        ResultDropReasonMetadata metadata,
        ReadOnlyMemory<byte> diagnostic,
        CancellationToken cancellationToken) =>
        operation.SendResultDropAsync(metadata, diagnostic, cancellationToken);
}

internal sealed class NnrpWireTargetClient(NnrpClient client) : IWireTargetClient
{
    public IWireTargetClientSession OpenSession() =>
        new NnrpWireTargetClientSession(client.OpenSession());

    public ValueTask DisposeAsync() => client.DisposeAsync();
}

internal sealed class NnrpWireTargetClientSession(NnrpClientSession session) : IWireTargetClientSession
{
    public ValueTask<ulong> SubmitNoWaitAsync(
        NnrpSubmitRequest request,
        CancellationToken cancellationToken) =>
        session.SubmitNoWaitAsync(request, cancellationToken);

    public async ValueTask<WireTargetReceivedEvent> NextEventAsync(CancellationToken cancellationToken) =>
        WireTargetReceivedEvent.FromRuntime(
            await session.NextEventAsync(cancellationToken).ConfigureAwait(false));

    public async ValueTask<WireTargetTerminalResult> NextResultAsync(CancellationToken cancellationToken)
    {
        NnrpResult result = await session.NextResultAsync(cancellationToken).ConfigureAwait(false);
        WireTargetReceivedEvent terminal = result.Event.Match(
            WireTargetReceivedEvent.FromRuntime,
            _ => throw new InvalidOperationException(
                "Preview4 wire conformance requires typed terminal evidence."));
        return new WireTargetTerminalResult(result.OperationId, result.TerminalState, terminal);
    }

    public ValueTask DisposeAsync() => session.DisposeAsync();
}
