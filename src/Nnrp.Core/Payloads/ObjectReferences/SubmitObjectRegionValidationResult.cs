using System;

namespace Nnrp.Core
{
    public readonly struct SubmitObjectRegionValidationResult
    {
        public SubmitObjectRegionValidationResult(
            InlineObjectBlockView[] inlineBlocks,
            ObjectReferenceBlock[] objectReferenceBlocks)
        {
            InlineBlocks = inlineBlocks ?? Array.Empty<InlineObjectBlockView>();
            ObjectReferenceBlocks = objectReferenceBlocks ?? Array.Empty<ObjectReferenceBlock>();
        }

        public InlineObjectBlockView[] InlineBlocks { get; }

        public ObjectReferenceBlock[] ObjectReferenceBlocks { get; }
    }
}
