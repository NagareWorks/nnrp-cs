using Nnrp.Core;

namespace Nnrp.Runtime
{
    /// <summary>Caller-controlled fields from the fixed NNRP common header.</summary>
    public readonly record struct RuntimeFrameHeader(
        MessageType MessageType,
        HeaderFlags Flags = HeaderFlags.None,
        uint SessionId = 0,
        uint FrameId = 0,
        ushort ViewId = 0,
        ushort RouteId = 0,
        ulong TraceId = 0,
        byte VersionMajor = NnrpHeader.CurrentVersionMajor,
        byte WireFormat = NnrpHeader.CurrentWireFormat);
}
