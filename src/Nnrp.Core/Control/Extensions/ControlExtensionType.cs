namespace Nnrp.Core
{
    /// <summary>
    /// Well-known control extension type codes for the active NNRP/1 wire.
    /// Bit 15 (0x8000) is reserved for the critical flag and must not be
    /// included in these enum values.
    /// </summary>
    public enum ControlExtensionType : ushort
    {
        /// <summary>No extension / terminator.</summary>
        None = 0x0000,

        /// <summary>Inference scheduling hint (optional).</summary>
        ScheduleHint = 0x0001,

        /// <summary>Capability renegotiation request (optional).</summary>
        RenegotiateCapabilities = 0x0002,

        /// <summary>Authenticated session rekey (critical).</summary>
        SessionRekey = 0x0003,

        /// <summary>Server-side model hot-swap notification (critical).</summary>
        ModelHotSwap = 0x0004,

        /// <summary>Known-value cache preload descriptor (optional).</summary>
        CachePreload = 0x0005,

        /// <summary>Client session loss tolerance declaration (optional).</summary>
        ClientLossTolerance = 0x0103,

        /// <summary>Server session loss tolerance acknowledgement (optional).</summary>
        ServerLossToleranceAck = 0x0104,

        /// <summary>Client payload capabilities declaration (optional).</summary>
        ClientPayloadCapabilities = 0x0105,

        /// <summary>Server payload capabilities acknowledgement (optional).</summary>
        ServerPayloadCapabilitiesAck = 0x0106,

        /// <summary>Client transport policy declaration (optional).</summary>
        ClientTransportPolicy = 0x0101,

        /// <summary>Server transport policy acknowledgement (optional).</summary>
        ServerTransportPolicyAck = 0x0102,
    }
}
