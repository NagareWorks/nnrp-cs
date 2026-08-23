using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nnrp.Core;
using Nnrp.NativeBridge;
using Nnrp.Runtime;
using Xunit;

namespace Nnrp.NativeBridge.Tests
{
    public sealed class NnrpNativeArtifactTests
    {
        [Fact]
        public void RuntimeHandleAllocatorProducesProcessWideNonZeroIdentities()
        {
            var identities = new HashSet<ulong>();
            for (var index = 0; index < 1_024; index++)
            {
                var identity = NnrpRuntimeHandleIdAllocator.Allocate();
                Assert.NotEqual((ulong)0, identity);
                Assert.True(identities.Add(identity));
            }

            var sessionIdentity = NnrpRuntimeHandleIdAllocator.AllocateSession();
            Assert.NotEqual((uint)0, sessionIdentity);
            Assert.DoesNotContain((ulong)sessionIdentity, identities);
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

        [Theory]
        [InlineData("tcp", "libnnrp_ffi_tcp.so")]
        [InlineData("quic", "libnnrp_ffi_quic.so")]
        [InlineData("ipc", "libnnrp_ffi_ipc.so")]
        [InlineData("websocket", "libnnrp_ffi_websocket.so")]
        public void ResolveTransportUsesTransportScopedNuGetRuntimeNativeLayout(
            string transportScope,
            string libraryName)
        {
            string root = CreateTempDirectory();
            try
            {
                string artifactDirectory = Path.Combine(
                    root,
                    "runtimes",
                    "linux-x64",
                    "native",
                    "nnrp",
                    "transport",
                    transportScope);
                Directory.CreateDirectory(artifactDirectory);
                string artifactPath = Path.Combine(artifactDirectory, libraryName);
                File.WriteAllBytes(artifactPath, new byte[] { 1 });

                Assert.Equal(
                    artifactPath,
                    NnrpNativeArtifact.ResolveTransport(
                        transportScope,
                        root,
                        new NnrpNativePlatform("linux", "x86_64")));
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [Theory]
        [InlineData("windows", "tcp", "nnrp_ffi_tcp.dll")]
        [InlineData("windows", "quic", "nnrp_ffi_quic.dll")]
        [InlineData("linux", "tcp", "libnnrp_ffi_tcp.so")]
        [InlineData("linux", "quic", "libnnrp_ffi_quic.so")]
        [InlineData("linux", "ipc", "libnnrp_ffi_ipc.so")]
        [InlineData("linux", "websocket", "libnnrp_ffi_websocket.so")]
        [InlineData("darwin", "tcp", "libnnrp_ffi_tcp.dylib")]
        [InlineData("darwin", "quic", "libnnrp_ffi_quic.dylib")]
        [InlineData("ios", "tcp", "libnnrp_ffi_tcp.a")]
        [InlineData("iossimulator", "quic", "libnnrp_ffi_quic.a")]
        public void TransportLibraryNameIncludesTransportScope(
            string osName,
            string transportScope,
            string expected)
        {
            Assert.Equal(expected, NnrpNativeArtifact.TransportLibraryName(osName, transportScope));
        }

        [Fact]
        public void TransportScopeFromTransportIdMapsKnownSlotsAndRejectsUnknownIds()
        {
            Assert.Equal("tcp", NnrpNativeArtifact.TransportScopeFromTransportId(NnrpNativeArtifact.TransportSlotTcp));
            Assert.Equal("quic", NnrpNativeArtifact.TransportScopeFromTransportId(NnrpNativeArtifact.TransportSlotQuic));
            Assert.Equal("ipc", NnrpNativeArtifact.TransportScopeFromTransportId(NnrpNativeArtifact.TransportSlotIpc));
            Assert.Equal("websocket", NnrpNativeArtifact.TransportScopeFromTransportId(NnrpNativeArtifact.TransportSlotWebSocket));

            var error = Assert.Throws<NnrpNativeArtifactException>(() =>
                NnrpNativeArtifact.TransportScopeFromTransportId(0x80000000));
            Assert.Contains("Unsupported native transport id", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void Preview4CacheLeaseInteropLayoutMatchesAbi4()
        {
            Assert.Equal(24, Marshal.SizeOf<NnrpCacheObjectId>());
            Assert.Equal(0, Marshal.OffsetOf<NnrpCacheObjectId>(nameof(NnrpCacheObjectId.CacheNamespace)).ToInt32());
            Assert.Equal(4, Marshal.OffsetOf<NnrpCacheObjectId>(nameof(NnrpCacheObjectId.ObjectKind)).ToInt32());
            Assert.Equal(8, Marshal.OffsetOf<NnrpCacheObjectId>(nameof(NnrpCacheObjectId.CacheKeyHigh)).ToInt32());
            Assert.Equal(16, Marshal.OffsetOf<NnrpCacheObjectId>(nameof(NnrpCacheObjectId.CacheKeyLow)).ToInt32());

            Assert.Equal(96, Marshal.SizeOf<NnrpCacheLeaseResult>());
            Assert.Equal(0, Marshal.OffsetOf<NnrpCacheLeaseResult>(nameof(NnrpCacheLeaseResult.OutcomeCode)).ToInt32());
            Assert.Equal(8, Marshal.OffsetOf<NnrpCacheLeaseResult>(nameof(NnrpCacheLeaseResult.LeaseHandle)).ToInt32());
            Assert.Equal(32, Marshal.OffsetOf<NnrpCacheLeaseResult>(nameof(NnrpCacheLeaseResult.ObjectId)).ToInt32());
            Assert.Equal(56, Marshal.OffsetOf<NnrpCacheLeaseResult>(nameof(NnrpCacheLeaseResult.ObjectVersion)).ToInt32());
            Assert.Equal(64, Marshal.OffsetOf<NnrpCacheLeaseResult>(nameof(NnrpCacheLeaseResult.LeaseId)).ToInt32());
            Assert.Equal(72, Marshal.OffsetOf<NnrpCacheLeaseResult>(nameof(NnrpCacheLeaseResult.OwnerScope)).ToInt32());
            Assert.Equal(76, Marshal.OffsetOf<NnrpCacheLeaseResult>(nameof(NnrpCacheLeaseResult.TtlMilliseconds)).ToInt32());
            Assert.Equal(80, Marshal.OffsetOf<NnrpCacheLeaseResult>(nameof(NnrpCacheLeaseResult.OwnerId)).ToInt32());
            Assert.Equal(88, Marshal.OffsetOf<NnrpCacheLeaseResult>(nameof(NnrpCacheLeaseResult.GrantedAtMilliseconds)).ToInt32());
        }

        [Fact]
        public void Preview4RuntimeObjectInteropLayoutsMatchAbi4()
        {
            Assert.Equal(40, Marshal.SizeOf<NnrpRuntimeObjectDescriptor>());
            Assert.Equal(0, Marshal.OffsetOf<NnrpRuntimeObjectDescriptor>(nameof(NnrpRuntimeObjectDescriptor.ObjectId)).ToInt32());
            Assert.Equal(16, Marshal.OffsetOf<NnrpRuntimeObjectDescriptor>(nameof(NnrpRuntimeObjectDescriptor.ByteSize)).ToInt32());
            Assert.Equal(36, Marshal.OffsetOf<NnrpRuntimeObjectDescriptor>(nameof(NnrpRuntimeObjectDescriptor.MetadataBytes)).ToInt32());

            Assert.Equal(56, Marshal.SizeOf<NnrpCacheReferenceDescriptor>());
            Assert.Equal(0, Marshal.OffsetOf<NnrpCacheReferenceDescriptor>(nameof(NnrpCacheReferenceDescriptor.CacheNamespace)).ToInt32());
            Assert.Equal(8, Marshal.OffsetOf<NnrpCacheReferenceDescriptor>(nameof(NnrpCacheReferenceDescriptor.CacheKeyHi)).ToInt32());
            Assert.Equal(48, Marshal.OffsetOf<NnrpCacheReferenceDescriptor>(nameof(NnrpCacheReferenceDescriptor.Flags)).ToInt32());

            Assert.Equal(7U, (uint)NnrpErrorFamily.Control);
            Assert.Equal(8U, (uint)NnrpErrorFamily.RuntimeObject);
            Assert.Equal(8U, (uint)NnrpHandleKind.ObjectDescriptor);
            Assert.Equal(9U, (uint)NnrpHandleKind.CacheReferenceDescriptor);
        }

        [Fact]
        public void Preview4RoleEventInteropLayoutsMatchAbi4()
        {
            Assert.True(IntPtr.Size == 4 || IntPtr.Size == 8);
            var is64Bit = IntPtr.Size == 8;

            Assert.Equal(16, Marshal.SizeOf<NnrpFfiStatus>());
            Assert.Equal(new IntPtr(0), Marshal.OffsetOf<NnrpFfiStatus>(nameof(NnrpFfiStatus.StatusCode)));
            Assert.Equal(new IntPtr(4), Marshal.OffsetOf<NnrpFfiStatus>(nameof(NnrpFfiStatus.ErrorFamily)));
            Assert.Equal(new IntPtr(8), Marshal.OffsetOf<NnrpFfiStatus>(nameof(NnrpFfiStatus.ProtocolErrorCode)));
            Assert.Equal(new IntPtr(12), Marshal.OffsetOf<NnrpFfiStatus>(nameof(NnrpFfiStatus.DetailCode)));

            Assert.Equal(24, Marshal.SizeOf<NnrpHandle>());
            Assert.Equal(new IntPtr(0), Marshal.OffsetOf<NnrpHandle>(nameof(NnrpHandle.Kind)));
            Assert.Equal(new IntPtr(8), Marshal.OffsetOf<NnrpHandle>(nameof(NnrpHandle.Id)));
            Assert.Equal(new IntPtr(16), Marshal.OffsetOf<NnrpHandle>(nameof(NnrpHandle.Generation)));
            Assert.Equal(new IntPtr(20), Marshal.OffsetOf<NnrpHandle>(nameof(NnrpHandle.Flags)));

            Assert.Equal(is64Bit ? 16 : 8, Marshal.SizeOf<NnrpBufferView>());
            Assert.Equal(new IntPtr(0), Marshal.OffsetOf<NnrpBufferView>(nameof(NnrpBufferView.Pointer)));
            Assert.Equal(
                new IntPtr(is64Bit ? 8 : 4),
                Marshal.OffsetOf<NnrpBufferView>(nameof(NnrpBufferView.Length)));

            Assert.Equal(48, Marshal.SizeOf<NnrpFfiDiagnostic>());
            Assert.Equal(new IntPtr(0), Marshal.OffsetOf<NnrpFfiDiagnostic>(nameof(NnrpFfiDiagnostic.Status)));
            Assert.Equal(new IntPtr(16), Marshal.OffsetOf<NnrpFfiDiagnostic>(nameof(NnrpFfiDiagnostic.RelatedConnectionId)));
            Assert.Equal(new IntPtr(24), Marshal.OffsetOf<NnrpFfiDiagnostic>(nameof(NnrpFfiDiagnostic.RelatedSessionId)));
            Assert.Equal(new IntPtr(32), Marshal.OffsetOf<NnrpFfiDiagnostic>(nameof(NnrpFfiDiagnostic.RelatedOperationId)));
            Assert.Equal(new IntPtr(40), Marshal.OffsetOf<NnrpFfiDiagnostic>(nameof(NnrpFfiDiagnostic.RelatedFrameId)));

            Assert.Equal(32, Marshal.SizeOf<NnrpFfiRuntimeFrameHeader>());
            Assert.Equal(new IntPtr(0), Marshal.OffsetOf<NnrpFfiRuntimeFrameHeader>(nameof(NnrpFfiRuntimeFrameHeader.Present)));
            Assert.Equal(new IntPtr(4), Marshal.OffsetOf<NnrpFfiRuntimeFrameHeader>(nameof(NnrpFfiRuntimeFrameHeader.Flags)));
            Assert.Equal(new IntPtr(8), Marshal.OffsetOf<NnrpFfiRuntimeFrameHeader>(nameof(NnrpFfiRuntimeFrameHeader.SessionId)));
            Assert.Equal(new IntPtr(12), Marshal.OffsetOf<NnrpFfiRuntimeFrameHeader>(nameof(NnrpFfiRuntimeFrameHeader.FrameId)));
            Assert.Equal(new IntPtr(16), Marshal.OffsetOf<NnrpFfiRuntimeFrameHeader>(nameof(NnrpFfiRuntimeFrameHeader.ViewId)));
            Assert.Equal(new IntPtr(18), Marshal.OffsetOf<NnrpFfiRuntimeFrameHeader>(nameof(NnrpFfiRuntimeFrameHeader.RouteId)));
            Assert.Equal(new IntPtr(24), Marshal.OffsetOf<NnrpFfiRuntimeFrameHeader>(nameof(NnrpFfiRuntimeFrameHeader.TraceId)));

            Assert.Equal(is64Bit ? 200 : 192, Marshal.SizeOf<NnrpEvent>());
            Assert.Equal(new IntPtr(0), Marshal.OffsetOf<NnrpEvent>(nameof(NnrpEvent.Kind)));
            Assert.Equal(new IntPtr(8), Marshal.OffsetOf<NnrpEvent>(nameof(NnrpEvent.Header)));
            Assert.Equal(new IntPtr(40), Marshal.OffsetOf<NnrpEvent>(nameof(NnrpEvent.Connection)));
            Assert.Equal(new IntPtr(64), Marshal.OffsetOf<NnrpEvent>(nameof(NnrpEvent.Session)));
            Assert.Equal(new IntPtr(88), Marshal.OffsetOf<NnrpEvent>(nameof(NnrpEvent.Operation)));
            Assert.Equal(new IntPtr(112), Marshal.OffsetOf<NnrpEvent>(nameof(NnrpEvent.PayloadOwner)));
            Assert.Equal(new IntPtr(136), Marshal.OffsetOf<NnrpEvent>(nameof(NnrpEvent.Payload)));
            Assert.Equal(
                new IntPtr(is64Bit ? 152 : 144),
                Marshal.OffsetOf<NnrpEvent>(nameof(NnrpEvent.Diagnostic)));

            Assert.Equal(is64Bit ? 224 : 216, Marshal.SizeOf<NnrpPollResult>());
            Assert.Equal(new IntPtr(0), Marshal.OffsetOf<NnrpPollResult>(nameof(NnrpPollResult.Status)));
            Assert.Equal(new IntPtr(16), Marshal.OffsetOf<NnrpPollResult>(nameof(NnrpPollResult.HasEvent)));
            Assert.Equal(new IntPtr(24), Marshal.OffsetOf<NnrpPollResult>(nameof(NnrpPollResult.Event)));

            Assert.Equal(40, Marshal.SizeOf<NnrpRoleEventPollRequest>());
            Assert.Equal(new IntPtr(0), Marshal.OffsetOf<NnrpRoleEventPollRequest>(nameof(NnrpRoleEventPollRequest.Scope)));
            Assert.Equal(new IntPtr(24), Marshal.OffsetOf<NnrpRoleEventPollRequest>(nameof(NnrpRoleEventPollRequest.MaxEvents)));
            Assert.Equal(new IntPtr(28), Marshal.OffsetOf<NnrpRoleEventPollRequest>(nameof(NnrpRoleEventPollRequest.TimeoutMilliseconds)));
            Assert.Equal(new IntPtr(32), Marshal.OffsetOf<NnrpRoleEventPollRequest>(nameof(NnrpRoleEventPollRequest.Flags)));
            Assert.Equal(new IntPtr(36), Marshal.OffsetOf<NnrpRoleEventPollRequest>(nameof(NnrpRoleEventPollRequest.Reserved0)));
        }

        [Fact]
        public void NativeRuntimeShutdownHookIsIdempotentWithoutLoadedLibraries()
        {
            NnrpNativeRuntimeEntrypoints.ShutdownPinnedTransportRuntimesForTesting();
            NnrpNativeRuntimeEntrypoints.ShutdownPinnedTransportRuntimesForTesting();
        }

        [Fact]
        public void ResolveTransportRejectsUnknownScopeAndMissingTransportArtifact()
        {
            var scopeError = Assert.Throws<NnrpNativeArtifactException>(() =>
                NnrpNativeArtifact.TransportLibraryName("linux", "udp"));
            Assert.Contains("Unsupported native transport scope", scopeError.Message, StringComparison.Ordinal);

            string root = CreateTempDirectory();
            try
            {
                var missingError = Assert.Throws<NnrpNativeArtifactException>(() =>
                    NnrpNativeArtifact.ResolveTransport(
                        "tcp",
                        root,
                        new NnrpNativePlatform("linux", "x86_64")));

                Assert.Contains("Native transport artifact was not found", missingError.Message, StringComparison.Ordinal);
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
            Assert.Equal(NnrpNativeArtifact.ExpectedAbiMajor, result.AbiMajor);
            Assert.Equal(NnrpNativeArtifact.ExpectedAbiMinor, result.AbiMinor);
            Assert.Equal(NnrpNativeArtifact.ExpectedAbiPatch, result.AbiPatch);
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

        [Theory]
        [InlineData(3, 0, 0)]
        [InlineData(4, 0, 0)]
        [InlineData(4, 1, 0)]
        [InlineData(4, 1, 2)]
        public void ProbeRejectsAbiMismatch(ushort abiMajor, ushort abiMinor, ushort abiPatch)
        {
            var error = Assert.Throws<NnrpNativeArtifactException>(() =>
                NnrpNativeArtifact.Probe(
                    "fake-path",
                    runtimeCapabilities: () => MatchingCapabilities(
                        abiMajor: abiMajor,
                        abiMinor: abiMinor,
                        abiPatch: abiPatch)));

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
        public void ProbeCanRequireSelectedTransportSlotForSplitProviderArtifacts()
        {
            var result = NnrpNativeArtifact.Probe(
                "fake-path",
                runtimeCapabilities: () => MatchingCapabilities(transportSlots: NnrpNativeArtifact.TransportSlotQuic),
                requiredTransportSlots: NnrpNativeArtifact.TransportSlotQuic);

            Assert.Equal(NnrpNativeArtifact.TransportSlotQuic, result.TransportSlots);
        }

        [Fact]
        public void ProbeRejectsWhenSelectedTransportSlotIsMissing()
        {
            var error = Assert.Throws<NnrpNativeArtifactException>(() =>
                NnrpNativeArtifact.Probe(
                    "fake-path",
                    runtimeCapabilities: () => MatchingCapabilities(transportSlots: NnrpNativeArtifact.TransportSlotTcp),
                    requiredTransportSlots: NnrpNativeArtifact.TransportSlotQuic));

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
            Assert.Equal(NnrpNativeArtifact.ExpectedAbiMajor, entrypoints.RuntimeCapabilities().AbiMajor);

            NnrpHandle handle;
            Assert.True(entrypoints.ConnectionBootstrap(new NnrpConnectionBootstrap(1, 1, 2), out handle).Succeeded);
            Assert.Equal(NnrpHandleKind.Connection, handle.Kind);

            Assert.True(entrypoints.ClientConnect(
                new NnrpClientConnectRequest(
                    2,
                    1,
                    new NnrpHandle(NnrpHandleKind.TransportConnection, 20, 1)),
                out handle).Succeeded);
            Assert.True(entrypoints.SessionOpen(MatchingSessionOpenRequest(), out handle).Succeeded);
            Assert.True(entrypoints.ClientOpenSession(MatchingSessionOpenRequest(), out handle).Succeeded);
            NnrpSessionRecoveryOutcome recoveryOutcome;
            Assert.True(entrypoints.ClientResumeSession(MatchingSessionResumeRequest(), out handle, out recoveryOutcome).Succeeded);
            Assert.Equal(NnrpHandleKind.Session, handle.Kind);
            Assert.Equal((uint)2, recoveryOutcome.ResumeWindowMilliseconds);
            Assert.True(entrypoints.Submit(MatchingSubmitRequest(), out handle).Succeeded);
            Assert.True(entrypoints.ClientSubmit(MatchingSubmitRequest(), out handle).Succeeded);
            NnrpCompactResult compactResult;
            UIntPtr completed;
            Assert.True(entrypoints.ClientSubmitResultCompactBatch(
                MatchingBatchSubmitResultRequest(),
                out compactResult,
                out completed).Succeeded);
            Assert.Equal(new UIntPtr(3), completed);
            Assert.Equal((ulong)5, compactResult.OperationId);
            Assert.Equal((uint)7, compactResult.FrameId);
            Assert.True(entrypoints.SessionClose(new NnrpHandle(NnrpHandleKind.Session, 3, 1)).Succeeded);
            Assert.True(entrypoints.ClientClose(new NnrpHandle(NnrpHandleKind.Session, 3, 1)).Succeeded);
            Assert.True(entrypoints.ConnectionClose(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)).Succeeded);
            Assert.True(entrypoints.ClientCloseConnection(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)).Succeeded);
            Assert.True(entrypoints.ClientCancel(new NnrpClientCancelRequest(new NnrpHandle(NnrpHandleKind.Session, 3, 1), 7)).Succeeded);

            NnrpPollResult pollResult;
            Assert.True(entrypoints.ClientAwaitEvent(new NnrpHandle(NnrpHandleKind.Connection, 1, 1), out pollResult).Succeeded);
            Assert.Equal((byte)0, pollResult.HasEvent);

            Assert.True(entrypoints.ServerBind(
                new NnrpServerBindRequest(
                    4,
                    1,
                    new NnrpHandle(NnrpHandleKind.TransportListener, 21, 1),
                    NnrpU16Slice.Empty,
                    NnrpU32Slice.Empty,
                    0,
                    0,
                    24,
                    4,
                    2,
                    30_000,
                    120_000,
                    NnrpHandle.Invalid,
                    NnrpServerPolicySink.AllowAll),
                out handle).Succeeded);
            Assert.True(entrypoints.ServerAcceptBegin(MatchingServerAcceptBeginRequest(), out handle).Succeeded);
            Assert.Equal(NnrpHandleKind.ServerAccept, handle.Kind);
            Assert.True(entrypoints.ServerAcceptWait(new NnrpServerAcceptWaitRequest(handle, 1)).Succeeded);
            NnrpServerAcceptResult accepted;
            Assert.True(entrypoints.ServerAcceptClaim(
                new NnrpServerAcceptClaimRequest(handle, 3, 1),
                out accepted).Succeeded);
            Assert.Equal(NnrpHandleKind.Session, accepted.Session.Kind);
            Assert.Equal((uint)TransportId.Tcp, accepted.ActiveTransportId);
            Assert.True(entrypoints.ServerAcceptRelease(
                new NnrpHandle(NnrpHandleKind.ServerAccept, 6, 1)).Succeeded);
            Assert.True(entrypoints.ServerReceiveSubmit(MatchingServerReceiveSubmitRequest(), out handle).Succeeded);
            Assert.True(entrypoints.ServerSendResult(new NnrpServerSendResultRequest(new NnrpHandle(NnrpHandleKind.Operation, 5, 1), NnrpBufferView.Empty)).Succeeded);
            Assert.True(entrypoints.ServerSendFlowUpdate(new NnrpServerFlowUpdateRequest(new NnrpHandle(NnrpHandleKind.Session, 3, 1), 7)).Succeeded);
            Assert.True(entrypoints.ServerClose(new NnrpHandle(NnrpHandleKind.Session, 3, 1)).Succeeded);
            Assert.True(entrypoints.Control(new NnrpControlRequest(new NnrpHandle(NnrpHandleKind.Connection, 1, 1), 9, NnrpBufferView.Empty)).Succeeded);
            Assert.True(entrypoints.PollEmpty(out pollResult).Succeeded);

            var eventValue = new NnrpEvent(
                0,
                0,
                NnrpHandle.Invalid,
                NnrpHandle.Invalid,
                NnrpHandle.Invalid,
                0,
                NnrpHandle.Invalid,
                NnrpBufferView.Empty,
                new NnrpFfiDiagnostic(NnrpFfiStatus.Ok));
            Assert.True(entrypoints.DispatchEvent(new NnrpCallbackSink(IntPtr.Zero, IntPtr.Zero), ref eventValue).Succeeded);

            Assert.True(entrypoints.TransportClientSecurityConfigCreate(
                new NnrpTransportClientSecurityConfigRequest(
                    TransportId.Tcp,
                    NnrpBufferView.Empty,
                    NnrpBufferView.Empty),
                out handle).Succeeded);
            Assert.Equal(NnrpHandleKind.TransportSecurityConfig, handle.Kind);
            Assert.True(entrypoints.TransportServerSecurityConfigCreate(
                new NnrpTransportServerSecurityConfigRequest(
                    TransportId.Tcp,
                    NnrpBufferView.Empty,
                    NnrpBufferView.Empty),
                out handle).Succeeded);
            Assert.True(entrypoints.TransportConnect(
                MatchingTransportOpenRequest(),
                out handle).Succeeded);
            Assert.Equal(NnrpHandleKind.TransportConnection, handle.Kind);
            Assert.True(entrypoints.TransportListen(
                MatchingTransportOpenRequest(),
                out handle).Succeeded);
            Assert.Equal(NnrpHandleKind.TransportListener, handle.Kind);
            Assert.True(entrypoints.TransportAccept(
                new NnrpTransportAcceptRequest(handle, 10),
                out var acceptedTransport).Succeeded);
            Assert.Equal(NnrpHandleKind.TransportConnection, acceptedTransport.Kind);
            Assert.True(entrypoints.TransportProbe(
                new NnrpTransportProbeRequest(MatchingTransportOpenRequest(), 3, 64),
                out var transportProbe).Succeeded);
            Assert.Equal((uint)3, transportProbe.SampleCount);
            Assert.Equal((uint)3, transportProbe.SuccessCount);
            Assert.True(entrypoints.TransportClose(acceptedTransport).Succeeded);

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
            Assert.Equal((ulong)1000, leaseResult.GrantedAtMilliseconds);
            Assert.Equal((uint)1500, leaseResult.TtlMilliseconds);
            Assert.Equal((uint)1, leaseResult.OwnerScope);
            Assert.Equal((ulong)3, leaseResult.OwnerId);

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
        public void TransportAbiUsesOwnedHandleAdoptionLayout()
        {
            var is64Bit = IntPtr.Size == 8;

            Assert.Equal(40, Marshal.SizeOf<NnrpClientConnectRequest>());
            Assert.Equal(new IntPtr(16), Marshal.OffsetOf<NnrpClientConnectRequest>(nameof(NnrpClientConnectRequest.TransportConnection)));
            var alignsUInt64ToEightBytes = Marshal.OffsetOf<NnrpHandle>(nameof(NnrpHandle.Id)) == new IntPtr(8);
            Assert.Equal(
                is64Bit ? 144 : alignsUInt64ToEightBytes ? 120 : 108,
                Marshal.SizeOf<NnrpServerBindRequest>());
            Assert.Equal(new IntPtr(16), Marshal.OffsetOf<NnrpServerBindRequest>(nameof(NnrpServerBindRequest.TransportListener)));
            Assert.Equal(is64Bit ? 64 : 56, Marshal.SizeOf<NnrpTransportOpenRequest>());
            Assert.Equal(
                new IntPtr(is64Bit ? 24 : 16),
                Marshal.OffsetOf<NnrpTransportOpenRequest>(nameof(NnrpTransportOpenRequest.Config)));
            Assert.Equal(
                new IntPtr(is64Bit ? 48 : 40),
                Marshal.OffsetOf<NnrpTransportOpenRequest>(nameof(NnrpTransportOpenRequest.MaxPacketBytes)));
            Assert.Equal(
                new IntPtr(is64Bit ? 60 : 52),
                Marshal.OffsetOf<NnrpTransportOpenRequest>(nameof(NnrpTransportOpenRequest.Reserved0)));
            Assert.Equal(
                is64Bit ? 88 : alignsUInt64ToEightBytes ? 80 : 72,
                Marshal.SizeOf<NnrpSessionOpenRequest>());
            Assert.Equal(new IntPtr(is64Bit ? 44 : alignsUInt64ToEightBytes ? 44 : 32), Marshal.OffsetOf<NnrpSessionOpenRequest>(nameof(NnrpSessionOpenRequest.ProfileId)));
            Assert.Equal(new IntPtr(is64Bit ? 48 : alignsUInt64ToEightBytes ? 48 : 36), Marshal.OffsetOf<NnrpSessionOpenRequest>(nameof(NnrpSessionOpenRequest.SchemaId)));
            Assert.Equal(
                is64Bit ? 104 : alignsUInt64ToEightBytes ? 88 : 80,
                Marshal.SizeOf<NnrpSessionResumeRequest>());
            Assert.Equal(
                new IntPtr(is64Bit ? 88 : alignsUInt64ToEightBytes ? 80 : 72),
                Marshal.OffsetOf<NnrpSessionResumeRequest>(nameof(NnrpSessionResumeRequest.RecoveryTicket)));
            Assert.Equal(is64Bit ? 72 : 64, Marshal.SizeOf<NnrpFfiSubmitRequest>());
            Assert.Equal(new IntPtr(24), Marshal.OffsetOf<NnrpFfiSubmitRequest>(nameof(NnrpFfiSubmitRequest.OperationId)));
            Assert.Equal(new IntPtr(32), Marshal.OffsetOf<NnrpFfiSubmitRequest>(nameof(NnrpFfiSubmitRequest.FrameId)));
            Assert.Equal(new IntPtr(36), Marshal.OffsetOf<NnrpFfiSubmitRequest>(nameof(NnrpFfiSubmitRequest.HeaderFlags)));
            Assert.Equal(new IntPtr(40), Marshal.OffsetOf<NnrpFfiSubmitRequest>(nameof(NnrpFfiSubmitRequest.ViewId)));
            Assert.Equal(new IntPtr(42), Marshal.OffsetOf<NnrpFfiSubmitRequest>(nameof(NnrpFfiSubmitRequest.RouteId)));
            Assert.Equal(new IntPtr(48), Marshal.OffsetOf<NnrpFfiSubmitRequest>(nameof(NnrpFfiSubmitRequest.TraceId)));
            Assert.Equal(new IntPtr(56), Marshal.OffsetOf<NnrpFfiSubmitRequest>(nameof(NnrpFfiSubmitRequest.Payload)));
            Assert.Equal(40, Marshal.SizeOf<NnrpServerAcceptBeginRequest>());
            Assert.Equal(40, Marshal.SizeOf<NnrpServerAcceptClaimRequest>());
            Assert.Equal(32, Marshal.SizeOf<NnrpServerAcceptWaitRequest>());
            Assert.Equal(32, Marshal.SizeOf<NnrpServerAcceptResult>());
            Assert.Throws<ArgumentException>(() => new NnrpClientConnectRequest(
                1,
                1,
                new NnrpHandle(NnrpHandleKind.TransportListener, 1, 1)));
            Assert.Throws<ArgumentException>(() => new NnrpServerBindRequest(
                1,
                1,
                new NnrpHandle(NnrpHandleKind.TransportConnection, 1, 1),
                NnrpU16Slice.Empty,
                NnrpU32Slice.Empty,
                0,
                0,
                24,
                4,
                2,
                30_000,
                120_000,
                NnrpHandle.Invalid,
                NnrpServerPolicySink.AllowAll));
        }

        [Fact]
        public void OpaqueTransportOwnershipClosesOrTransfersExactlyOnce()
        {
            var transportCloseCount = 0;
            var entrypoints = CreateEntrypoints(
                transportClose: handle =>
                {
                    transportCloseCount++;
                    return HandleStatus(handle);
                });
            var connection = new NnrpTransportConnection(
                new NnrpNativeEntrypointLease(entrypoints),
                TransportId.Tcp,
                new NnrpHandle(NnrpHandleKind.TransportConnection, 30, 1));

            using (var runtime = connection.AdoptClient(40, 1))
            {
                connection.Dispose();
                Assert.Equal(NnrpHandleKind.Connection, runtime.Handle.Handle.Kind);
            }

            Assert.Equal(0, transportCloseCount);

            var listenerEntrypoints = CreateEntrypoints(
                transportClose: handle =>
                {
                    transportCloseCount++;
                    return HandleStatus(handle);
                });
            var listener = new NnrpTransportListener(
                new NnrpNativeEntrypointLease(listenerEntrypoints),
                TransportId.Tcp,
                NnrpProviderEndpoint.Parse("tcp://127.0.0.1:7443"),
                new NnrpHandle(NnrpHandleKind.TransportListener, 31, 1));
            var accepted = listener.Accept(10);

            listener.Dispose();
            listener.Dispose();
            accepted.Dispose();
            accepted.Dispose();

            Assert.Equal(2, transportCloseCount);
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
            Assert.Equal((ulong)1500, query.GrantedAtMilliseconds);
            Assert.Equal((uint)500, query.TtlMilliseconds);
            Assert.Equal((uint)NnrpCacheLeaseOutcome.Valid, touch.OutcomeCode);
            Assert.Equal((ulong)1000, touch.GrantedAtMilliseconds);
            Assert.Equal((uint)1500, touch.TtlMilliseconds);
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
        public void NativeRuntimeObjectsOwnDescriptorAndMetadataLifetimes()
        {
            using var store = new NativeObjectStore();
            var objects = new NnrpNativeRuntimeObjects(CreateEntrypoints(objectStore: store));
            var objectMetadata = new byte[] { 1, 2, 3 };
            var objectDescriptor = new ObjectDescriptorMetadata(
                11,
                RuntimeObjectKind.Tensor,
                RuntimeRole.Runtime,
                RuntimeRole.Client,
                12,
                4096,
                17,
                MemoryLocationHint.DeviceMemory,
                OwnershipHint.Borrowed,
                500,
                (uint)objectMetadata.Length);

            using (var descriptor = objects.CreateObjectDescriptor(objectDescriptor, objectMetadata))
            {
                Assert.Equal(NnrpHandleKind.ObjectDescriptor, descriptor.Handle.NativeHandle.Kind);
                Assert.Equal(objectDescriptor, descriptor.ReadDescriptor());
                var snapshot = descriptor.Snapshot();
                Assert.Equal(objectDescriptor, snapshot.Descriptor);
                Assert.Equal(objectMetadata, snapshot.Metadata.ToArray());

                using var metadata = descriptor.AcquireMetadataSnapshot();
                Assert.Equal(NnrpHandleKind.Buffer, metadata.Handle.NativeHandle.Kind);
                Assert.Equal(objectMetadata, metadata.CopyToArray());
                metadata.RefreshView();
                Assert.Equal(objectMetadata, metadata.CopyToArray());
            }

            var cacheMetadata = new byte[] { 4, 5 };
            var cacheDescriptor = new CacheReferenceMetadata(
                21,
                22,
                23,
                24,
                CacheReuseScope.Session,
                25,
                26,
                1000,
                (uint)cacheMetadata.Length,
                1);
            var cache = objects.CreateCacheReference(cacheDescriptor, cacheMetadata);
            Assert.Equal(NnrpHandleKind.CacheReferenceDescriptor, cache.Handle.NativeHandle.Kind);
            Assert.Equal(cacheDescriptor, cache.ReadDescriptor());
            Assert.Equal(cacheMetadata, cache.Snapshot().Metadata.ToArray());
            cache.Dispose();
            Assert.True(cache.Handle.IsClosed);
            Assert.Throws<ObjectDisposedException>(() => cache.ReadDescriptor());

            using var copied = objects.AcquireMetadataCopy(objectMetadata);
            Assert.Equal(objectMetadata, copied.CopyToArray());

            var deltaMetadata = new ObjectDeltaMetadata(11, 2, 8, 4, 4, 3, 2);
            using var patch = objects.AcquireObjectPatchMetadataCopy(deltaMetadata, new byte[] { 6, 7 }, new byte[] { 8, 9, 10, 11 });
            using var delta = objects.AcquireObjectDeltaMetadataCopy(deltaMetadata, new byte[] { 12, 13 }, new byte[] { 14, 15, 16, 17 });
            var decodedPatch = NnrpRuntimeObject.Decode(MessageType.ObjectPatch, patch.CopyToArray());
            var decodedDelta = NnrpRuntimeObject.Decode(MessageType.ObjectDelta, delta.CopyToArray());
            Assert.Equal(deltaMetadata, decodedPatch.Metadata);
            Assert.Equal(new byte[] { 6, 7, 8, 9, 10, 11 }, decodedPatch.Tail.ToArray());
            Assert.Equal(deltaMetadata, decodedDelta.Metadata);
            Assert.Equal(new byte[] { 12, 13, 14, 15, 16, 17 }, decodedDelta.Tail.ToArray());
            Assert.Equal(0, store.ObjectDescriptorCount);
            Assert.Equal(0, store.CacheReferenceDescriptorCount);
        }

        [Fact]
        public void NativeRuntimeObjectsRejectInvalidConstructionAndMissingEntrypoints()
        {
            Assert.Throws<ArgumentNullException>(() => new NnrpNativeRuntimeObjects(null!));

            var missing = new NnrpNativeRuntimeObjects(CreateEntrypoints());
            Assert.Throws<NnrpNativeInternalException>(() => missing.AcquireMetadataCopy(Array.Empty<byte>()));
            Assert.Throws<NnrpNativeInternalException>(() => missing.CreateObjectDescriptor(default(ObjectDescriptorMetadata), Array.Empty<byte>()));
            Assert.Throws<NnrpNativeInternalException>(() => missing.CreateCacheReference(default(CacheReferenceMetadata), Array.Empty<byte>()));

            using var store = new NativeObjectStore();
            var objects = new NnrpNativeRuntimeObjects(CreateEntrypoints(objectStore: store));
            Assert.Throws<ArgumentNullException>(() => objects.AcquireMetadataCopy(null!));
            Assert.Throws<ArgumentNullException>(() => objects.CreateObjectDescriptor(default(ObjectDescriptorMetadata), null!));
            Assert.Throws<ArgumentNullException>(() => objects.CreateCacheReference(default(CacheReferenceMetadata), null!));
            var deltaMetadata = new ObjectDeltaMetadata(1, 2, 3, 4, 1, 0, 1);
            Assert.Throws<ArgumentNullException>(() => objects.AcquireObjectPatchMetadataCopy(deltaMetadata, null!, new byte[] { 1 }));
            Assert.Throws<ArgumentNullException>(() => objects.AcquireObjectDeltaMetadataCopy(deltaMetadata, new byte[] { 1 }, null!));
            Assert.Throws<ArgumentException>(() => objects.AcquireObjectPatchMetadataCopy(deltaMetadata, Array.Empty<byte>(), new byte[] { 1 }));
            Assert.Throws<ArgumentException>(() => objects.AcquireObjectDeltaMetadataCopy(deltaMetadata, new byte[] { 1 }, Array.Empty<byte>()));
            Assert.Throws<ArgumentException>(() => objects.CreateObjectDescriptor(
                new ObjectDescriptorMetadata(1, RuntimeObjectKind.Tensor, RuntimeRole.Runtime, RuntimeRole.Client, 1, 1, 1, MemoryLocationHint.HostMemory, OwnershipHint.Borrowed, 1, 2),
                new byte[] { 1 }));
            Assert.Throws<ArgumentException>(() => objects.CreateCacheReference(
                new CacheReferenceMetadata(1, 2, 3, 4, CacheReuseScope.Session, 5, 6, 7, 2, 0),
                new byte[] { 1 }));
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
                    ServerAcceptBegin,
                    ServerAcceptWait,
                    ServerAcceptClaim,
                    ServerAcceptRelease,
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
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

            NnrpRuntimeObjectDescriptor objectDescriptor;
            NnrpCacheReferenceDescriptor cacheDescriptor;
            Assert.Equal(NnrpErrorFamily.RuntimeObject, entrypoints.ObjectMetadataBufferAcquireCopy(NnrpBufferView.Empty, out registry, out bufferView).ErrorFamily);
            Assert.Equal(NnrpFfiStatusCode.InternalError, entrypoints.ObjectMetadataBufferView(NnrpHandle.Invalid, out bufferView).StatusCode);
            Assert.Equal(NnrpErrorFamily.RuntimeObject, entrypoints.ObjectMetadataBufferRelease(NnrpHandle.Invalid).ErrorFamily);
            Assert.Equal(NnrpErrorFamily.RuntimeObject, entrypoints.ObjectDescriptorCreate(default(NnrpRuntimeObjectDescriptor), NnrpBufferView.Empty, out registry).ErrorFamily);
            Assert.Equal(NnrpErrorFamily.RuntimeObject, entrypoints.ObjectDescriptorView(NnrpHandle.Invalid, out objectDescriptor, out bufferView).ErrorFamily);
            Assert.Equal(default(NnrpRuntimeObjectDescriptor), objectDescriptor);
            Assert.Equal(NnrpErrorFamily.RuntimeObject, entrypoints.ObjectDescriptorMetadataSnapshot(NnrpHandle.Invalid, out registry, out bufferView).ErrorFamily);
            Assert.Equal(NnrpErrorFamily.RuntimeObject, entrypoints.ObjectDescriptorRelease(NnrpHandle.Invalid).ErrorFamily);
            Assert.Equal(NnrpErrorFamily.RuntimeObject, entrypoints.CacheReferenceDescriptorCreate(default(NnrpCacheReferenceDescriptor), NnrpBufferView.Empty, out registry).ErrorFamily);
            Assert.Equal(NnrpErrorFamily.RuntimeObject, entrypoints.CacheReferenceDescriptorView(NnrpHandle.Invalid, out cacheDescriptor, out bufferView).ErrorFamily);
            Assert.Equal(default(NnrpCacheReferenceDescriptor), cacheDescriptor);
            Assert.Equal(NnrpErrorFamily.RuntimeObject, entrypoints.CacheReferenceDescriptorMetadataSnapshot(NnrpHandle.Invalid, out registry, out bufferView).ErrorFamily);
            Assert.Equal(NnrpErrorFamily.RuntimeObject, entrypoints.CacheReferenceDescriptorRelease(NnrpHandle.Invalid).ErrorFamily);

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
            var operation = session.Submit(99, SubmitHeader(7), nativePayload);
            var operationScope = session.SubmitOperation(100, SubmitHeader(8), nativePayload, parentOperationId: 99, operationGroupId: 1234);
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
            Assert.Throws<ArgumentNullException>(() => session.SubmitOperation(101, SubmitHeader(9), (NnrpNativeBuffer)null!));
        }

        [Fact]
        public void NativeRuntimeClientBorrowsArrayBackedMemoryPayloadSlices()
        {
            var submittedPayload = Array.Empty<byte>();
            var submittedRequest = default(NnrpFfiSubmitRequest);
            var controlledPayload = Array.Empty<byte>();
            var pendingEvents = new Queue<Func<NnrpHandle, NnrpPollResult>>();

            NnrpFfiStatus CaptureSubmit(NnrpFfiSubmitRequest request, out NnrpHandle operation)
            {
                submittedRequest = request;
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                schemaRegistryCreate: SchemaRegistryCreate,
                schemaRegistryInstall: SchemaRegistryInstall,
                schemaRegistryRelease: HandleStatus);
            using var carrier = CreateTransportCarrier(entrypoints, TransportId.Tcp, 11, 2);
            using var host = NnrpNativeRuntimeSessionHost.Open(
                carrier,
                new NnrpNativeRuntimeSessionHostOptions(11, 2, 41, 3, 4, 5, 6));
            var source = new byte[] { 0, 1, 2, 3, 4 };
            var payload = source.AsMemory(1, 3);

            var operation = host.SubmitOperation(99, SubmitHeader(7), payload);
            pendingEvents.Enqueue(connection => new NnrpPollResult(
                NnrpFfiStatus.Ok,
                1,
                new NnrpEvent(
                    6,
                    (uint)MessageType.ResultPush,
                    connection,
                    new NnrpHandle(NnrpHandleKind.Session, 41, 3),
                    new NnrpHandle(NnrpHandleKind.Operation, 100, 1),
                    8,
                    NnrpHandle.Invalid,
                    NnrpBufferView.Empty,
                    new NnrpFfiDiagnostic(NnrpFfiStatus.Ok))));
            pendingEvents.Enqueue(connection => new NnrpPollResult(
                NnrpFfiStatus.Ok,
                1,
                new NnrpEvent(
                    6,
                    (uint)MessageType.ResultPush,
                    connection,
                    new NnrpHandle(NnrpHandleKind.Session, 41, 3),
                    new NnrpHandle(NnrpHandleKind.Operation, 99, 1),
                    7,
                    NnrpHandle.Invalid,
                    new NnrpBufferView(EventPayloadHandle.AddrOfPinnedObject(), new UIntPtr((uint)EventPayload.Length)),
                    new NnrpFfiDiagnostic(NnrpFfiStatus.Ok))));
            var polledResult = host.SubmitAndPollResult(99, SubmitHeader(7), payload, maxEvents: 2);
            host.Control(10, payload);

            Assert.Equal((ulong)99, operation.OperationId);
            Assert.Equal((ulong)99, polledResult.OperationId);
            Assert.Equal((uint)7, polledResult.FrameId);
            Assert.Equal(new byte[] { 1, 2, 3 }, polledResult.Payload);
            Assert.Equal(new byte[] { 1, 2, 3 }, submittedPayload);
            Assert.Equal((uint)HeaderFlags.AckRequired, submittedRequest.HeaderFlags);
            Assert.Equal((ushort)2, submittedRequest.ViewId);
            Assert.Equal((ushort)3, submittedRequest.RouteId);
            Assert.Equal((ulong)4, submittedRequest.TraceId);
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
                CaptureServerReceiveSubmit,
                CaptureServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                schemaRegistryCreate: SchemaRegistryCreate,
                schemaRegistryInstall: SchemaRegistryInstall,
                schemaRegistryRelease: HandleStatus);
            var payload = new byte[] { 1, 2, 3 };
            var payloadHandle = GCHandle.Alloc(payload, GCHandleType.Pinned);

            try
            {
                var expectedPointer = payloadHandle.AddrOfPinnedObject();
                var client = new NnrpNativeRuntimeClient(entrypoints);
                var connection = client.Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp);
                var session = connection.OpenSession(41, 3, 4, 5, 6);
                session.SubmitOperation(99, SubmitHeader(7), payload);
                session.Control(10, payload);

                using var server = CreateRuntimeServer(entrypoints, 50, 2, NnrpNativeArtifact.TransportSlotTcp);
                var serverSession = server.AcceptSession(42, 3);
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent);
            using var carrier = CreateTransportCarrier(entrypoints, TransportId.Tcp, 11, 2);
            using var connectionHost = NnrpNativeRuntimeConnectionHost.Open(
                carrier,
                new NnrpNativeRuntimeConnectionHostOptions(11, 2));
            var session = connectionHost.OpenSession(new NnrpNativeRuntimeSessionOptions(41, 3, 4, 5, 6));
            var source = new byte[] { 0, 1, 2, 3, 4 };
            var payload = source.AsMemory(1, 3);

            var submitHandle = session.Submit(98, SubmitHeader(6), payload);
            var operation = await session.SubmitOperationAsync(99, SubmitHeader(7), payload);
            var result = connectionHost.SubmitAndPollResult(41, 99, SubmitHeader(7), payload, maxEvents: 1);
            connectionHost.SubmitOperation(41, 100, SubmitHeader(8), payload);
            connectionHost.Control(41, 10, payload);
            connectionHost.Connection.Control(11, payload);

            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            await Assert.ThrowsAsync<TaskCanceledException>(() => session.SubmitOperationAsync(101, SubmitHeader(9), payload, cancellationToken: cancelled.Token));

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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
                CaptureServerReceiveSubmit,
                CaptureServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                schemaRegistryCreate: SchemaRegistryCreate,
                schemaRegistryInstall: SchemaRegistryInstall,
                schemaRegistryRelease: HandleStatus,
                bufferAcquireCopy: BufferAcquireCopy,
                bufferView: BufferView,
                bufferRelease: HandleStatus);

            using var nativePayload = new NnrpNativeBuffers(entrypoints).AcquireCopy(new byte[] { 1, 2, 3 });
            var borrowed = nativePayload.BorrowView();
            var client = new NnrpNativeRuntimeClient(entrypoints);
            var connection = client.Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp);
            var session = connection.OpenSession(41, 3, 4, 5, 6);
            session.SubmitOperation(99, SubmitHeader(7), nativePayload);
            session.Control(17, nativePayload);

            using var server = CreateRuntimeServer(entrypoints, 50, 2, NnrpNativeArtifact.TransportSlotTcp);
            var serverSession = server.AcceptSession(41, 3);
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
                    ServerAcceptBegin,
                    ServerAcceptWait,
                    ServerAcceptClaim,
                    ServerAcceptRelease,
                    CaptureServerReceiveSubmit,
                    CaptureServerSendResult,
                    ServerFlowUpdate,
                    HandleStatus,
                    Control,
                    PollEmpty,
                    DispatchEvent,
                    schemaRegistryCreate: SchemaRegistryCreate,
                    schemaRegistryInstall: SchemaRegistryInstall,
                    schemaRegistryRelease: HandleStatus,
                    bufferAcquireCopy: CaptureBufferAcquireCopy,
                    bufferView: BufferView,
                    bufferRelease: HandleStatus);
                using var nativePayload = new NnrpNativeBuffers(entrypoints).AcquireCopy(new byte[PayloadBytes]);
                var clientSession = new NnrpNativeRuntimeClient(entrypoints)
                    .Connect(11, 2, NnrpNativeArtifact.TransportSlotTcp)
                    .OpenSession(41, 3, 4, 5, 6);
                using var server = CreateRuntimeServer(entrypoints, 50, 2, NnrpNativeArtifact.TransportSlotTcp);
                var serverSession = server.AcceptSession(42, 3);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                var before = GC.GetAllocatedBytesForCurrentThread();
                for (var index = 0; index < Iterations; index += 1)
                {
                    clientSession.SubmitAndPollResult((ulong)(100 + index), SubmitHeader((uint)(10 + index)), nativePayload, maxEvents: 1);
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
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
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.Submit(99, SubmitHeader(7)));
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
            var firstOperation = firstSession.SubmitOperation(99, SubmitHeader(7));
            var secondOperation = secondSession.SubmitOperation(100, SubmitHeader(8));

            Assert.Equal(connection.Handle.Handle, firstSession.Connection.Handle);
            Assert.Equal(connection.Handle.Handle, secondSession.Connection.Handle);
            Assert.Equal((ulong)41, firstSession.Handle.Handle.Id);
            Assert.Equal((ulong)42, secondSession.Handle.Handle.Id);
            Assert.Equal(firstSession.Handle, firstOperation.Session);
            Assert.Equal(secondSession.Handle, secondOperation.Session);
        }

        [Fact]
        public void NativeRuntimeClientAdoptsCarrierAndAwaitsEmptyEvent()
        {
            var client = new NnrpNativeRuntimeClient(CreateEntrypoints());

            var connection = client.Connect(12, 2, NnrpNativeArtifact.TransportSlotTcp);
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
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
                new NnrpFfiRuntimeFrameHeader((byte)MessageType.Error, 7, present: 0),
                new NnrpHandle(NnrpHandleKind.Connection, 12, 2),
                new NnrpHandle(NnrpHandleKind.Session, 41, 3),
                new NnrpHandle(NnrpHandleKind.Operation, 99, 1),
                Array.Empty<byte>(),
                new NnrpNativeRuntimeDiagnostic(new NnrpFfiStatus(NnrpFfiStatusCode.InternalError), 12, 41, 99, 7));
            var dropEvent = new NnrpNativeRuntimeEvent(
                7,
                new NnrpFfiRuntimeFrameHeader((byte)MessageType.ResultDropReason, 7, present: 0),
                new NnrpHandle(NnrpHandleKind.Connection, 12, 2),
                new NnrpHandle(NnrpHandleKind.Session, 41, 3),
                new NnrpHandle(NnrpHandleKind.Operation, 99, 1),
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
                session.SubmitOperationAsync(101, SubmitHeader(9), new byte[] { 1, 2, 3 }, cancellationToken: cancellation.Token));
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
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
                SubmitHeader(7),
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
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
                41,
                3,
                4,
                5,
                6);

            using var carrier = CreateTransportCarrier(entrypoints, TransportId.Tcp, 12, 2);
            using (var host = NnrpNativeRuntimeSessionHost.Open(carrier, options))
            {
                var operation = host.SubmitOperation(99, SubmitHeader(7), parentOperationId: 1, operationGroupId: 2);
                var polled = host.PollResult(operation, maxEvents: 1);
                var events = host.PollAvailableEvents(1);
                var result = host.SubmitAndPollResult(99, SubmitHeader(7), new byte[] { 1, 2, 3 }, maxEvents: 1);

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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                schemaRegistryCreate: SchemaRegistryCreate,
                schemaRegistryInstall: SchemaRegistryInstall,
                schemaRegistryRelease: HandleStatus,
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
                41,
                3,
                4,
                5,
                6);
            using var carrier = CreateTransportCarrier(entrypoints, TransportId.Tcp, 12, 2);
            var host = NnrpNativeRuntimeSessionHost.Open(carrier, options);
            var objectId = MatchingCacheObjectId();
            using var nativePayload = new NnrpNativeBuffers(entrypoints).AcquireCopy(new byte[] { 1, 2, 3 });

            var operation = host.SubmitOperation(98, SubmitHeader(6), nativePayload);
            var result = host.SubmitAndPollResult(99, SubmitHeader(7), nativePayload, maxEvents: 1);
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
            Assert.Equal((ulong)1000, touch.GrantedAtMilliseconds);
            Assert.Equal((uint)1500, touch.TtlMilliseconds);
            Assert.Single(prefetch);
            Assert.Equal((uint)NnrpCacheLeaseOutcome.Released, release.OutcomeCode);
            Assert.True(host.IsClosed);
            Assert.True(host.Session.IsClosed);
            Assert.True(host.Connection.IsClosed);
            Assert.Throws<NnrpNativeInvalidStateException>(() => host.Cancel(72));
            host.Dispose();
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeSessionHost.Open(null!, options));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeSessionHost.Open(carrier, null!));
        }

        [Fact]
        public void NativeRuntimeSessionHostRequiresProviderCarrier()
        {
            var entrypoints = CreateEntrypoints();
            var options = new NnrpNativeRuntimeSessionHostOptions(
                12,
                2,
                41,
                3,
                4,
                5,
                6);

            Assert.Throws<ArgumentNullException>(() =>
                NnrpNativeRuntimeSessionHost.Open(null!, options));

            using var carrier = CreateTransportCarrier(entrypoints, TransportId.Tcp, 12, 2);
            using (var host = NnrpNativeRuntimeSessionHost.Open(carrier, options))
            {
                Assert.Equal((ulong)12, host.Connection.Handle.Handle.Id);
            }
        }

        [Fact]
        public void NativeRuntimeHostsAllocateOpaqueSessionHandlesIndependentlyOfRequestedIds()
        {
            var requests = new List<NnrpSessionOpenRequest>();

            NnrpFfiStatus CaptureSessionOpen(NnrpSessionOpenRequest request, out NnrpHandle session)
            {
                requests.Add(request);
                session = new NnrpHandle(
                    NnrpHandleKind.Session,
                    request.SessionHandleId,
                    request.Generation);
                return NnrpFfiStatus.Ok;
            }

            using var entrypoints = CreateEntrypoints(clientOpenSession: CaptureSessionOpen);
            using (var carrier = CreateTransportCarrier(entrypoints, TransportId.Tcp, 11, 2))
            using (var host = NnrpNativeRuntimeSessionHost.Open(
                carrier,
                new NnrpNativeRuntimeSessionHostOptions(11, 2, 0, 3, 4, 5, 6)))
            {
                var request = Assert.Single(requests);
                Assert.Equal((uint)0, request.RequestedSessionId);
                Assert.NotEqual((ulong)0, request.SessionHandleId);
                Assert.Equal(request.SessionHandleId, host.Session.Handle.Handle.Id);
            }

            requests.Clear();
            using (var carrier = CreateTransportCarrier(entrypoints, TransportId.Tcp, 12, 2))
            using (var host = NnrpNativeRuntimeConnectionHost.Open(
                carrier,
                new NnrpNativeRuntimeConnectionHostOptions(12, 2)))
            {
                var session = host.OpenSession(new NnrpNativeRuntimeSessionOptions(0, 3, 4, 5, 6));
                var request = Assert.Single(requests);
                Assert.Equal((uint)0, request.RequestedSessionId);
                Assert.NotEqual((ulong)0, request.SessionHandleId);
                Assert.Equal(request.SessionHandleId, session.Handle.Handle.Id);
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent);
            var options = new NnrpNativeRuntimeConnectionHostOptions(
                12,
                2);
            using var carrier = CreateTransportCarrier(entrypoints, TransportId.Tcp, 12, 2);
            using (var host = NnrpNativeRuntimeConnectionHost.Open(carrier, options))
            {
                var firstSession = host.OpenSession(new NnrpNativeRuntimeSessionOptions(41, 3, 4, 5, 6));
                var secondSession = host.OpenSession(new NnrpNativeRuntimeSessionOptions(42, 4, 4, 5, 6));
                var firstOperation = host.SubmitOperation(41, 99, SubmitHeader(7), parentOperationId: 1, operationGroupId: 2);
                var secondOperation = host.SubmitOperation(42, 100, SubmitHeader(8));

                pendingEvents.Enqueue(CreatePollResult(host.Connection.Handle.Handle, secondSession.Handle.Handle, secondOperation.Handle.Handle, 8));
                pendingEvents.Enqueue(CreatePollResult(host.Connection.Handle.Handle, firstSession.Handle.Handle, firstOperation.Handle.Handle, 7));

                var firstResult = host.PollResult(41, firstOperation, maxEvents: 2);
                var secondResult = host.PollResult(42, secondOperation, maxEvents: 1);

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
        public void NativeRuntimeConnectionHostManagesControlsThroughProviderCarrier()
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                schemaRegistryCreate: SchemaRegistryCreate,
                schemaRegistryInstall: SchemaRegistryInstall,
                schemaRegistryRelease: HandleStatus,
                bufferAcquireCopy: BufferAcquireCopy,
                bufferView: BufferView,
                bufferRelease: HandleStatus);
            var options = new NnrpNativeRuntimeConnectionHostOptions(
                12,
                2);
            using var carrier = CreateTransportCarrier(entrypoints, TransportId.Tcp, 12, 2);
            var host = NnrpNativeRuntimeConnectionHost.Open(carrier, options);
            var session = host.OpenSession(new NnrpNativeRuntimeSessionOptions(41, 3, 4, 5, 6));
            using var nativePayload = new NnrpNativeBuffers(entrypoints).AcquireCopy(new byte[] { 1, 2, 3 });

            var routedOperation = host.SubmitOperation(41, 98, SubmitHeader(6), nativePayload);
            var result = host.SubmitAndPollResult(41, 99, SubmitHeader(7), nativePayload, maxEvents: 1);
            var events = host.PollAvailableEvents(1);
            host.Cancel(41, 71);
            host.Control(41, 17, nativePayload);
            host.Close();

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
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeConnectionHost.Open(null!, options));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeConnectionHost.Open(carrier, null!));
        }

        [Fact]
        public void NativeRuntimeConnectionHostRoutesSchemaAndCacheLeaseHelpers()
        {
            var entrypoints = CreateEntrypoints();
            using var carrier = CreateTransportCarrier(entrypoints, TransportId.Tcp, 12, 2);
            var host = NnrpNativeRuntimeConnectionHost.Open(
                carrier,
                new NnrpNativeRuntimeConnectionHostOptions(
                    12,
                    2));
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
            Assert.Equal((ulong)1000, touch.GrantedAtMilliseconds);
            Assert.Equal((uint)1500, touch.TtlMilliseconds);
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
                session.SubmitAndPollResult(99, SubmitHeader(7), new byte[] { 1, 2, 3 }, maxEvents: 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                session.PollResult(session.SubmitOperation(99, SubmitHeader(7)), maxEvents: -1));
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
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
            var operation = session.SubmitOperation(99, SubmitHeader(7));

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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
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
            var firstOperation = firstSession.SubmitOperation(99, SubmitHeader(7));
            var secondOperation = secondSession.SubmitOperation(100, SubmitHeader(8));

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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
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
            var firstOperation = firstSession.SubmitOperation(99, SubmitHeader(7));
            var secondOperation = secondSession.SubmitOperation(100, SubmitHeader(8));

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
            var operation = session.SubmitOperation(99, SubmitHeader(7));

            session.Close();

            Assert.True(session.IsClosed);
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.Submit(100, SubmitHeader(8)));
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent,
                transportClose: HandleStatus);
            var client = new NnrpNativeRuntimeClient(entrypoints);
            using var transportConnection = new NnrpTransportConnection(
                new NnrpNativeEntrypointLease(entrypoints),
                TransportId.Tcp,
                new NnrpHandle(NnrpHandleKind.TransportConnection, 30, 1));

            Assert.Throws<NnrpNativeInvalidStateException>(() => client.Connect(11, 2, transportConnection));
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
                CaptureServerReceiveSubmit,
                CaptureServerSendResult,
                CaptureServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                schemaRegistryCreate: SchemaRegistryCreate,
                schemaRegistryInstall: SchemaRegistryInstall,
                schemaRegistryRelease: HandleStatus,
                bufferAcquireCopy: BufferAcquireCopy,
                bufferView: BufferView,
                bufferRelease: HandleStatus);

            using (var server = CreateRuntimeServer(entrypoints, 50, 2, NnrpNativeArtifact.TransportSlotTcp))
            {
                var session = server.AcceptSession(41, 3);
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
                Assert.Throws<NnrpNativeInvalidStateException>(() => server.AcceptSession(42, 3));
            }

            Assert.Throws<ArgumentNullException>(() => CreateRuntimeServer(null!, 50, 2, NnrpNativeArtifact.TransportSlotTcp));
        }

        [Fact]
        public void NativeRuntimeServerSessionRejectsUseAfterServerClose()
        {
            var server = CreateRuntimeServer(CreateEntrypoints(), 50, 2, NnrpNativeArtifact.TransportSlotTcp);
            var session = server.AcceptSession(41, 3);

            server.Close();

            Assert.Throws<NnrpNativeInvalidStateException>(() => session.ReceiveSubmit(99, 7));
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.SendFlowUpdate(7));
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.Control(17));
            Assert.Throws<NnrpNativeInvalidStateException>(() => session.Close());
            server.Dispose();
        }

        [Fact]
        public void NativeRuntimeServerSessionClosesThroughServerRoleEntrypoint()
        {
            var sessionCloseCount = 0;
            var serverCloseCount = 0;
            var entrypoints = CreateEntrypoints(
                sessionClose: _ =>
                {
                    sessionCloseCount += 1;
                    return NnrpFfiStatus.Ok;
                },
                serverClose: _ =>
                {
                    serverCloseCount += 1;
                    return NnrpFfiStatus.Ok;
                });
            var server = CreateRuntimeServer(entrypoints, 50, 2, NnrpNativeArtifact.TransportSlotTcp);
            var session = server.AcceptSession(41, 3);

            session.Close();

            Assert.Equal(0, sessionCloseCount);
            Assert.Equal(1, serverCloseCount);
            Assert.True(session.IsClosed);
            server.Dispose();
        }

        [Fact]
        public void NativeRuntimeServerSessionCopiesBatchEventsAndReleasesEveryPayloadOwner()
        {
            var firstPayload = Marshal.AllocHGlobal(2);
            var secondPayload = Marshal.AllocHGlobal(3);
            var releasedOwners = new List<ulong>();
            var sessionHandle = new NnrpHandle(NnrpHandleKind.Session, 41, 3);
            try
            {
                Marshal.Copy(new byte[] { 1, 2 }, 0, firstPayload, 2);
                Marshal.Copy(new byte[] { 3, 4, 5 }, 0, secondPayload, 3);

                NnrpFfiStatus AwaitBatch(
                    NnrpRoleEventPollRequest request,
                    IntPtr events,
                    UIntPtr eventCapacity,
                    out UIntPtr eventCount)
                {
                    Assert.Equal(sessionHandle, request.Scope);
                    Assert.Equal((uint)2, request.MaxEvents);
                    Assert.Equal((uint)25, request.TimeoutMilliseconds);
                    Assert.Equal(new UIntPtr(2), eventCapacity);
                    var eventSize = Marshal.SizeOf<NnrpEvent>();
                    Marshal.StructureToPtr(
                        new NnrpEvent(
                            1,
                            (uint)MessageType.Progress,
                            new NnrpHandle(NnrpHandleKind.Connection, 11, 2),
                            sessionHandle,
                            new NnrpHandle(NnrpHandleKind.Operation, 91, 1),
                            7,
                            new NnrpHandle(NnrpHandleKind.Buffer, 101, 1),
                            new NnrpBufferView(firstPayload, new UIntPtr(2)),
                            new NnrpFfiDiagnostic(NnrpFfiStatus.Ok)),
                        events,
                        false);
                    Marshal.StructureToPtr(
                        new NnrpEvent(
                            2,
                            (uint)MessageType.PartialResult,
                            new NnrpHandle(NnrpHandleKind.Connection, 11, 2),
                            sessionHandle,
                            new NnrpHandle(NnrpHandleKind.Operation, 92, 1),
                            8,
                            new NnrpHandle(NnrpHandleKind.Buffer, 102, 1),
                            new NnrpBufferView(secondPayload, new UIntPtr(3)),
                            new NnrpFfiDiagnostic(NnrpFfiStatus.Ok)),
                        IntPtr.Add(events, eventSize),
                        false);
                    eventCount = new UIntPtr(2);
                    return NnrpFfiStatus.Ok;
                }

                NnrpFfiStatus ReleaseOwner(NnrpHandle owner)
                {
                    owner.RequireKind(NnrpHandleKind.Buffer);
                    releasedOwners.Add(owner.Id);
                    return NnrpFfiStatus.Ok;
                }

                var entrypoints = CreateEntrypoints(
                    serverAwaitEvents: AwaitBatch,
                    bufferRelease: ReleaseOwner);
                var session = new NnrpNativeRuntimeServerSession(
                    entrypoints,
                    new NnrpConnectionHandle(new NnrpHandle(NnrpHandleKind.Connection, 11, 2)),
                    new NnrpSessionHandle(sessionHandle),
                    TransportId.Tcp);

                var events = session.AwaitEvents(2, 25);

                Assert.Collection(
                    events,
                    item =>
                    {
                        Assert.Equal((uint)MessageType.Progress, item.MessageType);
                        Assert.Equal(new byte[] { 1, 2 }, item.Payload);
                    },
                    item =>
                    {
                        Assert.Equal((uint)MessageType.PartialResult, item.MessageType);
                        Assert.Equal(new byte[] { 3, 4, 5 }, item.Payload);
                    });
                Assert.Equal(new ulong[] { 101, 102 }, releasedOwners);
                session.Close();
            }
            finally
            {
                Marshal.FreeHGlobal(firstPayload);
                Marshal.FreeHGlobal(secondPayload);
            }
        }

        [Fact]
        public void NativeRuntimeClientSessionCopiesBatchEventsAndReleasesEveryPayloadOwner()
        {
            var payload = Marshal.AllocHGlobal(3);
            var releasedOwners = new List<ulong>();
            var sessionHandle = new NnrpHandle(NnrpHandleKind.Session, 42, 4);
            try
            {
                Marshal.Copy(new byte[] { 6, 7, 8 }, 0, payload, 3);

                NnrpFfiStatus AwaitBatch(
                    NnrpRoleEventPollRequest request,
                    IntPtr events,
                    UIntPtr eventCapacity,
                    out UIntPtr eventCount)
                {
                    Assert.Equal(sessionHandle, request.Scope);
                    Assert.Equal((uint)4, request.MaxEvents);
                    Assert.Equal((uint)30, request.TimeoutMilliseconds);
                    Assert.Equal(new UIntPtr(4), eventCapacity);
                    Marshal.StructureToPtr(
                        new NnrpEvent(
                            1,
                            (uint)MessageType.Progress,
                            new NnrpHandle(NnrpHandleKind.Connection, 12, 2),
                            sessionHandle,
                            new NnrpHandle(NnrpHandleKind.Operation, 10_091, 1),
                            7,
                            new NnrpHandle(NnrpHandleKind.Buffer, 103, 1),
                            new NnrpBufferView(payload, new UIntPtr(3)),
                            new NnrpFfiDiagnostic(
                                NnrpFfiStatus.Ok,
                                relatedConnectionId: 12,
                                relatedSessionId: 42,
                                relatedOperationId: 91,
                                relatedFrameId: 7)),
                        events,
                        false);
                    eventCount = new UIntPtr(1);
                    return NnrpFfiStatus.Ok;
                }

                NnrpFfiStatus ReleaseOwner(NnrpHandle owner)
                {
                    owner.RequireKind(NnrpHandleKind.Buffer);
                    releasedOwners.Add(owner.Id);
                    return NnrpFfiStatus.Ok;
                }

                var entrypoints = CreateEntrypoints(
                    clientAwaitEvents: AwaitBatch,
                    bufferRelease: ReleaseOwner);
                var session = new NnrpNativeRuntimeSession(
                    entrypoints,
                    new NnrpConnectionHandle(new NnrpHandle(NnrpHandleKind.Connection, 12, 2)),
                    new NnrpSessionHandle(sessionHandle));

                var events = session.AwaitEvents(4, 30);

                var @event = Assert.Single(events);
                Assert.Equal(new byte[] { 6, 7, 8 }, @event.Payload);
                Assert.Equal((ulong)10_091, @event.Operation.Id);
                Assert.Equal((ulong)91, @event.Diagnostic.RelatedOperationId);
                Assert.Equal(new ulong[] { 103 }, releasedOwners);
                session.Close();
            }
            finally
            {
                Marshal.FreeHGlobal(payload);
            }
        }

        [Fact]
        public void NativeRuntimeServerSessionRejectsEventCountsBeyondNativeCapacity()
        {
            var entrypoints = CreateEntrypoints(
                serverAwaitEvents: (
                    NnrpRoleEventPollRequest _,
                    IntPtr _,
                    UIntPtr _,
                    out UIntPtr eventCount) =>
                {
                    eventCount = new UIntPtr(2);
                    return NnrpFfiStatus.Ok;
                });
            var session = new NnrpNativeRuntimeServerSession(
                entrypoints,
                new NnrpConnectionHandle(new NnrpHandle(NnrpHandleKind.Connection, 11, 2)),
                new NnrpSessionHandle(new NnrpHandle(NnrpHandleKind.Session, 41, 3)),
                TransportId.Tcp);

            Assert.Empty(session.AwaitEvents(0));
            Assert.Throws<NnrpNativeArtifactException>(() => session.AwaitEvents(1));
            session.Close();
        }

        [Fact]
        public void NativeRuntimeServerRetainsAcceptTicketAcrossWouldBlock()
        {
            var beginCount = 0;
            var waitCount = 0;
            var claimCount = 0;

            NnrpFfiStatus Begin(NnrpServerAcceptBeginRequest request, out NnrpHandle accept)
            {
                beginCount += 1;
                accept = new NnrpHandle(NnrpHandleKind.ServerAccept, request.AcceptHandleId, request.Generation);
                return NnrpFfiStatus.Ok;
            }

            NnrpFfiStatus Wait(NnrpServerAcceptWaitRequest request)
            {
                waitCount += 1;
                return waitCount == 1
                    ? new NnrpFfiStatus(NnrpFfiStatusCode.WouldBlock)
                    : NnrpFfiStatus.Ok;
            }

            NnrpFfiStatus Claim(NnrpServerAcceptClaimRequest request, out NnrpServerAcceptResult result)
            {
                claimCount += 1;
                result = new NnrpServerAcceptResult(
                    new NnrpHandle(NnrpHandleKind.Session, request.SessionHandleId, request.Generation),
                    (uint)TransportId.Ipc);
                return NnrpFfiStatus.Ok;
            }

            using var server = CreateRuntimeServer(
                CreateEntrypoints(
                    serverAcceptBegin: Begin,
                    serverAcceptWait: Wait,
                    serverAcceptClaim: Claim),
                50,
                2,
                NnrpNativeArtifact.TransportSlotTcp);

            Assert.Throws<NnrpNativeWouldBlockException>(() => server.AcceptSession(41, 3, 1));
            var session = server.AcceptSession(42, 4, 25);

            Assert.Equal(1, beginCount);
            Assert.Equal(2, waitCount);
            Assert.Equal(1, claimCount);
            Assert.Equal((ulong)42, session.Handle.Handle.Id);
            Assert.Equal(TransportId.Ipc, session.ActiveTransportId);
        }

        [Fact]
        public void NativeRuntimeServerCanReleasePendingAcceptWithoutClosing()
        {
            var releaseCount = 0;
            using var server = CreateRuntimeServer(
                CreateEntrypoints(
                    serverAcceptWait: _ => new NnrpFfiStatus(NnrpFfiStatusCode.WouldBlock),
                    serverAcceptRelease: accept =>
                    {
                        releaseCount++;
                        Assert.Equal(NnrpHandleKind.ServerAccept, accept.Kind);
                        return NnrpFfiStatus.Ok;
                    }),
                50,
                2,
                NnrpNativeArtifact.TransportSlotTcp);

            Assert.False(server.ReleasePendingAccept());
            Assert.Throws<NnrpNativeWouldBlockException>(() => server.AcceptSession(41, 3, 1));
            Assert.True(server.ReleasePendingAccept());
            Assert.False(server.ReleasePendingAccept());
            Assert.False(server.IsClosed);
            Assert.Equal(1, releaseCount);
        }

        [Fact]
        public void NativeRuntimeServerReleasesPendingAcceptBeforeConnectionClose()
        {
            var releaseCount = 0;
            var closeCount = 0;
            var entrypoints = CreateEntrypoints(
                serverAcceptWait: _ => new NnrpFfiStatus(NnrpFfiStatusCode.WouldBlock),
                serverAcceptRelease: accept =>
                {
                    releaseCount += 1;
                    Assert.Equal(NnrpHandleKind.ServerAccept, accept.Kind);
                    return new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState);
                },
                connectionClose: handle =>
                {
                    closeCount += 1;
                    Assert.Equal(NnrpHandleKind.Connection, handle.Kind);
                    return NnrpFfiStatus.Ok;
                });
            var server = CreateRuntimeServer(entrypoints, 50, 2, NnrpNativeArtifact.TransportSlotTcp);

            Assert.Throws<NnrpNativeWouldBlockException>(() => server.AcceptSession(41, 3, 1));
            Assert.Throws<NnrpNativeInvalidStateException>(() => server.Close());

            Assert.Equal(1, releaseCount);
            Assert.Equal(1, closeCount);
            Assert.True(server.IsClosed);
            server.Dispose();
        }

        [Fact]
        public void NativeRuntimeServerRejectsUnknownAcceptedTransport()
        {
            NnrpFfiStatus Claim(NnrpServerAcceptClaimRequest request, out NnrpServerAcceptResult result)
            {
                result = new NnrpServerAcceptResult(
                    new NnrpHandle(NnrpHandleKind.Session, request.SessionHandleId, request.Generation),
                    999);
                return NnrpFfiStatus.Ok;
            }

            using var server = CreateRuntimeServer(
                CreateEntrypoints(serverAcceptClaim: Claim),
                50,
                2,
                NnrpNativeArtifact.TransportSlotTcp);

            Assert.Throws<NnrpNativeArtifactException>(() => server.AcceptSession(41, 3));
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
                CaptureServerReceiveSubmit,
                CaptureServerSendResult,
                CaptureServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                schemaRegistryCreate: SchemaRegistryCreate,
                schemaRegistryInstall: SchemaRegistryInstall,
                schemaRegistryRelease: HandleStatus,
                bufferAcquireCopy: BufferAcquireCopy,
                bufferView: BufferView,
                bufferRelease: HandleStatus);
            var options = new NnrpNativeRuntimeServerHostOptions(50, 2);
            using var listener = CreateTransportListener(entrypoints, TransportId.Tcp, 50, 2);

            using (var host = NnrpNativeRuntimeServerHost.Open(listener, options))
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

            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeServerHost.Open(null!, options));
            Assert.Throws<ArgumentNullException>(() => NnrpNativeRuntimeServerHost.Open(listener, null!));
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
                ServerAcceptBegin,
                ServerAcceptWait,
                ServerAcceptClaim,
                ServerAcceptRelease,
                CaptureServerReceiveSubmit,
                CaptureServerSendResult,
                ServerFlowUpdate,
                HandleStatus,
                CaptureControl,
                PollEmpty,
                DispatchEvent,
                schemaRegistryCreate: SchemaRegistryCreate,
                schemaRegistryInstall: SchemaRegistryInstall,
                schemaRegistryRelease: HandleStatus);
            using var listener = CreateTransportListener(entrypoints, TransportId.Tcp, 50, 2);
            using var host = NnrpNativeRuntimeServerHost.Open(
                listener,
                new NnrpNativeRuntimeServerHostOptions(50, 2));
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
            using var listener = CreateTransportListener(entrypoints, TransportId.Tcp, 50, 2);
            var host = NnrpNativeRuntimeServerHost.Open(
                listener,
                new NnrpNativeRuntimeServerHostOptions(
                    50,
                    2));
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
            Assert.Equal((ulong)1000, touch.GrantedAtMilliseconds);
            Assert.Equal((uint)1500, touch.TtlMilliseconds);
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
        public void NativeRuntimeSessionSubmitResultCompactBatchReturnsCompletedOperations()
        {
            var entrypoints = CreateEntrypoints();
            using var carrier = CreateTransportCarrier(entrypoints, TransportId.Tcp, 11, 2);
            var host = NnrpNativeRuntimeSessionHost.Open(
                carrier,
                new NnrpNativeRuntimeSessionHostOptions(11, 2, 41, 3, 4, 5, 6));

            using (host)
            {
                var completed = host.SubmitResultCompactBatch(
                    operationIdStart: 5,
                    frameIdStart: 7,
                    frameIdStride: 2,
                    submitPayload: new byte[] { 1, 2, 3 },
                    resultPayload: new byte[] { 4, 5 },
                    maxEvents: 6,
                    iterations: 3);

                Assert.Equal((ulong)3, completed);
            }
        }

        [Fact]
        public void NativeRuntimeSessionSubmitResultCompactBatchRejectsInvalidArguments()
        {
            var entrypoints = CreateEntrypoints();
            using var carrier = CreateTransportCarrier(entrypoints, TransportId.Tcp, 11, 2);
            var host = NnrpNativeRuntimeSessionHost.Open(
                carrier,
                new NnrpNativeRuntimeSessionHostOptions(11, 2, 41, 3, 4, 5, 6));

            using (host)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    host.SubmitResultCompactBatch(5, 7, 0, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, 1, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    host.SubmitResultCompactBatch(5, 7, 1, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, -1, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    host.SubmitResultCompactBatch(5, 7, 1, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty, 1, 0));
            }
        }

        [Fact]
        public void ServerPolicyDispatcherRejectsInvalidOrLateCallbacks()
        {
            using var entrypoints = CreateEntrypoints();
            var dispatcher = new NnrpNativeServerPolicyDispatcher(
                entrypoints,
                _ => new ValueTask<NnrpNativeServerPolicyDecision>(NnrpNativeServerPolicyDecision.Accept()));

            var accepted = NnrpNativeServerPolicyDecision.Accept();
            Assert.True(accepted.Accepted);
            Assert.Equal(SessionErrorCode.None, accepted.SessionErrorCode);
            Assert.Null(accepted.Diagnostic);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NnrpNativeServerPolicyDecision.Reject(SessionErrorCode.None, null));
            Assert.Equal(
                (uint)NnrpFfiStatusCode.CallbackRejected,
                dispatcher.BeginCallback(IntPtr.Zero, 0, NnrpBufferView.Empty));

            dispatcher.Dispose();

            Assert.Equal(
                (uint)NnrpFfiStatusCode.CallbackRejected,
                dispatcher.BeginCallback(IntPtr.Zero, 2, NnrpBufferView.Empty));
            dispatcher.Dispose();
        }

        [Fact]
        public async Task ServerPolicyDispatcherBoundsShutdownAndRetainsNativeOwnership()
        {
            var policyEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releasePolicy = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ownership = new DisposableProbe();
            using var entrypoints = CreateEntrypoints();
            var dispatcher = new NnrpNativeServerPolicyDispatcher(
                entrypoints,
                async _ =>
                {
                    policyEntered.TrySetResult(true);
                    await releasePolicy.Task.ConfigureAwait(false);
                    return NnrpNativeServerPolicyDecision.Accept();
                },
                ownership,
                TimeSpan.FromMilliseconds(50));

            var metadata = PolicyMetadata(42).ToArray();
            var owner = GCHandle.Alloc(metadata, GCHandleType.Pinned);
            try
            {
                Assert.Equal(
                    (uint)NnrpFfiStatusCode.Ok,
                    dispatcher.BeginCallback(
                        IntPtr.Zero,
                        7,
                        new NnrpBufferView(owner.AddrOfPinnedObject(), new UIntPtr((uint)metadata.Length))));
            }
            finally
            {
                owner.Free();
            }

            await policyEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var disposeError = await Task.Run(() => Record.Exception(dispatcher.Dispose))
                .WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsType<TimeoutException>(disposeError);
            Assert.False(ownership.IsDisposed);

            releasePolicy.TrySetResult(true);
            await ownership.Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(ownership.IsDisposed);
        }

        [Fact]
        public async Task ServerPolicyDispatcherSurfacesCompletionFailureAfterBoundedDrain()
        {
            var completionEntered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var releaseCompletion = new ManualResetEventSlim();
            var ownership = new DisposableProbe();
            using var entrypoints = CreateEntrypoints(
                serverPolicyComplete: _ =>
                {
                    completionEntered.TrySetResult(true);
                    releaseCompletion.Wait();
                    return new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState);
                });
            var dispatcher = new NnrpNativeServerPolicyDispatcher(
                entrypoints,
                _ => new ValueTask<NnrpNativeServerPolicyDecision>(NnrpNativeServerPolicyDecision.Accept()),
                ownership);

            var metadata = PolicyMetadata(42).ToArray();
            var owner = GCHandle.Alloc(metadata, GCHandleType.Pinned);
            try
            {
                Assert.Equal(
                    (uint)NnrpFfiStatusCode.Ok,
                    dispatcher.BeginCallback(
                        IntPtr.Zero,
                        7,
                        new NnrpBufferView(owner.AddrOfPinnedObject(), new UIntPtr((uint)metadata.Length))));
            }
            finally
            {
                owner.Free();
            }

            await completionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var releasing = Task.Run(async () =>
            {
                await Task.Delay(20);
                releaseCompletion.Set();
            });

            Assert.Throws<NnrpNativeInvalidStateException>(() => dispatcher.Dispose());
            await releasing;
            Assert.True(ownership.IsDisposed);
        }

        [Fact]
        public async Task ServerPolicyDispatcherCopiesMetadataAndCompletesEachDecisionExactlyOnce()
        {
            var completions = new List<(ulong RequestId, byte Accepted, uint ErrorCode, string Diagnostic)>();
            var completionSignal = new SemaphoreSlim(0, 2);
            var firstPolicyEntered = new TaskCompletionSource<SessionOpenMetadata>(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseFirstPolicy = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            NnrpFfiStatus Complete(NnrpServerPolicyCompleteRequest request)
            {
                lock (completions)
                {
                    completions.Add((
                        request.RequestId,
                        request.Decision.Accepted,
                        request.Decision.SessionErrorCode,
                        Encoding.UTF8.GetString(CopyBufferView(request.Decision.Diagnostic))));
                }

                completionSignal.Release();
                return NnrpFfiStatus.Ok;
            }

            using var entrypoints = CreateEntrypoints(serverPolicyComplete: Complete);
            using var dispatcher = new NnrpNativeServerPolicyDispatcher(
                entrypoints,
                async open =>
                {
                    if (open.RequestedSessionId == 43)
                    {
                        throw new InvalidOperationException("policy failed");
                    }

                    firstPolicyEntered.TrySetResult(open);
                    await releaseFirstPolicy.Task.ConfigureAwait(false);
                    return NnrpNativeServerPolicyDecision.Reject(
                        SessionErrorCode.PriorityRejected,
                        "priority rejected");
                });

            var firstMetadata = PolicyMetadata(42).ToArray();
            var firstOwner = GCHandle.Alloc(firstMetadata, GCHandleType.Pinned);
            try
            {
                Assert.Equal(
                    (uint)NnrpFfiStatusCode.Ok,
                    dispatcher.BeginCallback(
                        IntPtr.Zero,
                        7,
                        new NnrpBufferView(firstOwner.AddrOfPinnedObject(), new UIntPtr((uint)firstMetadata.Length))));
                firstMetadata[0] = 0;
            }
            finally
            {
                firstOwner.Free();
            }

            var copied = await firstPolicyEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal((uint)42, copied.RequestedSessionId);

            var secondMetadata = PolicyMetadata(43).ToArray();
            var secondOwner = GCHandle.Alloc(secondMetadata, GCHandleType.Pinned);
            try
            {
                Assert.Equal(
                    (uint)NnrpFfiStatusCode.Ok,
                    dispatcher.BeginCallback(
                        IntPtr.Zero,
                        8,
                        new NnrpBufferView(secondOwner.AddrOfPinnedObject(), new UIntPtr((uint)secondMetadata.Length))));
            }
            finally
            {
                secondOwner.Free();
            }

            Assert.True(await completionSignal.WaitAsync(TimeSpan.FromSeconds(5)));
            releaseFirstPolicy.SetResult(true);
            Assert.True(await completionSignal.WaitAsync(TimeSpan.FromSeconds(5)));

            (ulong RequestId, byte Accepted, uint ErrorCode, string Diagnostic)[] snapshot;
            lock (completions)
            {
                snapshot = completions.OrderBy(value => value.RequestId).ToArray();
            }

            Assert.Collection(
                snapshot,
                first =>
                {
                    Assert.Equal((ulong)7, first.RequestId);
                    Assert.Equal((byte)0, first.Accepted);
                    Assert.Equal((uint)SessionErrorCode.PriorityRejected, first.ErrorCode);
                    Assert.Equal("priority rejected", first.Diagnostic);
                },
                second =>
                {
                    Assert.Equal((ulong)8, second.RequestId);
                    Assert.Equal((byte)0, second.Accepted);
                    Assert.Equal((uint)SessionErrorCode.SessionLimitReached, second.ErrorCode);
                    Assert.Equal("application policy evaluation failed", second.Diagnostic);
                });
        }

        private static SessionOpenMetadata PolicyMetadata(uint sessionId) =>
            new SessionOpenMetadata(
                sessionId,
                TypedPayloadProfileId.TokenValue,
                SessionPriorityClass.Balanced,
                SessionFlags.AllowResume,
                TypedPayloadDescriptor.TokenDeltaSchemaId,
                TypedPayloadDescriptor.TokenDeltaSchemaVersion,
                500,
                4,
                30_000,
                24,
                0,
                0,
                99);

        private sealed class DisposableProbe : IDisposable
        {
            private int disposed;

            internal TaskCompletionSource<bool> Disposed { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                {
                    Disposed.TrySetResult(true);
                }
            }
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "nnrp-native-artifact-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static NnrpRuntimeCapabilities MatchingCapabilities(
            ushort abiMajor = NnrpNativeArtifact.ExpectedAbiMajor,
            ushort abiMinor = NnrpNativeArtifact.ExpectedAbiMinor,
            ushort abiPatch = NnrpNativeArtifact.ExpectedAbiPatch,
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

        [Fact]
        public void ClientRuntimeControlsUseOneNativeFrameSendAndIncrementFrameIds()
        {
            var sent = new List<(NnrpRuntimeFrameSendRequest Request, byte[] Payload)>();
            var entrypoints = CreateEntrypoints(
                request =>
                {
                    sent.Add((request, CopyBufferView(request.Payload)));
                    return NnrpFfiStatus.Ok;
                });
            var session = new NnrpNativeRuntimeSession(
                entrypoints,
                new NnrpConnectionHandle(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)),
                new NnrpSessionHandle(new NnrpHandle(NnrpHandleKind.Session, 2, 1)));

            session.CancelOperation(
                new ControlRequestMetadata(10, 1, 1, RuntimeRole.Client, 1, 2),
                new byte[] { 7, 8 });
            session.UpdatePriority(new SchedulingMetadata(10, 2, 3, -1, 0, 1));
            session.SendRouteHint(
                new RouteHintMetadata(10, 4, 3, 2, 99, 1, 2),
                new byte[] { 9 });
            session.SendTraceContext(
                0,
                new TraceContextMetadata(10, 5, 0, 1, 0, 0));
            session.UpdateBudget(new BudgetMetadata(10, 6, 1, 2, 3, 0));

            Assert.Collection(
                sent,
                item => AssertRuntimeFrame(item, MessageType.Cancel, 1, typeof(ControlRequestMetadata), new byte[] { 7, 8 }),
                item => AssertRuntimeFrame(item, MessageType.PriorityUpdate, 2, typeof(SchedulingMetadata), Array.Empty<byte>()),
                item => AssertRuntimeFrame(item, MessageType.RouteHint, 3, typeof(RouteHintMetadata), new byte[] { 9 }),
                item => AssertRuntimeFrame(item, MessageType.TraceContext, 0, typeof(TraceContextMetadata), Array.Empty<byte>()),
                item => AssertRuntimeFrame(item, MessageType.BudgetUpdate, 4, typeof(BudgetMetadata), Array.Empty<byte>()));
        }

        [Fact]
        public void ServerRuntimeControlsMapToFrozenMessageTypes()
        {
            var sent = new List<(NnrpRuntimeFrameSendRequest Request, byte[] Payload)>();
            var entrypoints = CreateEntrypoints(
                request =>
                {
                    sent.Add((request, CopyBufferView(request.Payload)));
                    return NnrpFfiStatus.Ok;
                });
            var session = new NnrpNativeRuntimeServerSession(
                entrypoints,
                new NnrpConnectionHandle(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)),
                new NnrpSessionHandle(new NnrpHandle(NnrpHandleKind.Session, 2, 1)),
                TransportId.Tcp);
            var operation = new NnrpNativeRuntimeOperation(
                entrypoints,
                new NnrpSessionHandle(new NnrpHandle(NnrpHandleKind.Session, 2, 1)),
                new NnrpOperationHandle(new NnrpHandle(NnrpHandleKind.Operation, 3, 1)),
                operationId: 20,
                frameId: 91);

            session.SendProgress(operation, new ProgressMetadata(20, 1, 5, 2500, 0, 1), new byte[] { 1 });
            session.SendPartialResult(operation, new PartialResultMetadata(20, 2, 0, 0, 2, 1), new byte[] { 2, 3 });
            session.DropResult(
                operation,
                new ResultDropReasonMetadata(20, 3, NnrpResultDropReasonCode.Backpressure, RuntimeRole.Server, 3, 1),
                new byte[] { 4 });
            session.SendBackpressure(new PressureMetadata(20, 4, 2, 3, 5, 2));
            session.SendCreditUpdate(new PressureMetadata(20, 8, 1, 0, 0, 1));
            session.NegotiateCapabilities(
                new CapabilityMetadata(2, 1, 3, 4, 5, 6, 1, 0),
                new byte[] { 7 });
            session.DegradeProfile(
                new CapabilityMetadata(2, 1, 3, 4, 5, 6, 1, 0),
                new byte[] { 8 });
            session.SendTraceContext(
                0,
                new TraceContextMetadata(20, 9, 0, 1, 0, 0));
            session.SendRecoverableError(
                new RecoverableErrorMetadata(1, 2, 3, RuntimeRole.Server, 1, 4, 5, 6, 7, 1),
                new byte[] { 5 });
            session.SendRetryAfter(
                new RetryAfterMetadata(20, 4, 5, 1, 2, RuntimeRole.Server, 1, 1),
                new byte[] { 6 });

            Assert.Equal(
                new[]
                {
                    MessageType.Progress,
                    MessageType.PartialResult,
                    MessageType.ResultDropReason,
                    MessageType.Backpressure,
                    MessageType.CreditUpdate,
                    MessageType.CapabilityNegotiation,
                    MessageType.DegradeProfile,
                    MessageType.TraceContext,
                    MessageType.ErrorRecoverable,
                    MessageType.RetryAfter,
                },
                sent.ConvertAll(item => (MessageType)item.Request.MessageType));
            var expectedFrameIds = new uint[] { 91, 91, 91, 1, 2, 3, 4, 0, 5, 6 };
            for (var index = 0; index < sent.Count; index++)
            {
                Assert.Equal(expectedFrameIds[index], sent[index].Request.FrameId);
                NnrpRuntimeControl.Decode((MessageType)sent[index].Request.MessageType, sent[index].Payload);
            }
            Assert.All(sent.GetRange(0, 3), item => Assert.Equal((ulong)3, item.Request.Handle.Id));

            Assert.Throws<ArgumentNullException>(() =>
                session.SendProgress(null!, new ProgressMetadata(20, 1, 5, 2500, 0, 1)));
            Assert.Throws<ArgumentException>(() =>
                session.SendPartialResult(operation, new PartialResultMetadata(21, 2, 0, 0, 0, 0)));
        }

        [Fact]
        public void RuntimeFrameSendFailurePreservesNativeStatus()
        {
            var entrypoints = CreateEntrypoints(
                _ => new NnrpFfiStatus(NnrpFfiStatusCode.InvalidState, NnrpErrorFamily.Session, 77));
            var session = new NnrpNativeRuntimeSession(
                entrypoints,
                new NnrpConnectionHandle(new NnrpHandle(NnrpHandleKind.Connection, 1, 1)),
                new NnrpSessionHandle(new NnrpHandle(NnrpHandleKind.Session, 2, 1)));

            var error = Assert.Throws<NnrpNativeInvalidStateException>(() =>
                session.UpdateBudget(new BudgetMetadata(1, 2, 3, 4, 5, 1)));

            Assert.Equal(NnrpErrorFamily.Session, error.Status.ErrorFamily);
            Assert.Equal(77u, error.Status.ProtocolErrorCode);
        }

        private static void AssertRuntimeFrame(
            (NnrpRuntimeFrameSendRequest Request, byte[] Payload) item,
            MessageType messageType,
            uint frameId,
            Type metadataType,
            byte[] tail)
        {
            Assert.Equal((uint)messageType, item.Request.MessageType);
            Assert.Equal(frameId, item.Request.FrameId);
            var decoded = NnrpRuntimeControl.Decode(messageType, item.Payload);
            Assert.Equal(metadataType, decoded.Metadata.GetType());
            Assert.Equal(tail, decoded.Tail.ToArray());
        }

        private static NnrpNativeRuntimeServer CreateRuntimeServer(
            NnrpNativeRuntimeEntrypoints entrypoints,
            ulong serverId,
            uint generation,
            uint transportId)
        {
            if (entrypoints == null)
            {
                throw new ArgumentNullException(nameof(entrypoints));
            }

            var listener = CreateTransportListener(
                entrypoints,
                transportId == NnrpNativeArtifact.TransportSlotQuic ? TransportId.Quic : TransportId.Tcp,
                transportId,
                generation == 0 ? 1u : generation);
            return NnrpNativeRuntimeServer.Bind(
                listener,
                new NnrpNativeRuntimeServerHostOptions(serverId, generation));
        }

        private static NnrpTransportListener CreateTransportListener(
            NnrpNativeRuntimeEntrypoints entrypoints,
            TransportId transportId,
            ulong handleId,
            uint generation)
        {
            var endpoint = transportId == TransportId.Quic
                ? NnrpProviderEndpoint.Parse("quic://127.0.0.1:0")
                : NnrpProviderEndpoint.Parse("tcp://127.0.0.1:0");
            return new NnrpTransportListener(
                new NnrpNativeEntrypointLease(entrypoints),
                transportId,
                endpoint,
                new NnrpHandle(NnrpHandleKind.TransportListener, handleId, generation));
        }

        private static NnrpTransportConnection CreateTransportCarrier(
            NnrpNativeRuntimeEntrypoints entrypoints,
            TransportId transportId,
            ulong handleId,
            uint generation)
        {
            return new NnrpTransportConnection(
                new NnrpNativeEntrypointLease(entrypoints),
                transportId,
                new NnrpHandle(NnrpHandleKind.TransportConnection, handleId, generation));
        }

        private static NnrpNativeRuntimeEntrypoints CreateEntrypoints(
            NnrpNativeRuntimeEntrypoints.RuntimeFrameSendInvoker? runtimeFrameSend = null,
            NativeObjectStore? objectStore = null,
            NnrpNativeRuntimeEntrypoints.ServerAcceptBeginInvoker? serverAcceptBegin = null,
            NnrpNativeRuntimeEntrypoints.ServerAcceptWaitInvoker? serverAcceptWait = null,
            NnrpNativeRuntimeEntrypoints.ServerAcceptClaimInvoker? serverAcceptClaim = null,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker? serverAcceptRelease = null,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker? connectionClose = null,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker? transportClose = null,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker? sessionClose = null,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker? serverClose = null,
            NnrpNativeRuntimeEntrypoints.RoleAwaitEventsInvoker? clientAwaitEvents = null,
            NnrpNativeRuntimeEntrypoints.RoleAwaitEventsInvoker? serverAwaitEvents = null,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker? bufferRelease = null,
            NnrpNativeRuntimeEntrypoints.TransportSecurityConfigCreateInvoker? transportClientSecurityConfigCreate = null,
            NnrpNativeRuntimeEntrypoints.TransportServerSecurityConfigCreateInvoker? transportServerSecurityConfigCreate = null,
            NnrpNativeRuntimeEntrypoints.TransportOpenInvoker? transportConnect = null,
            NnrpNativeRuntimeEntrypoints.TransportOpenInvoker? transportListen = null,
            NnrpNativeRuntimeEntrypoints.TransportListenerEndpointInvoker? transportListenerEndpoint = null,
            NnrpNativeRuntimeEntrypoints.TransportProbeInvoker? transportProbe = null,
            NnrpNativeRuntimeEntrypoints.ServerPolicyCompleteInvoker? serverPolicyComplete = null,
            NnrpNativeRuntimeEntrypoints.SessionOpenInvoker? clientOpenSession = null)
        {
            return new NnrpNativeRuntimeEntrypoints(
                CurrentProtocolVersion,
                () => MatchingCapabilities(),
                ConnectionBootstrap,
                ClientConnect,
                SessionOpen,
                clientOpenSession ?? SessionOpen,
                Submit,
                Submit,
                sessionClose ?? HandleStatus,
                HandleStatus,
                ClientCancel,
                AwaitEvent,
                ServerBind,
                serverAcceptBegin ?? ServerAcceptBegin,
                serverAcceptWait ?? ServerAcceptWait,
                serverAcceptClaim ?? ServerAcceptClaim,
                serverAcceptRelease ?? ServerAcceptRelease,
                ServerReceiveSubmit,
                ServerSendResult,
                ServerFlowUpdate,
                serverClose ?? HandleStatus,
                Control,
                PollEmpty,
                DispatchEvent,
                connectionClose: connectionClose,
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
                bufferRelease: bufferRelease ?? HandleStatus,
                cacheQuery: CacheQuery,
                cacheTouch: CacheTouch,
                cachePrefetch: CachePrefetch,
                cacheRelease: CacheRelease,
                clientSubmitResultCompactBatch: ClientSubmitResultCompactBatch,
                runtimeFrameSend: runtimeFrameSend,
                objectMetadataBufferAcquireCopy: objectStore == null ? null : objectStore.AcquireMetadataCopy,
                objectMetadataBufferView: objectStore == null ? null : objectStore.MetadataBufferView,
                objectMetadataBufferRelease: objectStore == null ? null : objectStore.MetadataBufferRelease,
                objectDescriptorCreate: objectStore == null ? null : objectStore.ObjectDescriptorCreate,
                objectDescriptorView: objectStore == null ? null : objectStore.ObjectDescriptorView,
                objectDescriptorMetadataSnapshot: objectStore == null ? null : objectStore.ObjectDescriptorMetadataSnapshot,
                objectDescriptorRelease: objectStore == null ? null : objectStore.ObjectDescriptorRelease,
                cacheReferenceDescriptorCreate: objectStore == null ? null : objectStore.CacheReferenceDescriptorCreate,
                cacheReferenceDescriptorView: objectStore == null ? null : objectStore.CacheReferenceDescriptorView,
                cacheReferenceDescriptorMetadataSnapshot: objectStore == null ? null : objectStore.CacheReferenceDescriptorMetadataSnapshot,
                cacheReferenceDescriptorRelease: objectStore == null ? null : objectStore.CacheReferenceDescriptorRelease,
                transportClientSecurityConfigCreate: transportClientSecurityConfigCreate ?? TransportClientSecurityConfigCreate,
                transportServerSecurityConfigCreate: transportServerSecurityConfigCreate ?? TransportServerSecurityConfigCreate,
                transportConnect: transportConnect ?? TransportConnect,
                transportListen: transportListen ?? TransportListen,
                transportAccept: TransportAccept,
                transportListenerEndpoint: transportListenerEndpoint ?? TransportListenerEndpoint,
                transportProbe: transportProbe ?? TransportProbe,
                transportClose: transportClose ?? HandleStatus,
                clientAwaitEvents: clientAwaitEvents,
                serverAwaitEvents: serverAwaitEvents,
                serverPolicyComplete: serverPolicyComplete);
        }

        internal static NnrpNativeRuntimeEntrypoints CreateTransportEntrypointsForTests(
            NnrpNativeRuntimeEntrypoints.TransportSecurityConfigCreateInvoker? transportClientSecurityConfigCreate,
            NnrpNativeRuntimeEntrypoints.TransportServerSecurityConfigCreateInvoker? transportServerSecurityConfigCreate,
            NnrpNativeRuntimeEntrypoints.TransportOpenInvoker? transportConnect,
            NnrpNativeRuntimeEntrypoints.TransportOpenInvoker? transportListen,
            NnrpNativeRuntimeEntrypoints.TransportListenerEndpointInvoker transportListenerEndpoint,
            NnrpNativeRuntimeEntrypoints.TransportProbeInvoker? transportProbe,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker? transportClose,
            NnrpNativeRuntimeEntrypoints.HandleStatusInvoker bufferRelease)
        {
            return CreateEntrypoints(
                transportClose: transportClose,
                bufferRelease: bufferRelease,
                transportClientSecurityConfigCreate: transportClientSecurityConfigCreate,
                transportServerSecurityConfigCreate: transportServerSecurityConfigCreate,
                transportConnect: transportConnect,
                transportListen: transportListen,
                transportListenerEndpoint: transportListenerEndpoint,
                transportProbe: transportProbe);
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

        private static NnrpTransportOpenRequest MatchingTransportOpenRequest()
        {
            return new NnrpTransportOpenRequest(
                TransportId.Tcp,
                NnrpBufferView.Empty,
                NnrpHandle.Invalid,
                1024,
                10);
        }

        private static NnrpFfiStatus TransportClientSecurityConfigCreate(
            NnrpTransportClientSecurityConfigRequest request,
            out NnrpHandle config)
        {
            config = new NnrpHandle(NnrpHandleKind.TransportSecurityConfig, 22, 1);
            return request.TransportId == (uint)TransportId.Tcp
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
        }

        private static NnrpFfiStatus TransportServerSecurityConfigCreate(
            NnrpTransportServerSecurityConfigRequest request,
            out NnrpHandle config)
        {
            config = new NnrpHandle(NnrpHandleKind.TransportSecurityConfig, 23, 1);
            return request.TransportId == (uint)TransportId.Tcp
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
        }

        private static NnrpFfiStatus TransportConnect(
            NnrpTransportOpenRequest request,
            out NnrpHandle connection)
        {
            connection = new NnrpHandle(NnrpHandleKind.TransportConnection, 30, 1);
            return request.TransportId == (uint)TransportId.Tcp
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
        }

        private static NnrpFfiStatus TransportListen(
            NnrpTransportOpenRequest request,
            out NnrpHandle listener)
        {
            listener = new NnrpHandle(NnrpHandleKind.TransportListener, 31, 1);
            return request.TransportId == (uint)TransportId.Tcp
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
        }

        private static NnrpFfiStatus TransportAccept(
            NnrpTransportAcceptRequest request,
            out NnrpHandle connection)
        {
            connection = new NnrpHandle(NnrpHandleKind.TransportConnection, 32, 1);
            return request.Listener.Kind == NnrpHandleKind.TransportListener
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
        }

        private static NnrpFfiStatus TransportListenerEndpoint(
            NnrpHandle listener,
            out NnrpHandle buffer,
            out NnrpBufferView endpoint)
        {
            buffer = NnrpHandle.Invalid;
            endpoint = NnrpBufferView.Empty;
            return listener.Kind == NnrpHandleKind.TransportListener
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
        }

        private static NnrpFfiStatus TransportProbe(
            NnrpTransportProbeRequest request,
            out NnrpTransportProbeResult result)
        {
            result = new NnrpTransportProbeResult(
                request.SampleCount,
                request.SampleCount,
                1_000_000,
                100);
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
            return new NnrpSessionOpenRequest(
                new NnrpHandle(NnrpHandleKind.Connection, 1, 1),
                requestedSessionId: 3,
                sessionHandleId: 3,
                generation: 1,
                profileId: 1,
                SessionPriorityClass.Balanced,
                allowResume: true,
                schemaId: 10,
                schemaVersion: 1,
                defaultDeadlineMilliseconds: 500,
                maxInFlightOperations: 4,
                leaseTtlHintMilliseconds: 30_000,
                resumeTokenBytes: 16,
                NnrpU32Slice.Empty);
        }

        private static NnrpFfiStatus SessionOpen(NnrpSessionOpenRequest request, out NnrpHandle session)
        {
            session = new NnrpHandle(NnrpHandleKind.Session, request.RequestedSessionId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpSessionResumeRequest MatchingSessionResumeRequest()
        {
            return new NnrpSessionResumeRequest(
                MatchingSessionOpenRequest(),
                new NnrpBufferView(new IntPtr(1), new UIntPtr(16)));
        }

        private static NnrpFfiStatus ClientResumeSession(
            NnrpSessionResumeRequest request,
            out NnrpHandle session,
            out NnrpSessionRecoveryOutcome outcome)
        {
            session = new NnrpHandle(
                NnrpHandleKind.Session,
                request.Open.SessionHandleId,
                request.Open.Generation);
            outcome = new NnrpSessionRecoveryOutcome(1, 2);
            return request.Open.Connection.Kind == NnrpHandleKind.Connection && request.RecoveryTicket.Length != UIntPtr.Zero
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
        }

        private static NnrpFfiSubmitRequest MatchingSubmitRequest()
        {
            return new NnrpFfiSubmitRequest(
                new NnrpHandle(NnrpHandleKind.Session, 3, 1),
                5,
                7,
                (uint)HeaderFlags.AckRequired,
                2,
                3,
                4,
                NnrpBufferView.Empty);
        }

        private static RuntimeFrameHeader SubmitHeader(uint frameId)
        {
            return new RuntimeFrameHeader(
                MessageType.FrameSubmit,
                HeaderFlags.AckRequired,
                FrameId: frameId,
                ViewId: 2,
                RouteId: 3,
                TraceId: 4);
        }

        private static NnrpClientSubmitResultBatchRequest MatchingBatchSubmitResultRequest()
        {
            return new NnrpClientSubmitResultBatchRequest(
                new NnrpHandle(NnrpHandleKind.Session, 3, 1),
                5,
                7,
                1,
                NnrpBufferView.Empty,
                NnrpBufferView.Empty,
                new UIntPtr(6),
                new UIntPtr(3));
        }

        private static NnrpFfiStatus Submit(NnrpFfiSubmitRequest request, out NnrpHandle operation)
        {
            operation = new NnrpHandle(NnrpHandleKind.Operation, request.OperationId, 1);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus ClientSubmitResultCompactBatch(
            NnrpClientSubmitResultBatchRequest request,
            out NnrpCompactResult lastResult,
            out UIntPtr completed)
        {
            completed = request.Iterations;
            lastResult = new NnrpCompactResult(
                NnrpFfiStatus.Ok,
                1,
                6,
                1,
                new NnrpHandle(NnrpHandleKind.Operation, request.OperationIdStart, 1),
                request.OperationIdStart,
                request.FrameIdStart,
                request.ResultPayload,
                new NnrpFfiDiagnostic(NnrpFfiStatus.Ok));
            return request.Session.Kind == NnrpHandleKind.Session && request.FrameIdStride > 0
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidArgument);
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
                    (uint)MessageType.ResultPush,
                    connection,
                    new NnrpHandle(NnrpHandleKind.Session, 41, 3),
                    new NnrpHandle(NnrpHandleKind.Operation, 99, 1),
                    7,
                    NnrpHandle.Invalid,
                    new NnrpBufferView(EventPayloadHandle.AddrOfPinnedObject(), new UIntPtr((uint)EventPayload.Length)),
                    new NnrpFfiDiagnostic(NnrpFfiStatus.Ok)));
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus ServerBind(NnrpServerBindRequest request, out NnrpHandle server)
        {
            server = new NnrpHandle(NnrpHandleKind.Connection, request.ServerId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpServerAcceptBeginRequest MatchingServerAcceptBeginRequest()
        {
            return new NnrpServerAcceptBeginRequest(
                new NnrpHandle(NnrpHandleKind.Connection, 4, 1),
                3,
                1);
        }

        private static NnrpFfiStatus ServerAcceptBegin(
            NnrpServerAcceptBeginRequest request,
            out NnrpHandle accept)
        {
            accept = new NnrpHandle(NnrpHandleKind.ServerAccept, request.AcceptHandleId, request.Generation);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus ServerAcceptWait(NnrpServerAcceptWaitRequest request)
        {
            return request.Accept.Kind == NnrpHandleKind.ServerAccept
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
        }

        private static NnrpFfiStatus ServerAcceptClaim(
            NnrpServerAcceptClaimRequest request,
            out NnrpServerAcceptResult result)
        {
            result = new NnrpServerAcceptResult(
                new NnrpHandle(NnrpHandleKind.Session, request.SessionHandleId, request.Generation),
                (uint)TransportId.Tcp);
            return NnrpFfiStatus.Ok;
        }

        private static NnrpFfiStatus ServerAcceptRelease(NnrpHandle accept)
        {
            return accept.Kind == NnrpHandleKind.ServerAccept
                ? NnrpFfiStatus.Ok
                : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle);
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
                (byte)PayloadKind.TokenChunk,
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
            ulong grantedAtMilliseconds = 1500,
            uint ttlMilliseconds = 500)
        {
            return new NnrpCacheLeaseResult(
                (uint)outcome,
                new NnrpHandle(NnrpHandleKind.CacheLease, 77, 1),
                objectId,
                9,
                88,
                1,
                ttlMilliseconds,
                3,
                grantedAtMilliseconds);
        }

        private static NnrpFfiStatus CacheQuery(NnrpCacheLeaseRequest request, out NnrpCacheLeaseResult result)
        {
            result = CreateCacheLeaseResult(request.ObjectId);
            return request.Owner.IsValid ? NnrpFfiStatus.Ok : new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle, NnrpErrorFamily.Cache);
        }

        private static NnrpFfiStatus CacheTouch(NnrpCacheLeaseRequest request, out NnrpCacheLeaseResult result)
        {
            result = CreateCacheLeaseResult(
                request.ObjectId,
                grantedAtMilliseconds: request.NowMilliseconds,
                ttlMilliseconds: checked(request.TtlMilliseconds + 1000));
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
                    CreateCacheLeaseResult(
                        objectId,
                        grantedAtMilliseconds: nowMilliseconds,
                        ttlMilliseconds: checked(ttlMilliseconds + (uint)index)),
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
                    0,
                    NnrpHandle.Invalid,
                    NnrpHandle.Invalid,
                    NnrpHandle.Invalid,
                    0,
                    NnrpHandle.Invalid,
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
                    (uint)MessageType.ResultPush,
                    connection,
                    session,
                    operation,
                    frameId,
                    NnrpHandle.Invalid,
                    NnrpBufferView.Empty,
                    new NnrpFfiDiagnostic(NnrpFfiStatus.Ok)));
        }

        private static readonly byte[] EventPayload = new byte[] { 1, 2, 3 };

        private static readonly System.Runtime.InteropServices.GCHandle EventPayloadHandle =
            System.Runtime.InteropServices.GCHandle.Alloc(EventPayload, System.Runtime.InteropServices.GCHandleType.Pinned);

        private sealed class NativeObjectStore : IDisposable
        {
            private readonly Dictionary<ulong, ObjectEntry> _objects = new Dictionary<ulong, ObjectEntry>();
            private readonly Dictionary<ulong, CacheEntry> _cacheReferences = new Dictionary<ulong, CacheEntry>();
            private readonly Dictionary<ulong, PinnedBytes> _buffers = new Dictionary<ulong, PinnedBytes>();
            private ulong _nextId = 100;

            public int ObjectDescriptorCount => _objects.Count;

            public int CacheReferenceDescriptorCount => _cacheReferences.Count;

            public NnrpFfiStatus AcquireMetadataCopy(
                NnrpBufferView source,
                out NnrpHandle buffer,
                out NnrpBufferView view)
            {
                var bytes = new PinnedBytes(CopyBufferView(source));
                buffer = NextHandle(NnrpHandleKind.Buffer);
                _buffers.Add(buffer.Id, bytes);
                view = bytes.View;
                return NnrpFfiStatus.Ok;
            }

            public NnrpFfiStatus MetadataBufferView(NnrpHandle buffer, out NnrpBufferView view)
            {
                PinnedBytes bytes;
                if (buffer.Kind != NnrpHandleKind.Buffer || !_buffers.TryGetValue(buffer.Id, out bytes!))
                {
                    view = NnrpBufferView.Empty;
                    return InvalidHandle();
                }

                view = bytes.View;
                return NnrpFfiStatus.Ok;
            }

            public NnrpFfiStatus MetadataBufferRelease(NnrpHandle buffer)
            {
                PinnedBytes bytes;
                if (buffer.Kind != NnrpHandleKind.Buffer || !_buffers.TryGetValue(buffer.Id, out bytes!))
                {
                    return InvalidHandle();
                }

                _buffers.Remove(buffer.Id);
                bytes.Dispose();
                return NnrpFfiStatus.Ok;
            }

            public NnrpFfiStatus ObjectDescriptorCreate(
                NnrpRuntimeObjectDescriptor descriptor,
                NnrpBufferView metadata,
                out NnrpHandle handle)
            {
                var bytes = new PinnedBytes(CopyBufferView(metadata));
                handle = NextHandle(NnrpHandleKind.ObjectDescriptor);
                _objects.Add(handle.Id, new ObjectEntry(descriptor, bytes));
                return NnrpFfiStatus.Ok;
            }

            public NnrpFfiStatus ObjectDescriptorView(
                NnrpHandle handle,
                out NnrpRuntimeObjectDescriptor descriptor,
                out NnrpBufferView metadata)
            {
                ObjectEntry entry;
                if (handle.Kind != NnrpHandleKind.ObjectDescriptor || !_objects.TryGetValue(handle.Id, out entry!))
                {
                    descriptor = default(NnrpRuntimeObjectDescriptor);
                    metadata = NnrpBufferView.Empty;
                    return InvalidHandle();
                }

                descriptor = entry.Descriptor;
                metadata = entry.Metadata.View;
                return NnrpFfiStatus.Ok;
            }

            public NnrpFfiStatus ObjectDescriptorMetadataSnapshot(
                NnrpHandle handle,
                out NnrpHandle buffer,
                out NnrpBufferView view)
            {
                ObjectEntry entry;
                if (handle.Kind != NnrpHandleKind.ObjectDescriptor || !_objects.TryGetValue(handle.Id, out entry!))
                {
                    buffer = NnrpHandle.Invalid;
                    view = NnrpBufferView.Empty;
                    return InvalidHandle();
                }

                return AcquireSnapshot(entry.Metadata.Bytes, out buffer, out view);
            }

            public NnrpFfiStatus ObjectDescriptorRelease(NnrpHandle handle)
            {
                ObjectEntry entry;
                if (handle.Kind != NnrpHandleKind.ObjectDescriptor || !_objects.TryGetValue(handle.Id, out entry!))
                {
                    return InvalidHandle();
                }

                _objects.Remove(handle.Id);
                entry.Dispose();
                return NnrpFfiStatus.Ok;
            }

            public NnrpFfiStatus CacheReferenceDescriptorCreate(
                NnrpCacheReferenceDescriptor descriptor,
                NnrpBufferView metadata,
                out NnrpHandle handle)
            {
                var bytes = new PinnedBytes(CopyBufferView(metadata));
                handle = NextHandle(NnrpHandleKind.CacheReferenceDescriptor);
                _cacheReferences.Add(handle.Id, new CacheEntry(descriptor, bytes));
                return NnrpFfiStatus.Ok;
            }

            public NnrpFfiStatus CacheReferenceDescriptorView(
                NnrpHandle handle,
                out NnrpCacheReferenceDescriptor descriptor,
                out NnrpBufferView metadata)
            {
                CacheEntry entry;
                if (handle.Kind != NnrpHandleKind.CacheReferenceDescriptor || !_cacheReferences.TryGetValue(handle.Id, out entry!))
                {
                    descriptor = default(NnrpCacheReferenceDescriptor);
                    metadata = NnrpBufferView.Empty;
                    return InvalidHandle();
                }

                descriptor = entry.Descriptor;
                metadata = entry.Metadata.View;
                return NnrpFfiStatus.Ok;
            }

            public NnrpFfiStatus CacheReferenceDescriptorMetadataSnapshot(
                NnrpHandle handle,
                out NnrpHandle buffer,
                out NnrpBufferView view)
            {
                CacheEntry entry;
                if (handle.Kind != NnrpHandleKind.CacheReferenceDescriptor || !_cacheReferences.TryGetValue(handle.Id, out entry!))
                {
                    buffer = NnrpHandle.Invalid;
                    view = NnrpBufferView.Empty;
                    return InvalidHandle();
                }

                return AcquireSnapshot(entry.Metadata.Bytes, out buffer, out view);
            }

            public NnrpFfiStatus CacheReferenceDescriptorRelease(NnrpHandle handle)
            {
                CacheEntry entry;
                if (handle.Kind != NnrpHandleKind.CacheReferenceDescriptor || !_cacheReferences.TryGetValue(handle.Id, out entry!))
                {
                    return InvalidHandle();
                }

                _cacheReferences.Remove(handle.Id);
                entry.Dispose();
                return NnrpFfiStatus.Ok;
            }

            public void Dispose()
            {
                foreach (var entry in _objects.Values)
                {
                    entry.Dispose();
                }

                foreach (var entry in _cacheReferences.Values)
                {
                    entry.Dispose();
                }

                foreach (var buffer in _buffers.Values)
                {
                    buffer.Dispose();
                }

                _objects.Clear();
                _cacheReferences.Clear();
                _buffers.Clear();
            }

            private NnrpFfiStatus AcquireSnapshot(
                byte[] source,
                out NnrpHandle buffer,
                out NnrpBufferView view)
            {
                var bytes = new PinnedBytes((byte[])source.Clone());
                buffer = NextHandle(NnrpHandleKind.Buffer);
                _buffers.Add(buffer.Id, bytes);
                view = bytes.View;
                return NnrpFfiStatus.Ok;
            }

            private NnrpHandle NextHandle(NnrpHandleKind kind)
            {
                return new NnrpHandle(kind, _nextId++, 1);
            }

            private static NnrpFfiStatus InvalidHandle()
            {
                return new NnrpFfiStatus(NnrpFfiStatusCode.InvalidHandle, NnrpErrorFamily.RuntimeObject);
            }

            private sealed class ObjectEntry : IDisposable
            {
                public ObjectEntry(NnrpRuntimeObjectDescriptor descriptor, PinnedBytes metadata)
                {
                    Descriptor = descriptor;
                    Metadata = metadata;
                }

                public NnrpRuntimeObjectDescriptor Descriptor { get; }
                public PinnedBytes Metadata { get; }
                public void Dispose() => Metadata.Dispose();
            }

            private sealed class CacheEntry : IDisposable
            {
                public CacheEntry(NnrpCacheReferenceDescriptor descriptor, PinnedBytes metadata)
                {
                    Descriptor = descriptor;
                    Metadata = metadata;
                }

                public NnrpCacheReferenceDescriptor Descriptor { get; }
                public PinnedBytes Metadata { get; }
                public void Dispose() => Metadata.Dispose();
            }

            private sealed class PinnedBytes : IDisposable
            {
                private GCHandle _handle;

                public PinnedBytes(byte[] bytes)
                {
                    Bytes = bytes;
                    if (bytes.Length > 0)
                    {
                        _handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    }
                }

                public byte[] Bytes { get; }

                public NnrpBufferView View => Bytes.Length == 0
                    ? NnrpBufferView.Empty
                    : new NnrpBufferView(_handle.AddrOfPinnedObject(), new UIntPtr((uint)Bytes.Length));

                public void Dispose()
                {
                    if (_handle.IsAllocated)
                    {
                        _handle.Free();
                    }
                }
            }
        }

    }
}
