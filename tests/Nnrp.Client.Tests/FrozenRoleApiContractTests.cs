using System;
using System.Collections.Generic;
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
                nameof(NnrpClient.OpenSessionAsync),
                typeof(ValueTask<NnrpClientSession>),
                isStatic: false,
                typeof(NnrpClientSessionOptions),
                typeof(CancellationToken));
            AssertMethod(
                typeof(NnrpClient),
                nameof(NnrpClient.ResumeSessionAsync),
                typeof(ValueTask<NnrpClientSession>),
                isStatic: false,
                typeof(NnrpSessionRecoveryTicket),
                typeof(NnrpClientSessionOptions),
                typeof(CancellationToken));

            AssertMethod(typeof(NnrpClientSession), nameof(NnrpClientSession.SubmitAsync), typeof(ValueTask<NnrpResult>), false, typeof(NnrpSubmitRequest), typeof(CancellationToken));
            AssertMethod(typeof(NnrpClientSession), nameof(NnrpClientSession.SubmitNoWaitAsync), typeof(ValueTask<ulong>), false, typeof(NnrpSubmitRequest), typeof(CancellationToken));
            AssertMethod(typeof(NnrpClientSession), nameof(NnrpClientSession.NextResultAsync), typeof(ValueTask<NnrpResult>), false, typeof(CancellationToken));
            AssertMethod(typeof(NnrpClientSession), nameof(NnrpClientSession.NextEventAsync), typeof(ValueTask<NnrpClientEvent>), false, typeof(CancellationToken));
            AssertMethod(typeof(NnrpClientSession), nameof(NnrpClientSession.GetRecoveryTicket), typeof(NnrpSessionRecoveryTicket), false);

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
            AssertTraceContextMethod(typeof(NnrpClientSession), nameof(NnrpClientSession.SendTraceContextAsync));
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
        public void ConfigurationAndRecoveryTypesMatchTheFrozenPreview4Properties()
        {
            AssertProperties(
                typeof(NnrpClientOptions),
                (nameof(NnrpClientOptions.Endpoint), typeof(NnrpEndpoint)),
                (nameof(NnrpClientOptions.ProviderRoutes), typeof(IReadOnlyDictionary<TransportId, NnrpClientProviderRoute>)),
                (nameof(NnrpClientOptions.TransportPolicy), typeof(TransportPolicy)),
                (nameof(NnrpClientOptions.SessionDefaults), typeof(NnrpClientSessionOptions)));
            AssertProperties(
                typeof(NnrpClientSessionOptions),
                (nameof(NnrpClientSessionOptions.RequestedSessionId), typeof(uint)),
                (nameof(NnrpClientSessionOptions.ProfileId), typeof(ushort)),
                (nameof(NnrpClientSessionOptions.SchemaId), typeof(uint)),
                (nameof(NnrpClientSessionOptions.SchemaVersion), typeof(uint)),
                (nameof(NnrpClientSessionOptions.PriorityClass), typeof(SessionPriorityClass)),
                (nameof(NnrpClientSessionOptions.DefaultDeadlineMilliseconds), typeof(uint)),
                (nameof(NnrpClientSessionOptions.MaxInFlightOperations), typeof(ushort)),
                (nameof(NnrpClientSessionOptions.LeaseTtlHintMilliseconds), typeof(uint)),
                (nameof(NnrpClientSessionOptions.AllowResume), typeof(bool)),
                (nameof(NnrpClientSessionOptions.ResumeTokenBytes), typeof(uint)),
                (nameof(NnrpClientSessionOptions.CacheHints), typeof(IReadOnlyList<CacheObjectKind>)));
            AssertProperties(
                typeof(NnrpSessionRecoveryTicket),
                (nameof(NnrpSessionRecoveryTicket.SessionId), typeof(uint)),
                (nameof(NnrpSessionRecoveryTicket.ResumeToken), typeof(ReadOnlyMemory<byte>)),
                (nameof(NnrpSessionRecoveryTicket.ResumeFromOperationId), typeof(ulong?)),
                (nameof(NnrpSessionRecoveryTicket.ResumeWindowMilliseconds), typeof(uint)));
            AssertProperties(
                typeof(NnrpServerOptions),
                (nameof(NnrpServerOptions.Endpoint), typeof(NnrpEndpoint)),
                (nameof(NnrpServerOptions.ProviderRoutes), typeof(IReadOnlyDictionary<TransportId, NnrpServerProviderRoute>)),
                (nameof(NnrpServerOptions.TransportPolicy), typeof(TransportPolicy)),
                (nameof(NnrpServerOptions.SessionDefaults), typeof(NnrpServerSessionOptions)));
            AssertProperties(
                typeof(NnrpServerAcceptOptions),
                (nameof(NnrpServerAcceptOptions.TimeoutMilliseconds), typeof(uint)));
            AssertProperties(
                typeof(NnrpServerSessionOptions),
                (nameof(NnrpServerSessionOptions.SupportedProfiles), typeof(IReadOnlyList<ushort>)),
                (nameof(NnrpServerSessionOptions.SupportedCacheObjects), typeof(IReadOnlyList<CacheObjectKind>)),
                (nameof(NnrpServerSessionOptions.MaxCacheObjects), typeof(ulong)),
                (nameof(NnrpServerSessionOptions.MaxCacheObjectBytes), typeof(uint)),
                (nameof(NnrpServerSessionOptions.SchemaRegistry), typeof(NnrpSchemaRegistry)),
                (nameof(NnrpServerSessionOptions.ResumeTokenBytes), typeof(uint)),
                (nameof(NnrpServerSessionOptions.MaxInFlightOperations), typeof(ushort)),
                (nameof(NnrpServerSessionOptions.GrantedOperationCredit), typeof(ushort)),
                (nameof(NnrpServerSessionOptions.LeaseTtlMilliseconds), typeof(uint)),
                (nameof(NnrpServerSessionOptions.ResumeWindowMilliseconds), typeof(uint)),
                (nameof(NnrpServerSessionOptions.ApplicationPolicy), typeof(INnrpServerSessionPolicy)));
            AssertMethod(
                typeof(INnrpServerSessionPolicy),
                nameof(INnrpServerSessionPolicy.EvaluateAsync),
                typeof(ValueTask<NnrpServerSessionPolicyDecision>),
                false,
                typeof(SessionOpenMetadata));
            AssertProperties(
                typeof(NnrpServerSessionPolicyDecision),
                (nameof(NnrpServerSessionPolicyDecision.Accepted), typeof(bool)),
                (nameof(NnrpServerSessionPolicyDecision.SessionErrorCode), typeof(SessionErrorCode)),
                (nameof(NnrpServerSessionPolicyDecision.Diagnostic), typeof(string)));

            AssertProperties(
                typeof(NnrpCacheLeaseResult),
                (nameof(NnrpCacheLeaseResult.ObjectId), typeof(NnrpCacheObjectId)),
                (nameof(NnrpCacheLeaseResult.Outcome), typeof(NnrpCacheLeaseOutcome)),
                (nameof(NnrpCacheLeaseResult.Lease), typeof(NnrpCacheLease?)),
                (nameof(NnrpCacheLeaseResult.ObjectVersion), typeof(NnrpCacheObjectVersion?)),
                (nameof(NnrpCacheLeaseResult.Diagnostic), typeof(string)));
            AssertProperties(
                typeof(CachePolicyOptions),
                (nameof(CachePolicyOptions.Enabled), typeof(bool)),
                (nameof(CachePolicyOptions.ReuseScope), typeof(CacheReuseScope?)),
                (nameof(CachePolicyOptions.ExpirationHintMilliseconds), typeof(ulong)),
                (nameof(CachePolicyOptions.InvalidationReason), typeof(CachePolicyInvalidationReason)));
        }

        [Fact]
        public void LifecycleTypesMatchTheFrozenPreview4Projection()
        {
            AssertProperties(
                typeof(NnrpConnectionLifecycle),
                (nameof(NnrpConnectionLifecycle.State), typeof(NnrpConnectionLifecycleState)),
                (nameof(NnrpConnectionLifecycle.SessionCount), typeof(int)),
                (nameof(NnrpConnectionLifecycle.Sessions), typeof(IReadOnlyList<NnrpSessionLifecycle>)));
            AssertProperties(
                typeof(NnrpSessionLifecycle),
                (nameof(NnrpSessionLifecycle.SessionId), typeof(uint)),
                (nameof(NnrpSessionLifecycle.State), typeof(NnrpSessionLifecycleState)),
                (nameof(NnrpSessionLifecycle.ProfileId), typeof(ushort)),
                (nameof(NnrpSessionLifecycle.PriorityClass), typeof(SessionPriorityClass)),
                (nameof(NnrpSessionLifecycle.SchemaId), typeof(uint)),
                (nameof(NnrpSessionLifecycle.SchemaVersion), typeof(uint)),
                (nameof(NnrpSessionLifecycle.MaxInFlightOperations), typeof(ushort)),
                (nameof(NnrpSessionLifecycle.RouteScopeId), typeof(uint)),
                (nameof(NnrpSessionLifecycle.LastOperationId), typeof(ulong)),
                (nameof(NnrpSessionLifecycle.SessionErrorCode), typeof(SessionErrorCode)),
                (nameof(NnrpSessionLifecycle.AcceptsSessionScopedMessages), typeof(bool)),
                (nameof(NnrpSessionLifecycle.AcceptsNewOperations), typeof(bool)));
        }

        [Fact]
        public void EveryFrozenCSharpProjectionTypeExistsInThePublishedAssemblies()
        {
            var assemblies = new[]
            {
                typeof(NnrpClient).Assembly,
                typeof(NnrpConnectionLifecycle).Assembly,
                typeof(NnrpServer).Assembly,
            };
            var projectedTypes = new[]
            {
                "Nnrp.Client.NnrpSubmitRequest",
                "Nnrp.Client.NnrpSubmitHeaderContext",
                "Nnrp.Runtime.RuntimeFrameHeader",
                "Nnrp.Runtime.NnrpRuntimeEvent",
                "Nnrp.Runtime.NnrpOperationLifecycleEvent",
                "Nnrp.Runtime.NnrpTerminalEvent",
                "Nnrp.Client.NnrpResult",
                "Nnrp.Client.NnrpClient",
                "Nnrp.Client.NnrpClientSession",
                "Nnrp.Server.NnrpServer",
                "Nnrp.Server.NnrpServerSession",
                "Nnrp.Runtime.CapabilityMetadata",
                "Nnrp.Core.NnrpConnectionLifecycle",
                "Nnrp.Core.NnrpSessionLifecycle",
                "Nnrp.Core.TypedPayloadDescriptor",
                "Nnrp.Core.TypedPayloadFrameView",
                "Nnrp.Core.NnrpCacheObjectId",
                "Nnrp.Core.NnrpCacheLease",
                "Nnrp.Core.NnrpCacheLeaseResult",
                "Nnrp.Core.CachePolicyOptions",
                "Nnrp.Core.NnrpTransportProviderMetadata",
                "Nnrp.Core.NnrpTransportProviderDescriptor",
                "Nnrp.Core.NnrpTransportSelectionOptions",
                "Nnrp.Core.NnrpTransportSelection",
                "Nnrp.Core.NnrpTransportSelectionException",
                "Nnrp.Core.NnrpEndpoint",
                "Nnrp.Core.NnrpProviderEndpoint",
                "Nnrp.Core.NnrpTransportClientSecurity",
                "Nnrp.Core.NnrpTransportServerSecurity",
                "Nnrp.Core.NnrpClientProviderRoute",
                "Nnrp.Core.NnrpServerProviderRoute",
                "Nnrp.Core.NnrpSchemaDescriptorHeader",
                "Nnrp.Core.NnrpSchemaRegistry",
                "Nnrp.Client.NnrpClientOptions",
                "Nnrp.Client.NnrpClientSessionOptions",
                "Nnrp.Core.NnrpSessionRecoveryTicket",
                "Nnrp.Server.NnrpServerOptions",
                "Nnrp.Server.NnrpServerSessionOptions",
                "Nnrp.Server.NnrpServerAcceptOptions",
                "Nnrp.Server.INnrpServerSessionPolicy",
            };

            foreach (var projectedType in projectedTypes)
            {
                Assert.Contains(assemblies, assembly => assembly.GetType(projectedType, throwOnError: false) != null);
            }
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
            AssertMethod(typeof(NnrpServerSession), nameof(NnrpServerSession.NextEventAsync), typeof(ValueTask<NnrpServerEvent>), false, typeof(CancellationToken));

            AssertServerMetadataMethod<PressureMetadata>(nameof(NnrpServerSession.SendBackpressureAsync));
            AssertServerMetadataMethod<PressureMetadata>(nameof(NnrpServerSession.SendCreditUpdateAsync));
            AssertServerTailMethod<CapabilityMetadata>(nameof(NnrpServerSession.NegotiateCapabilitiesAsync));
            AssertServerTailMethod<CapabilityMetadata>(nameof(NnrpServerSession.DegradeProfileAsync));
            AssertTraceContextMethod(typeof(NnrpServerSession), nameof(NnrpServerSession.SendTraceContextAsync));
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
            AssertMethod(typeof(NnrpServerOperation), nameof(NnrpServerOperation.SendProgressAsync), typeof(ValueTask), false, typeof(ProgressMetadata), typeof(ReadOnlyMemory<byte>), typeof(CancellationToken));
            AssertMethod(typeof(NnrpServerOperation), nameof(NnrpServerOperation.SendPartialResultAsync), typeof(ValueTask), false, typeof(PartialResultMetadata), typeof(ReadOnlyMemory<byte>), typeof(CancellationToken));
            Assert.Null(typeof(NnrpServerSession).GetMethod("SendProgressAsync"));
            Assert.Null(typeof(NnrpServerSession).GetMethod("SendPartialResultAsync"));
            Assert.Null(typeof(NnrpServerSession).GetMethod("SendResultDropAsync"));
            Assert.Null(typeof(NnrpServerSession).GetMethod("SendResultDropReasonAsync"));
        }

        [Fact]
        public void RuntimeAndTerminalEvidenceMatchTheFrozenClosedUnions()
        {
            Assert.Same(typeof(NnrpRuntimeEvent).Assembly, typeof(NnrpResultTerminalState).Assembly);
            Assert.Same(typeof(RuntimeFrameHeader).Assembly, typeof(NnrpResultTerminalState).Assembly);

            AssertProperties(
                typeof(NnrpResult),
                (nameof(NnrpResult.OperationId), typeof(ulong)),
                (nameof(NnrpResult.TerminalState), typeof(NnrpResultTerminalState)),
                (nameof(NnrpResult.Event), typeof(NnrpTerminalEvent)));
            AssertProperties(
                typeof(NnrpRuntimeEvent),
                (nameof(NnrpRuntimeEvent.Header), typeof(RuntimeFrameHeader)),
                (nameof(NnrpRuntimeEvent.Metadata), typeof(NnrpRuntimeEventMetadata)),
                (nameof(NnrpRuntimeEvent.Tail), typeof(NnrpRuntimeEventTail)));
            AssertProperties(
                typeof(NnrpRuntimeEventTail),
                (nameof(NnrpRuntimeEventTail.Kind), typeof(NnrpRuntimeEventTailKind)));
            AssertProperties(
                typeof(NnrpTerminalEvent),
                (nameof(NnrpTerminalEvent.Kind), typeof(NnrpTerminalEventKind)));
            AssertProperties(
                typeof(NnrpOperationLifecycleEvent),
                (nameof(NnrpOperationLifecycleEvent.OperationId), typeof(ulong)),
                (nameof(NnrpOperationLifecycleEvent.State), typeof(NnrpOperationState)));
            AssertProperties(
                typeof(NnrpClientEvent),
                (nameof(NnrpClientEvent.Kind), typeof(NnrpClientEventKind)));
            AssertProperties(
                typeof(NnrpServerEvent),
                (nameof(NnrpServerEvent.Kind), typeof(NnrpServerEventKind)));
            Assert.Single(
                typeof(NnrpClientEvent).GetMethods(BindingFlags.Public | BindingFlags.Instance),
                method => method.Name == nameof(NnrpClientEvent.Match));
            Assert.Single(
                typeof(NnrpServerEvent).GetMethods(BindingFlags.Public | BindingFlags.Instance),
                method => method.Name == nameof(NnrpServerEvent.Match));
            Assert.Single(
                typeof(NnrpRuntimeEventTail).GetMethods(BindingFlags.Public | BindingFlags.Instance),
                method => method.Name == nameof(NnrpRuntimeEventTail.Match));
            Assert.Single(
                typeof(NnrpTerminalEvent).GetMethods(BindingFlags.Public | BindingFlags.Instance),
                method => method.Name == nameof(NnrpTerminalEvent.Match));
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

        private static void AssertTraceContextMethod(Type declaringType, string name) =>
            AssertMethod(
                declaringType,
                name,
                typeof(ValueTask),
                false,
                typeof(TraceContextMetadata),
                typeof(ReadOnlyMemory<byte>),
                typeof(ulong?),
                typeof(CancellationToken));

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

        private static void AssertProperties(
            Type type,
            params (string Name, Type Type)[] expected)
        {
            var properties = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .ToDictionary(property => property.Name, property => property.PropertyType, StringComparer.Ordinal);
            Assert.Equal(expected.Length, properties.Count);
            foreach (var property in expected)
            {
                Assert.True(properties.TryGetValue(property.Name, out var actualType));
                Assert.Equal(property.Type, actualType);
            }
        }
    }
}
