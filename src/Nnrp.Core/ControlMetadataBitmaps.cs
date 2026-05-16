using System;
using System.Collections.Generic;

namespace Nnrp.Core
{
    public static class ControlMetadataBitmaps
    {
        public const uint CurrentWireFormatBitmap = 1u << NnrpHeader.CurrentWireFormat;
        public const uint TensorProfileBitmap = 0x00000001;
        public const uint TensorPayloadKindBitmap = (uint)PayloadKind.Tensor;
        public const uint SupportedTileIndexBitmap = 0x0000000F;
        public const uint CacheDigestBitmap = 0x00000001;
        public const uint TensorProfileCacheObjectBitmap = 0x00000007;
        public const uint LowFrequencyObjectBitmap = 0x0000003F;
        public const uint CacheObjectBitmap = TensorProfileCacheObjectBitmap;
        public const uint TileLayoutId = 1;
        public const uint DefaultQualityTier = 2;
        public const uint DefaultCacheBytes = 1024 * 1024;

        public static uint BuildCacheObjectBitmap(params CacheObjectKind[] objectKinds)
        {
            uint bitmap = 0;
            if (objectKinds == null)
            {
                return 0;
            }

            foreach (var objectKind in objectKinds)
            {
                var bitIndex = (int)objectKind - 1;
                if (bitIndex < 0 || bitIndex >= sizeof(uint) * 8)
                {
                    throw new ArgumentOutOfRangeException(nameof(objectKinds), objectKind, "Cache object kind is out of bitmap range.");
                }

                bitmap |= 1u << bitIndex;
            }

            return bitmap;
        }

        public static uint EncodeCurrentWireFormatBitmap()
        {
            return CurrentWireFormatBitmap;
        }

        public static uint EncodeCodecBitmap(IEnumerable<CodecId> codecs)
        {
            return EncodeEnumBitmap(codecs);
        }

        public static uint EncodeDTypeBitmap(IEnumerable<DTypeId> dtypes)
        {
            return EncodeEnumBitmap(dtypes);
        }

        public static uint EncodeTensorLayoutBitmap(IEnumerable<TensorLayoutId> layouts)
        {
            return EncodeEnumBitmap(layouts);
        }

        public static T[] DecodeCodecBitmap<T>(uint bitmap) where T : struct, Enum
        {
            return DecodeEnumBitmap<T>(bitmap);
        }

        private static uint EncodeEnumBitmap<T>(IEnumerable<T> values) where T : struct, Enum
        {
            uint bitmap = 0;
            if (values == null)
            {
                return 0;
            }

            foreach (var value in values)
            {
                var bitIndex = Convert.ToInt32(value);
                if (bitIndex < 0 || bitIndex >= sizeof(uint) * 8)
                {
                    throw new ArgumentOutOfRangeException(nameof(values), value, "Bitmap field value is out of range.");
                }

                bitmap |= 1u << bitIndex;
            }

            return bitmap;
        }

        private static T[] DecodeEnumBitmap<T>(uint bitmap) where T : struct, Enum
        {
            var values = new List<T>();
            foreach (T value in Enum.GetValues(typeof(T)))
            {
                var bitIndex = Convert.ToInt32(value);
                if (bitIndex < 0 || bitIndex >= sizeof(uint) * 8)
                {
                    continue;
                }

                if ((bitmap & (1u << bitIndex)) != 0)
                {
                    values.Add(value);
                }
            }

            return values.ToArray();
        }
    }
}
