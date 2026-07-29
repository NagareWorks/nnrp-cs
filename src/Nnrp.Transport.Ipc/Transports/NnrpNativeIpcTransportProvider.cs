using System.Runtime.InteropServices;
using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Transport.Ipc
{
    public sealed class NnrpNativeIpcTransportProvider : NnrpNativeTransportProvider
    {
        public static NnrpNativeIpcTransportProvider Instance { get; } =
            new NnrpNativeIpcTransportProvider();

        public NnrpNativeIpcTransportProvider(
            string? artifactPath = null,
            string? artifactRoot = null,
            NnrpNativePlatform? platform = null)
            : base(
                NnrpNativeIpcRuntime.CreateDescriptor(artifactPath, platform),
                "ipc",
                NnrpNativeArtifact.TransportSlotIpc,
                artifactPath,
                artifactRoot,
                platform)
        {
        }
    }

    internal static class NnrpNativeIpcRuntime
    {
        internal static NnrpTransportProviderDescriptor CreateDescriptor(
            string? artifactPath,
            NnrpNativePlatform? platform)
        {
            var isWindows = platform.HasValue
                ? platform.Value.OsName == "windows"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            var endpointLimitation = isWindows
                ? NnrpTransportProviderLimitation.WindowsNamedPipe
                : NnrpTransportProviderLimitation.UnixDomainSocket;
            return new NnrpTransportProviderDescriptor(
                "ipc",
                "1.0.0-preview.4",
                TransportId.Ipc,
                NnrpTransportProviderKind.NativeDynamic,
                true,
                artifactPath,
                new NnrpTransportProviderMetadata(
                    "nnrp.transport.ipc.native",
                    new NnrpTransportProviderCost(0, 0),
                    0,
                    new NnrpTransportProviderLimits(67_108_864),
                    new[]
                    {
                        NnrpTransportProviderLimitation.LocalHostOnly,
                        NnrpTransportProviderLimitation.NativeHostOnly,
                        endpointLimitation,
                    }));
        }
    }
}
