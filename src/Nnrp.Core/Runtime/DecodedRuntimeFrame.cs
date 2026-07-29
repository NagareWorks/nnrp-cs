using System;

namespace Nnrp.Runtime
{
    /// <summary>An immutable runtime-frame projection with decoder-owned payload regions.</summary>
    public sealed class DecodedRuntimeFrame
    {
        internal DecodedRuntimeFrame(
            RuntimeFrameHeader header,
            ReadOnlySpan<byte> metadata,
            ReadOnlySpan<byte> body)
        {
            Header = header;
            Metadata = metadata.ToArray();
            Body = body.ToArray();
        }

        public RuntimeFrameHeader Header { get; }

        public ReadOnlyMemory<byte> Metadata { get; }

        public ReadOnlyMemory<byte> Body { get; }
    }
}
