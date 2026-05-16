using System;

namespace Nnrp.Core
{
    public readonly struct BodyView
    {
        public BodyView(
            BodyRegionPrelude prelude,
            ReadOnlyMemory<byte> inlineObjectRegion,
            ReadOnlyMemory<byte> objectReferenceRegion,
            ReadOnlyMemory<byte> typedPayloadDescriptorRegion,
            ReadOnlyMemory<byte> typedPayloadFrameRegion,
            ReadOnlyMemory<byte> extensionDescriptorRegion,
            ReadOnlyMemory<byte> extensionPayloadRegion)
        {
            Prelude = prelude;
            InlineObjectRegion = inlineObjectRegion;
            ObjectReferenceRegion = objectReferenceRegion;
            TypedPayloadDescriptorRegion = typedPayloadDescriptorRegion;
            TypedPayloadFrameRegion = typedPayloadFrameRegion;
            ExtensionDescriptorRegion = extensionDescriptorRegion;
            ExtensionPayloadRegion = extensionPayloadRegion;
        }

        public BodyRegionPrelude Prelude { get; }

        public ReadOnlyMemory<byte> InlineObjectRegion { get; }

        public ReadOnlyMemory<byte> ObjectReferenceRegion { get; }

        public ReadOnlyMemory<byte> TypedPayloadDescriptorRegion { get; }

        public ReadOnlyMemory<byte> TypedPayloadFrameRegion { get; }

        public ReadOnlyMemory<byte> ExtensionDescriptorRegion { get; }

        public ReadOnlyMemory<byte> ExtensionPayloadRegion { get; }
    }
}
