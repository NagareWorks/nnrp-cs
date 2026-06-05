using System;

namespace Nnrp.Core
{
    public readonly struct ObjectReferenceRegionView
    {
        public ObjectReferenceRegionView(ObjectReferenceBlock[] blocks)
        {
            Blocks = blocks ?? Array.Empty<ObjectReferenceBlock>();
        }

        public ObjectReferenceBlock[] Blocks { get; }
    }
}
