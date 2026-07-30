using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;
using Nnrp.Server;
using Nnrp.Transport.Ipc;
using Xunit;

namespace Nnrp.Client.Tests
{
    public sealed class LiveRuntimeRoleTests
    {
        [LiveRuntimeRoleFact]
        public async Task ProductionRolesExchangePreview4FramesThroughRust()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var artifactPath = Environment.GetEnvironmentVariable(
                LiveRuntimeRoleFactAttribute.ArtifactPathVariableName)!;
            var providerEndpoint = NnrpProviderEndpoint.Parse($"npipe://nnrp-cs-role-{Guid.NewGuid():N}");
            var serverProvider = new NnrpNativeIpcTransportProvider(artifactPath);
            await using var server = await NnrpServer.ListenAsync(
                new NnrpServerOptions(
                    NnrpEndpoint.Parse("nnrp://abi.local/runtime"),
                    new Dictionary<TransportId, NnrpServerProviderRoute>
                    {
                        [TransportId.Ipc] = new NnrpServerProviderRoute
                        {
                            ProviderEndpoint = providerEndpoint,
                        },
                    },
                    transportPolicy: TransportPolicy.ForceIpc,
                    transports: new[] { serverProvider }),
                timeout.Token);

            var boundProviderEndpoint = server.BoundProviderEndpoints[TransportId.Ipc];
            Assert.Equal("npipe", boundProviderEndpoint.Scheme);
            var acceptTask = server.AcceptAsync(
                new NnrpServerAcceptOptions(timeoutMilliseconds: 15_000),
                timeout.Token).AsTask();

            var clientProvider = new NnrpNativeIpcTransportProvider(artifactPath);
            await using var client = await NnrpClient.ConnectAsync(
                new NnrpClientOptions(
                    NnrpEndpoint.Parse("nnrp://abi.local/runtime"),
                    new Dictionary<TransportId, NnrpClientProviderRoute>
                    {
                        [TransportId.Ipc] = new NnrpClientProviderRoute
                        {
                            ProviderEndpoint = boundProviderEndpoint,
                        },
                    },
                    transportPolicy: TransportPolicy.ForceIpc,
                    transports: new[] { clientProvider }),
                timeout.Token);
            await using var clientSession = client.OpenSession();
            await using var serverSession = await acceptTask;

            Assert.Equal(TransportId.Ipc, client.ActiveTransportId);
            Assert.Equal(TransportId.Ipc, serverSession.ActiveTransportId);
            Assert.NotEqual((uint)0, clientSession.Options.SessionId);
            Assert.Equal(TypedPayloadProfileId.Token.Value, clientSession.Options.ProfileId);
            Assert.Equal(TypedPayloadDescriptor.TokenDeltaSchemaId, clientSession.Options.SchemaId);
            Assert.Equal(TypedPayloadDescriptor.TokenDeltaSchemaVersion, clientSession.Options.SchemaVersion);

            async Task CloseRolesAsync()
            {
                var closingClient = clientSession.DisposeAsync().AsTask();
                var closingEvent = await serverSession.NextEventAsync(timeout.Token);
                Assert.Equal(MessageType.SessionClose, closingEvent.Header.MessageType);
                await serverSession.DisposeAsync();
                await closingClient;
            }

            var request = NnrpSubmitRequest.CreateToken(new NnrpTokenSubmitInput(
                new NnrpSubmitIdentity(701, 71, new NnrpSubmitHeaderContext(traceId: 7001)),
                new NnrpSubmitPolicy(),
                new[] { new NnrpTokenChunk(new byte[] { 1, 2, 3 }) }));
            var receiveTask = serverSession.ReceiveSubmitAsync(timeout.Token).AsTask();
            Assert.Equal((ulong)701, await clientSession.SubmitNoWaitAsync(request, timeout.Token));
            var operation = await receiveTask;
            Assert.Equal((ulong)701, operation.OperationId);
            Assert.Equal((uint)71, operation.FrameId);
            Assert.Equal((ulong)7001, operation.TraceId);
            Assert.Equal(PayloadKind.TokenChunk, operation.Metadata.PayloadKindBitmap);

            var clientTrace = new TraceContextMetadata(701, 11, 12, 13, 0, 2);
            await clientSession.SendTraceContextAsync(clientTrace, new byte[] { 4, 5 }, timeout.Token);
            var serverTrace = await serverSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.TraceContext, serverTrace.Header.MessageType);
            Assert.Equal(clientTrace, serverTrace.Metadata.Get<TraceContextMetadata>());
            Assert.Equal(new byte[] { 4, 5 }, serverTrace.TraceAttributes.ToArray());

            var clientCapability = new CapabilityMetadata(701, 12, 1, 2, 3, 4, 3, 0);
            await clientSession.NegotiateCapabilitiesAsync(
                clientCapability,
                new byte[] { 20, 21, 22 },
                timeout.Token);
            var serverCapability = await serverSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.CapabilityNegotiation, serverCapability.Header.MessageType);
            Assert.Equal(clientCapability, serverCapability.Metadata.Get<CapabilityMetadata>());
            Assert.Equal(new byte[] { 20, 21, 22 }, serverCapability.CapabilityEntries.ToArray());

            var clientRoute = new RouteHintMetadata(701, 13, 1, 2, 3, 2, 0);
            await clientSession.SendRouteHintAsync(clientRoute, new byte[] { 23, 24 }, timeout.Token);
            var serverRoute = await serverSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.RouteHint, serverRoute.Header.MessageType);
            Assert.Equal(clientRoute, serverRoute.Metadata.Get<RouteHintMetadata>());
            Assert.Equal(new byte[] { 23, 24 }, serverRoute.HintBody.ToArray());

            var clientObject = new ObjectDescriptorMetadata(
                81,
                RuntimeObjectKind.Tensor,
                RuntimeRole.Client,
                RuntimeRole.Server,
                1,
                2,
                3,
                MemoryLocationHint.HostMemory,
                OwnershipHint.TransferOnRef,
                4,
                2);
            await clientSession.DeclareObjectAsync(clientObject, new byte[] { 6, 7 }, timeout.Token);
            var serverObject = await serverSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.ObjectDeclare, serverObject.Header.MessageType);
            Assert.Equal(clientObject, serverObject.Metadata.Get<ObjectDescriptorMetadata>());

            var clientCache = new CacheReferenceMetadata(
                701,
                81,
                1,
                2,
                CacheReuseScope.Session,
                3,
                4,
                5,
                2,
                0);
            await clientSession.ReferenceCacheAsync(clientCache, new byte[] { 8, 9 }, timeout.Token);
            var serverCache = await serverSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.CacheReference, serverCache.Header.MessageType);
            Assert.Equal(clientCache, serverCache.Metadata.Get<CacheReferenceMetadata>());

            var clientDelta = new ObjectDeltaMetadata(81, 1, 2, 1, 2, 0, 2);
            await clientSession.SendObjectDeltaAsync(
                clientDelta,
                new byte[] { 25, 26 },
                new byte[] { 27, 28 },
                timeout.Token);
            var serverDelta = await serverSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.ObjectDelta, serverDelta.Header.MessageType);
            Assert.Equal(clientDelta, serverDelta.Metadata.Get<ObjectDeltaMetadata>());
            Assert.Equal(new byte[] { 25, 26 }, serverDelta.ObjectMetadata.ToArray());
            Assert.Equal(new byte[] { 27, 28 }, serverDelta.Delta.ToArray());

            var clientInvalidate = new CacheInvalidateMetadata(
                CacheInvalidateScope.ObjectKey,
                1,
                2,
                3,
                4);
            await clientSession.InvalidateCacheAsync(clientInvalidate, timeout.Token);
            var serverInvalidate = await serverSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.CacheInvalidate, serverInvalidate.Header.MessageType);
            Assert.Equal(clientInvalidate, serverInvalidate.Metadata.Get<CacheInvalidateMetadata>());

            var progress = new ProgressMetadata(701, 1, 2, 5000, 0, 2);
            await serverSession.SendProgressAsync(progress, new byte[] { 10, 11 }, timeout.Token);
            var clientProgress = await clientSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.Progress, clientProgress.Header.MessageType);
            Assert.Equal(progress, clientProgress.Metadata.Get<ProgressMetadata>());

            var partial = new PartialResultMetadata(701, 2, 3, 4, 2, 0);
            await serverSession.SendPartialResultAsync(partial, new byte[] { 12, 13 }, timeout.Token);
            var clientPartial = await clientSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.PartialResult, clientPartial.Header.MessageType);
            Assert.Equal(partial, clientPartial.Metadata.Get<PartialResultMetadata>());

            var serverTraceMetadata = new TraceContextMetadata(701, 21, 22, 23, 0, 2);
            await serverSession.SendTraceContextAsync(
                serverTraceMetadata,
                new byte[] { 29, 30 },
                timeout.Token);
            var clientTraceEvent = await clientSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.TraceContext, clientTraceEvent.Header.MessageType);
            Assert.Equal(serverTraceMetadata, clientTraceEvent.Metadata.Get<TraceContextMetadata>());
            Assert.Equal(new byte[] { 29, 30 }, clientTraceEvent.TraceAttributes.ToArray());

            var recoverableError = new RecoverableErrorMetadata(
                501,
                2,
                3,
                RuntimeRole.Server,
                0,
                4,
                41,
                71,
                0,
                2);
            await serverSession.SendRecoverableErrorAsync(
                recoverableError,
                new byte[] { 31, 32 },
                timeout.Token);
            var clientError = await clientSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.ErrorRecoverable, clientError.Header.MessageType);
            Assert.Equal(recoverableError, clientError.Metadata.Get<RecoverableErrorMetadata>());
            Assert.Equal(new byte[] { 31, 32 }, clientError.Diagnostic.ToArray());

            var serverObjectReference = new ObjectReferenceMetadata(81, 701, 3, 4, 5, 0, 2);
            await serverSession.ReferenceObjectAsync(
                serverObjectReference,
                new byte[] { 14, 15 },
                timeout.Token);
            var clientObjectReference = await clientSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.ObjectRef, clientObjectReference.Header.MessageType);
            Assert.Equal(serverObjectReference, clientObjectReference.Metadata.Get<ObjectReferenceMetadata>());

            var serverCacheMiss = new CacheMissMetadata(701, 81, 2, CacheMissReason.NotFound, 3, 2);
            await serverSession.ReportCacheMissAsync(serverCacheMiss, new byte[] { 16, 17 }, timeout.Token);
            var clientCacheMiss = await clientSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.CacheMiss, clientCacheMiss.Header.MessageType);
            Assert.Equal(serverCacheMiss, clientCacheMiss.Metadata.Get<CacheMissMetadata>());

            var serverDeltaMetadata = new ObjectDeltaMetadata(81, 2, 3, 2, 2, 0, 1);
            await serverSession.PatchObjectAsync(
                serverDeltaMetadata,
                new byte[] { 33 },
                new byte[] { 34, 35 },
                timeout.Token);
            var clientDeltaEvent = await clientSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.ObjectPatch, clientDeltaEvent.Header.MessageType);
            Assert.Equal(serverDeltaMetadata, clientDeltaEvent.Metadata.Get<ObjectDeltaMetadata>());
            Assert.Equal(new byte[] { 33 }, clientDeltaEvent.ObjectMetadata.ToArray());
            Assert.Equal(new byte[] { 34, 35 }, clientDeltaEvent.Delta.ToArray());

            var serverInvalidateMetadata = new CacheInvalidateMetadata(
                CacheInvalidateScope.ObjectKey,
                1,
                2,
                3,
                5);
            await serverSession.InvalidateCacheAsync(serverInvalidateMetadata, timeout.Token);
            var clientInvalidateEvent = await clientSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.CacheInvalidate, clientInvalidateEvent.Header.MessageType);
            Assert.Equal(
                serverInvalidateMetadata,
                clientInvalidateEvent.Metadata.Get<CacheInvalidateMetadata>());

            await operation.SendResultAsync(
                new ResultPushMetadata(
                    ResultStatusCode.Success,
                    ResultFlags.None,
                    0,
                    PayloadKind.TokenChunk,
                    1,
                    1,
                    2,
                    3,
                    0,
                    0,
                    0,
                    0),
                new byte[] { 18, 19 },
                timeout.Token);
            var result = await clientSession.NextResultAsync(timeout.Token);
            Assert.Equal((ulong)701, result.OperationId);
            Assert.Equal(NnrpResultTerminalState.Success, result.TerminalState);
            Assert.Equal(new byte[] { 18, 19 }, result.Body.ToArray());

            var cancelledRequest = NnrpSubmitRequest.CreateToken(new NnrpTokenSubmitInput(
                new NnrpSubmitIdentity(702, 72, new NnrpSubmitHeaderContext(traceId: 7002)),
                new NnrpSubmitPolicy(),
                new[] { new NnrpTokenChunk(new byte[] { 36, 37 }) }));
            var receiveCancelledTask = serverSession.ReceiveSubmitAsync(timeout.Token).AsTask();
            Assert.Equal(
                (ulong)702,
                await clientSession.SubmitNoWaitAsync(cancelledRequest, timeout.Token));
            var cancelledOperation = await receiveCancelledTask;
            var cancel = new ControlRequestMetadata(702, 31, 9, RuntimeRole.Client, 0, 2);
            await clientSession.CancelAsync(cancel, new byte[] { 38, 39 }, timeout.Token);
            var serverCancel = await serverSession.NextEventAsync(timeout.Token);
            Assert.Equal(MessageType.Cancel, serverCancel.Header.MessageType);
            Assert.Equal(cancel, serverCancel.Metadata.Get<ControlRequestMetadata>());
            Assert.Equal(new byte[] { 38, 39 }, serverCancel.Diagnostic.ToArray());

            await serverSession.SendPartialResultAsync(
                new PartialResultMetadata(702, 1, 2, 3, 2, 0),
                new byte[] { 40, 41 },
                timeout.Token);
            var cancellationTrace = new TraceContextMetadata(702, 32, 33, 34, 0, 1);
            await serverSession.SendTraceContextAsync(
                cancellationTrace,
                new byte[] { 42 },
                timeout.Token);
            var drop = new ResultDropReasonMetadata(
                702,
                33,
                NnrpResultDropReasonCode.PeerCancelled,
                RuntimeRole.Server,
                0,
                2);
            await cancelledOperation.SendResultDropAsync(drop, new byte[] { 43, 44 }, timeout.Token);
            var cancelledResult = await clientSession.NextResultAsync(timeout.Token);
            var postCancellationTrace = await clientSession.NextEventAsync(timeout.Token);
            Assert.Equal((ulong)702, cancelledResult.OperationId);
            Assert.Equal(NnrpResultTerminalState.Cancelled, cancelledResult.TerminalState);
            Assert.Equal(drop, cancelledResult.DropMetadata);
            Assert.Equal(new byte[] { 43, 44 }, cancelledResult.Diagnostic.ToArray());
            Assert.Equal(MessageType.TraceContext, postCancellationTrace.Header.MessageType);
            Assert.Equal(
                cancellationTrace,
                postCancellationTrace.Metadata.Get<TraceContextMetadata>());

            await CloseRolesAsync();
        }

        [LiveRuntimeRoleFact]
        public void NativeStatusIdentitySurvivesManagedExceptionProjection()
        {
            var artifactPath = Environment.GetEnvironmentVariable(
                LiveRuntimeRoleFactAttribute.ArtifactPathVariableName)!;
            using var entrypoints = NnrpNativeRuntimeEntrypoints.Load(
                artifactPath,
                requiredTransportSlots: NnrpNativeArtifact.TransportSlotIpc,
                transportScope: "ipc");
            var status = entrypoints.SchemaRegistryLookup(
                NnrpHandle.Invalid,
                TypedPayloadDescriptor.TokenDeltaSchemaId,
                TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                out _);

            Assert.False(status.Succeeded);
            var error = Assert.ThrowsAny<NnrpNativeRuntimeException>(status.ThrowIfError);
            Assert.Equal(status.StatusCode, error.Status.StatusCode);
            Assert.Equal(status.ErrorFamily, error.Status.ErrorFamily);
            Assert.Equal(status.ProtocolErrorCode, error.Status.ProtocolErrorCode);
            Assert.Equal(status.DetailCode, error.Status.DetailCode);
        }
    }
}
