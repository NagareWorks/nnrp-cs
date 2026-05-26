using System;

namespace Nnrp.Core
{
    /// <summary>
    /// Common NNRP message type values.
    /// </summary>
    public enum MessageType : byte
    {
        /// <summary>Client hello, encoded as <c>0x01</c>.</summary>
        ClientHello = 0x01,

        /// <summary>Server hello acknowledgement, encoded as <c>0x02</c>.</summary>
        ServerHelloAck = 0x02,

        /// <summary>Session patch, encoded as <c>0x03</c>.</summary>
        SessionPatch = 0x03,

        /// <summary>Session patch acknowledgement, encoded as <c>0x04</c>.</summary>
        SessionPatchAck = 0x04,

        /// <summary>Close message, encoded as <c>0x05</c>.</summary>
        Close = 0x05,

        /// <summary>Error message, encoded as <c>0x06</c>.</summary>
        Error = 0x06,

        /// <summary>Session open request, encoded as <c>0x07</c>.</summary>
        SessionOpen = 0x07,

        /// <summary>Session open acknowledgement, encoded as <c>0x08</c>.</summary>
        SessionOpenAck = 0x08,

        /// <summary>Session close request, encoded as <c>0x09</c>.</summary>
        SessionClose = 0x09,

        /// <summary>Session close acknowledgement, encoded as <c>0x0A</c>.</summary>
        SessionCloseAck = 0x0A,

        /// <summary>Frame submit message, encoded as <c>0x10</c>.</summary>
        FrameSubmit = 0x10,

        /// <summary>Frame cancel message, encoded as <c>0x11</c>.</summary>
        FrameCancel = 0x11,

        /// <summary>Result push message, encoded as <c>0x12</c>.</summary>
        ResultPush = 0x12,

        /// <summary>Result drop message, encoded as <c>0x13</c>.</summary>
        ResultDrop = 0x13,

        /// <summary>Cache put message, encoded as <c>0x14</c>.</summary>
        CachePut = 0x14,

        /// <summary>Cache acknowledgement, encoded as <c>0x15</c>.</summary>
        CacheAck = 0x15,

        /// <summary>Cache invalidation message, encoded as <c>0x16</c>.</summary>
        CacheInvalidate = 0x16,

        /// <summary>Flow update message, encoded as <c>0x17</c>.</summary>
        FlowUpdate = 0x17,

        /// <summary>Result hint message, encoded as <c>0x18</c>.</summary>
        ResultHint = 0x18,

        /// <summary>Transport probe, encoded as <c>0x19</c>.</summary>
        TransportProbe = 0x19,

        /// <summary>Transport probe acknowledgement, encoded as <c>0x1A</c>.</summary>
        TransportProbeAck = 0x1A,

        /// <summary>Session migrate, encoded as <c>0x1B</c>.</summary>
        SessionMigrate = 0x1B,

        /// <summary>Session migrate acknowledgement, encoded as <c>0x1C</c>.</summary>
        SessionMigrateAck = 0x1C,

        /// <summary>Ping message, encoded as <c>0x20</c>.</summary>
        Ping = 0x20,

        /// <summary>Pong message, encoded as <c>0x21</c>.</summary>
        Pong = 0x21,
    }

    public static class MessageTypeExtensions
    {
        public static bool IsSessionLifecycle(this MessageType messageType)
        {
            return messageType == MessageType.SessionOpen
                || messageType == MessageType.SessionOpenAck
                || messageType == MessageType.SessionClose
                || messageType == MessageType.SessionCloseAck;
        }
    }

    /// <summary>
    /// Session priority class identifiers.
    /// </summary>
    public enum SessionPriorityClass : byte
    {
        /// <summary>Interactive work, encoded as <c>0</c>.</summary>
        Interactive = 0,

        /// <summary>Balanced latency/throughput work, encoded as <c>1</c>.</summary>
        Balanced = 1,

        /// <summary>Background work, encoded as <c>2</c>.</summary>
        Background = 2,
    }

    /// <summary>
    /// SESSION_OPEN request flags.
    /// </summary>
    [Flags]
    public enum SessionFlags : byte
    {
        /// <summary>No session flags, encoded as <c>0x00</c>.</summary>
        None = 0,

        /// <summary>Allow session resume, encoded as <c>0x01</c>.</summary>
        AllowResume = 0x01,

        /// <summary>Allow background results, encoded as <c>0x02</c>.</summary>
        AllowBackgroundResults = 0x02,

        /// <summary>Allow cache leases, encoded as <c>0x04</c>.</summary>
        AllowCacheLeases = 0x04,

        /// <summary>Allow schema override, encoded as <c>0x08</c>.</summary>
        AllowSchemaOverride = 0x08,
    }

    /// <summary>
    /// SESSION_OPEN_ACK status identifiers.
    /// </summary>
    public enum SessionStatus : byte
    {
        /// <summary>Session opened, encoded as <c>0</c>.</summary>
        Opened = 0,

        /// <summary>Session rejected, encoded as <c>1</c>.</summary>
        Rejected = 1,

        /// <summary>Client should retry later, encoded as <c>2</c>.</summary>
        RetryLater = 2,

        /// <summary>Session resumed, encoded as <c>3</c>.</summary>
        Resumed = 3,
    }

    /// <summary>
    /// SESSION_OPEN_ACK negotiated flags.
    /// </summary>
    [Flags]
    public enum SessionAckFlags : uint
    {
        /// <summary>No acknowledgement flags, encoded as <c>0x00000000</c>.</summary>
        None = 0,

        /// <summary>Resume is enabled, encoded as <c>0x00000001</c>.</summary>
        ResumeEnabled = 0x00000001,

        /// <summary>Background results are enabled, encoded as <c>0x00000002</c>.</summary>
        BackgroundResultsEnabled = 0x00000002,

        /// <summary>Cache leases are enabled, encoded as <c>0x00000004</c>.</summary>
        CacheLeasesEnabled = 0x00000004,

        /// <summary>Schema override is enabled, encoded as <c>0x00000008</c>.</summary>
        SchemaOverrideEnabled = 0x00000008,

        /// <summary>Priority was downgraded, encoded as <c>0x00000010</c>.</summary>
        PriorityDowngraded = 0x00000010,
    }

    /// <summary>
    /// Session lifecycle error identifiers.
    /// </summary>
    public enum SessionErrorCode : uint
    {
        /// <summary>No session error, encoded as <c>0</c>.</summary>
        None = 0,

        /// <summary>Authentication failed, encoded as <c>0x00010001</c>.</summary>
        AuthFailed = 0x00010001,

        /// <summary>Profile unsupported, encoded as <c>0x00010002</c>.</summary>
        ProfileUnsupported = 0x00010002,

        /// <summary>Schema unsupported, encoded as <c>0x00010003</c>.</summary>
        SchemaUnsupported = 0x00010003,

        /// <summary>Priority rejected, encoded as <c>0x00010004</c>.</summary>
        PriorityRejected = 0x00010004,

        /// <summary>Lease policy rejected, encoded as <c>0x00010005</c>.</summary>
        LeasePolicyRejected = 0x00010005,

        /// <summary>Resume rejected, encoded as <c>0x00010006</c>.</summary>
        ResumeRejected = 0x00010006,

        /// <summary>Session limit reached, encoded as <c>0x00010007</c>.</summary>
        SessionLimitReached = 0x00010007,
    }

    /// <summary>
    /// SESSION_CLOSE reason identifiers.
    /// </summary>
    public enum SessionCloseReason : ushort
    {
        /// <summary>Normal close, encoded as <c>0</c>.</summary>
        Normal = 0,

        /// <summary>Client shutdown, encoded as <c>1</c>.</summary>
        ClientShutdown = 1,

        /// <summary>Server shutdown, encoded as <c>2</c>.</summary>
        ServerShutdown = 2,

        /// <summary>Idle timeout, encoded as <c>3</c>.</summary>
        IdleTimeout = 3,

        /// <summary>Protocol error, encoded as <c>4</c>.</summary>
        ProtocolError = 4,

        /// <summary>Authentication revoked, encoded as <c>5</c>.</summary>
        AuthRevoked = 5,
    }

    /// <summary>
    /// SESSION_CLOSE in-flight operation policy identifiers.
    /// </summary>
    public enum InFlightPolicy : byte
    {
        /// <summary>Drain in-flight operations, encoded as <c>0</c>.</summary>
        Drain = 0,

        /// <summary>Abort in-flight operations, encoded as <c>1</c>.</summary>
        Abort = 1,
    }

    /// <summary>
    /// SESSION_CLOSE_ACK status identifiers.
    /// </summary>
    public enum SessionCloseStatus : byte
    {
        /// <summary>Close acknowledged, encoded as <c>0</c>.</summary>
        Acknowledged = 0,

        /// <summary>Session is draining, encoded as <c>1</c>.</summary>
        Draining = 1,

        /// <summary>Session is closed, encoded as <c>2</c>.</summary>
        Closed = 2,

        /// <summary>Close rejected, encoded as <c>3</c>.</summary>
        Rejected = 3,
    }

    /// <summary>
    /// Common header flags.
    /// </summary>
    [Flags]
    public enum HeaderFlags : uint
    {
        /// <summary>No flags, encoded as <c>0x00000000</c>.</summary>
        None = 0,

        /// <summary>Acknowledgement required, encoded as <c>0x00000001</c>.</summary>
        AckRequired = 0x00000001,

        /// <summary>Message may be dropped, encoded as <c>0x00000002</c>.</summary>
        CanDrop = 0x00000002,

        /// <summary>Payload is stale, encoded as <c>0x00000004</c>.</summary>
        Stale = 0x00000004,

        /// <summary>End of stream, encoded as <c>0x00000008</c>.</summary>
        Eos = 0x00000008,

        /// <summary>Retransmitted message, encoded as <c>0x00000010</c>.</summary>
        Retransmit = 0x00000010,

        /// <summary>Keyframe message, encoded as <c>0x00000020</c>.</summary>
        Keyframe = 0x00000020,
    }

    /// <summary>
    /// Frame class values.
    /// </summary>
    public enum FrameClass : byte
    {
        /// <summary>Keyframe, encoded as <c>0</c>.</summary>
        Keyframe = 0,

        /// <summary>Delta frame, encoded as <c>1</c>.</summary>
        Delta = 1,

        /// <summary>Retransmit frame, encoded as <c>2</c>.</summary>
        Retransmit = 2,

        /// <summary>Discardable frame, encoded as <c>3</c>.</summary>
        Discardable = 3,
    }

    /// <summary>
    /// Input profile values for frame submit metadata.
    /// </summary>
    public enum InputProfile : byte
    {
        /// <summary>Unspecified input profile, encoded as <c>0</c>.</summary>
        Unspecified = 0,

        /// <summary>Changed luma tiles only, encoded as <c>1</c>.</summary>
        ChangedTilesLuma = 1,

        /// <summary>Dense luma frame, encoded as <c>2</c>.</summary>
        DenseLumaFrame = 2,
    }

    /// <summary>
    /// Tile index encoding mode values.
    /// </summary>
    public enum TileIndexMode : byte
    {
        /// <summary>Dense range, encoded as <c>0</c>.</summary>
        DenseRange = 0,

        /// <summary>Raw unsigned 16-bit tile ids, encoded as <c>1</c>.</summary>
        RawUInt16 = 1,

        /// <summary>Delta unsigned 16-bit tile ids, encoded as <c>2</c>.</summary>
        DeltaUInt16 = 2,

        /// <summary>Bitset tile ids, encoded as <c>3</c>.</summary>
        Bitset = 3,
    }

    /// <summary>
    /// Tensor section role values.
    /// </summary>
    public enum TensorRole : ushort
    {
        /// <summary>Luma hint input, encoded as <c>0x0001</c>.</summary>
        LumaHint = 0x0001,

        /// <summary>Depth input, encoded as <c>0x0002</c>.</summary>
        Depth = 0x0002,

        /// <summary>Normal input, encoded as <c>0x0003</c>.</summary>
        Normal = 0x0003,

        /// <summary>Motion vector input, encoded as <c>0x0004</c>.</summary>
        Motion = 0x0004,

        /// <summary>Roughness and metalness input, encoded as <c>0x0005</c>.</summary>
        RoughMetal = 0x0005,

        /// <summary>Super-resolution residual result, encoded as <c>0x0100</c>.</summary>
        SrResidual = 0x0100,

        /// <summary>Detail residual result, encoded as <c>0x0101</c>.</summary>
        DetailResidual = 0x0101,
    }

    /// <summary>
    /// Tensor section codec values.
    /// </summary>
    public enum CodecId : byte
    {
        /// <summary>Raw uncompressed payload, encoded as <c>0</c>.</summary>
        Raw = 0,

        /// <summary>LZ4 block-compressed payload, encoded as <c>1</c>.</summary>
        Lz4 = 1,
    }

    /// <summary>
    /// Tensor scalar data type values.
    /// </summary>
    public enum DTypeId : byte
    {
        /// <summary>16-bit floating point, encoded as <c>0</c>.</summary>
        Float16 = 0,

        /// <summary>32-bit floating point, encoded as <c>1</c>.</summary>
        Float32 = 1,

        /// <summary>FP8 E4M3, encoded as <c>2</c>.</summary>
        Float8E4M3 = 2,

        /// <summary>FP8 E5M2, encoded as <c>3</c>.</summary>
        Float8E5M2 = 3,

        /// <summary>Signed 8-bit integer, encoded as <c>4</c>.</summary>
        Int8 = 4,

        /// <summary>Unsigned 8-bit integer, encoded as <c>5</c>.</summary>
        UInt8 = 5,

        /// <summary>Signed 16-bit integer, encoded as <c>6</c>.</summary>
        Int16 = 6,

        /// <summary>Unsigned 16-bit integer, encoded as <c>7</c>.</summary>
        UInt16 = 7,
    }

    /// <summary>
    /// Tensor memory layout values.
    /// </summary>
    public enum TensorLayoutId : byte
    {
        /// <summary>NHWC layout, encoded as <c>0</c>.</summary>
        Nhwc = 0,

        /// <summary>NCHW layout, encoded as <c>1</c>.</summary>
        Nchw = 1,
    }

    /// <summary>
    /// Quantization scale policy values.
    /// </summary>
    public enum ScalePolicy : byte
    {
        /// <summary>No scale policy, encoded as <c>0</c>.</summary>
        None = 0,

        /// <summary>One scale for the whole tensor section, encoded as <c>1</c>.</summary>
        PerTensor = 1,

        /// <summary>One scale per tile, encoded as <c>2</c>.</summary>
        PerTile = 2,

        /// <summary>One scale per channel, encoded as <c>3</c>.</summary>
        PerChannel = 3,
    }

    /// <summary>
    /// Result status code values.
    /// </summary>
    public enum ResultStatusCode : ushort
    {
        /// <summary>Successful result, encoded as <c>0</c>.</summary>
        Success = 0,

        /// <summary>Successful but degraded result, encoded as <c>1</c>.</summary>
        Degraded = 1,

        /// <summary>Rejected frame result, encoded as <c>2</c>.</summary>
        Rejected = 2,

        /// <summary>Failed frame result, encoded as <c>3</c>.</summary>
        Failed = 3,
    }

    /// <summary>
    /// Result flag values.
    /// </summary>
    [Flags]
    public enum ResultFlags : ushort
    {
        /// <summary>No result flags, encoded as <c>0x0000</c>.</summary>
        None = 0,

        /// <summary>Result is stale, encoded as <c>0x0001</c>.</summary>
        Stale = 0x0001,

        /// <summary>Result used fallback behavior, encoded as <c>0x0002</c>.</summary>
        Fallback = 0x0002,

        /// <summary>Result is partial, encoded as <c>0x0004</c>.</summary>
        Partial = 0x0004,
    }

    /// <summary>
    /// Budget policy flags applied to a frame submission or result.
    /// </summary>
    [Flags]
    public enum BudgetPolicy : byte
    {
        /// <summary>No budget policy, encoded as <c>0x00</c>.</summary>
        None = 0,

        /// <summary>Allow partial results, encoded as <c>0x01</c>.</summary>
        AllowPartial = 0x01,

        /// <summary>Allow stale result reuse, encoded as <c>0x02</c>.</summary>
        AllowStaleReuse = 0x02,

        /// <summary>Allow degraded results, encoded as <c>0x04</c>.</summary>
        AllowDegraded = 0x04,

        /// <summary>Allow result dropping, encoded as <c>0x08</c>.</summary>
        AllowDrop = 0x08,
    }

    /// <summary>
    /// Submit mode values.
    /// </summary>
    public enum SubmitMode : byte
    {
        /// <summary>All objects and payload are inline, encoded as <c>0</c>.</summary>
        Inline = 0,

        /// <summary>Objects are referenced from cache, encoded as <c>1</c>.</summary>
        Reference = 1,

        /// <summary>Inline and referenced objects are mixed, encoded as <c>2</c>.</summary>
        Mixed = 2,
    }

    /// <summary>
    /// Result classification values.
    /// </summary>
    public enum ResultClass : byte
    {
        /// <summary>Complete result, encoded as <c>0</c>.</summary>
        Complete = 0,

        /// <summary>Partial result, encoded as <c>1</c>.</summary>
        Partial = 1,

        /// <summary>Reused stale result, encoded as <c>2</c>.</summary>
        StaleReuse = 2,

        /// <summary>Degraded result, encoded as <c>3</c>.</summary>
        Degraded = 3,
    }

    /// <summary>
    /// Payload kind bitmap values.
    /// </summary>
    [Flags]
    public enum PayloadKind : uint
    {
        /// <summary>Tensor payload, encoded as <c>0x00000001</c>.</summary>
        Tensor = 0x00000001,

        /// <summary>Token chunk payload, encoded as <c>0x00000002</c>.</summary>
        TokenChunk = 0x00000002,

        /// <summary>Audio chunk payload, encoded as <c>0x00000004</c>.</summary>
        AudioChunk = 0x00000004,

        /// <summary>Video chunk payload, encoded as <c>0x00000008</c>.</summary>
        VideoChunk = 0x00000008,

        /// <summary>Structured event payload, encoded as <c>0x00000010</c>.</summary>
        StructuredEvent = 0x00000010,

        /// <summary>Tool delta payload, encoded as <c>0x00000020</c>.</summary>
        ToolDelta = 0x00000020,

        /// <summary>Opaque byte payload, encoded as <c>0x00000040</c>.</summary>
        OpaqueBytes = 0x00000040,
    }

    /// <summary>
    /// Protocol error code values.
    /// </summary>
    public enum ErrorCode : ushort
    {
        /// <summary>Unsupported version, encoded as <c>0x0001</c>.</summary>
        UnsupportedVersion = 0x0001,

        /// <summary>Authentication failed, encoded as <c>0x0002</c>.</summary>
        AuthFailed = 0x0002,

        /// <summary>Invalid state, encoded as <c>0x0003</c>.</summary>
        InvalidState = 0x0003,

        /// <summary>Malformed header, encoded as <c>0x0004</c>.</summary>
        MalformedHeader = 0x0004,

        /// <summary>Malformed body, encoded as <c>0x0005</c>.</summary>
        MalformedBody = 0x0005,

        /// <summary>Unsupported capability, encoded as <c>0x0006</c>.</summary>
        UnsupportedCapability = 0x0006,

        /// <summary>Limit exceeded, encoded as <c>0x0007</c>.</summary>
        LimitExceeded = 0x0007,

        /// <summary>Frame expired, encoded as <c>0x0008</c>.</summary>
        FrameExpired = 0x0008,

        /// <summary>Frame cancelled, encoded as <c>0x0009</c>.</summary>
        FrameCancelled = 0x0009,

        /// <summary>Cache miss, encoded as <c>0x000A</c>.</summary>
        CacheMiss = 0x000A,

        /// <summary>Server busy, encoded as <c>0x000B</c>.</summary>
        ServerBusy = 0x000B,

        /// <summary>Internal error, encoded as <c>0x000C</c>.</summary>
        InternalError = 0x000C,
    }

    /// <summary>
    /// Transport identifiers.
    /// </summary>
    public enum TransportId : uint
    {
        /// <summary>Transport unspecified, encoded as <c>0</c>.</summary>
        Unspecified = 0,

        /// <summary>QUIC transport, encoded as <c>1</c>.</summary>
        Quic = 1,

        /// <summary>TCP transport, encoded as <c>2</c>.</summary>
        Tcp = 2,
    }

    /// <summary>
    /// Transport policy identifiers.
    /// </summary>
    public enum TransportPolicy : byte
    {
        /// <summary>Auto-select transport from probe results, encoded as <c>0</c>.</summary>
        Auto = 0,

        /// <summary>Prefer QUIC when probing, encoded as <c>1</c>.</summary>
        PreferQuic = 1,

        /// <summary>Prefer TCP when probing, encoded as <c>2</c>.</summary>
        PreferTcp = 2,

        /// <summary>Force QUIC without probing, encoded as <c>3</c>.</summary>
        ForceQuic = 3,

        /// <summary>Force TCP without probing, encoded as <c>4</c>.</summary>
        ForceTcp = 4,
    }

    /// <summary>
    /// Session-level loss tolerance preference identifiers.
    /// </summary>
    public enum LossTolerance : byte
    {
        /// <summary>Strict reliability, encoded as <c>0</c>.</summary>
        Strict = 0,

        /// <summary>Best effort reliability, encoded as <c>1</c>.</summary>
        BestEffort = 1,

        /// <summary>Favor low latency over perfect delivery, encoded as <c>2</c>.</summary>
        LowLatency = 2,

        /// <summary>Fire-and-forget delivery, encoded as <c>3</c>.</summary>
        FireAndForget = 3,
    }

    /// <summary>
    /// FLOW_UPDATE scope identifiers.
    /// </summary>
    public enum FlowUpdateScopeKind : byte
    {
        /// <summary>Connection-scoped update, encoded as <c>0</c>.</summary>
        Connection = 0,

        /// <summary>Session-scoped update, encoded as <c>1</c>.</summary>
        Session = 1,

        /// <summary>Operation-scoped update, encoded as <c>2</c>.</summary>
        Operation = 2,
    }

    /// <summary>
    /// FLOW_UPDATE reason identifiers.
    /// </summary>
    public enum FlowUpdateReason : byte
    {
        /// <summary>Grant additional credit, encoded as <c>0</c>.</summary>
        Grant = 0,

        /// <summary>Reduce available credit, encoded as <c>1</c>.</summary>
        Reduce = 1,

        /// <summary>Pause new work, encoded as <c>2</c>.</summary>
        Pause = 2,

        /// <summary>Resume paused work, encoded as <c>3</c>.</summary>
        Resume = 3,

        /// <summary>Report congestion-driven throttling, encoded as <c>4</c>.</summary>
        Congestion = 4,
    }

    /// <summary>
    /// FLOW_UPDATE backpressure levels.
    /// </summary>
    public enum FlowUpdateBackpressureLevel : byte
    {
        /// <summary>No backpressure, encoded as <c>0</c>.</summary>
        None = 0,

        /// <summary>Soft backpressure, encoded as <c>1</c>.</summary>
        Soft = 1,

        /// <summary>Hard backpressure, encoded as <c>2</c>.</summary>
        Hard = 2,
    }

    /// <summary>
    /// FLOW_UPDATE flags.
    /// </summary>
    [Flags]
    public enum FlowUpdateFlags : uint
    {
        /// <summary>No flags, encoded as <c>0x00000000</c>.</summary>
        None = 0,

        /// <summary>Credit fields are authoritative, encoded as <c>0x00000001</c>.</summary>
        CreditValid = 0x00000001,

        /// <summary>Retry-after field is authoritative, encoded as <c>0x00000002</c>.</summary>
        RetryAfterValid = 0x00000002,

        /// <summary>Apply only to background work, encoded as <c>0x00000004</c>.</summary>
        BackgroundOnly = 0x00000004,

        /// <summary>Only drain in-flight work, encoded as <c>0x00000008</c>.</summary>
        DrainInFlightOnly = 0x00000008,
    }

    /// <summary>
    /// RESULT_HINT applied budget policy identifiers.
    /// </summary>
    public enum ResultHintBudgetPolicy : uint
    {
        /// <summary>No applied budget policy, encoded as <c>0</c>.</summary>
        None = 0,

        /// <summary>Full-quality policy, encoded as <c>1</c>.</summary>
        Full = 1,

        /// <summary>Partial-result policy, encoded as <c>2</c>.</summary>
        Partial = 2,

        /// <summary>Stale-reuse policy, encoded as <c>3</c>.</summary>
        StaleReuse = 3,

        /// <summary>Drop policy, encoded as <c>4</c>.</summary>
        Drop = 4,
    }

    /// <summary>
    /// RESULT_HINT congestion state identifiers.
    /// </summary>
    public enum ResultHintCongestionState : uint
    {
        /// <summary>No congestion state hint, encoded as <c>0</c>.</summary>
        None = 0,

        /// <summary>Steady-state congestion, encoded as <c>1</c>.</summary>
        Steady = 1,

        /// <summary>Elevated congestion, encoded as <c>2</c>.</summary>
        Elevated = 2,

        /// <summary>Saturated congestion, encoded as <c>3</c>.</summary>
        Saturated = 3,
    }

    /// <summary>
    /// RESULT_HINT reason identifiers.
    /// </summary>
    public enum ResultHintReason : uint
    {
        /// <summary>No reason hint, encoded as <c>0</c>.</summary>
        None = 0,

        /// <summary>Queue full, encoded as <c>1</c>.</summary>
        QueueFull = 1,

        /// <summary>Server busy, encoded as <c>2</c>.</summary>
        ServerBusy = 2,

        /// <summary>Budget exceeded, encoded as <c>3</c>.</summary>
        BudgetExceeded = 3,

        /// <summary>Result superseded, encoded as <c>4</c>.</summary>
        Superseded = 4,
    }
}
