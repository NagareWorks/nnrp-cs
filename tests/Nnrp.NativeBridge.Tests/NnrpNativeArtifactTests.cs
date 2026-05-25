using System;
using System.IO;
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
            Assert.Equal(3, result.SdkPreview);
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
    }
}
