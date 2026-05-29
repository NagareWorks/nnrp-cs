using System;

namespace Nnrp.Core
{
    public static class ExtensionFrameRegionValidator
    {
        public static bool TryValidateAndCollectSkippableFrames(
            ReadOnlyMemory<byte> descriptorRegion,
            ReadOnlyMemory<byte> payloadRegion,
            out ExtensionFrameDescriptor[] descriptors,
            out NnrpParseError error)
        {
            descriptors = Array.Empty<ExtensionFrameDescriptor>();
            if (!TypedPayloadRegionValidator.TryValidateExtensionDescriptorRegion(
                    descriptorRegion,
                    payloadRegion,
                    out var parsedDescriptors,
                    out error))
            {
                return false;
            }

            for (var index = 0; index < parsedDescriptors.Length; index++)
            {
                if ((parsedDescriptors[index].ExtensionFlags & 0x0001) != 0)
                {
                    descriptors = Array.Empty<ExtensionFrameDescriptor>();
                    error = NnrpParseError.UnsupportedExtension;
                    return false;
                }
            }

            descriptors = parsedDescriptors;
            error = NnrpParseError.None;
            return true;
        }
    }
}
