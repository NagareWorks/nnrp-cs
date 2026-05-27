using System;

namespace Nnrp.Core
{
    public static class TypedPayloadRegionValidator
    {
        public static bool TryValidateTypedPayloadRegion(
            PayloadKind payloadKindBitmap,
            ushort payloadFrameCount,
            ReadOnlyMemory<byte> descriptorRegion,
            ReadOnlyMemory<byte> payloadRegion,
            out TypedPayloadDescriptor[] descriptors,
            out NnrpParseError error)
        {
            descriptors = Array.Empty<TypedPayloadDescriptor>();
            error = NnrpParseError.None;

            var expectedDescriptorBytes = checked((int)(payloadFrameCount * TypedPayloadDescriptor.DescriptorLength));
            if (descriptorRegion.Length != expectedDescriptorBytes)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (payloadFrameCount == 0)
            {
                if (!descriptorRegion.IsEmpty || !payloadRegion.IsEmpty)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                return true;
            }

            var parsedDescriptors = new TypedPayloadDescriptor[payloadFrameCount];
            uint nextExpectedOffset = 0;
            for (var index = 0; index < payloadFrameCount; index++)
            {
                if (!TypedPayloadDescriptor.TryParse(
                        descriptorRegion.Slice(index * TypedPayloadDescriptor.DescriptorLength, TypedPayloadDescriptor.DescriptorLength).Span,
                        strict: true,
                        out var descriptor,
                        out error))
                {
                    descriptors = Array.Empty<TypedPayloadDescriptor>();
                    return false;
                }

                descriptor = descriptor.WithPayloadKind(ResolvePayloadKind(payloadKindBitmap, descriptor));
                var rawPayloadKind = (uint)descriptor.PayloadKind;
                if (rawPayloadKind == 0
                    || (rawPayloadKind & (rawPayloadKind - 1)) != 0
                    || (rawPayloadKind & ~(uint)payloadKindBitmap) != 0)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    descriptors = Array.Empty<TypedPayloadDescriptor>();
                    return false;
                }

                if (!TryValidateDescriptorProfile(payloadKindBitmap, descriptor, out error))
                {
                    descriptors = Array.Empty<TypedPayloadDescriptor>();
                    return false;
                }

                if (descriptor.PayloadOffset != nextExpectedOffset)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    descriptors = Array.Empty<TypedPayloadDescriptor>();
                    return false;
                }

                if (!CheckedArithmetic.TryAdd(descriptor.PayloadOffset, descriptor.PayloadLength, out var payloadEnd)
                    || payloadEnd > payloadRegion.Length)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    descriptors = Array.Empty<TypedPayloadDescriptor>();
                    return false;
                }

                parsedDescriptors[index] = descriptor;
                nextExpectedOffset = payloadEnd;
            }

            if (!TryValidateTypedPayloadDescriptors(
                    payloadKindBitmap,
                    payloadFrameCount,
                    parsedDescriptors,
                    payloadRegion,
                    out error))
            {
                descriptors = Array.Empty<TypedPayloadDescriptor>();
                return false;
            }

            descriptors = parsedDescriptors;
            return true;
        }

        public static bool TryValidateTypedPayloadDescriptors(
            PayloadKind payloadKindBitmap,
            ushort payloadFrameCount,
            ReadOnlySpan<TypedPayloadDescriptor> descriptors,
            ReadOnlyMemory<byte> payloadRegion,
            out NnrpParseError error)
        {
            error = NnrpParseError.None;

            if (descriptors.Length != payloadFrameCount)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (payloadFrameCount == 0)
            {
                if (!payloadRegion.IsEmpty)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                return true;
            }

            uint nextExpectedOffset = 0;
            for (var index = 0; index < descriptors.Length; index++)
            {
                var descriptor = descriptors[index];
                var rawPayloadKind = (uint)descriptor.PayloadKind;
                if (rawPayloadKind == 0
                    || (rawPayloadKind & (rawPayloadKind - 1)) != 0
                    || (rawPayloadKind & ~(uint)payloadKindBitmap) != 0)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                if (!TryValidateDescriptorProfile(payloadKindBitmap, descriptor, out error))
                {
                    return false;
                }

                if (descriptor.PayloadOffset != nextExpectedOffset)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                if (!CheckedArithmetic.TryAdd(descriptor.PayloadOffset, descriptor.PayloadLength, out var payloadEnd)
                    || payloadEnd > payloadRegion.Length)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                nextExpectedOffset = payloadEnd;
            }

            if (nextExpectedOffset != payloadRegion.Length)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (!TrySummarizeProfileCoverage(descriptors, out _, out var descriptorPayloadKindBitmap, out error))
            {
                return false;
            }

            var metadataNonTensorPayloadKindBitmap = payloadKindBitmap & ~PayloadKind.Tensor;
            var descriptorNonTensorPayloadKindBitmap = descriptorPayloadKindBitmap & ~PayloadKind.Tensor;
            if (descriptorNonTensorPayloadKindBitmap != metadataNonTensorPayloadKindBitmap)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            return true;
        }

        public static bool TrySummarizeProfileCoverage(
            ReadOnlySpan<TypedPayloadDescriptor> descriptors,
            out TypedPayloadProfileCoverage[] coverages,
            out PayloadKind payloadKindBitmap,
            out NnrpParseError error)
        {
            coverages = Array.Empty<TypedPayloadProfileCoverage>();
            payloadKindBitmap = 0;
            error = NnrpParseError.None;

            if (descriptors.Length == 0)
            {
                return true;
            }

            var aggregatedCoverages = new TypedPayloadProfileCoverage[descriptors.Length];
            var aggregatedCount = 0;

            for (var index = 0; index < descriptors.Length; index++)
            {
                var descriptor = descriptors[index];
                var rawPayloadKind = (uint)descriptor.PayloadKind;
                if (rawPayloadKind == 0
                    || (rawPayloadKind & (rawPayloadKind - 1)) != 0
                    || !PayloadKindValidator.IsDefinedBitmap(descriptor.PayloadKind))
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    coverages = Array.Empty<TypedPayloadProfileCoverage>();
                    payloadKindBitmap = 0;
                    return false;
                }

                payloadKindBitmap |= descriptor.PayloadKind;

                var entryIndex = -1;
                for (var coverageIndex = 0; coverageIndex < aggregatedCount; coverageIndex++)
                {
                    if (aggregatedCoverages[coverageIndex].PayloadKind == descriptor.PayloadKind
                        && aggregatedCoverages[coverageIndex].ProfileId == descriptor.ProfileId)
                    {
                        entryIndex = coverageIndex;
                        break;
                    }
                }

                if (entryIndex < 0)
                {
                    aggregatedCoverages[aggregatedCount] = new TypedPayloadProfileCoverage(
                        descriptor.PayloadKind,
                        descriptor.ProfileId,
                        frameCount: 1,
                        payloadBytes: descriptor.PayloadLength);
                    aggregatedCount++;
                    continue;
                }

                var existingCoverage = aggregatedCoverages[entryIndex];
                if (existingCoverage.FrameCount == ushort.MaxValue
                    || !CheckedArithmetic.TryAdd(existingCoverage.PayloadBytes, descriptor.PayloadLength, out var aggregatedPayloadBytes))
                {
                    error = NnrpParseError.MessageTooLarge;
                    coverages = Array.Empty<TypedPayloadProfileCoverage>();
                    payloadKindBitmap = 0;
                    return false;
                }

                aggregatedCoverages[entryIndex] = new TypedPayloadProfileCoverage(
                    existingCoverage.PayloadKind,
                    existingCoverage.ProfileId,
                    checked((ushort)(existingCoverage.FrameCount + 1)),
                    aggregatedPayloadBytes);
            }

            if (aggregatedCount == aggregatedCoverages.Length)
            {
                coverages = aggregatedCoverages;
                return true;
            }

            coverages = new TypedPayloadProfileCoverage[aggregatedCount];
            Array.Copy(aggregatedCoverages, coverages, aggregatedCount);
            return true;
        }

        public static bool TryProjectTypedPayloadFrames(
            PayloadKind payloadKindBitmap,
            ushort payloadFrameCount,
            ReadOnlySpan<TypedPayloadDescriptor> descriptors,
            ReadOnlyMemory<byte> payloadRegion,
            out TypedPayloadFrameView[] frames,
            out NnrpParseError error)
        {
            frames = Array.Empty<TypedPayloadFrameView>();
            if (!TryValidateTypedPayloadDescriptors(payloadKindBitmap, payloadFrameCount, descriptors, payloadRegion, out error))
            {
                return false;
            }

            if (descriptors.Length == 0)
            {
                error = NnrpParseError.None;
                return true;
            }

            var projectedFrames = new TypedPayloadFrameView[descriptors.Length];
            for (var index = 0; index < descriptors.Length; index++)
            {
                var descriptor = descriptors[index];
                projectedFrames[index] = new TypedPayloadFrameView(
                    descriptor,
                    payloadRegion.Slice(checked((int)descriptor.PayloadOffset), checked((int)descriptor.PayloadLength)));
            }

            frames = projectedFrames;
            error = NnrpParseError.None;
            return true;
        }

        public static bool TryValidateExtensionDescriptorRegion(
            ReadOnlyMemory<byte> descriptorRegion,
            ReadOnlyMemory<byte> payloadRegion,
            out ExtensionFrameDescriptor[] descriptors,
            out NnrpParseError error)
        {
            descriptors = Array.Empty<ExtensionFrameDescriptor>();
            error = NnrpParseError.None;

            if (descriptorRegion.Length == 0)
            {
                if (!payloadRegion.IsEmpty)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    return false;
                }

                return true;
            }

            if (descriptorRegion.Length % ExtensionFrameDescriptor.DescriptorLength != 0)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            var descriptorCount = descriptorRegion.Length / ExtensionFrameDescriptor.DescriptorLength;
            var parsedDescriptors = new ExtensionFrameDescriptor[descriptorCount];
            uint nextExpectedOffset = 0;
            for (var index = 0; index < descriptorCount; index++)
            {
                if (!ExtensionFrameDescriptor.TryParse(
                        descriptorRegion.Slice(index * ExtensionFrameDescriptor.DescriptorLength, ExtensionFrameDescriptor.DescriptorLength).Span,
                        strict: true,
                        out var descriptor,
                        out error))
                {
                    descriptors = Array.Empty<ExtensionFrameDescriptor>();
                    return false;
                }

                if (descriptor.PayloadOffset != nextExpectedOffset)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    descriptors = Array.Empty<ExtensionFrameDescriptor>();
                    return false;
                }

                if (!CheckedArithmetic.TryAdd(descriptor.PayloadOffset, descriptor.PayloadLength, out var payloadEnd)
                    || payloadEnd > payloadRegion.Length)
                {
                    error = NnrpParseError.InvalidMessageLayout;
                    descriptors = Array.Empty<ExtensionFrameDescriptor>();
                    return false;
                }

                parsedDescriptors[index] = descriptor;
                nextExpectedOffset = payloadEnd;
            }

            if (nextExpectedOffset != payloadRegion.Length)
            {
                error = NnrpParseError.InvalidMessageLayout;
                descriptors = Array.Empty<ExtensionFrameDescriptor>();
                return false;
            }

            descriptors = parsedDescriptors;
            return true;
        }

        private static bool TryValidateDescriptorProfile(
            PayloadKind payloadKindBitmap,
            TypedPayloadDescriptor descriptor,
            out NnrpParseError error)
        {
            var nonTensorPayloads = payloadKindBitmap & ~PayloadKind.Tensor;
            if (nonTensorPayloads != 0 && descriptor.ProfileId == TypedPayloadDescriptor.ProfileTensor)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (nonTensorPayloads == PayloadKind.TokenChunk && descriptor.ProfileId != TypedPayloadDescriptor.ProfileToken)
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            if (descriptor.ProfileId == TypedPayloadDescriptor.ProfileUnspecified
                && (descriptor.SchemaId != 0 || descriptor.SchemaVersion != 0))
            {
                error = NnrpParseError.InvalidMessageLayout;
                return false;
            }

            error = NnrpParseError.None;
            return true;
        }

        private static PayloadKind ResolvePayloadKind(PayloadKind payloadKindBitmap, TypedPayloadDescriptor descriptor)
        {
            if (descriptor.ProfileId == TypedPayloadDescriptor.ProfileTensor)
            {
                return PayloadKind.Tensor;
            }

            if (payloadKindBitmap == PayloadKind.TokenChunk && descriptor.ProfileId == TypedPayloadDescriptor.ProfileToken)
            {
                return PayloadKind.TokenChunk;
            }

            var nonTensorPayloadKindBitmap = payloadKindBitmap & ~PayloadKind.Tensor;
            var rawNonTensorPayloadKindBitmap = (uint)nonTensorPayloadKindBitmap;
            if (rawNonTensorPayloadKindBitmap != 0
                && (rawNonTensorPayloadKindBitmap & (rawNonTensorPayloadKindBitmap - 1)) == 0)
            {
                return nonTensorPayloadKindBitmap;
            }

            var rawPayloadKindBitmap = (uint)payloadKindBitmap;
            if (rawPayloadKindBitmap != 0 && (rawPayloadKindBitmap & (rawPayloadKindBitmap - 1)) == 0)
            {
                return payloadKindBitmap;
            }

            return descriptor.PayloadKind;
        }
    }
}
