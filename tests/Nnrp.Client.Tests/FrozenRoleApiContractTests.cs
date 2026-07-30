using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Client;
using Nnrp.Core;
using Nnrp.Runtime;
using Nnrp.Server;
using Xunit;

namespace Nnrp.Client.Tests
{
    public sealed class FrozenRoleApiContractTests
    {
        [Fact]
        public void ClientRoleMatchesTheFrozenPreview4Surface()
        {
            AssertMethod(
                typeof(NnrpClient),
                nameof(NnrpClient.ConnectAsync),
                typeof(ValueTask<NnrpClient>),
                isStatic: true,
                typeof(NnrpClientOptions),
                typeof(CancellationToken));
            AssertMethod(
                typeof(NnrpClient),
                nameof(NnrpClient.OpenSession),
                typeof(NnrpClientSession),
                isStatic: false,
                typeof(NnrpClientSessionOptions));

            AssertMethod(typeof(NnrpClientSession), nameof(NnrpClientSession.SubmitAsync), typeof(ValueTask<NnrpResult>), false, typeof(NnrpSubmitRequest), typeof(CancellationToken));
            AssertMethod(typeof(NnrpClientSession), nameof(NnrpClientSession.SubmitNoWaitAsync), typeof(ValueTask<ulong>), false, typeof(NnrpSubmitRequest), typeof(CancellationToken));
            AssertMethod(typeof(NnrpClientSession), nameof(NnrpClientSession.NextResultAsync), typeof(ValueTask<NnrpResult>), false, typeof(CancellationToken));
            AssertMethod(typeof(NnrpClientSession), nameof(NnrpClientSession.NextEventAsync), typeof(ValueTask<NnrpRuntimeEvent>), false, typeof(CancellationToken));

            AssertTailMethod<ControlRequestMetadata>(nameof(NnrpClientSession.CancelAsync));
            AssertTailMethod<ControlRequestMetadata>(nameof(NnrpClientSession.AbortAsync));
            AssertMetadataMethod<SchedulingMetadata>(nameof(NnrpClientSession.UpdatePriorityAsync));
            AssertMetadataMethod<SchedulingMetadata>(nameof(NnrpClientSession.UpdateDeadlineAsync));
            AssertMetadataMethod<SchedulingMetadata>(nameof(NnrpClientSession.ExpireAtAsync));
            AssertTailMethod<SupersedeMetadata>(nameof(NnrpClientSession.SupersedeAsync));
            AssertMetadataMethod<BudgetMetadata>(nameof(NnrpClientSession.UpdateBudgetAsync));
            AssertTailMethod<CapabilityMetadata>(nameof(NnrpClientSession.NegotiateCapabilitiesAsync));
            AssertTailMethod<CapabilityMetadata>(nameof(NnrpClientSession.DegradeProfileAsync));
            AssertTailMethod<RouteHintMetadata>(nameof(NnrpClientSession.SendRouteHintAsync));
            AssertTailMethod<RouteHintMetadata>(nameof(NnrpClientSession.SendExecutionHintAsync));
            AssertTailMethod<TraceContextMetadata>(nameof(NnrpClientSession.SendTraceContextAsync));
            AssertMethod(
                typeof(NnrpClientSession),
                nameof(NnrpClientSession.SendControlAsync),
                typeof(ValueTask),
                false,
                typeof(MessageType),
                typeof(IRuntimeControlMetadata),
                typeof(ReadOnlyMemory<byte>),
                typeof(CancellationToken));

            AssertClientObjectAndCacheMethods();
        }

        [Fact]
        public void ServerRoleMatchesTheFrozenPreview4Surface()
        {
            AssertMethod(
                typeof(NnrpServer),
                nameof(NnrpServer.ListenAsync),
                typeof(ValueTask<NnrpServer>),
                isStatic: true,
                typeof(NnrpServerOptions),
                typeof(CancellationToken));
            AssertMethod(
                typeof(NnrpServer),
                nameof(NnrpServer.AcceptAsync),
                typeof(ValueTask<NnrpServerSession>),
                isStatic: false,
                typeof(NnrpServerAcceptOptions),
                typeof(CancellationToken));
            AssertMethod(typeof(NnrpServerSession), nameof(NnrpServerSession.ReceiveSubmitAsync), typeof(ValueTask<NnrpServerOperation>), false, typeof(CancellationToken));
            AssertMethod(typeof(NnrpServerSession), nameof(NnrpServerSession.NextEventAsync), typeof(ValueTask<NnrpRuntimeEvent>), false, typeof(CancellationToken));

            AssertServerTailMethod<ProgressMetadata>(nameof(NnrpServerSession.SendProgressAsync));
            AssertServerTailMethod<PartialResultMetadata>(nameof(NnrpServerSession.SendPartialResultAsync));
            AssertServerMetadataMethod<PressureMetadata>(nameof(NnrpServerSession.SendBackpressureAsync));
            AssertServerMetadataMethod<PressureMetadata>(nameof(NnrpServerSession.SendCreditUpdateAsync));
            AssertServerTailMethod<ResultDropReasonMetadata>(nameof(NnrpServerSession.SendResultDropReasonAsync));
            AssertServerTailMethod<TraceContextMetadata>(nameof(NnrpServerSession.SendTraceContextAsync));
            AssertServerTailMethod<RecoverableErrorMetadata>(nameof(NnrpServerSession.SendRecoverableErrorAsync));
            AssertServerTailMethod<RetryAfterMetadata>(nameof(NnrpServerSession.SendRetryAfterAsync));
            AssertMethod(
                typeof(NnrpServerSession),
                nameof(NnrpServerSession.SendControlAsync),
                typeof(ValueTask),
                false,
                typeof(MessageType),
                typeof(IRuntimeControlMetadata),
                typeof(ReadOnlyMemory<byte>),
                typeof(CancellationToken));

            AssertServerObjectAndCacheMethods();
            AssertMethod(typeof(NnrpServerOperation), nameof(NnrpServerOperation.SendResultAsync), typeof(ValueTask), false, typeof(ResultPushMetadata), typeof(ReadOnlyMemory<byte>), typeof(CancellationToken));
            AssertMethod(typeof(NnrpServerOperation), nameof(NnrpServerOperation.SendResultDropAsync), typeof(ValueTask), false, typeof(ResultDropReasonMetadata), typeof(ReadOnlyMemory<byte>), typeof(CancellationToken));
        }

        [Fact]
        public void RuntimeEventAndTerminalStateLiveInTheSharedCoreAssembly()
        {
            Assert.Same(typeof(NnrpRuntimeEvent).Assembly, typeof(NnrpResultTerminalState).Assembly);
            Assert.Same(typeof(RuntimeFrameHeader).Assembly, typeof(NnrpResultTerminalState).Assembly);

            var properties = typeof(NnrpRuntimeEvent)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(property => property.Name, property => property.PropertyType, StringComparer.Ordinal);
            Assert.Equal(typeof(RuntimeFrameHeader), properties[nameof(NnrpRuntimeEvent.Header)]);
            Assert.Equal(typeof(NnrpRuntimeEventMetadata), properties[nameof(NnrpRuntimeEvent.Metadata)]);
            Assert.Equal(typeof(ReadOnlyMemory<byte>), properties[nameof(NnrpRuntimeEvent.Body)]);
            Assert.Equal(typeof(ReadOnlyMemory<byte>), properties[nameof(NnrpRuntimeEvent.Diagnostic)]);
            Assert.Equal(typeof(ReadOnlyMemory<byte>), properties[nameof(NnrpRuntimeEvent.CapabilityEntries)]);
            Assert.Equal(typeof(ReadOnlyMemory<byte>), properties[nameof(NnrpRuntimeEvent.HintBody)]);
            Assert.Equal(typeof(ReadOnlyMemory<byte>), properties[nameof(NnrpRuntimeEvent.TraceAttributes)]);
            Assert.Equal(typeof(ReadOnlyMemory<byte>), properties[nameof(NnrpRuntimeEvent.ObjectMetadata)]);
            Assert.Equal(typeof(ReadOnlyMemory<byte>), properties[nameof(NnrpRuntimeEvent.Delta)]);
            Assert.Equal(typeof(ReadOnlyMemory<byte>), properties[nameof(NnrpRuntimeEvent.CacheMetadata)]);
        }

        [Fact]
        public void ProductionAssembliesDoNotRetainEarlierPreviewManagedRoles()
        {
            Assert.Null(typeof(NnrpClient).Assembly.GetType("Nnrp.Client.ClientProfile"));
            Assert.Null(typeof(NnrpClient).Assembly.GetType("Nnrp.Client.NnrpDiagnosticClient"));
            Assert.Null(typeof(NnrpClient).Assembly.GetType("Nnrp.Client.NnrpDiagnosticClientSession"));
            Assert.Null(typeof(NnrpServer).Assembly.GetType("Nnrp.Server.ServerProfile"));
            Assert.Null(typeof(NnrpServer).Assembly.GetType("Nnrp.Server.NnrpDiagnosticServerSession"));
            Assert.Null(typeof(RuntimeFrameHeader).Assembly.GetType("Nnrp.Core.INnrpMessageTransport"));
            Assert.Null(typeof(RuntimeFrameHeader).Assembly.GetType("Nnrp.Core.NnrpManagedDiagnosticSurfaceAttribute"));
        }

        private static void AssertClientObjectAndCacheMethods()
        {
            AssertTailMethod<ObjectDescriptorMetadata>(nameof(NnrpClientSession.DeclareObjectAsync));
            AssertTailMethod<ObjectReferenceMetadata>(nameof(NnrpClientSession.ReferenceObjectAsync));
            AssertTailMethod<ObjectReleaseMetadata>(nameof(NnrpClientSession.ReleaseObjectAsync));
            AssertClientDeltaMethod(nameof(NnrpClientSession.PatchObjectAsync));
            AssertClientDeltaMethod(nameof(NnrpClientSession.SendObjectDeltaAsync));
            AssertTailMethod<CacheReferenceMetadata>(nameof(NnrpClientSession.ReferenceCacheAsync));
            AssertTailMethod<CacheMissMetadata>(nameof(NnrpClientSession.ReportCacheMissAsync));
            AssertMetadataMethod<CacheInvalidateMetadata>(nameof(NnrpClientSession.InvalidateCacheAsync));
        }

        private static void AssertServerObjectAndCacheMethods()
        {
            AssertServerTailMethod<ObjectDescriptorMetadata>(nameof(NnrpServerSession.DeclareObjectAsync));
            AssertServerTailMethod<ObjectReferenceMetadata>(nameof(NnrpServerSession.ReferenceObjectAsync));
            AssertServerTailMethod<ObjectReleaseMetadata>(nameof(NnrpServerSession.ReleaseObjectAsync));
            AssertServerDeltaMethod(nameof(NnrpServerSession.PatchObjectAsync));
            AssertServerDeltaMethod(nameof(NnrpServerSession.SendObjectDeltaAsync));
            AssertServerTailMethod<CacheReferenceMetadata>(nameof(NnrpServerSession.ReferenceCacheAsync));
            AssertServerTailMethod<CacheMissMetadata>(nameof(NnrpServerSession.ReportCacheMissAsync));
            AssertServerMetadataMethod<CacheInvalidateMetadata>(nameof(NnrpServerSession.InvalidateCacheAsync));
        }

        private static void AssertTailMethod<TMetadata>(string name) where TMetadata : struct =>
            AssertMethod(typeof(NnrpClientSession), name, typeof(ValueTask), false, typeof(TMetadata), typeof(ReadOnlyMemory<byte>), typeof(CancellationToken));

        private static void AssertMetadataMethod<TMetadata>(string name) where TMetadata : struct =>
            AssertMethod(typeof(NnrpClientSession), name, typeof(ValueTask), false, typeof(TMetadata), typeof(CancellationToken));

        private static void AssertClientDeltaMethod(string name) =>
            AssertMethod(typeof(NnrpClientSession), name, typeof(ValueTask), false, typeof(ObjectDeltaMetadata), typeof(ReadOnlyMemory<byte>), typeof(ReadOnlyMemory<byte>), typeof(CancellationToken));

        private static void AssertServerTailMethod<TMetadata>(string name) where TMetadata : struct =>
            AssertMethod(typeof(NnrpServerSession), name, typeof(ValueTask), false, typeof(TMetadata), typeof(ReadOnlyMemory<byte>), typeof(CancellationToken));

        private static void AssertServerMetadataMethod<TMetadata>(string name) where TMetadata : struct =>
            AssertMethod(typeof(NnrpServerSession), name, typeof(ValueTask), false, typeof(TMetadata), typeof(CancellationToken));

        private static void AssertServerDeltaMethod(string name) =>
            AssertMethod(typeof(NnrpServerSession), name, typeof(ValueTask), false, typeof(ObjectDeltaMetadata), typeof(ReadOnlyMemory<byte>), typeof(ReadOnlyMemory<byte>), typeof(CancellationToken));

        private static void AssertMethod(
            Type declaringType,
            string name,
            Type returnType,
            bool isStatic,
            params Type[] parameterTypes)
        {
            var method = declaringType.GetMethod(
                name,
                BindingFlags.Public | (isStatic ? BindingFlags.Static : BindingFlags.Instance),
                binder: null,
                types: parameterTypes,
                modifiers: null);
            Assert.NotNull(method);
            Assert.Equal(returnType, method!.ReturnType);
        }
    }
}
