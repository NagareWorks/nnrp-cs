using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Transport.Tcp
{
    public static class NnrpNativeTcpRuntime
    {
        public const string BindingName = "native-tcp";

        public const uint NativeTransportSlot = NnrpNativeArtifact.TransportSlotTcp;

        public static TransportId TransportId => Nnrp.Core.TransportId.Tcp;

        public static NnrpNativeRuntimeSessionHost OpenSessionHost(NnrpNativeTcpRuntimeSessionHostOptions options)
        {
            return NnrpNativeRuntimeSessionHost.Open(ToNativeOptions(options));
        }

        public static NnrpNativeRuntimeSessionHost OpenSessionHost(
            INnrpNativeRuntimeBackend backend,
            NnrpNativeTcpRuntimeSessionHostOptions options)
        {
            return NnrpNativeRuntimeSessionHost.Open(backend, ToNativeOptions(options));
        }

        public static NnrpNativeRuntimeConnectionHost OpenConnectionHost(
            NnrpNativeTcpRuntimeConnectionHostOptions options)
        {
            return NnrpNativeRuntimeConnectionHost.Open(ToNativeOptions(options));
        }

        public static NnrpNativeRuntimeConnectionHost OpenConnectionHost(
            INnrpNativeRuntimeBackend backend,
            NnrpNativeTcpRuntimeConnectionHostOptions options)
        {
            return NnrpNativeRuntimeConnectionHost.Open(backend, ToNativeOptions(options));
        }

        public static NnrpNativeRuntimeServerHost OpenServerHost(NnrpNativeTcpRuntimeServerHostOptions options)
        {
            return NnrpNativeRuntimeServerHost.Open(ToNativeOptions(options));
        }

        public static NnrpNativeRuntimeServerHost OpenServerHost(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpNativeTcpRuntimeServerHostOptions options)
        {
            return NnrpNativeRuntimeServerHost.Open(entrypoints, ToNativeOptions(options));
        }

        public static NnrpNativeRuntimeSessionHostOptions ToNativeOptions(
            NnrpNativeTcpRuntimeSessionHostOptions options)
        {
            return (options ?? throw new System.ArgumentNullException(nameof(options))).ToNativeOptions();
        }

        public static NnrpNativeRuntimeConnectionHostOptions ToNativeOptions(
            NnrpNativeTcpRuntimeConnectionHostOptions options)
        {
            return (options ?? throw new System.ArgumentNullException(nameof(options))).ToNativeOptions();
        }

        public static NnrpNativeRuntimeServerHostOptions ToNativeOptions(
            NnrpNativeTcpRuntimeServerHostOptions options)
        {
            return (options ?? throw new System.ArgumentNullException(nameof(options))).ToNativeOptions();
        }
    }

    public sealed class NnrpNativeTcpRuntimeSessionHostOptions
    {
        public NnrpNativeTcpRuntimeSessionHostOptions(
            ulong connectionId,
            uint connectionGeneration,
            uint sessionId,
            uint sessionGeneration,
            ushort profileId,
            uint schemaId,
            uint schemaVersion)
        {
            ConnectionId = connectionId;
            ConnectionGeneration = connectionGeneration;
            SessionId = sessionId;
            SessionGeneration = sessionGeneration;
            ProfileId = profileId;
            SchemaId = schemaId;
            SchemaVersion = schemaVersion;
        }

        public ulong ConnectionId { get; }

        public uint ConnectionGeneration { get; }

        public uint SessionId { get; }

        public uint SessionGeneration { get; }

        public ushort ProfileId { get; }

        public uint SchemaId { get; }

        public uint SchemaVersion { get; }

        public bool BootstrapConnection { get; set; }

        public string? ArtifactPath { get; set; }

        public string? ArtifactRoot { get; set; }

        public NnrpNativePlatform? Platform { get; set; }

        public INnrpNativeRuntimeBackend? FallbackBackend { get; set; }

        public NnrpNativeRuntimeFallbackPolicy FallbackPolicy { get; set; } =
            NnrpNativeRuntimeFallbackPolicy.RequireNative;

        public NnrpNativeRuntimeSessionHostOptions ToNativeOptions()
        {
            return new NnrpNativeRuntimeSessionHostOptions(
                ConnectionId,
                ConnectionGeneration,
                (uint)NnrpNativeTcpRuntime.TransportId,
                SessionId,
                SessionGeneration,
                ProfileId,
                SchemaId,
                SchemaVersion)
            {
                BootstrapConnection = BootstrapConnection,
                ArtifactPath = ArtifactPath,
                ArtifactRoot = ArtifactRoot,
                Platform = Platform,
                FallbackBackend = FallbackBackend,
                FallbackPolicy = FallbackPolicy,
            };
        }
    }

    public sealed class NnrpNativeTcpRuntimeConnectionHostOptions
    {
        public NnrpNativeTcpRuntimeConnectionHostOptions(ulong connectionId, uint connectionGeneration)
        {
            ConnectionId = connectionId;
            ConnectionGeneration = connectionGeneration;
        }

        public ulong ConnectionId { get; }

        public uint ConnectionGeneration { get; }

        public bool BootstrapConnection { get; set; }

        public string? ArtifactPath { get; set; }

        public string? ArtifactRoot { get; set; }

        public NnrpNativePlatform? Platform { get; set; }

        public INnrpNativeRuntimeBackend? FallbackBackend { get; set; }

        public NnrpNativeRuntimeFallbackPolicy FallbackPolicy { get; set; } =
            NnrpNativeRuntimeFallbackPolicy.RequireNative;

        public NnrpNativeRuntimeConnectionHostOptions ToNativeOptions()
        {
            return new NnrpNativeRuntimeConnectionHostOptions(
                ConnectionId,
                ConnectionGeneration,
                (uint)NnrpNativeTcpRuntime.TransportId)
            {
                BootstrapConnection = BootstrapConnection,
                ArtifactPath = ArtifactPath,
                ArtifactRoot = ArtifactRoot,
                Platform = Platform,
                FallbackBackend = FallbackBackend,
                FallbackPolicy = FallbackPolicy,
            };
        }
    }

    public sealed class NnrpNativeTcpRuntimeServerHostOptions
    {
        public NnrpNativeTcpRuntimeServerHostOptions(ulong serverId, uint serverGeneration)
        {
            ServerId = serverId;
            ServerGeneration = serverGeneration;
        }

        public ulong ServerId { get; }

        public uint ServerGeneration { get; }

        public string? ArtifactPath { get; set; }

        public string? ArtifactRoot { get; set; }

        public NnrpNativePlatform? Platform { get; set; }

        public NnrpNativeRuntimeServerHostOptions ToNativeOptions()
        {
            return new NnrpNativeRuntimeServerHostOptions(
                ServerId,
                ServerGeneration,
                (uint)NnrpNativeTcpRuntime.TransportId)
            {
                ArtifactPath = ArtifactPath,
                ArtifactRoot = ArtifactRoot,
                Platform = Platform,
            };
        }
    }
}
