using System;

namespace Nnrp.Core
{
    public readonly struct InlineObjectBlockView
    {
        public InlineObjectBlockView(InlineObjectBlockHeader header, ReadOnlyMemory<byte> payload)
        {
            Header = header;
            Payload = payload;
        }

        public InlineObjectBlockHeader Header { get; }

        public ReadOnlyMemory<byte> Payload { get; }
    }
}
