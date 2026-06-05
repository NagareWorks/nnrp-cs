using Nnrp.Core;
using Nnrp.NativeBridge;

namespace Nnrp.Transport.Quic
{
    public static class NnrpNativeQuicRuntime
    {
        public const string BindingName = "native-quic";

        public const uint NativeTransportSlot = NnrpNativeArtifact.TransportSlotQuic;

        public static TransportId TransportId => Nnrp.Core.TransportId.Quic;

        public static NnrpNativeRuntimeSessionHost OpenSessionHost(NnrpNativeQuicRuntimeSessionHostOptions options)
        {
            return NnrpNativeRuntimeSessionHost.Open(ToNativeOptions(options));
        }

        public static NnrpNativeRuntimeSessionHost OpenSessionHost(
            INnrpNativeRuntimeBackend backend,
            NnrpNativeQuicRuntimeSessionHostOptions options)
        {
            return NnrpNativeRuntimeSessionHost.Open(backend, ToNativeOptions(options));
        }

        public static NnrpNativeRuntimeConnectionHost OpenConnectionHost(
            NnrpNativeQuicRuntimeConnectionHostOptions options)
        {
            return NnrpNativeRuntimeConnectionHost.Open(ToNativeOptions(options));
        }

        public static NnrpNativeRuntimeConnectionHost OpenConnectionHost(
            INnrpNativeRuntimeBackend backend,
            NnrpNativeQuicRuntimeConnectionHostOptions options)
        {
            return NnrpNativeRuntimeConnectionHost.Open(backend, ToNativeOptions(options));
        }

        public static NnrpNativeRuntimeServerHost OpenServerHost(NnrpNativeQuicRuntimeServerHostOptions options)
        {
            return NnrpNativeRuntimeServerHost.Open(ToNativeOptions(options));
        }

        public static NnrpNativeRuntimeServerHost OpenServerHost(
            NnrpNativeRuntimeEntrypoints entrypoints,
            NnrpNativeQuicRuntimeServerHostOptions options)
        {
            return NnrpNativeRuntimeServerHost.Open(entrypoints, ToNativeOptions(options));
        }

        public static NnrpNativeRuntimeSessionHostOptions ToNativeOptions(
            NnrpNativeQuicRuntimeSessionHostOptions options)
        {
            return (options ?? throw new System.ArgumentNullException(nameof(options))).ToNativeOptions();
        }

        public static NnrpNativeRuntimeConnectionHostOptions ToNativeOptions(
            NnrpNativeQuicRuntimeConnectionHostOptions options)
        {
            return (options ?? throw new System.ArgumentNullException(nameof(options))).ToNativeOptions();
        }

        public static NnrpNativeRuntimeServerHostOptions ToNativeOptions(
            NnrpNativeQuicRuntimeServerHostOptions options)
        {
            return (options ?? throw new System.ArgumentNullException(nameof(options))).ToNativeOptions();
        }
    }

    public sealed class NnrpNativeQuicRuntimeSessionHostOptions
    {
        public NnrpNativeQuicRuntimeSessionHostOptions(
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
                (uint)NnrpNativeQuicRuntime.TransportId,
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

    public sealed class NnrpNativeQuicRuntimeConnectionHostOptions
    {
        public NnrpNativeQuicRuntimeConnectionHostOptions(ulong connectionId, uint connectionGeneration)
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
                (uint)NnrpNativeQuicRuntime.TransportId)
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

    public sealed class NnrpNativeQuicRuntimeServerHostOptions
    {
        public NnrpNativeQuicRuntimeServerHostOptions(ulong serverId, uint serverGeneration)
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
                (uint)NnrpNativeQuicRuntime.TransportId)
            {
                ArtifactPath = ArtifactPath,
                ArtifactRoot = ArtifactRoot,
                Platform = Platform,
            };
        }
    }
}
