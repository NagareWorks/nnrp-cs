using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class NnrpNativeArtifactTests
    {
        [Fact]
        public void NativeBridgeAssemblyIsNotMarkedAsManagedDiagnosticSurface()
        {
            Assert.Empty(
                typeof(NnrpNativeArtifact).Assembly.GetCustomAttributes<NnrpManagedDiagnosticSurfaceAttribute>());
        }

        [Theory]
        [InlineData("windows", "nnrp_ffi.dll")]
        [InlineData("win32", "nnrp_ffi.dll")]
        [InlineData("linux", "libnnrp_ffi.so")]
        [InlineData("android", "libnnrp_ffi.so")]
        [InlineData("darwin", "libnnrp_ffi.dylib")]
        [InlineData("ios", "libnnrp_ffi.a")]
        [InlineData("iossimulator", "libnnrp_ffi.a")]
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
        [InlineData("ios", "arm64", "ios-arm64")]
        [InlineData("iossimulator", "amd64", "iossimulator-x64")]
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
            Assert.Equal(NnrpNativeArtifact.MinimumAbiMinor, result.AbiMinor);
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
            Assert.Equal(NnrpHandleKind.SchemaRegistry, new NnrpSchemaRegistryHandle(new NnrpHandle(NnrpHandleKind.SchemaRegistry, 6, 1)).Handle.Kind);
            Assert.Equal(NnrpHandleKind.CacheLease, new NnrpCacheLeaseHandle(new NnrpHandle(NnrpHandleKind.CacheLease, 7, 1)).Handle.Kind);

            Assert.Throws<ArgumentException>(() => new NnrpConnectionHandle(new NnrpHandle(NnrpHandleKind.Session, 2, 1)));
            Assert.Throws<ArgumentException>(() => new NnrpSessionHandle(new NnrpHandle(NnrpHandleKind.Operation, 3, 1)));
            Assert.Throws<ArgumentException>(() => new NnrpOperationHandle(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)));
            Assert.Throws<ArgumentException>(() => new NnrpEventPumpHandle(new NnrpHandle(NnrpHandleKind.Buffer, 5, 1)));
            Assert.Throws<ArgumentException>(() => new NnrpBufferHandle(new NnrpHandle(NnrpHandleKind.EventPump, 4, 1)));
            Assert.Throws<ArgumentException>(() => new NnrpSchemaRegistryHandle(new NnrpHandle(NnrpHandleKind.Buffer, 5, 1)));
            Assert.Throws<ArgumentException>(() => new NnrpCacheLeaseHandle(new NnrpHandle(NnrpHandleKind.SchemaRegistry, 6, 1)));
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
        public void CallbackSinksRequireBoundDispatchersForManagedUse()
        {
            var empty = NnrpCallbackSink.None;

            Assert.True(empty.IsEmpty);
            Assert.False(empty.HasDispatcher);
            Assert.Throws<InvalidOperationException>(() => empty.EnsureDispatchable());
            Assert.Throws<ArgumentException>(() => NnrpCallbackSink.Create(new IntPtr(0x1000), IntPtr.Zero));

            var sink = NnrpCallbackSink.Create(new IntPtr(0x1000), new IntPtr(0x2000));

            sink.EnsureDispatchable();
            Assert.False(sink.IsEmpty);
            Assert.True(sink.HasDispatcher);
            Assert.Equal(new IntPtr(0x1000), sink.UserData);
            Assert.Equal(new IntPtr(0x2000), sink.OnEvent);
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
            NnrpSessionRecoveryOutcome recoveryOutcome;
            Assert.True(entrypoints.ClientResumeSession(MatchingSessionResumeRequest(), out handle, out recoveryOutcome).Succeeded);
            Assert.Equal(NnrpHandleKind.Session, handle.Kind);
            Assert.Equal((uint)2, recoveryOutcome.ResumeWindowMilliseconds);
            Assert.True(entrypoints.Submit(MatchingSubmitRequest(), out handle).Succeeded);
            Assert.True(entrypoints.ClientSubmit(MatchingSubmitRequest(), out handle).Succeeded);
            Assert.True(entrypoints.SessionClose(new NnrpHandle(NnrpHandleKind.Session, 3, 1)).Succeeded);
            Assert.True(entrypoints.ClientClose(new NnrpHandle(NnrpHandleKind.Session, 3, 1)).Succeeded);
            Assert.True(entrypoints.ConnectionClose(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)).Succeeded);
            Assert.True(entrypoints.ClientCloseConnection(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)).Succeeded);
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

            NnrpHandle registry;
            Assert.True(entrypoints.SchemaRegistryCreate(out registry).Succeeded);
            Assert.Equal(NnrpHandleKind.SchemaRegistry, registry.Kind);

            uint action;
            Assert.True(entrypoints.SchemaRegistryInstall(registry, TokenSchemaDescriptor(), out action).Succeeded);
            Assert.Equal((uint)NnrpSchemaRegistryAction.Installed, action);

            NnrpSchemaDescriptorHeader descriptor;
            Assert.True(entrypoints.SchemaRegistryLookup(registry, 0x1001, 3, out descriptor).Succeeded);
            Assert.Equal((uint)0x1001, descriptor.SchemaId);
            Assert.Equal((uint)3, descriptor.SchemaVersion);

            Assert.True(entrypoints.SchemaRegistryValidateBinding(registry, MatchingTypedPayloadDescriptor()).Succeeded);
            Assert.True(entrypoints.SchemaRegistryInvalidate(registry, 0x1001, 3, out action).Succeeded);
            Assert.Equal((uint)NnrpSchemaRegistryAction.Invalidated, action);
            Assert.True(entrypoints.SchemaRegistryRelease(registry).Succeeded);

            Assert.True(entrypoints.TokenDeltaSchemaDescriptor(out descriptor).Succeeded);
            Assert.Equal((uint)0x1001, descriptor.SchemaId);
            Assert.True(entrypoints.SchemaDescriptorParse(new NnrpBufferView(new IntPtr(0x1000), new UIntPtr(32)), out descriptor).Succeeded);

            var descriptorBytes = new byte[32];
            var descriptorBytesHandle = GCHandle.Alloc(descriptorBytes, GCHandleType.Pinned);
            try
            {
                Assert.True(entrypoints.SchemaDescriptorWrite(
                    descriptor,
                    new NnrpMutableBufferView(descriptorBytesHandle.AddrOfPinnedObject(), new UIntPtr((uint)descriptorBytes.Length))).Succeeded);
            }
            finally
            {
                descriptorBytesHandle.Free();
            }

            var descriptors = new[] { TokenSchemaDescriptor() };
            var descriptorsHandle = GCHandle.Alloc(descriptors, GCHandleType.Pinned);
            try
            {
                Assert.True(entrypoints.TypedPayloadValidateBinding(
                    descriptorsHandle.AddrOfPinnedObject(),
                    new UIntPtr((uint)descriptors.Length),
                    MatchingTypedPayloadDescriptor()).Succeeded);
            }
            finally
            {
                descriptorsHandle.Free();
            }

            Assert.True(entrypoints.SessionRecoveryRequestValidate(NnrpBufferView.Empty).Succeeded);
            Assert.True(entrypoints.SessionRecoveryAckValidate(NnrpBufferView.Empty, NnrpBufferView.Empty, out recoveryOutcome).Succeeded);
            Assert.Equal((uint)2, recoveryOutcome.ResumeWindowMilliseconds);
            Assert.True(entrypoints.MigrationRecoveryValidate(NnrpBufferView.Empty, NnrpBufferView.Empty).Succeeded);
            byte shouldReplay;
            Assert.True(entrypoints.MigrationShouldReplayFrame(NnrpBufferView.Empty, 7, out shouldReplay).Succeeded);
            Assert.Equal((byte)1, shouldReplay);

            NnrpBufferView bufferView;
            Assert.True(entrypoints.BufferAcquireCopy(new NnrpBufferView(new IntPtr(0x1000), new UIntPtr(3)), out handle, out bufferView).Succeeded);
            Assert.Equal(NnrpHandleKind.Buffer, handle.Kind);
            Assert.Equal(new UIntPtr(3), bufferView.Length);
            Assert.True(entrypoints.BufferView(handle, out bufferView).Succeeded);
            Assert.Equal(new UIntPtr(3), bufferView.Length);
            Assert.True(entrypoints.BufferRelease(handle).Succeeded);

            NnrpCacheLeaseResult leaseResult;
            Assert.True(entrypoints.CacheQuery(MatchingCacheLeaseRequest(), out leaseResult).Succeeded);
            Assert.Equal((uint)NnrpCacheLeaseOutcome.Valid, leaseResult.OutcomeCode);
            Assert.Equal(NnrpHandleKind.CacheLease, leaseResult.LeaseHandle.Kind);
            Assert.True(entrypoints.CacheTouch(MatchingCacheLeaseRequest(), out leaseResult).Succeeded);
            Assert.Equal((ulong)2500, leaseResult.ExpiresAtMilliseconds);

            var objects = new[] { MatchingCacheObjectId(), new NnrpCacheObjectId(5, 6, 7, 8) };
            var results = new NnrpCacheLeaseResult[objects.Length];
            var objectHandle = GCHandle.Alloc(objects, GCHandleType.Pinned);
            var resultHandle = GCHandle.Alloc(results, GCHandleType.Pinned);
            try
            {
                Assert.True(entrypoints.CachePrefetch(
                    new NnrpHandle(NnrpHandleKind.Session, 3, 1),
                    objectHandle.AddrOfPinnedObject(),
                    new UIntPtr((uint)objects.Length),
                    1000,
                    500,
                    resultHandle.AddrOfPinnedObject()).Succeeded);
            }
            finally
            {
                resultHandle.Free();
                objectHandle.Free();
            }

            Assert.Equal((uint)1, results[0].ObjectId.CacheNamespace);
            Assert.Equal((uint)5, results[1].ObjectId.CacheNamespace);
            Assert.True(entrypoints.CacheRelease(new NnrpHandle(NnrpHandleKind.CacheLease, 77, 1), out leaseResult).Succeeded);
            Assert.Equal((uint)NnrpCacheLeaseOutcome.Released, leaseResult.OutcomeCode);

            entrypoints.Dispose();
            entrypoints.Dispose();
        }

        [Fact]
        public void NativeSchemaRegistryRoutesNativeEntrypoints()
        {
            var registry = NnrpNativeSchemaRegistry.Create(CreateEntrypoints());

            Assert.False(registry.IsReleased);
            Assert.Equal(NnrpHandleKind.SchemaRegistry, registry.Handle.Handle.Kind);
            Assert.Equal(NnrpSchemaRegistryAction.Installed, registry.Install(TokenSchemaDescriptor()));

            var descriptor = registry.Lookup(0x1001, 3);

            Assert.Equal((uint)0x1001, descriptor.SchemaId);
            Assert.Equal((uint)3, descriptor.SchemaVersion);
            registry.ValidateBinding(MatchingTypedPayloadDescriptor());
            Assert.Equal(NnrpSchemaRegistryAction.Invalidated, registry.Invalidate(0x1001, 3));

            registry.Release();

            Assert.True(registry.IsReleased);
            Assert.Throws<NnrpNativeInvalidStateException>(() => registry.Lookup(0x1001, 3));
            registry.Dispose();
        }

        [Fact]
        public void NativeCacheLeasesRouteNativeEntrypoints()
        {
            var cache = new NnrpNativeCacheLeases(CreateEntrypoints());

            var query = cache.Query(MatchingCacheLeaseRequest());
            var touch = cache.Touch(MatchingCacheLeaseRequest());
            var prefetch = cache.Prefetch(
                new NnrpHandle(NnrpHandleKind.Session, 3, 1),
                new[] { MatchingCacheObjectId(), new NnrpCacheObjectId(5, 6, 7, 8) },
                1000,
                500);
            var release = cache.Release(new NnrpCacheLeaseHandle(query.LeaseHandle));

            Assert.Equal((uint)NnrpCacheLeaseOutcome.Valid, query.OutcomeCode);
            Assert.Equal((ulong)2000, query.ExpiresAtMilliseconds);
            Assert.Equal((uint)NnrpCacheLeaseOutcome.Valid, touch.OutcomeCode);
            Assert.Equal((ulong)2500, touch.ExpiresAtMilliseconds);
            Assert.Equal(2, prefetch.Length);
            Assert.Equal((uint)1, prefetch[0].ObjectId.CacheNamespace);
            Assert.Equal((uint)5, prefetch[1].ObjectId.CacheNamespace);
            Assert.Equal((uint)NnrpCacheLeaseOutcome.Released, release.OutcomeCode);
            Assert.Empty(cache.Prefetch(new NnrpHandle(NnrpHandleKind.Session, 3, 1), Array.Empty<NnrpCacheObjectId>(), 1000, 500));
            Assert.Throws<ArgumentNullException>(() => new NnrpNativeCacheLeases(null!));
            Assert.Throws<ArgumentNullException>(() => cache.Prefetch(new NnrpHandle(NnrpHandleKind.Session, 3, 1), null!, 1000, 500));
        }

        [Fact]
        public void NativeSchemaDescriptorsRouteNativeEntrypoints()
        {
            var schemas = new NnrpNativeSchemaDescriptors(CreateEntrypoints());
            var descriptor = schemas.TokenDelta();
            var destination = new byte[32];

            schemas.Write(descriptor, destination);
            var parsed = schemas.Parse(new byte[] { 1, 2, 3 });
            schemas.ValidateBinding(new[] { descriptor }, MatchingTypedPayloadDescriptor());

            Assert.Equal((uint)0x1001, descriptor.SchemaId);
            Assert.Equal((uint)0x1001, parsed.SchemaId);
            Assert.NotEqual(0, BitConverter.ToInt32(destination, 0));
            Assert.Throws<ArgumentNullException>(() => new NnrpNativeSchemaDescriptors(null!));
            Assert.Throws<ArgumentNullException>(() => schemas.Parse(null!));
            Assert.Throws<ArgumentNullException>(() => schemas.Write(descriptor, null!));
            Assert.Throws<ArgumentNullException>(() => schemas.ValidateBinding(null!, MatchingTypedPayloadDescriptor()));
        }

        [Fact]
        public void NativeRecoveryRoutesNativeEntrypoints()
        {
            var recovery = new NnrpNativeRecovery(CreateEntrypoints());

            recovery.ValidateSessionRecoveryRequest(Array.Empty<byte>());
            var outcome = recovery.ValidateSessionRecoveryAck(Array.Empty<byte>(), Array.Empty<byte>());
            recovery.ValidateMigrationRecovery(Array.Empty<byte>(), Array.Empty<byte>());
            var shouldReplay = recovery.ShouldReplayFrame(Array.Empty<byte>(), 7);

            Assert.Equal((uint)2, outcome.ResumeWindowMilliseconds);
            Assert.True(shouldReplay);
            Assert.Throws<ArgumentNullException>(() => new NnrpNativeRecovery(null!));
            Assert.Throws<ArgumentNullException>(() => recovery.ValidateSessionRecoveryRequest(null!));
            Assert.Throws<ArgumentNullException>(() => recovery.ValidateSessionRecoveryAck(null!, Array.Empty<byte>()));
            Assert.Throws<ArgumentNullException>(() => recovery.ValidateMigrationRecovery(null!, Array.Empty<byte>()));
            Assert.Throws<ArgumentNullException>(() => recovery.ShouldReplayFrame(null!, 7));
        }

        [Fact]
        public void NativeBuffersOwnNativeBufferHandles()
        {
            var buffers = new NnrpNativeBuffers(CreateEntrypoints());

            using (var buffer = buffers.AcquireCopy(new byte[] { 1, 2, 3 }))
            {
                Assert.Equal(NnrpHandleKind.Buffer, buffer.Handle.Handle.Kind);
                Assert.Equal(new byte[] { 1, 2, 3 }, buffer.CopyToArray());

                buffer.RefreshView();

                Assert.Equal(new byte[] { 1, 2, 3 }, buffer.CopyToArray());
                buffer.Release();
                Assert.True(buffer.IsReleased);
                Assert.Throws<NnrpNativeInvalidStateException>(() => buffer.RefreshView());
            }

            Assert.Throws<ArgumentNullException>(() => new NnrpNativeBuffers(null!));
            Assert.Throws<ArgumentNullException>(() => buffers.AcquireCopy(null!));
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
        public void NativeRuntimeEntrypointsMissingOptionalDelegatesReturnDeterministicErrors()
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

            NnrpHandle registry;
            uint action;
            NnrpSchemaDescriptorHeader schemaDescriptor;
            NnrpCacheLeaseResult leaseResult;

            Assert.Equal(NnrpErrorFamily.Schema, entrypoints.SchemaRegistryCreate(out registry).ErrorFamily);
            Assert.False(registry.IsValid);
            Assert.Equal(
                NnrpErrorFamily.Schema,
                entrypoints.SchemaRegistryInstall(NnrpHandle.Invalid, TokenSchemaDescriptor(), out action).ErrorFamily);
            Assert.Equal(
                NnrpErrorFamily.Schema,
                entrypoints.SchemaRegistryLookup(NnrpHandle.Invalid, 0x1001, 3, out schemaDescriptor).ErrorFamily);
            Assert.Equal(default(NnrpSchemaDescriptorHeader), schemaDescriptor);
            Assert.Equal(
                NnrpErrorFamily.Schema,
                entrypoints.SchemaRegistryInvalidate(NnrpHandle.Invalid, 0x1001, 3, out action).ErrorFamily);
            Assert.Equal(
                NnrpErrorFamily.Schema,
                entrypoints.SchemaRegistryValidateBinding(NnrpHandle.Invalid, MatchingTypedPayloadDescriptor()).ErrorFamily);
            Assert.Equal(NnrpErrorFamily.Schema, entrypoints.SchemaRegistryRelease(NnrpHandle.Invalid).ErrorFamily);

            NnrpSessionRecoveryOutcome recoveryOutcome;
            Assert.Equal(NnrpFfiStatusCode.InternalError, entrypoints.ClientResumeSession(MatchingSessionResumeRequest(), out registry, out recoveryOutcome).StatusCode);
            Assert.Equal(NnrpFfiStatusCode.InternalError, entrypoints.SchemaDescriptorParse(NnrpBufferView.Empty, out schemaDescriptor).StatusCode);
            Assert.Equal(default(NnrpSchemaDescriptorHeader), schemaDescriptor);
            Assert.Equal(NnrpFfiStatusCode.InternalError, entrypoints.SchemaDescriptorWrite(TokenSchemaDescriptor(), NnrpMutableBufferView.Empty).StatusCode);
            Assert.Equal(NnrpFfiStatusCode.InternalError, entrypoints.TokenDeltaSchemaDescriptor(out schemaDescriptor).StatusCode);
            Assert.Equal(NnrpFfiStatusCode.InternalError, entrypoints.TypedPayloadValidateBinding(IntPtr.Zero, UIntPtr.Zero, MatchingTypedPayloadDescriptor()).StatusCode);
            Assert.Equal(NnrpFfiStatusCode.InternalError, entrypoints.SessionRecoveryRequestValidate(NnrpBufferView.Empty).StatusCode);
            Assert.Equal(NnrpFfiStatusCode.InternalError, entrypoints.SessionRecoveryAckValidate(NnrpBufferView.Empty, NnrpBufferView.Empty, out recoveryOutcome).StatusCode);
            Assert.Equal(NnrpFfiStatusCode.InternalError, entrypoints.MigrationRecoveryValidate(NnrpBufferView.Empty, NnrpBufferView.Empty).StatusCode);
            byte shouldReplay;
            Assert.Equal(NnrpFfiStatusCode.InternalError, entrypoints.MigrationShouldReplayFrame(NnrpBufferView.Empty, 7, out shouldReplay).StatusCode);
            NnrpBufferView bufferView;
            Assert.Equal(NnrpFfiStatusCode.InternalError, entrypoints.BufferAcquireCopy(NnrpBufferView.Empty, out registry, out bufferView).StatusCode);
            Assert.Equal(NnrpFfiStatusCode.InternalError, entrypoints.BufferView(NnrpHandle.Invalid, out bufferView).StatusCode);
            Assert.Equal(NnrpErrorFamily.Schema, entrypoints.BufferRelease(NnrpHandle.Invalid).ErrorFamily);

            Assert.Equal(NnrpErrorFamily.Cache, entrypoints.CacheQuery(MatchingCacheLeaseRequest(), out leaseResult).ErrorFamily);
            Assert.Equal(default(NnrpCacheLeaseResult), leaseResult);
            Assert.Equal(NnrpErrorFamily.Cache, entrypoints.CacheTouch(MatchingCacheLeaseRequest(), out leaseResult).ErrorFamily);
            Assert.Equal(
                NnrpErrorFamily.Cache,
                entrypoints.CachePrefetch(
                    new NnrpHandle(NnrpHandleKind.Session, 3, 1),
                    IntPtr.Zero,
                    UIntPtr.Zero,
                    1000,
                    500,
                    IntPtr.Zero).ErrorFamily);
            Assert.Equal(NnrpErrorFamily.Cache, entrypoints.CacheRelease(NnrpHandle.Invalid, out leaseResult).ErrorFamily);
        }

        [Fact]
        public void NativeRuntimeClientRunsConnectionSessionSubmitCloseRoundtrip()
        {
            var client = new NnrpNativeRuntimeClient(CreateEntrypoints());

            var connection = client.Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp);
            var session = connection.OpenSession(41, 3, 4, 5, 6);
            var resumed = connection.ResumeSession(42, 4, 4, 5, 6, 16, out var recoveryOutcome);
            using var nativePayload = new NnrpNativeBuffers(connection.Entrypoints).AcquireCopy(new byte[] { 1, 2, 3 });
            var operation = session.Submit(99, 7, nativePayload);
            var operationScope = session.SubmitOperation(100, 8, nativePayload, parentOperationId: 99, operationGroupId: 1234);
            connection.Control(10, nativePayload);
            operationScope.Cancel();
            session.Cancel(7);
            session.Control(11, nativePayload);
            resumed.Close();
            session.Close();

            Assert.Equal((ulong)11, connection.Handle.Handle.Id);
            Assert.Equal((uint)2, connection.Handle.Handle.Generation);
            Assert.Equal((ulong)11, session.Connection.Handle.Id);
            Assert.Equal((ulong)41, session.Handle.Handle.Id);
            Assert.Equal((uint)3, session.Handle.Handle.Generation);
            Assert.Equal((ulong)42, resumed.Handle.Handle.Id);
            Assert.Equal((uint)4, resumed.Handle.Handle.Generation);
            Assert.Equal((uint)2, recoveryOutcome.ResumeWindowMilliseconds);
            Assert.Equal((ulong)99, operation.Handle.Id);
            Assert.Equal((ulong)100, operationScope.OperationId);
            Assert.Equal((uint)8, operationScope.FrameId);
            Assert.Equal((ulong)99, operationScope.ParentOperationId);
            Assert.Equal((ulong)1234, operationScope.OperationGroupId);
            Assert.Throws<ArgumentNullException>(() => session.SubmitOperation(101, 9, (NnrpNativeBuffer)null!));
        }

        [Fact]
        public void NativeRuntimeClientBorrowsArrayBackedMemoryPayloadSlices()
        {
            var submittedPayload = Array.Empty<byte>();
            var controlledPayload = Array.Empty<byte>();
            var pendingEvents = new Queue<Func<NnrpHandle, NnrpPollResult>>();

            NnrpFfiStatus CaptureSubmit(NnrpFfiSubmitRequest request, out NnrpHandle operation)
            {
                submittedPayload = CopyBufferView(request.Payload);
                operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
                return NnrpFfiStatus.Ok;
            }

            NnrpFfiStatus CaptureControl(NnrpControlRequest request)
            {
                controlledPayload = CopyBufferView(request.Payload);
                return request.Handle.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus AwaitQueuedEvent(NnrpHandle connection, out NnrpPollResult result)
            {
                if (pendingEvents.Count == 0)
                {
                    result = EmptyPollResult();
                    return NnrpFfiStatus.Ok;
                }

                result = pendingEvents.Dequeue()(connection);
                return connection.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            var entrypoints = new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                () => MatchingCapabilities(),
                ConnectionBootstrap,
                ClientConnect,
                SessionOpen,
                SessionOpen,
                CaptureSubmit,
                CaptureSubmit,
                HandleStatus,
                HandleStatus,
                ClientCancel,
                AwaitQueuedEvent,
                ServerBind,
                ServerAccept,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent);
            using var host = NnrpNativeRuntimeSessionHost.Open(
                new NnrpNativeRuntimeClient(entrypoints),
                new NnrpNativeRuntimeSessionHostOptions(11, 2, NnrpNativeArtifact.TransportSlotTcp, 41, 3, 4, 5, 6));
            var source = new byte[] { 0, 1, 2, 3, 4 };
            var payload = source.AsMemory(1, 3);

            var operation = host.SubmitOperation(99, 7, payload);
            pendingEvents.Enqueue(connection => new NnrpPollResult(
                NnrpFfiStatus.Ok,
                1,
                new NnrpEvent(
                    6,
                    connection,
                    new NnrpHandle(NnrpHandleKind.Session, 41, 3),
                    new NnrpHandle(NnrpHandleKind.Operation, 100, 1),
                    8,
                    NnrpBufferView.Empty,
                    new NnrpFfiDiagnostic(NnrpFfiStatus.Ok))));
            pendingEvents.Enqueue(connection => new NnrpPollResult(
                NnrpFfiStatus.Ok,
                1,
                new NnrpEvent(
                    6,
                    connection,
                    new NnrpHandle(NnrpHandleKind.Session, 41, 3),
                    new NnrpHandle(NnrpHandleKind.Operation, 99, 1),
                    7,
                    new NnrpBufferView(EventPayloadHandle.AddrOfPinnedObject(), new UIntPtr((uint)EventPayload.Length)),
                    new NnrpFfiDiagnostic(NnrpFfiStatus.Ok))));
            var polledResult = host.SubmitAndPollResult(99, 7, payload, maxEvents: 2);
            host.Control(10, payload);

            Assert.Equal((ulong)99, operation.OperationId);
            Assert.Equal((ulong)99, polledResult.OperationId);
            Assert.Equal((uint)7, polledResult.FrameId);
            Assert.Equal(new byte[] { 1, 2, 3 }, polledResult.Payload);
            Assert.Equal(new byte[] { 1, 2, 3 }, submittedPayload);
            Assert.Equal(new byte[] { 1, 2, 3 }, controlledPayload);
        }

        [Fact]
        public void NativeRuntimeByteArrayPayloadOverloadsBorrowOriginalArray()
        {
            var submittedViews = new List<NnrpBufferView>();
            var receivedViews = new List<NnrpBufferView>();
            var resultViews = new List<NnrpBufferView>();
            var controlViews = new List<NnrpBufferView>();

            NnrpFfiStatus CaptureSubmit(NnrpFfiSubmitRequest request, out NnrpHandle operation)
            {
                submittedViews.Add(request.Payload);
                operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureServerReceiveSubmit(NnrpServerReceiveSubmitRequest request, out NnrpHandle operation)
            {
                receivedViews.Add(request.Payload);
                operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureServerSendResult(NnrpServerSendResultRequest request)
            {
                resultViews.Add(request.Payload);
                return request.Operation.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureControl(NnrpControlRequest request)
            {
                controlViews.Add(request.Payload);
                return request.Handle.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            var entrypoints = new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                () => MatchingCapabilities(),
                ConnectionBootstrap,
                ClientConnect,
                SessionOpen,
                SessionOpen,
                CaptureSubmit,
                CaptureSubmit,
                HandleStatus,
                HandleStatus,
                ClientCancel,
                AwaitEvent,
                ServerBind,
                ServerAccept,
                CaptureServerReceiveSubmit,
                CaptureServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent);
            var payload = new byte[] { 1, 2, 3 };
            var payloadHandle = GCHandle.Alloc(payload, GCHandleType.Pinned);

            try
            {
                var expectedPointer = payloadHandle.AddrOfPinnedObject();
                var client = new NnrpNativeRuntimeClient(entrypoints);
                var connection = client.Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp);
                var session = connection.OpenSession(41, 3, 4, 5, 6);
                session.SubmitOperation(99, 7, payload);
                session.Control(10, payload);

                using var server = NnrpNativeRuntimeServer.Bind(entrypoints, 50, 2, NnrpNativeArtifact.TransportSlotTcp);
                var serverSession = server.AcceptSession(42, 3, 4, 5, 6);
                var operation = serverSession.ReceiveSubmit(100, 8, payload);
                serverSession.SendResult(operation, payload);
                serverSession.Control(11, payload);

                AssertBorrowedView(expectedPointer, payload.Length, Assert.Single(submittedViews));
                AssertBorrowedView(expectedPointer, payload.Length, Assert.Single(receivedViews));
                AssertBorrowedView(expectedPointer, payload.Length, Assert.Single(resultViews));
                Assert.Collection(
                    controlViews,
                    view => AssertBorrowedView(expectedPointer, payload.Length, view),
                    view => AssertBorrowedView(expectedPointer, payload.Length, view));
            }
            finally
            {
                payloadHandle.Free();
            }
        }

        [Fact]
        public async Task NativeRuntimeClientBorrowedMemoryOverloadsCoverAsyncAndPollingPaths()
        {
            var submitCount = 0;
            var submittedPayload = Array.Empty<byte>();
            var controlledPayload = Array.Empty<byte>();

            NnrpFfiStatus CaptureSubmit(NnrpFfiSubmitRequest request, out NnrpHandle operation)
            {
                submitCount++;
                submittedPayload = CopyBufferView(request.Payload);
                operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
                return NnrpFfiStatus.Ok;
            }

            NnrpFfiStatus CaptureControl(NnrpControlRequest request)
            {
                controlledPayload = CopyBufferView(request.Payload);
                return request.Handle.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            var entrypoints = new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                () => MatchingCapabilities(),
                ConnectionBootstrap,
                ClientConnect,
                SessionOpen,
                SessionOpen,
                CaptureSubmit,
                CaptureSubmit,
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
                CaptureControl,
                PollEmpty,
                DispatchEvent);
            using var connectionHost = NnrpNativeRuntimeConnectionHost.Open(
                new NnrpNativeRuntimeClient(entrypoints),
                new NnrpNativeRuntimeConnectionHostOptions(11, 2, NnrpNativeArtifact.TransportSlotTcp));
            var session = connectionHost.OpenSession(new NnrpNativeRuntimeSessionOptions(41, 3, 4, 5, 6));
            var source = new byte[] { 0, 1, 2, 3, 4 };
            var payload = source.AsMemory(1, 3);

            var submitHandle = session.Submit(98, 6, payload);
            var operation = await session.SubmitOperationAsync(99, 7, payload);
            var result = connectionHost.SubmitAndPollResult(41, 99, 7, payload, maxEvents: 1);
            connectionHost.SubmitOperation(41, 100, 8, payload);
            connectionHost.Control(41, 10, payload);
            connectionHost.Connection.Control(11, payload);

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(() => session.SubmitOperationAsync(101, 9, payload, cancellationToken: cancelled.Token));

            Assert.Equal((ulong)98, submitHandle.Handle.Id);
            Assert.Equal((ulong)99, operation.OperationId);
            Assert.Equal((ulong)99, result.OperationId);
            Assert.True(submitCount >= 4);
            Assert.Equal(new byte[] { 1, 2, 3 }, submittedPayload);
            Assert.Equal(new byte[] { 1, 2, 3 }, controlledPayload);
        }

        [Fact]
        public void NativeRuntimeBorrowedMemoryRejectsInvalidHelperInputs()
        {
            Assert.Throws<ArgumentNullException>(() =>
                NnrpNativeRuntimeSession.WithBorrowedView<int>(ReadOnlyMemory<byte>.Empty, null!));

            var visitedEmpty = NnrpNativeRuntimeSession.WithBorrowedView(
                ReadOnlyMemory<byte>.Empty,
                view => view.Length == UIntPtr.Zero);

            Assert.True(visitedEmpty);
        }

        [Fact]
        public void NativeRuntimeHotPathUsesBorrowedNativeBufferViews()
        {
            var submittedViews = new List<NnrpBufferView>();
            var receivedViews = new List<NnrpBufferView>();
            var resultViews = new List<NnrpBufferView>();
            var controlViews = new List<NnrpBufferView>();

            NnrpFfiStatus CaptureSubmit(NnrpFfiSubmitRequest request, out NnrpHandle operation)
            {
                submittedViews.Add(request.Payload);
                operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureServerReceiveSubmit(NnrpServerReceiveSubmitRequest request, out NnrpHandle operation)
            {
                receivedViews.Add(request.Payload);
                operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureServerSendResult(NnrpServerSendResultRequest request)
            {
                resultViews.Add(request.Payload);
                return request.Operation.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureControl(NnrpControlRequest request)
            {
                controlViews.Add(request.Payload);
                return request.Handle.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            var entrypoints = new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                () => MatchingCapabilities(),
                ConnectionBootstrap,
                ClientConnect,
                SessionOpen,
                SessionOpen,
                CaptureSubmit,
                CaptureSubmit,
                HandleStatus,
                HandleStatus,
                ClientCancel,
                AwaitEvent,
                ServerBind,
                ServerAccept,
                CaptureServerReceiveSubmit,
                CaptureServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                bufferAcquireCopy: BufferAcquireCopy,
                bufferView: BufferView,
                bufferRelease: HandleStatus);

            using var nativePayload = new NnrpNativeBuffers(entrypoints).AcquireCopy(new byte[] { 1, 2, 3 });
            var borrowed = nativePayload.BorrowView();
            var client = new NnrpNativeRuntimeClient(entrypoints);
            var connection = client.Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp);
            var session = connection.OpenSession(41, 3, 4, 5, 6);
            session.SubmitOperation(99, 7, nativePayload);
            session.Control(17, nativePayload);

            using var server = NnrpNativeRuntimeServer.Bind(entrypoints, 50, 2, NnrpNativeArtifact.TransportSlotTcp);
            var serverSession = server.AcceptSession(41, 3, 4, 5, 6);
            var operation = serverSession.ReceiveSubmit(100, 8, nativePayload);
            serverSession.SendResult(operation, nativePayload);
            serverSession.Control(18, nativePayload);

            AssertBorrowedView(borrowed, Assert.Single(submittedViews));
            AssertBorrowedView(borrowed, Assert.Single(receivedViews));
            AssertBorrowedView(borrowed, Assert.Single(resultViews));
            Assert.Collection(
                controlViews,
                view => AssertBorrowedView(borrowed, view),
                view => AssertBorrowedView(borrowed, view));
        }

        [Fact]
        public void NativeRuntimeNativeBufferSubmitAndResultLoopDoesNotAllocatePayloadCopies()
        {
            const int PayloadBytes = 64 * 1024;
            const int Iterations = 32;
            var completedEvents = new Queue<NnrpPollResult>();
            var submittedPayloadBytes = 0UL;
            var resultPayloadBytes = 0UL;
            var nativePayloadBacking = new byte[PayloadBytes];
            var nativePayloadHandle = GCHandle.Alloc(nativePayloadBacking, GCHandleType.Pinned);

            NnrpFfiStatus CaptureSubmit(NnrpFfiSubmitRequest request, out NnrpHandle operation)
            {
                submittedPayloadBytes += request.Payload.Length.ToUInt64();
                operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
                completedEvents.Enqueue(CreatePollResult(
                    new NnrpHandle(NnrpHandleKind.Connection, 11, 2),
                    request.Session,
                    operation,
                    request.FrameId));
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus AwaitCompletedEvent(NnrpHandle connection, out NnrpPollResult result)
            {
                result = completedEvents.Dequeue();
                return connection.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureServerReceiveSubmit(NnrpServerReceiveSubmitRequest request, out NnrpHandle operation)
            {
                operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureServerSendResult(NnrpServerSendResultRequest request)
            {
                resultPayloadBytes += request.Payload.Length.ToUInt64();
                return request.Operation.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureBufferAcquireCopy(NnrpBufferView source, out NnrpHandle buffer, out NnrpBufferView view)
            {
                buffer = new NnrpHandle(NnrpHandleKind.Buffer, 90, 1);
                view = new NnrpBufferView(nativePayloadHandle.AddrOfPinnedObject(), new UIntPtr(PayloadBytes));
                return source.Pointer != IntPtr.Zero || source.Length == UIntPtr.Zero
                    ? NnrpFfiStatus.Ok
                    : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
            }

            try
            {
                var entrypoints = new NnrpNativeRuntimeEntrypoints(
                    CurrentProtocolVersion,
                    () => MatchingCapabilities(),
                    ConnectionBootstrap,
                    ClientConnect,
                    SessionOpen,
                    SessionOpen,
                    CaptureSubmit,
                    CaptureSubmit,
                    HandleStatus,
                    HandleStatus,
                    ClientCancel,
                    AwaitCompletedEvent,
                    ServerBind,
                    ServerAccept,
                    CaptureServerReceiveSubmit,
                    CaptureServerSendResult,
                    ServerFlowUpdate,
                    HandleStatus,
                    Control,
                    PollEmpty,
                    DispatchEvent,
                    bufferAcquireCopy: CaptureBufferAcquireCopy,
                    bufferView: BufferView,
                    bufferRelease: HandleStatus);
                using var nativePayload = new NnrpNativeBuffers(entrypoints).AcquireCopy(new byte[PayloadBytes]);
                var clientSession = new NnrpNativeRuntimeClient(entrypoints)
                    .Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp)
                    .OpenSession(41, 3, 4, 5, 6);
                using var server = NnrpNativeRuntimeServer.Bind(entrypoints, 50, 2, NnrpNativeArtifact.TransportSlotTcp);
                var serverSession = server.AcceptSession(42, 3, 4, 5, 6);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var index = 0; index < Iterations; index += 1)
                {
                    clientSession.SubmitAndPollResult((ulong)(100 + index), (uint)(10 + index), nativePayload, maxEvents: 1);
                    var operation = serverSession.ReceiveSubmit((ulong)(200 + index), (uint)(20 + index), nativePayload);
                    serverSession.SendResult(operation, nativePayload);
                }

                var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                var payloadBytesPerLoop = (long)PayloadBytes * Iterations;

                Assert.Equal((ulong)PayloadBytes * Iterations, submittedPayloadBytes);
                Assert.Equal((ulong)PayloadBytes * Iterations, resultPayloadBytes);
                Assert.True(
                    allocated < payloadBytesPerLoop / 4,
                    $"Expected native-buffer loops not to allocate payload copies; allocated {allocated} bytes for {payloadBytesPerLoop} payload bytes.");
            }
            finally
            {
                nativePayloadHandle.Free();
            }
        }

        [Fact]
        public void NativeRuntimeConnectionClosesThroughNativeEntrypointAndGuardsChildren()
        {
            var closeCount = 0;
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
                AwaitEvent,
                ServerBind,
                ServerAccept,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent,
                clientCloseConnection: handle =>
                {
                    closeCount++;
                    return HandleStatus(handle);
                });
            var connection = new NnrpNativeRuntimeClient(entrypoints).Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp);
            var session = connection.OpenSession(41, 3, 4, 5, 6);

            connection.Close();

            Assert.True(connection.IsClosed);
            Assert.Equal(1, closeCount);
            Assert.Throws<NnrpNativeInvalidStateException>(() => connection.OpenSession(42, 4, 4, 5, 6));
            Assert.Throws<NnrpNativeInvalidStateException>(() => connection.AwaitEvent());
            Assert.Throws<NnrpNativeInvalidStateException>(() => connection.Control(10));
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.Submit(99, 7));
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.Close());

            connection.Dispose();
            Assert.Equal(1, closeCount);
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
            Assert.Equal(new byte[] { 1, 2, 3 }, result.Event.PayloadMemory.ToArray());
            Assert.Equal(new byte[] { 1, 2, 3 }, result.Event.PayloadSpan.ToArray());
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
            Assert.Equal(new byte[] { 1, 2, 3 }, completed.PayloadMemory.ToArray());
            Assert.Equal(new byte[] { 1, 2, 3 }, completed.PayloadSpan.ToArray());
            Assert.Equal(completed.PayloadMemory.ToArray(), completed.Event.PayloadMemory.ToArray());
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
        public void NativeRuntimeSessionHostOpensAndSubmitsThroughNativeBackend()
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
            var options = new NnrpNativeRuntimeSessionHostOptions(
                12,
                2,
                NnrpNativeArtifact.TransportSlotTcp,
                41,
                3,
                4,
                5,
                6);

            var client = new NnrpNativeRuntimeClient(entrypoints);
            using (var host = NnrpNativeRuntimeSessionHost.Open(client, options))
            {
                var operation = host.SubmitOperation(99, 7, parentOperationId: 1, operationGroupId: 2);
                var polled = host.PollResult(operation, maxEvents: 1);
                var events = host.PollAvailableEvents(1);
                var result = host.SubmitAndPollResult(99, 7, new byte[] { 1, 2, 3 }, maxEvents: 1);

                Assert.Same(client, host.Backend);
                Assert.Equal((ulong)99, operation.OperationId);
                Assert.Equal((uint)7, operation.FrameId);
                Assert.Equal((ulong)99, polled.OperationId);
                Assert.Single(events);
                Assert.Equal((ulong)99, result.OperationId);
                Assert.Equal((uint)7, result.FrameId);
                Assert.Equal(new byte[] { 1, 2, 3 }, result.Payload);
                Assert.Equal(options, host.Options);
                Assert.Equal((ulong)12, host.Connection.Handle.Handle.Id);
                Assert.Equal((uint)41, host.Session.Handle.Handle.Id);
            }
        }

        [Fact]
        public void NativeRuntimeSessionHostBootstrapsControlsCancelsAndCloses()
        {
            uint cancelledFrameId = 0;
            uint controlCode = 0;
            UIntPtr controlPayloadLength = UIntPtr.Zero;

            NnrpFfiStatus CaptureCancel(NnrpClientCancelRequest request)
            {
                cancelledFrameId = request.FrameId;
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureControl(NnrpControlRequest request)
            {
                controlCode = request.ControlCode;
                controlPayloadLength = request.Payload.Length;
                return request.Handle.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

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
                CaptureCancel,
                AwaitEventWithPayload,
                ServerBind,
                ServerAccept,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                bufferAcquireCopy: BufferAcquireCopy,
                bufferView: BufferView,
                bufferRelease: HandleStatus,
                cacheQuery: CacheQuery,
                cacheTouch: CacheTouch,
                cachePrefetch: CachePrefetch,
                cacheRelease: CacheRelease);
            var options = new NnrpNativeRuntimeSessionHostOptions(
                12,
                2,
                NnrpNativeArtifact.TransportSlotTcp,
                41,
                3,
                4,
                5,
                6)
            {
                BootstrapConnection = true
            };
            var host = NnrpNativeRuntimeSessionHost.Open(new NnrpNativeRuntimeClient(entrypoints), options);
            var objectId = MatchingCacheObjectId();
            using var nativePayload = new NnrpNativeBuffers(entrypoints).AcquireCopy(new byte[] { 1, 2, 3 });

            var operation = host.SubmitOperation(98, 6, nativePayload);
            var result = host.SubmitAndPollResult(99, 7, nativePayload, maxEvents: 1);
            host.Cancel(71);
            host.Control(17, nativePayload);
            var query = host.QueryCacheLease(objectId, 9, 1000, 500);
            var touch = host.TouchCacheLease(objectId, 9, 1000, 500);
            var prefetch = host.PrefetchCacheLeases(new[] { objectId }, 1000, 500);
            var release = host.ReleaseCacheLease(new NnrpCacheLeaseHandle(query.LeaseHandle));
            host.Close();

            Assert.Equal((uint)71, cancelledFrameId);
            Assert.Equal((uint)17, controlCode);
            Assert.Equal(new UIntPtr(3), controlPayloadLength);
            Assert.Equal((ulong)98, operation.OperationId);
            Assert.Equal((ulong)99, result.OperationId);
            Assert.Equal((uint)NnrpCacheLeaseOutcome.Valid, query.OutcomeCode);
            Assert.Equal((ulong)2500, touch.ExpiresAtMilliseconds);
            Assert.Single(prefetch);
            Assert.Equal((uint)NnrpCacheLeaseOutcome.Released, release.OutcomeCode);
            Assert.True(host.IsClosed);
            Assert.True(host.Session.IsClosed);
            Assert.True(host.Connection.IsClosed);
            Assert.Throws<NnrpNativeInvalidStateException>(() => host.Cancel(72));
            host.Dispose();
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeSessionHost.Open(null!));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeSessionHost.Open((INnrpNativeRuntimeBackend)null!, options));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeSessionHost.Open(new NnrpNativeRuntimeClient(CreateEntrypoints()), null!));
        }

        [Fact]
        public void NativeRuntimeSessionHostSelectsFallbackBackendWhenArtifactIsUnavailable()
        {
            var fallback = new NnrpNativeRuntimeClient(CreateEntrypoints());
            var options = new NnrpNativeRuntimeSessionHostOptions(
                12,
                2,
                NnrpNativeArtifact.TransportSlotTcp,
                41,
                3,
                4,
                5,
                6)
            {
                ArtifactPath = "missing-native-runtime.dll",
                ArtifactRoot = "unused-native-root",
                Platform = new NnrpNativePlatform("windows", "x86_64"),
                FallbackBackend = fallback,
                FallbackPolicy = NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics
            };

            using (var host = NnrpNativeRuntimeSessionHost.Open(options))
            {
                Assert.Same(fallback, host.Backend);
                Assert.Equal("missing-native-runtime.dll", host.Options.ArtifactPath);
                Assert.Equal("unused-native-root", host.Options.ArtifactRoot);
                Assert.Equal(new NnrpNativePlatform("windows", "x86_64"), host.Options.Platform);
                Assert.Same(fallback, host.Options.FallbackBackend);
                Assert.Equal(NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics, host.Options.FallbackPolicy);
            }
        }

        [Fact]
        public void NativeRuntimeConnectionHostRoutesRegisteredSessions()
        {
            var pendingEvents = new Queue<NnrpPollResult>();

            NnrpFfiStatus AwaitRoutedEvent(NnrpHandle connection, out NnrpPollResult result)
            {
                result = pendingEvents.Count > 0 ? pendingEvents.Dequeue() : EmptyPollResult();
                return connection.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

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
                AwaitRoutedEvent,
                ServerBind,
                ServerAccept,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent);
            var backend = new NnrpNativeRuntimeClient(entrypoints);
            var options = new NnrpNativeRuntimeConnectionHostOptions(
                12,
                2,
                NnrpNativeArtifact.TransportSlotTcp);
            using (var host = NnrpNativeRuntimeConnectionHost.Open(backend, options))
            {
                var firstSession = host.OpenSession(new NnrpNativeRuntimeSessionOptions(41, 3, 4, 5, 6));
                var secondSession = host.OpenSession(new NnrpNativeRuntimeSessionOptions(42, 4, 4, 5, 6));
                var firstOperation = host.SubmitOperation(41, 99, 7, parentOperationId: 1, operationGroupId: 2);
                var secondOperation = host.SubmitOperation(42, 100, 8);

                pendingEvents.Enqueue(CreatePollResult(host.Connection.Handle.Handle, secondSession.Handle.Handle, secondOperation.Handle.Handle, 8));
                pendingEvents.Enqueue(CreatePollResult(host.Connection.Handle.Handle, firstSession.Handle.Handle, firstOperation.Handle.Handle, 7));

                var firstResult = host.PollResult(41, firstOperation, maxEvents: 2);
                var secondResult = host.PollResult(42, secondOperation, maxEvents: 1);

                Assert.Same(backend, host.Backend);
                Assert.Same(options, host.Options);
                Assert.True(host.TryGetSession(41, out var resolvedSession));
                Assert.Same(firstSession, resolvedSession);
                Assert.Same(secondSession, host.GetSession(42));
                Assert.Equal(2, host.Sessions.Count);
                Assert.Equal((ulong)99, firstResult.OperationId);
                Assert.Equal((uint)8, secondResult.FrameId);
                Assert.Throws<InvalidOperationException>(() => host.OpenSession(new NnrpNativeRuntimeSessionOptions(41, 3, 4, 5, 6)));
                Assert.Throws<KeyNotFoundException>(() => host.GetSession(1000));
                Assert.False(host.CloseSession(1000));
                Assert.True(host.CloseSession(41));
                Assert.False(host.Sessions.ContainsKey(41));
            }
        }

        [Fact]
        public void NativeRuntimeConnectionHostSelectsFallbackAndManagesControls()
        {
            uint cancelledFrameId = 0;
            uint controlCode = 0;
            UIntPtr controlPayloadLength = UIntPtr.Zero;

            NnrpFfiStatus CaptureCancel(NnrpClientCancelRequest request)
            {
                cancelledFrameId = request.FrameId;
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureControl(NnrpControlRequest request)
            {
                controlCode = request.ControlCode;
                controlPayloadLength = request.Payload.Length;
                return request.Handle.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

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
                CaptureCancel,
                AwaitEventWithPayload,
                ServerBind,
                ServerAccept,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                bufferAcquireCopy: BufferAcquireCopy,
                bufferView: BufferView,
                bufferRelease: HandleStatus);
            var fallback = new NnrpNativeRuntimeClient(entrypoints);
            var options = new NnrpNativeRuntimeConnectionHostOptions(
                12,
                2,
                NnrpNativeArtifact.TransportSlotTcp)
            {
                BootstrapConnection = true,
                ArtifactPath = "missing-native-runtime.dll",
                ArtifactRoot = "unused-native-root",
                Platform = new NnrpNativePlatform("windows", "x86_64"),
                FallbackBackend = fallback,
                FallbackPolicy = NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics
            };
            var host = NnrpNativeRuntimeConnectionHost.Open(options);
            var session = host.OpenSession(new NnrpNativeRuntimeSessionOptions(41, 3, 4, 5, 6));
            using var nativePayload = new NnrpNativeBuffers(entrypoints).AcquireCopy(new byte[] { 1, 2, 3 });

            var routedOperation = host.SubmitOperation(41, 98, 6, nativePayload);
            var result = host.SubmitAndPollResult(41, 99, 7, nativePayload, maxEvents: 1);
            var events = host.PollAvailableEvents(1);
            host.Cancel(41, 71);
            host.Control(41, 17, nativePayload);
            host.Close();

            Assert.Same(fallback, host.Backend);
            Assert.Equal(new NnrpNativePlatform("windows", "x86_64"), host.Options.Platform);
            Assert.Equal((ulong)98, routedOperation.OperationId);
            Assert.Equal((ulong)99, result.OperationId);
            Assert.Single(events);
            Assert.Equal((uint)71, cancelledFrameId);
            Assert.Equal((uint)17, controlCode);
            Assert.Equal(new UIntPtr(3), controlPayloadLength);
            Assert.True(host.IsClosed);
            Assert.True(session.IsClosed);
            Assert.True(host.Connection.IsClosed);
            Assert.Empty(host.Sessions);
            Assert.Throws<NnrpNativeInvalidStateException>(() => host.OpenSession(new NnrpNativeRuntimeSessionOptions(43, 4, 4, 5, 6)));
            Assert.Throws<NnrpNativeInvalidStateException>(() => host.TryGetSession(41, out _));
            host.Dispose();
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeConnectionHost.Open(null!));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeConnectionHost.Open((INnrpNativeRuntimeBackend)null!, options));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeConnectionHost.Open(new NnrpNativeRuntimeClient(CreateEntrypoints()), null!));
        }

        [Fact]
        public void NativeRuntimeConnectionHostRoutesSchemaAndCacheLeaseHelpers()
        {
            var entrypoints = CreateEntrypoints();
            var host = NnrpNativeRuntimeConnectionHost.Open(
                new NnrpNativeRuntimeClient(entrypoints),
                new NnrpNativeRuntimeConnectionHostOptions(
                    12,
                    2,
                    NnrpNativeArtifact.TransportSlotTcp));
            var session = host.OpenSession(new NnrpNativeRuntimeSessionOptions(3, 1, 4, 0x1001, 3));
            var objectId = MatchingCacheObjectId();

            using (var registry = host.CreateSchemaRegistry())
            {
                Assert.Equal(NnrpSchemaRegistryAction.Installed, registry.Install(TokenSchemaDescriptor()));
                registry.ValidateBinding(MatchingTypedPayloadDescriptor());
                Assert.Equal((uint)0x1001, registry.Lookup(0x1001, 3).SchemaId);
            }

            var query = host.QueryCacheLease(3, objectId, 9, 1000, 500);
            var touch = host.TouchCacheLease(3, objectId, 9, 1000, 500);
            var prefetch = host.PrefetchCacheLeases(3, new[] { objectId }, 1000, 500);
            var release = host.ReleaseCacheLease(new NnrpCacheLeaseHandle(query.LeaseHandle));

            Assert.Equal((uint)NnrpCacheLeaseOutcome.Valid, query.OutcomeCode);
            Assert.Equal((ulong)2500, touch.ExpiresAtMilliseconds);
            Assert.Single(prefetch);
            Assert.Equal((uint)1, prefetch[0].ObjectId.CacheNamespace);
            Assert.Equal((uint)NnrpCacheLeaseOutcome.Released, release.OutcomeCode);

            host.Close();

            Assert.True(host.IsClosed);
            Assert.True(session.IsClosed);
            Assert.Throws<NnrpNativeInvalidStateException>(() => host.CreateSchemaRegistry());
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.QueryCacheLease(objectId, 9, 1000, 500));
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
        public void NativeRuntimeConnectionRoutesBufferedMultiSessionResults()
        {
            var pendingEvents = new Queue<NnrpPollResult>();

            NnrpFfiStatus AwaitRoutedEvent(NnrpHandle connection, out NnrpPollResult result)
            {
                result = pendingEvents.Count > 0 ? pendingEvents.Dequeue() : EmptyPollResult();
                return connection.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

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
                AwaitRoutedEvent,
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
            var firstSession = connection.OpenSession(41, 3, 4, 5, 6);
            var secondSession = connection.OpenSession(42, 4, 4, 5, 6);
            var firstOperation = firstSession.SubmitOperation(99, 7);
            var secondOperation = secondSession.SubmitOperation(100, 8);

            pendingEvents.Enqueue(CreatePollResult(connection.Handle.Handle, secondSession.Handle.Handle, secondOperation.Handle.Handle, 8));
            pendingEvents.Enqueue(CreatePollResult(connection.Handle.Handle, firstSession.Handle.Handle, firstOperation.Handle.Handle, 7));

            var firstResult = firstSession.PollResult(firstOperation, maxEvents: 2);
            var secondResult = secondSession.PollResult(secondOperation, maxEvents: 1);

            Assert.Equal((ulong)99, firstResult.OperationId);
            Assert.Equal((uint)7, firstResult.FrameId);
            Assert.Equal(firstSession.Handle.Handle, firstResult.Event.Session);
            Assert.Equal((ulong)100, secondResult.OperationId);
            Assert.Equal((uint)8, secondResult.FrameId);
            Assert.Equal(secondSession.Handle.Handle, secondResult.Event.Session);
            Assert.Empty(pendingEvents);
        }

        [Fact]
        public void NativeRuntimeConnectionPollsBufferedSessionResult()
        {
            var pendingEvents = new Queue<NnrpPollResult>();

            NnrpFfiStatus AwaitRoutedEvent(NnrpHandle connection, out NnrpPollResult result)
            {
                result = pendingEvents.Count > 0 ? pendingEvents.Dequeue() : EmptyPollResult();
                return connection.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

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
                AwaitRoutedEvent,
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
            var firstSession = connection.OpenSession(41, 3, 4, 5, 6);
            var secondSession = connection.OpenSession(42, 4, 4, 5, 6);
            var firstOperation = firstSession.SubmitOperation(99, 7);
            var secondOperation = secondSession.SubmitOperation(100, 8);

            pendingEvents.Enqueue(CreatePollResult(connection.Handle.Handle, secondSession.Handle.Handle, secondOperation.Handle.Handle, 8));

            Assert.Throws<NnrpNativeWouldBlockException>(() => firstSession.PollResult(firstOperation, maxEvents: 1));

            var buffered = connection.AwaitEvent();

            Assert.NotNull(buffered.Event);
            Assert.Equal(secondSession.Handle.Handle, buffered.Event!.Session);
            Assert.Equal(secondOperation.Handle.Handle, buffered.Event.Operation);
            Assert.Empty(pendingEvents);
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
        public void NativeRuntimeServerFacadeRoutesNativeSubmitResultFlowAndControl()
        {
            ulong receivedOperationId = 0;
            uint receivedFrameId = 0;
            UIntPtr receivedPayloadLength = UIntPtr.Zero;
            UIntPtr resultPayloadLength = UIntPtr.Zero;
            uint flowFrameId = 0;
            uint controlCode = 0;
            UIntPtr controlPayloadLength = UIntPtr.Zero;

            NnrpFfiStatus CaptureServerReceiveSubmit(NnrpServerReceiveSubmitRequest request, out NnrpHandle operation)
            {
                receivedOperationId = request.OperationId;
                receivedFrameId = request.FrameId;
                receivedPayloadLength = request.Payload.Length;
                operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureServerSendResult(NnrpServerSendResultRequest request)
            {
                resultPayloadLength = request.Payload.Length;
                return request.Operation.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureServerFlowUpdate(NnrpServerFlowUpdateRequest request)
            {
                flowFrameId = request.FrameId;
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureControl(NnrpControlRequest request)
            {
                controlCode = request.ControlCode;
                controlPayloadLength = request.Payload.Length;
                return request.Handle.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

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
                AwaitEvent,
                ServerBind,
                ServerAccept,
                CaptureServerReceiveSubmit,
                CaptureServerSendResult,
                CaptureServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                bufferAcquireCopy: BufferAcquireCopy,
                bufferView: BufferView,
                bufferRelease: HandleStatus);

            using (var server = NnrpNativeRuntimeServer.Bind(entrypoints, 50, 2, NnrpNativeArtifact.TransportSlotTcp))
            {
                var session = server.AcceptSession(41, 3, 4, 5, 6);
                using var nativePayload = new NnrpNativeBuffers(entrypoints).AcquireCopy(new byte[] { 1, 2, 3 });
                var operation = session.ReceiveSubmit(99, 7, nativePayload);

                session.SendResult(operation, nativePayload);
                session.SendFlowUpdate(7);
                session.Control(17, nativePayload);
                Assert.Throws<ArgumentNullException>(() => session.ReceiveSubmit(100, 8, (NnrpNativeBuffer)null!));
                Assert.Throws<ArgumentNullException>(() => session.SendResult(null!, nativePayload));
                Assert.Throws<ArgumentNullException>(() => session.SendResult(operation, (NnrpNativeBuffer)null!));
                Assert.Throws<ArgumentNullException>(() => session.Control(18, (NnrpNativeBuffer)null!));
                session.Close();
                server.Close();

                Assert.Equal((ulong)99, receivedOperationId);
                Assert.Equal((uint)7, receivedFrameId);
                Assert.Equal(new UIntPtr(3), receivedPayloadLength);
                Assert.Equal(new UIntPtr(3), resultPayloadLength);
                Assert.Equal((uint)7, flowFrameId);
                Assert.Equal((uint)17, controlCode);
                Assert.Equal(new UIntPtr(3), controlPayloadLength);
                Assert.True(session.IsClosed);
                Assert.True(server.IsClosed);
                Assert.Throws<NnrpNativeInvalidStateException>(() => server.AcceptSession(42, 3, 4, 5, 6));
            }

            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeServer.Bind(null!, 50, 2, NnrpNativeArtifact.TransportSlotTcp));
        }

        [Fact]
        public void NativeRuntimeServerSessionRejectsUseAfterServerClose()
        {
            var server = NnrpNativeRuntimeServer.Bind(CreateEntrypoints(), 50, 2, NnrpNativeArtifact.TransportSlotTcp);
            var session = server.AcceptSession(41, 3, 4, 5, 6);

            server.Close();

            Assert.Throws<NnrpNativeInvalidStateException>(() => session.ReceiveSubmit(99, 7));
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.SendFlowUpdate(7));
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.Control(17));
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.Close());
            server.Dispose();
        }

        [Fact]
        public void NativeRuntimeServerHostRoutesRegisteredSessions()
        {
            UIntPtr receivedPayloadLength = UIntPtr.Zero;
            UIntPtr resultPayloadLength = UIntPtr.Zero;
            uint flowFrameId = 0;
            uint controlCode = 0;
            UIntPtr controlPayloadLength = UIntPtr.Zero;

            NnrpFfiStatus CaptureServerReceiveSubmit(NnrpServerReceiveSubmitRequest request, out NnrpHandle operation)
            {
                receivedPayloadLength = request.Payload.Length;
                operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureServerSendResult(NnrpServerSendResultRequest request)
            {
                resultPayloadLength = request.Payload.Length;
                return request.Operation.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureServerFlowUpdate(NnrpServerFlowUpdateRequest request)
            {
                flowFrameId = request.FrameId;
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureControl(NnrpControlRequest request)
            {
                controlCode = request.ControlCode;
                controlPayloadLength = request.Payload.Length;
                return request.Handle.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

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
                AwaitEvent,
                ServerBind,
                ServerAccept,
                CaptureServerReceiveSubmit,
                CaptureServerSendResult,
                CaptureServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                bufferAcquireCopy: BufferAcquireCopy,
                bufferView: BufferView,
                bufferRelease: HandleStatus);
            var options = new NnrpNativeRuntimeServerHostOptions(50, 2, NnrpNativeArtifact.TransportSlotTcp);

            using (var host = NnrpNativeRuntimeServerHost.Open(entrypoints, options))
            {
                var first = host.AcceptSession(new NnrpNativeRuntimeSessionOptions(41, 3, 4, 5, 6));
                var second = host.AcceptSession(new NnrpNativeRuntimeSessionOptions(42, 4, 4, 5, 6));
                using var nativePayload = new NnrpNativeBuffers(entrypoints).AcquireCopy(new byte[] { 1, 2, 3 });
                var operation = host.ReceiveSubmit(41, 99, 7, nativePayload);

                host.SendResult(41, operation, nativePayload);
                host.SendFlowUpdate(41, 7);
                host.Control(42, 17, nativePayload);

                Assert.Same(first, host.GetSession(41));
                Assert.True(host.TryGetSession(42, out var routedSecond));
                Assert.Same(second, routedSecond);
                Assert.Equal(new UIntPtr(3), receivedPayloadLength);
                Assert.Equal(new UIntPtr(3), resultPayloadLength);
                Assert.Equal((uint)7, flowFrameId);
                Assert.Equal((uint)17, controlCode);
                Assert.Equal(new UIntPtr(3), controlPayloadLength);
                Assert.Throws<InvalidOperationException>(() => host.AcceptSession(new NnrpNativeRuntimeSessionOptions(41, 5, 4, 5, 6)));
                Assert.True(host.CloseSession(41));
                Assert.False(host.CloseSession(99));
                Assert.Throws<KeyNotFoundException>(() => host.GetSession(41));

                host.Close();

                Assert.True(first.IsClosed);
                Assert.True(second.IsClosed);
                Assert.True(host.Server.IsClosed);
                Assert.True(host.IsClosed);
                Assert.Empty(host.Sessions);
                Assert.Throws<NnrpNativeInvalidStateException>(() => host.AcceptSession(new NnrpNativeRuntimeSessionOptions(43, 5, 4, 5, 6)));
                host.Dispose();
            }

            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeServerHost.Open((NnrpNativeRuntimeServerHostOptions)null!));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeServerHost.Open((NnrpNativeRuntimeEntrypoints)null!, options));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeServerHost.Open(entrypoints, null!));
        }

        [Fact]
        public void NativeRuntimeServerHostBorrowsArrayBackedMemoryPayloadSlices()
        {
            var receivedPayload = Array.Empty<byte>();
            var resultPayload = Array.Empty<byte>();
            var controlledPayload = Array.Empty<byte>();

            NnrpFfiStatus CaptureServerReceiveSubmit(NnrpServerReceiveSubmitRequest request, out NnrpHandle operation)
            {
                receivedPayload = CopyBufferView(request.Payload);
                operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
                return request.Session.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureServerSendResult(NnrpServerSendResultRequest request)
            {
                resultPayload = CopyBufferView(request.Payload);
                return request.Operation.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

            NnrpFfiStatus CaptureControl(NnrpControlRequest request)
            {
                controlledPayload = CopyBufferView(request.Payload);
                return request.Handle.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
            }

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
                AwaitEvent,
                ServerBind,
                ServerAccept,
                CaptureServerReceiveSubmit,
                CaptureServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent);
            using var host = NnrpNativeRuntimeServerHost.Open(
                entrypoints,
                new NnrpNativeRuntimeServerHostOptions(50, 2, NnrpNativeArtifact.TransportSlotTcp));
            host.AcceptSession(new NnrpNativeRuntimeSessionOptions(41, 3, 4, 5, 6));
            var source = new byte[] { 0, 1, 2, 3, 4 };
            var payload = source.AsMemory(1, 3);

            var operation = host.ReceiveSubmit(41, 99, 7, payload);
            host.SendResult(41, operation, payload);
            host.Control(41, 10, payload);

            Assert.Throws<ArgumentNullException>(() => host.SendResult(41, null!, payload));
            Assert.Equal(new byte[] { 1, 2, 3 }, receivedPayload);
            Assert.Equal(new byte[] { 1, 2, 3 }, resultPayload);
            Assert.Equal(new byte[] { 1, 2, 3 }, controlledPayload);
        }

        [Fact]
        public void NativeRuntimeServerHostRoutesSchemaAndCacheLeaseHelpers()
        {
            var entrypoints = CreateEntrypoints();
            var host = NnrpNativeRuntimeServerHost.Open(
                entrypoints,
                new NnrpNativeRuntimeServerHostOptions(
                    50,
                    2,
                    NnrpNativeArtifact.TransportSlotTcp));
            var session = host.AcceptSession(new NnrpNativeRuntimeSessionOptions(3, 1, 4, 0x1001, 3));
            var objectId = MatchingCacheObjectId();

            using (var registry = host.CreateSchemaRegistry())
            {
                Assert.Equal(NnrpSchemaRegistryAction.Installed, registry.Install(TokenSchemaDescriptor()));
                registry.ValidateBinding(MatchingTypedPayloadDescriptor());
                Assert.Equal(NnrpSchemaRegistryAction.Invalidated, registry.Invalidate(0x1001, 3));
            }

            var query = host.QueryCacheLease(3, objectId, 9, 1000, 500);
            var touch = host.TouchCacheLease(3, objectId, 9, 1000, 500);
            var prefetch = host.PrefetchCacheLeases(3, new[] { objectId }, 1000, 500);
            var release = host.ReleaseCacheLease(new NnrpCacheLeaseHandle(query.LeaseHandle));
            var sessionRelease = session.ReleaseCacheLease(new NnrpCacheLeaseHandle(query.LeaseHandle));

            Assert.Equal((uint)NnrpCacheLeaseOutcome.Valid, query.OutcomeCode);
            Assert.Equal((ulong)2500, touch.ExpiresAtMilliseconds);
            Assert.Single(prefetch);
            Assert.Equal((uint)1, prefetch[0].ObjectId.CacheNamespace);
            Assert.Equal((uint)NnrpCacheLeaseOutcome.Released, release.OutcomeCode);
            Assert.Equal((uint)NnrpCacheLeaseOutcome.Released, sessionRelease.OutcomeCode);

            host.Close();

            Assert.True(host.IsClosed);
            Assert.True(session.IsClosed);
            Assert.Throws<NnrpNativeInvalidStateException>(() => host.CreateSchemaRegistry());
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.QueryCacheLease(objectId, 9, 1000, 500));
        }

        [Fact]
        public void NativeRuntimeServerHostRequiresNativeArtifactWhenOpeningFromOptions()
        {
            var options = new NnrpNativeRuntimeServerHostOptions(50, 2, NnrpNativeArtifact.TransportSlotTcp)
            {
                ArtifactPath = "missing-native-runtime.dll",
                ArtifactRoot = "unused-native-root",
                Platform = new NnrpNativePlatform("windows", "x86_64")
            };

            Assert.Throws<NnrpNativeArtifactException>(() => NnrpNativeRuntimeServerHost.Open(options));
        }

        [Fact]
        public void NativeRuntimeBackendSelectorRequiresNativeByDefault()
        {
            Assert.Throws<NnrpNativeArtifactException>(() =>
                NnrpNativeRuntimeBackendSelector.Select(
                    artifactPath: "missing-native-artifact.dll",
                    fallback: new FakeRuntimeBackend()));
        }

        [Fact]
        public void NativeRuntimeBackendSelectorUsesFallbackOnlyForDiagnosticsPolicy()
        {
            var fallback = new FakeRuntimeBackend();

            var selected = NnrpNativeRuntimeBackendSelector.Select(
                artifactPath: "missing-native-artifact.dll",
                fallback: fallback,
                fallbackPolicy: NnrpNativeRuntimeFallbackPolicy.UseFallbackForDiagnostics);

            Assert.Same(fallback, selected);
        }

        [Fact]
        public void NativeRuntimeBackendSelectorExplicitRequireNativeRejectsFallback()
        {
            Assert.Throws<NnrpNativeArtifactException>(() =>
                NnrpNativeRuntimeBackendSelector.Select(
                    artifactPath: "missing-native-artifact.dll",
                    fallback: new FakeRuntimeBackend(),
                    fallbackPolicy: NnrpNativeRuntimeFallbackPolicy.RequireNative));
        }

        [Fact]
        public void NativeRuntimeHostOptionsRequireNativeByDefault()
        {
            var sessionOptions = new NnrpNativeRuntimeSessionHostOptions(11, 2, NnrpNativeArtifact.TransportSlotTcp, 41, 3, 4, 5, 6);
            var connectionOptions = new NnrpNativeRuntimeConnectionHostOptions(11, 2, NnrpNativeArtifact.TransportSlotTcp);

            Assert.Equal(NnrpNativeRuntimeFallbackPolicy.RequireNative, sessionOptions.FallbackPolicy);
            Assert.Equal(NnrpNativeRuntimeFallbackPolicy.RequireNative, connectionOptions.FallbackPolicy);
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
            ushort abiMinor = NnrpNativeArtifact.MinimumAbiMinor,
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
                DispatchEvent,
                schemaRegistryCreate: SchemaRegistryCreate,
                schemaRegistryInstall: SchemaRegistryInstall,
                schemaRegistryLookup: SchemaRegistryLookup,
                schemaRegistryInvalidate: SchemaRegistryInvalidate,
                schemaRegistryValidateBinding: SchemaRegistryValidateBinding,
                schemaRegistryRelease: HandleStatus,
                clientResumeSession: ClientResumeSession,
                schemaDescriptorParse: SchemaDescriptorParse,
                schemaDescriptorWrite: SchemaDescriptorWrite,
                tokenDeltaSchemaDescriptor: TokenDeltaSchemaDescriptor,
                typedPayloadValidateBinding: TypedPayloadValidateBinding,
                sessionRecoveryRequestValidate: SessionRecoveryRequestValidate,
                sessionRecoveryAckValidate: SessionRecoveryAckValidate,
                migrationRecoveryValidate: MigrationRecoveryValidate,
                migrationShouldReplayFrame: MigrationShouldReplayFrame,
                bufferAcquireCopy: BufferAcquireCopy,
                bufferView: BufferView,
                bufferRelease: HandleStatus,
                cacheQuery: CacheQuery,
                cacheTouch: CacheTouch,
                cachePrefetch: CachePrefetch,
                cacheRelease: CacheRelease);
        }

        private static byte[] CopyBufferView(NnrpBufferView view)
        {
            if (view.Length == UIntPtr.Zero)
            {
                return Array.Empty<byte>();
            }

            if (view.Pointer == IntPtr.Zero)
            {
                throw new ArgumentException("Non-empty buffer view has a null pointer.", nameof(view));
            }

            var bytes = new byte[checked((int)view.Length.ToUInt64())];
            Marshal.Copy(view.Pointer, bytes, 0, bytes.Length);
            return bytes;
        }

        private static void AssertBorrowedView(NnrpBufferView expected, NnrpBufferView actual)
        {
            Assert.Equal(expected.Pointer, actual.Pointer);
            Assert.Equal(expected.Length, actual.Length);
        }

        private static void AssertBorrowedView(IntPtr expectedPointer, int expectedLength, NnrpBufferView actual)
        {
            Assert.Equal(expectedPointer, actual.Pointer);
            Assert.Equal(new UIntPtr((uint)expectedLength), actual.Length);
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

        private static NnrpSessionResumeRequest MatchingSessionResumeRequest()
        {
            return new NnrpSessionResumeRequest(new NnrpHandle(NnrpHandleKind.Connection, 1, 1), 3, 1, 1, 10, 1, 16);
        }

        private static NnrpFfiStatus ClientResumeSession(
            NnrpSessionResumeRequest request,
            out NnrpHandle session,
            out NnrpSessionRecoveryOutcome outcome)
        {
            session = new NnrpHandle(NnrpHandleKind.Session, request.RequestedSessionId, request.Generation);
            outcome = new NnrpSessionRecoveryOutcome(1, 2);
            return request.Connection.Kind == NnrpHandleKind.Connection && request.ResumeTokenBytes > 0
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
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

        private static NnrpSchemaDescriptorHeader TokenSchemaDescriptor()
        {
            return new NnrpSchemaDescriptorHeader(
                0x1001,
                3,
                2,
                0,
                1,
                1,
                32,
                0,
                1,
                0x6e6e7270746f6b33UL);
        }

        private static NnrpTypedPayloadDescriptor MatchingTypedPayloadDescriptor()
        {
            return new NnrpTypedPayloadDescriptor(
                2,
                0,
                0x1001,
                3,
                1,
                0,
                16);
        }

        private static NnrpFfiStatus SchemaDescriptorParse(NnrpBufferView source, out NnrpSchemaDescriptorHeader descriptor)
        {
            descriptor = TokenSchemaDescriptor();
            return source.Pointer != IntPtr.Zero || source.Length == UIntPtr.Zero
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus SchemaDescriptorWrite(NnrpSchemaDescriptorHeader descriptor, NnrpMutableBufferView destination)
        {
            if (destination.Length != UIntPtr.Zero && destination.Pointer == IntPtr.Zero)
            {
                return new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument, NnrpErrorFamily.Schema);
            }

            if (destination.Length != UIntPtr.Zero)
            {
                Marshal.WriteInt32(destination.Pointer, unchecked((int)descriptor.SchemaId));
            }

            return descriptor.SchemaId != 0
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus TokenDeltaSchemaDescriptor(out NnrpSchemaDescriptorHeader descriptor)
        {
            descriptor = TokenSchemaDescriptor();
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus TypedPayloadValidateBinding(
            IntPtr schemaDescriptors,
            UIntPtr schemaCount,
            NnrpTypedPayloadDescriptor descriptor)
        {
            return schemaDescriptors != IntPtr.Zero && schemaCount != UIntPtr.Zero && descriptor.SchemaId == 0x1001
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus SessionRecoveryRequestValidate(NnrpBufferView sessionOpenMetadata)
        {
            return sessionOpenMetadata.Pointer != IntPtr.Zero || sessionOpenMetadata.Length == UIntPtr.Zero
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
        }

        private static NnrpFfiStatus SessionRecoveryAckValidate(
            NnrpBufferView sessionOpenMetadata,
            NnrpBufferView sessionOpenAckMetadata,
            out NnrpSessionRecoveryOutcome outcome)
        {
            outcome = new NnrpSessionRecoveryOutcome(1, 2);
            return sessionOpenMetadata.Length == UIntPtr.Zero || sessionOpenAckMetadata.Length == UIntPtr.Zero
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
        }

        private static NnrpFfiStatus MigrationRecoveryValidate(
            NnrpBufferView sessionMigrateMetadata,
            NnrpBufferView sessionMigrateAckMetadata)
        {
            return sessionMigrateMetadata.Pointer != IntPtr.Zero || sessionMigrateMetadata.Length == UIntPtr.Zero
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
        }

        private static NnrpFfiStatus MigrationShouldReplayFrame(
            NnrpBufferView sessionMigrateAckMetadata,
            ulong frameId,
            out byte shouldReplay)
        {
            shouldReplay = frameId > 0 ? (byte)1 : (byte)0;
            return sessionMigrateAckMetadata.Pointer != IntPtr.Zero || sessionMigrateAckMetadata.Length == UIntPtr.Zero
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
        }

        private static NnrpFfiStatus SchemaRegistryCreate(out NnrpHandle registry)
        {
            registry = new NnrpHandle(NnrpHandleKind.SchemaRegistry, 70, 1);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus SchemaRegistryInstall(
            NnrpHandle registry,
            NnrpSchemaDescriptorHeader descriptor,
            out uint action)
        {
            action = (uint)NnrpSchemaRegistryAction.Installed;
            return registry.Kind == NnrpHandleKind.SchemaRegistry && descriptor.SchemaId != 0
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus SchemaRegistryLookup(
            NnrpHandle registry,
            uint schemaId,
            uint schemaVersion,
            out NnrpSchemaDescriptorHeader descriptor)
        {
            descriptor = TokenSchemaDescriptor();
            return registry.Kind == NnrpHandleKind.SchemaRegistry && schemaId == descriptor.SchemaId && schemaVersion == descriptor.SchemaVersion
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus SchemaRegistryInvalidate(
            NnrpHandle registry,
            uint schemaId,
            uint schemaVersion,
            out uint action)
        {
            action = (uint)NnrpSchemaRegistryAction.Invalidated;
            return registry.Kind == NnrpHandleKind.SchemaRegistry && schemaId == 0x1001 && schemaVersion == 3
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument, NnrpErrorFamily.Schema);
        }

        private static NnrpFfiStatus SchemaRegistryValidateBinding(
            NnrpHandle registry,
            NnrpTypedPayloadDescriptor descriptor)
        {
            return registry.Kind == NnrpHandleKind.SchemaRegistry && descriptor.SchemaId == 0x1001 && descriptor.SchemaVersion == 3
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument, NnrpErrorFamily.Schema);
        }

        private static NnrpCacheObjectId MatchingCacheObjectId()
        {
            return new NnrpCacheObjectId(1, 2, 3, 4);
        }

        private static NnrpCacheLeaseRequest MatchingCacheLeaseRequest()
        {
            return new NnrpCacheLeaseRequest(
                new NnrpHandle(NnrpHandleKind.Session, 3, 1),
                MatchingCacheObjectId(),
                9,
                1000,
                500);
        }

        private static NnrpCacheLeaseResult CreateCacheLeaseResult(
            NnrpCacheObjectId objectId,
            NnrpCacheLeaseOutcome outcome = NnrpCacheLeaseOutcome.Valid,
            ulong expiresAtMilliseconds = 2000)
        {
            return new NnrpCacheLeaseResult(
                (uint)outcome,
                new NnrpHandle(NnrpHandleKind.CacheLease, 77, 1),
                objectId,
                9,
                88,
                expiresAtMilliseconds);
        }

        private static NnrpFfiStatus CacheQuery(NnrpCacheLeaseRequest request, out NnrpCacheLeaseResult result)
        {
            result = CreateCacheLeaseResult(request.ObjectId);
            return request.Owner.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle, NnrpErrorFamily.Cache);
        }

        private static NnrpFfiStatus CacheTouch(NnrpCacheLeaseRequest request, out NnrpCacheLeaseResult result)
        {
            result = CreateCacheLeaseResult(request.ObjectId, expiresAtMilliseconds: request.NowMilliseconds + request.TtlMilliseconds + 1000);
            return request.Owner.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle, NnrpErrorFamily.Cache);
        }

        private static NnrpFfiStatus CachePrefetch(
            NnrpHandle owner,
            IntPtr objects,
            UIntPtr objectCount,
            ulong nowMilliseconds,
            uint ttlMilliseconds,
            IntPtr results)
        {
            if (!owner.IsValid)
            {
                return new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle, NnrpErrorFamily.Cache);
            }

            int objectSize = Marshal.SizeOf<NnrpCacheObjectId>();
            int resultSize = Marshal.SizeOf<NnrpCacheLeaseResult>();
            int count = checked((int)objectCount.ToUInt64());
            for (int index = 0; index < count; index++)
            {
                var objectId = Marshal.PtrToStructure<NnrpCacheObjectId>(IntPtr.Add(objects, index * objectSize));
                Marshal.StructureToPtr(
                    CreateCacheLeaseResult(objectId, expiresAtMilliseconds: nowMilliseconds + ttlMilliseconds + (ulong)index),
                    IntPtr.Add(results, index * resultSize),
                    false);
            }

            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus CacheRelease(NnrpHandle lease, out NnrpCacheLeaseResult result)
        {
            result = CreateCacheLeaseResult(MatchingCacheObjectId(), NnrpCacheLeaseOutcome.Released);
            return lease.Kind == NnrpHandleKind.CacheLease ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle, NnrpErrorFamily.Cache);
        }

        private static NnrpFfiStatus BufferAcquireCopy(NnrpBufferView source, out NnrpHandle buffer, out NnrpBufferView view)
        {
            buffer = new NnrpHandle(NnrpHandleKind.Buffer, 90, 1);
            view = new NnrpBufferView(EventPayloadHandle.AddrOfPinnedObject(), new UIntPtr((uint)EventPayload.Length));
            return source.Pointer != IntPtr.Zero || source.Length == UIntPtr.Zero
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
        }

        private static NnrpFfiStatus BufferView(NnrpHandle buffer, out NnrpBufferView view)
        {
            view = new NnrpBufferView(EventPayloadHandle.AddrOfPinnedObject(), new UIntPtr((uint)EventPayload.Length));
            return buffer.Kind == NnrpHandleKind.Buffer
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
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

        private static NnrpPollResult CreatePollResult(
            NnrpHandle connection,
            NnrpHandle session,
            NnrpHandle operation,
            uint frameId)
        {
            return new NnrpPollResult(
                NnrpFfiStatus.Ok,
                1,
                new NnrpEvent(
                    6,
                    connection,
                    session,
                    operation,
                    frameId,
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
