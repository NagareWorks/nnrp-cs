using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.NativeBridge;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class NnrpNativeArtifactTests
    {
        [Theory]
        [InlineData("windows", "nnrp_ffi.dll")]
        [InlineData("win32", "nnrp_ffi.dll")]
        [InlineData("linux", "libnnrp_ffi.so")]
        [InlineData("android", "libnnrp_ffi.so")]
        [InlineData("darwin", "libnnrp_ffi.dylib")]
        [InlineData("ios", "libnnrp_ffi.dylib")]
        public void LibraryNameMatchesSupportedPlatforms(string osName, string expected)
        {
            Assert.Equal(expected, NnrpNativeArtifact.LibraryName(osName));
        }

        [Theory]
        [InlineData("windows", "x86_64", "win-x64")]
        [InlineData("windows", "i386", "win-x86")]
        [InlineData("macos", "arm64", "osx-arm64")]
        [InlineData("linux", "aarch64", "linux-arm64")]
        [InlineData("android", "armv7", "android-arm")]
        [InlineData("ios", "amd64", "ios-x64")]
        public void PlatformNormalizesRuntimeIdentifier(string osName, string architecture, string expected)
        {
            var platform = new NnrpNativePlatform(osName, architecture);

            Assert.Equal(expected, platform.RuntimeIdentifier);
        }

        [Fact]
        public void PlatformValueEqualityUsesNormalizedValues()
        {
            var left = new NnrpNativePlatform("win32", "amd64");
            var right = new NnrpNativePlatform("windows", "x86_64");
            var different = new NnrpNativePlatform("linux", "x86_64");

            Assert.Equal(left, right);
            Assert.True(left == right);
            Assert.False(left != right);
            Assert.NotEqual(left, different);
            Assert.False(left.Equals("not-a-platform"));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void ExceptionKeepsInnerException()
        {
            var inner = new InvalidOperationException("inner");

            var error = new NnrpNativeArtifactException("outer", inner);

            Assert.Equal("outer", error.Message);
            Assert.Same(inner, error.InnerException);
        }

        [Fact]
        public void DefaultPlatformRejectsRuntimeIdentifier()
        {
            var platform = default(NnrpNativePlatform);

            Assert.Throws<NnrpNativeArtifactException>(() => platform.RuntimeIdentifier);
        }

        [Fact]
        public void DefaultArtifactRootUsesEnvironmentWhenConfigured()
        {
            string? previous = Environment.GetEnvironmentVariable(NnrpNativeArtifact.ArtifactRootEnvironmentVariable);
            string root = CreateTempDirectory();
            try
            {
                Environment.SetEnvironmentVariable(NnrpNativeArtifact.ArtifactRootEnvironmentVariable, root);

                Assert.Equal(root, NnrpNativeArtifact.DefaultArtifactRoot);
            }
            finally
            {
                Environment.SetEnvironmentVariable(NnrpNativeArtifact.ArtifactRootEnvironmentVariable, previous);
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void DefaultArtifactRootFallsBackToBaseDirectory()
        {
            string? previous = Environment.GetEnvironmentVariable(NnrpNativeArtifact.ArtifactRootEnvironmentVariable);
            try
            {
                Environment.SetEnvironmentVariable(NnrpNativeArtifact.ArtifactRootEnvironmentVariable, null);

                Assert.EndsWith("native_artifacts", NnrpNativeArtifact.DefaultArtifactRoot, StringComparison.Ordinal);
            }
            finally
            {
                Environment.SetEnvironmentVariable(NnrpNativeArtifact.ArtifactRootEnvironmentVariable, previous);
            }
        }

        [Fact]
        public void ResolveUsesNuGetRuntimeNativeLayout()
        {
            string root = CreateTempDirectory();
            try
            {
                string artifactDirectory = Path.Combine(root, "runtimes", "linux-x64", "native");
                Directory.CreateDirectory(artifactDirectory);
                string artifactPath = Path.Combine(artifactDirectory, "libnnrp_ffi.so");
                File.WriteAllBytes(artifactPath, new byte[] { 1 });

                Assert.Equal(
                    artifactPath,
                    NnrpNativeArtifact.Resolve(root, new NnrpNativePlatform("linux", "x86_64")));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void ResolveRejectsMissingArtifact()
        {
            string root = CreateTempDirectory();
            try
            {
                var error = Assert.Throws<NnrpNativeArtifactException>(() =>
                    NnrpNativeArtifact.Resolve(root, new NnrpNativePlatform("linux", "x86_64")));

                Assert.Contains("Native artifact was not found", error.Message, StringComparison.Ordinal);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void ProbeAcceptsMatchingProtocol()
        {
            var result = NnrpNativeArtifact.Probe(
                "fake-path",
                runtimeCapabilities: () => MatchingCapabilities());

            Assert.Equal("fake-path", result.ArtifactPath);
            Assert.Equal(1, result.AbiMajor);
            Assert.Equal(0, result.AbiMinor);
            Assert.Equal(0, result.AbiPatch);
            Assert.Equal(1, result.ProtocolMajor);
            Assert.Equal(0, result.ProtocolWireFormat);
            Assert.Equal(1, result.SdkMajor);
            Assert.Equal(0, result.SdkMinor);
            Assert.Equal(0, result.SdkPatch);
            Assert.Equal(3, result.SdkChannel);
            Assert.Equal(1, result.SdkRevision);
            Assert.Equal(NnrpNativeArtifact.TransportSlotTcp, result.TransportSlots);
            Assert.Equal(NnrpNativeArtifact.RequiredRuntimeFeatures, result.FeatureFlags);
        }

        [Fact]
        public void ProbeCanResolveArtifactFromRootBeforeCallingInjectedProbe()
        {
            string root = CreateTempDirectory();
            try
            {
                string artifactDirectory = Path.Combine(root, "runtimes", "win-x64", "native");
                Directory.CreateDirectory(artifactDirectory);
                string artifactPath = Path.Combine(artifactDirectory, "nnrp_ffi.dll");
                File.WriteAllBytes(artifactPath, new byte[] { 1 });

                var result = NnrpNativeArtifact.Probe(
                    artifactRoot: root,
                    platform: new NnrpNativePlatform("windows", "x64"),
                    runtimeCapabilities: () => MatchingCapabilities());

                Assert.Equal(artifactPath, result.ArtifactPath);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void ProbeRejectsProtocolMismatch()
        {
            var error = Assert.Throws<NnrpNativeArtifactException>(() =>
                NnrpNativeArtifact.Probe(
                    "fake-path",
                    runtimeCapabilities: () => MatchingCapabilities(protocolMajor: 2)));

            Assert.Contains("protocol mismatch", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProbeRejectsAbiMismatch()
        {
            var error = Assert.Throws<NnrpNativeArtifactException>(() =>
                NnrpNativeArtifact.Probe(
                    "fake-path",
                    runtimeCapabilities: () => MatchingCapabilities(abiMajor: 2)));

            Assert.Contains("ABI mismatch", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProbeRejectsMissingRequiredFeature()
        {
            var error = Assert.Throws<NnrpNativeArtifactException>(() =>
                NnrpNativeArtifact.Probe(
                    "fake-path",
                    runtimeCapabilities: () => MatchingCapabilities(
                        featureFlags: NnrpNativeArtifact.RequiredRuntimeFeatures & ~NnrpNativeArtifact.RuntimeFeatureProtocolCore)));

            Assert.Contains("required runtime feature flags", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProbeRejectsMissingRequiredTransportSlot()
        {
            var error = Assert.Throws<NnrpNativeArtifactException>(() =>
                NnrpNativeArtifact.Probe(
                    "fake-path",
                    runtimeCapabilities: () => MatchingCapabilities(transportSlots: NnrpNativeArtifact.TransportSlotQuic)));

            Assert.Contains("required transport slots", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void ProbeDoesNotRequireQuicTransportSlot()
        {
            var result = NnrpNativeArtifact.Probe(
                "fake-path",
                runtimeCapabilities: () => MatchingCapabilities(transportSlots: NnrpNativeArtifact.TransportSlotTcp));

            Assert.Equal(NnrpNativeArtifact.TransportSlotTcp, result.TransportSlots);
        }

        [Fact]
        public void ProbeRejectsMissingResolvedArtifactBeforeInjectedProbeRuns()
        {
            string root = CreateTempDirectory();
            try
            {
                Assert.Throws<NnrpNativeArtifactException>(() =>
                    NnrpNativeArtifact.Probe(
                        artifactRoot: root,
                        platform: new NnrpNativePlatform("windows", "x64"),
                        runtimeCapabilities: () => MatchingCapabilities()));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Fact]
        public void PlatformRejectsUnsupportedValues()
        {
            Assert.Throws<NnrpNativeArtifactException>(() => new NnrpNativePlatform("plan9", "x64"));
            Assert.Throws<NnrpNativeArtifactException>(() => new NnrpNativePlatform("linux", "sparc"));
            Assert.Throws<ArgumentException>(() => new NnrpNativePlatform("", "x64"));
            Assert.Throws<ArgumentException>(() => new NnrpNativePlatform("linux", ""));
        }

        [Fact]
        public void NativeHandleKeepsStableFfiShapeAndValueEquality()
        {
            var left = new NnrpHandle(NnrpHandleKind.Connection, 7, 2);
            var right = new NnrpHandle(NnrpHandleKind.Connection, 7, 2);
            var different = new NnrpHandle(NnrpHandleKind.Session, 7, 2);

            Assert.True(left.IsValid);
            Assert.Equal(NnrpHandleKind.Connection, left.Kind);
            Assert.Equal((ulong)7, left.Id);
            Assert.Equal((uint)2, left.Generation);
            Assert.Equal((uint)0, left.Flags);
            Assert.Equal(left, right);
            Assert.True(left == right);
            Assert.False(left != right);
            Assert.NotEqual(left, different);
            Assert.False(left.Equals("not-a-handle"));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void NativeHandleInvalidShapeIsZeroOnly()
        {
            var invalid = NnrpHandle.Invalid;

            Assert.False(invalid.IsValid);
            Assert.Equal(NnrpHandleKind.Invalid, invalid.Kind);
            Assert.Throws<ArgumentException>(() => new NnrpHandle(NnrpHandleKind.Invalid, 1, 0));
        }

        [Fact]
        public void NativeHandleRejectsMissingIdOrGeneration()
        {
            Assert.Throws<ArgumentException>(() => new NnrpHandle(NnrpHandleKind.Connection, 0, 1));
            Assert.Throws<ArgumentException>(() => new NnrpHandle(NnrpHandleKind.Connection, 1, 0));
        }

        [Fact]
        public void TypedNativeHandlesAcceptOnlyMatchingKinds()
        {
            Assert.Equal(NnrpHandleKind.Connection, new NnrpConnectionHandle(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)).Handle.Kind);
            Assert.Equal(NnrpHandleKind.Session, new NnrpSessionHandle(new NnrpHandle(NnrpHandleKind.Session, 2, 1)).Handle.Kind);
            Assert.Equal(NnrpHandleKind.Operation, new NnrpOperationHandle(new NnrpHandle(NnrpHandleKind.Operation, 3, 1)).Handle.Kind);
            Assert.Equal(NnrpHandleKind.EventPump, new NnrpEventPumpHandle(new NnrpHandle(NnrpHandleKind.EventPump, 4, 1)).Handle.Kind);
            Assert.Equal(NnrpHandleKind.Buffer, new NnrpBufferHandle(new NnrpHandle(NnrpHandleKind.Buffer, 5, 1)).Handle.Kind);

            Assert.Throws<ArgumentException>(() => new NnrpConnectionHandle(new NnrpHandle(NnrpHandleKind.Session, 2, 1)));
            Assert.Throws<ArgumentException>(() => new NnrpSessionHandle(new NnrpHandle(NnrpHandleKind.Operation, 3, 1)));
            Assert.Throws<ArgumentException>(() => new NnrpOperationHandle(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)));
            Assert.Throws<ArgumentException>(() => new NnrpEventPumpHandle(new NnrpHandle(NnrpHandleKind.Buffer, 5, 1)));
            Assert.Throws<ArgumentException>(() => new NnrpBufferHandle(new NnrpHandle(NnrpHandleKind.EventPump, 4, 1)));
        }

        [Fact]
        public void BufferViewsAcceptEmptyOrNonNullPointers()
        {
            var view = new NnrpBufferView(new IntPtr(0x1000), new UIntPtr(64));
            var mutableView = new NnrpMutableBufferView(new IntPtr(0x2000), new UIntPtr(128));

            Assert.Equal(new IntPtr(0x1000), view.Pointer);
            Assert.Equal(new UIntPtr(64), view.Length);
            Assert.Equal(new IntPtr(0x2000), mutableView.Pointer);
            Assert.Equal(new UIntPtr(128), mutableView.Length);
            Assert.Equal(IntPtr.Zero, NnrpBufferView.Empty.Pointer);
            Assert.Equal(UIntPtr.Zero, NnrpBufferView.Empty.Length);
            Assert.Equal(IntPtr.Zero, NnrpMutableBufferView.Empty.Pointer);
            Assert.Equal(UIntPtr.Zero, NnrpMutableBufferView.Empty.Length);
        }

        [Fact]
        public void BufferViewsRejectNonEmptyNullPointers()
        {
            Assert.Throws<ArgumentException>(() => new NnrpBufferView(IntPtr.Zero, new UIntPtr(1)));
            Assert.Throws<ArgumentException>(() => new NnrpMutableBufferView(IntPtr.Zero, new UIntPtr(1)));
        }

        [Fact]
        public void NativeStatusKeepsStableFfiShapeAndValueEquality()
        {
            var left = new NnrpFfiStatus(NnrpFfiStatusCode.ProtocolError, NnrpErrorFamily.Cache, 7, 9);
            var right = new NnrpFfiStatus(NnrpFfiStatusCode.ProtocolError, NnrpErrorFamily.Cache, 7, 9);
            var different = new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState, NnrpErrorFamily.Cache, 7, 9);

            Assert.False(left.Succeeded);
            Assert.True(NnrpFfiStatus.Ok.Succeeded);
            Assert.Equal(NnrpFfiStatusCode.ProtocolError, left.StatusCode);
            Assert.Equal(NnrpErrorFamily.Cache, left.ErrorFamily);
            Assert.Equal((uint)7, left.ProtocolErrorCode);
            Assert.Equal((uint)9, left.DetailCode);
            Assert.Equal(left, right);
            Assert.True(left == right);
            Assert.False(left != right);
            Assert.NotEqual(left, different);
            Assert.False(left.Equals("not-a-status"));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Theory]
        [InlineData(NnrpFfiStatusCode.InvalidArgument, typeof(NnrpNativeInvalidArgumentException))]
        [InlineData(NnrpFfiStatusCode.InvalidHandle, typeof(NnrpNativeInvalidHandleException))]
        [InlineData(NnrpFfiStatusCode.InvalidState, typeof(NnrpNativeInvalidStateException))]
        [InlineData(NnrpFfiStatusCode.ProtocolError, typeof(NnrpNativeProtocolException))]
        [InlineData(NnrpFfiStatusCode.WouldBlock, typeof(NnrpNativeWouldBlockException))]
        [InlineData(NnrpFfiStatusCode.CallbackRejected, typeof(NnrpNativeCallbackRejectedException))]
        [InlineData(NnrpFfiStatusCode.InternalError, typeof(NnrpNativeInternalException))]
        public void NativeStatusMapsStableStatusCodesToExceptions(NnrpFfiStatusCode statusCode, Type expectedExceptionType)
        {
            var status = new NnrpFfiStatus(statusCode, NnrpErrorFamily.Cache, 7, 9);

            var error = Assert.Throws(expectedExceptionType, () => status.ThrowIfError());
            var runtimeError = Assert.IsAssignableFrom<NnrpNativeRuntimeException>(error);

            Assert.Equal(status, runtimeError.Status);
            Assert.Contains("status_code=", runtimeError.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void NativeStatusDoesNotThrowForOkAndMapsUnknownStatusToInternal()
        {
            NnrpFfiStatus.Ok.ThrowIfError();

            var status = new NnrpFfiStatus((NnrpFfiStatusCode)0x1234, NnrpErrorFamily.Internal, 0, 0);

            Assert.Throws<NnrpNativeInternalException>(() => status.ThrowIfError());
        }

        [Fact]
        public void NativeRuntimeEntrypointsKeepFrozenDelegateTable()
        {
            var entrypoints = CreateEntrypoints();

            Assert.Equal(1, entrypoints.CurrentProtocolVersion().Major);
            Assert.Equal(1, entrypoints.RuntimeCapabilities().AbiMajor);

            NnrpHandle handle;
            Assert.True(entrypoints.ConnectionBootstrap(new NnrpConnectionBootstrap(1, 1, 2), out handle).Succeeded);
            Assert.Equal(NnrpHandleKind.Connection, handle.Kind);

            Assert.True(entrypoints.ClientConnect(new NnrpClientConnectRequest(2, 1, 2), out handle).Succeeded);
            Assert.True(entrypoints.SessionOpen(MatchingSessionOpenRequest(), out handle).Succeeded);
            Assert.True(entrypoints.ClientOpenSession(MatchingSessionOpenRequest(), out handle).Succeeded);
            Assert.True(entrypoints.Submit(MatchingSubmitRequest(), out handle).Succeeded);
            Assert.True(entrypoints.ClientSubmit(MatchingSubmitRequest(), out handle).Succeeded);
            Assert.True(entrypoints.SessionClose(new NnrpHandle(NnrpHandleKind.Session, 3, 1)).Succeeded);
            Assert.True(entrypoints.ClientClose(new NnrpHandle(NnrpHandleKind.Session, 3, 1)).Succeeded);
            Assert.True(entrypoints.ClientCancel(new NnrpClientCancelRequest(new NnrpHandle(NnrpHandleKind.Session, 3, 1), 7)).Succeeded);

            NnrpPollResult pollResult;
            Assert.True(entrypoints.ClientAwaitEvent(new NnrpHandle(NnrpHandleKind.Connection, 1, 1), out pollResult).Succeeded);
            Assert.Equal((byte)0, pollResult.HasEvent);

            Assert.True(entrypoints.ServerBind(new NnrpServerBindRequest(4, 1, 2), out handle).Succeeded);
            Assert.True(entrypoints.ServerAccept(MatchingServerAcceptRequest(), out handle).Succeeded);
            Assert.True(entrypoints.ServerReceiveSubmit(MatchingServerReceiveSubmitRequest(), out handle).Succeeded);
            Assert.True(entrypoints.ServerSendResult(new NnrpServerSendResultRequest(new NnrpHandle(NnrpHandleKind.Operation, 5, 1), NnrpBufferView.Empty)).Succeeded);
            Assert.True(entrypoints.ServerSendFlowUpdate(new NnrpServerFlowUpdateRequest(new NnrpHandle(NnrpHandleKind.Session, 3, 1), 7)).Succeeded);
            Assert.True(entrypoints.ServerClose(new NnrpHandle(NnrpHandleKind.Session, 3, 1)).Succeeded);
            Assert.True(entrypoints.Control(new NnrpControlRequest(new NnrpHandle(NnrpHandleKind.Connection, 1, 1), 9, NnrpBufferView.Empty)).Succeeded);
            Assert.True(entrypoints.PollEmpty(out pollResult).Succeeded);

            var eventValue = new NnrpEvent(
                0,
                NnrpHandle.Invalid,
                NnrpHandle.Invalid,
                NnrpHandle.Invalid,
                0,
                NnrpBufferView.Empty,
                new NnrpFfiDiagnostic(NnrpFfiStatus.Ok));
            Assert.True(entrypoints.DispatchEvent(new NnrpCallbackSink(IntPtr.Zero, IntPtr.Zero), ref eventValue).Succeeded);

            entrypoints.Dispose();
            entrypoints.Dispose();
        }

        [Fact]
        public void NativeRuntimeEntrypointsRejectMissingDelegate()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new NnrpNativeRuntimeEntrypoints(
                    null!,
                    () => MatchingCapabilities(),
                    ConnectionBootstrap,
                    ClientConnect,
                    SessionOpen,
                    SessionOpen,
                    Submit,
                    Submit,
                    HandleStatus,
                    HandleStatus,
                    ClientCancel,
                    AwaitEvent,
                    ServerBind,
                    ServerAccept,
                    ServerReceiveSubmit,
                    ServerSendResult,
                    ServerFlowUpdate,
                    HandleStatus,
                    Control,
                    PollEmpty,
                    DispatchEvent));
        }

        [Fact]
        public void NativeRuntimeClientRunsConnectionSessionSubmitCloseRoundtrip()
        {
            var client = new NnrpNativeRuntimeClient(CreateEntrypoints());

            var connection = client.Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp);
            var session = connection.OpenSession(41, 3, 4, 5, 6);
            var operation = session.Submit(99, 7, new byte[] { 1, 2, 3 });
            var operationScope = session.SubmitOperation(100, 8, new byte[] { 1, 2, 3 }, parentOperationId: 99, operationGroupId: 1234);
            connection.Control(10, new byte[] { 4, 5 });
            operationScope.Cancel();
            session.Cancel(7);
            session.Control(11, new byte[] { 6, 7, 8 });
            session.Close();

            Assert.Equal((ulong)11, connection.Handle.Handle.Id);
            Assert.Equal((uint)2, connection.Handle.Handle.Generation);
            Assert.Equal((ulong)11, session.Connection.Handle.Id);
            Assert.Equal((ulong)41, session.Handle.Handle.Id);
            Assert.Equal((uint)3, session.Handle.Handle.Generation);
            Assert.Equal((ulong)99, operation.Handle.Id);
            Assert.Equal((ulong)100, operationScope.OperationId);
            Assert.Equal((uint)8, operationScope.FrameId);
            Assert.Equal((ulong)99, operationScope.ParentOperationId);
            Assert.Equal((ulong)1234, operationScope.OperationGroupId);
        }

        [Fact]
        public void NativeRuntimeConnectionCanOpenMultipleSessions()
        {
            var client = new NnrpNativeRuntimeClient(CreateEntrypoints());

            var connection = client.Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp);
            var firstSession = connection.OpenSession(41, 3, 4, 5, 6);
            var secondSession = connection.OpenSession(42, 4, 4, 5, 6);
            var firstOperation = firstSession.SubmitOperation(99, 7);
            var secondOperation = secondSession.SubmitOperation(100, 8);

            Assert.Equal(connection.Handle.Handle, firstSession.Connection.Handle);
            Assert.Equal(connection.Handle.Handle, secondSession.Connection.Handle);
            Assert.Equal((ulong)41, firstSession.Handle.Handle.Id);
            Assert.Equal((ulong)42, secondSession.Handle.Handle.Id);
            Assert.Equal(firstSession.Handle, firstOperation.Session);
            Assert.Equal(secondSession.Handle, secondOperation.Session);
        }

        [Fact]
        public void NativeRuntimeClientBootstrapsAndAwaitsEmptyEvent()
        {
            var client = new NnrpNativeRuntimeClient(CreateEntrypoints());

            var connection = client.BootstrapConnection(12, 2, NnrpNativeArtifact.TransportSlotTcp);
            var result = connection.AwaitEvent();

            Assert.Equal((ulong)12, connection.Handle.Handle.Id);
            Assert.Null(result.Event);
        }

        [Fact]
        public void NativeRuntimeEventSnapshotCopiesPayload()
        {
            var entrypoints = new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                () => MatchingCapabilities(),
                ConnectionBootstrap,
                ClientConnect,
                SessionOpen,
                SessionOpen,
                Submit,
                Submit,
                HandleStatus,
                HandleStatus,
                ClientCancel,
                AwaitEventWithPayload,
                ServerBind,
                ServerAccept,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent);
            var connection = new NnrpNativeRuntimeClient(entrypoints).Connect(12, 2, NnrpNativeArtifact.TransportSlotTcp);

            var result = connection.AwaitEvent();

            Assert.NotNull(result.Event);
            Assert.Equal(6u, result.Event!.Kind);
            Assert.Equal(new byte[] { 1, 2, 3 }, result.Event.Payload);
            Assert.Equal((ulong)12, result.Event.Connection.Id);
            Assert.Equal((ulong)41, result.Event.Session.Id);
            Assert.Equal((ulong)99, result.Event.Operation.Id);
            Assert.True(result.Event.Diagnostic.Status.Succeeded);
        }

        [Fact]
        public void NativeRuntimeResultPreservesLifecycleSurface()
        {
            var entrypoints = new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                () => MatchingCapabilities(),
                ConnectionBootstrap,
                ClientConnect,
                SessionOpen,
                SessionOpen,
                Submit,
                Submit,
                HandleStatus,
                HandleStatus,
                ClientCancel,
                AwaitEventWithPayload,
                ServerBind,
                ServerAccept,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent);
            var connection = new NnrpNativeRuntimeClient(entrypoints).Connect(12, 2, NnrpNativeArtifact.TransportSlotTcp);
            var @event = connection.PollEvent();

            Assert.NotNull(@event);
            var completed = NnrpNativeRuntimeResult.FromEvent(@event!);
            var partial = NnrpNativeRuntimeResult.FromEvent(@event!, NnrpNativeOperationLifecycle.Partial);
            var degraded = NnrpNativeRuntimeResult.FromEvent(@event!, NnrpNativeOperationLifecycle.Degraded);
            var stale = NnrpNativeRuntimeResult.FromEvent(@event!, NnrpNativeOperationLifecycle.StaleReuse);

            Assert.Equal(NnrpNativeOperationLifecycle.Completed, completed.State);
            Assert.Equal((ulong)99, completed.OperationId);
            Assert.Equal((uint)7, completed.FrameId);
            Assert.Equal(new byte[] { 1, 2, 3 }, completed.Payload);
            Assert.Equal(NnrpNativeOperationLifecycle.Partial, partial.State);
            Assert.Equal(NnrpNativeOperationLifecycle.Degraded, degraded.State);
            Assert.Equal(NnrpNativeOperationLifecycle.StaleReuse, stale.State);
        }

        [Fact]
        public void NativeRuntimeResultMapsErrorAndDropEvents()
        {
            var errorEvent = new NnrpNativeRuntimeEvent(
                10,
                new NnrpHandle(NnrpHandleKind.Connection, 12, 2),
                new NnrpHandle(NnrpHandleKind.Session, 41, 3),
                new NnrpHandle(NnrpHandleKind.Operation, 99, 1),
                7,
                Array.Empty<byte>(),
                new NnrpNativeRuntimeDiagnostic(new NnrpFfiStatus(NnrpFfiStatusCode.InternalError), 12, 41, 99, 7));
            var dropEvent = new NnrpNativeRuntimeEvent(
                7,
                new NnrpHandle(NnrpHandleKind.Connection, 12, 2),
                new NnrpHandle(NnrpHandleKind.Session, 41, 3),
                new NnrpHandle(NnrpHandleKind.Operation, 99, 1),
                7,
                Array.Empty<byte>(),
                new NnrpNativeRuntimeDiagnostic(NnrpFfiStatus.Ok, 12, 41, 99, 7));

            Assert.Equal(NnrpNativeOperationLifecycle.Failed, NnrpNativeRuntimeResult.FromEvent(errorEvent).State);
            Assert.Equal(NnrpNativeOperationLifecycle.Cancelled, NnrpNativeRuntimeResult.FromEvent(dropEvent).State);
        }

        [Fact]
        public async Task NativeRuntimeAsyncSubmitCancelsNativeFrameWhenTokenIsCancelled()
        {
            var session = new NnrpNativeRuntimeClient(CreateEntrypoints())
                .Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp)
                .OpenSession(41, 3, 4, 5, 6);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<TaskCanceledException>(() =>
                session.SubmitOperationAsync(101, 9, new byte[] { 1, 2, 3 }, cancellationToken: cancellation.Token));
        }

        [Fact]
        public void NativeRuntimeConnectionPollsEventDeliveryModel()
        {
            var entrypoints = new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                () => MatchingCapabilities(),
                ConnectionBootstrap,
                ClientConnect,
                SessionOpen,
                SessionOpen,
                Submit,
                Submit,
                HandleStatus,
                HandleStatus,
                ClientCancel,
                AwaitEventWithPayload,
                ServerBind,
                ServerAccept,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent);
            var connection = new NnrpNativeRuntimeClient(entrypoints).Connect(12, 2, NnrpNativeArtifact.TransportSlotTcp);

            var @event = connection.PollEvent();
            var events = connection.PollAvailableEvents(1);

            Assert.NotNull(@event);
            Assert.Equal(new byte[] { 1, 2, 3 }, @event!.Payload);
            Assert.Single(events);
            Assert.Equal(new byte[] { 1, 2, 3 }, events[0].Payload);
            Assert.Throws<ArgumentOutOfRangeException>(() => connection.PollAvailableEvents(-1));
        }

        [Fact]
        public void NativeRuntimeSessionSubmitsAndPollsResult()
        {
            var entrypoints = new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                () => MatchingCapabilities(),
                ConnectionBootstrap,
                ClientConnect,
                SessionOpen,
                SessionOpen,
                Submit,
                Submit,
                HandleStatus,
                HandleStatus,
                ClientCancel,
                AwaitEventWithPayload,
                ServerBind,
                ServerAccept,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent);
            var session = new NnrpNativeRuntimeClient(entrypoints)
                .Connect(12, 2, NnrpNativeArtifact.TransportSlotTcp)
                .OpenSession(41, 3, 4, 5, 6);

            var result = session.SubmitAndPollResult(
                99,
                7,
                new byte[] { 1, 2, 3 },
                state: NnrpNativeOperationLifecycle.Partial,
                maxEvents: 1);

            Assert.Equal(NnrpNativeOperationLifecycle.Partial, result.State);
            Assert.Equal((ulong)99, result.OperationId);
            Assert.Equal((uint)7, result.FrameId);
            Assert.Equal(new byte[] { 1, 2, 3 }, result.Payload);
        }

        [Fact]
        public void NativeRuntimeSessionRaisesWhenResultIsNotAvailable()
        {
            var session = new NnrpNativeRuntimeClient(CreateEntrypoints())
                .Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp)
                .OpenSession(41, 3, 4, 5, 6);

            Assert.Throws<NnrpNativeWouldBlockException>(() =>
                session.SubmitAndPollResult(99, 7, new byte[] { 1, 2, 3 }, maxEvents: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.PollResult(session.SubmitOperation(99, 7), maxEvents: -1));
        }

        [Fact]
        public void NativeRuntimeSessionIgnoresResultForDifferentSession()
        {
            var entrypoints = new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                () => MatchingCapabilities(),
                ConnectionBootstrap,
                ClientConnect,
                SessionOpen,
                SessionOpen,
                Submit,
                Submit,
                HandleStatus,
                HandleStatus,
                ClientCancel,
                AwaitEventWithPayload,
                ServerBind,
                ServerAccept,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent);
            var session = new NnrpNativeRuntimeClient(entrypoints)
                .Connect(12, 2, NnrpNativeArtifact.TransportSlotTcp)
                .OpenSession(42, 4, 4, 5, 6);
            var operation = session.SubmitOperation(99, 7);

            Assert.Throws<NnrpNativeWouldBlockException>(() => session.PollResult(operation, maxEvents: 1));
        }

        [Fact]
        public void NativeRuntimeSessionRejectsUseAfterClose()
        {
            var session = new NnrpNativeRuntimeClient(CreateEntrypoints())
                .Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp)
                .OpenSession(41, 3, 4, 5, 6);
            var operation = session.SubmitOperation(99, 7);

            session.Close();

            Assert.True(session.IsClosed);
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.Submit(100, 8));
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.PollResult(operation, maxEvents: 1));
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.Cancel(7));
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.Control(11));
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.Close());
        }

        [Fact]
        public void NativeRuntimeClientRaisesMappedStatusErrors()
        {
            var entrypoints = new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                () => MatchingCapabilities(),
                ConnectionBootstrap,
                FailingClientConnect,
                SessionOpen,
                SessionOpen,
                Submit,
                Submit,
                HandleStatus,
                HandleStatus,
                ClientCancel,
                AwaitEvent,
                ServerBind,
                ServerAccept,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent);
            var client = new NnrpNativeRuntimeClient(entrypoints);

            Assert.Throws<NnrpNativeInvalidStateException>(() => client.Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp));
            Assert.Throws<ArgumentNullException>(() => new NnrpNativeRuntimeClient(null!));
        }

        [Fact]
        public void NativeRuntimeBackendSelectorFallsBackWhenNativeArtifactIsUnavailable()
        {
            var fallback = new FakeRuntimeBackend();

            var selected = NnrpNativeRuntimeBackendSelector.Select(
                artifactPath: "missing-native-artifact.dll",
                fallback: fallback);

            Assert.Same(fallback, selected);
        }

        [Fact]
        public void NativeRuntimeBackendSelectorCanRequireNativeArtifact()
        {
            Assert.Throws<NnrpNativeArtifactException>(() =>
                NnrpNativeRuntimeBackendSelector.Select(
                    artifactPath: "missing-native-artifact.dll",
                    fallback: new FakeRuntimeBackend(),
                    requireNative: true));
        }

        [Fact]
        public void NativeRuntimeClientImplementsBackendInterface()
        {
            INnrpNativeRuntimeBackend backend = new NnrpNativeRuntimeClient(CreateEntrypoints());

            var connection = backend.Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp);

            Assert.Equal((ulong)11, connection.Handle.Handle.Id);
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "nnrp-native-artifact-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static NnrpRuntimeCapabilities MatchingCapabilities(
            ushort abiMajor = 1,
            ushort abiMinor = 0,
            ushort abiPatch = 0,
            byte protocolMajor = 1,
            byte protocolWireFormat = 0,
            uint transportSlots = NnrpNativeArtifact.TransportSlotTcp,
            ulong featureFlags = NnrpNativeArtifact.RequiredRuntimeFeatures)
        {
            return new NnrpRuntimeCapabilities(
                abiMajor,
                abiMinor,
                abiPatch,
                new NnrpProtocolVersion(protocolMajor, protocolWireFormat),
                1,
                0,
                0,
                3,
                1,
                transportSlots,
                featureFlags);
        }

        private static NnrpNativeRuntimeEntrypoints CreateEntrypoints()
        {
            return new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                () => MatchingCapabilities(),
                ConnectionBootstrap,
                ClientConnect,
                SessionOpen,
                SessionOpen,
                Submit,
                Submit,
                HandleStatus,
                HandleStatus,
                ClientCancel,
                AwaitEvent,
                ServerBind,
                ServerAccept,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent);
        }

        private static NnrpProtocolVersion CurrentProtocolVersion()
        {
            return new NnrpProtocolVersion(1, 0);
        }

        private static NnrpFfiStatus ConnectionBootstrap(NnrpConnectionBootstrap request, out NnrpHandle connection)
        {
            connection = new NnrpHandle(NnrpHandleKind.Connection, request.ConnectionId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus ClientConnect(NnrpClientConnectRequest request, out NnrpHandle connection)
        {
            connection = new NnrpHandle(NnrpHandleKind.Connection, request.ConnectionId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus FailingClientConnect(NnrpClientConnectRequest request, out NnrpHandle connection)
        {
            connection = NnrpHandle.Invalid;
            return new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState);
        }

        private static NnrpSessionOpenRequest MatchingSessionOpenRequest()
        {
            return new NnrpSessionOpenRequest(new NnrpHandle(NnrpHandleKind.Connection, 1, 1), 3, 1, 1, 10, 1);
        }

        private static NnrpFfiStatus SessionOpen(NnrpSessionOpenRequest request, out NnrpHandle session)
        {
            session = new NnrpHandle(NnrpHandleKind.Session, request.RequestedSessionId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiSubmitRequest MatchingSubmitRequest()
        {
            return new NnrpFfiSubmitRequest(new NnrpHandle(NnrpHandleKind.Session, 3, 1), 5, 7, NnrpBufferView.Empty);
        }

        private static NnrpFfiStatus Submit(NnrpFfiSubmitRequest request, out NnrpHandle operation)
        {
            operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus HandleStatus(NnrpHandle handle)
        {
            return handle.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
        }

        private static NnrpFfiStatus ClientCancel(NnrpClientCancelRequest request)
        {
            return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
        }

        private static NnrpFfiStatus AwaitEvent(NnrpHandle connection, out NnrpPollResult result)
        {
            result = EmptyPollResult();
            return connection.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
        }

        private static NnrpFfiStatus AwaitEventWithPayload(NnrpHandle connection, out NnrpPollResult result)
        {
            result = new NnrpPollResult(
                NnrpFfiStatus.Ok,
                1,
                new NnrpEvent(
                    6,
                    connection,
                    new NnrpHandle(NnrpHandleKind.Session, 41, 3),
                    new NnrpHandle(NnrpHandleKind.Operation, 99, 1),
                    7,
                    new NnrpBufferView(EventPayloadHandle.AddrOfPinnedObject(), new UIntPtr((uint)EventPayload.Length)),
                    new NnrpFfiDiagnostic(NnrpFfiStatus.Ok)));
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus ServerBind(NnrpServerBindRequest request, out NnrpHandle server)
        {
            server = new NnrpHandle(NnrpHandleKind.Connection, request.ServerId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpServerAcceptRequest MatchingServerAcceptRequest()
        {
            return new NnrpServerAcceptRequest(new NnrpHandle(NnrpHandleKind.Connection, 4, 1), 3, 1, 1, 10, 1);
        }

        private static NnrpFfiStatus ServerAccept(NnrpServerAcceptRequest request, out NnrpHandle session)
        {
            session = new NnrpHandle(NnrpHandleKind.Session, request.SessionId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpServerReceiveSubmitRequest MatchingServerReceiveSubmitRequest()
        {
            return new NnrpServerReceiveSubmitRequest(new NnrpHandle(NnrpHandleKind.Session, 3, 1), 5, 7, NnrpBufferView.Empty);
        }

        private static NnrpFfiStatus ServerReceiveSubmit(NnrpServerReceiveSubmitRequest request, out NnrpHandle operation)
        {
            operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus ServerSendResult(NnrpServerSendResultRequest request)
        {
            return request.Operation.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
        }

        private static NnrpFfiStatus ServerFlowUpdate(NnrpServerFlowUpdateRequest request)
        {
            return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
        }

        private static NnrpFfiStatus Control(NnrpControlRequest request)
        {
            return request.Handle.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
        }

        private static NnrpFfiStatus PollEmpty(out NnrpPollResult result)
        {
            result = EmptyPollResult();
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus DispatchEvent(NnrpCallbackSink sink, ref NnrpEvent @event)
        {
            return NnrpFfiStatus.Ok;
        }

        private static NnrpPollResult EmptyPollResult()
        {
            return new NnrpPollResult(
                NnrpFfiStatus.Ok,
                0,
                new NnrpEvent(
                    0,
                    NnrpHandle.Invalid,
                    NnrpHandle.Invalid,
                    NnrpHandle.Invalid,
                    0,
                    NnrpBufferView.Empty,
                    new NnrpFfiDiagnostic(NnrpFfiStatus.Ok)));
        }

        private static readonly byte[] EventPayload = new byte[] { 1, 2, 3 };

        private static readonly System.Runtime.InteropServices.GCHandle EventPayloadHandle =
            System.Runtime.InteropServices.GCHandle.Alloc(EventPayload, System.Runtime.InteropServices.GCHandleType.Pinned);

        private sealed class FakeRuntimeBackend : INnrpNativeRuntimeBackend
        {
            public NnrpNativeRuntimeConnection Connect(ulong connectionId, uint generation, uint transportId)
            {
                throw new NotSupportedException("fixture connect");
            }

            public NnrpNativeRuntimeConnection BootstrapConnection(ulong connectionId, uint generation, uint transportId)
            {
                throw new NotSupportedException("fixture bootstrap");
            }
        }
    }
}
