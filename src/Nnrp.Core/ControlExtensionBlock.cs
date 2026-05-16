using System;

namespace Nnrp.Core
{
    /// <summary>
    /// A single control extension TLV (Type-Length-Value) block used to carry
    /// low-frequency optional or critical extensions within control messages.
    /// </summary>
    /// <remarks>
    /// <para>Wire layout (little-endian, 8-byte header + value + zero padding):</para>
    /// <list type="table">
    ///   <item><term>extension_type</term><description>uint16 — raw extension type code</description></item>
    ///   <item><term>extension_flags</term><description>uint16 — bit 0 indicates a critical extension</description></item>
    ///   <item><term>extension_length</term><description>uint32 — value length in bytes</description></item>
    ///   <item><term>extension_value</term><description>N bytes</description></item>
    ///   <item><term>padding</term><description>0-7 zero bytes so the next extension is 8-byte aligned</description></item>
    /// </list>
    /// </remarks>
    public readonly struct ControlExtensionBlock
    {
        /// <summary>Bitmask indicating a critical (must-understand) extension in the in-memory type field.</summary>
        public const ushort CriticalFlag = 0x8000;

        /// <summary>Wire flag value indicating a critical extension.</summary>
        public const ushort WireCriticalFlag = 0x0001;

        /// <summary>Wire alignment in bytes for control extensions.</summary>
        public const int Alignment = 8;

        /// <summary>Header size in bytes: type (2) + flags (2) + length (4).</summary>
        public const int HeaderSize = 8;

        public ControlExtensionBlock(ushort extensionType, ReadOnlyMemory<byte> value)
        {
            ExtensionType = extensionType;
            Value = value;
        }

        public ControlExtensionBlock(ControlExtensionType type, ReadOnlyMemory<byte> value)
            : this((ushort)type, value)
        {
        }

        /// <summary>Raw extension type value (bit 15 = critical).</summary>
        public ushort ExtensionType { get; }

        /// <summary>Extension payload bytes.</summary>
        public ReadOnlyMemory<byte> Value { get; }

        /// <summary>The length of the extension value in bytes.</summary>
        public uint Length => checked((uint)Value.Length);

        /// <summary>Total wire size of this TLV block (header + value + zero padding).</summary>
        public int TotalLength => HeaderSize + Value.Length + PaddingLength;

        /// <summary>Padding bytes added so the next extension begins on an 8-byte boundary.</summary>
        public int PaddingLength => GetPaddingLength(Value.Length);

        /// <summary>True when the critical flag (bit 15) is set.</summary>
        public bool IsCritical => (ExtensionType & CriticalFlag) != 0;

        /// <summary>The extension type without the critical flag.</summary>
        public ushort TypeCode => (ushort)(ExtensionType & ~CriticalFlag);

        /// <summary>Cast to the typed enum representation.</summary>
        public ControlExtensionType TypedType => (ControlExtensionType)ExtensionType;

        public void WriteTo(Span<byte> destination)
        {
            if (destination.Length < TotalLength)
            {
                throw new ArgumentException("Destination buffer is too small.", nameof(destination));
            }

            destination.Slice(0, TotalLength).Clear();
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(destination, TypeCode);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
                destination.Slice(2),
                IsCritical ? WireCriticalFlag : (ushort)0);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(4), Length);
            Value.Span.CopyTo(destination.Slice(HeaderSize, Value.Length));
        }

        public byte[] ToArray()
        {
            var buffer = new byte[TotalLength];
            WriteTo(buffer);
            return buffer;
        }

        /// <summary>
        /// Attempt to parse one TLV block from <paramref name="source"/>.
        /// On success, <paramref name="bytesConsumed"/> is the total number of
        /// bytes consumed (header + value).
        /// </summary>
        public static bool TryParse(
            ReadOnlySpan<byte> source,
            out ControlExtensionBlock block,
            out int bytesConsumed,
            out NnrpParseError error)
        {
            block = default;
            bytesConsumed = 0;
            error = NnrpParseError.None;

            if (source.Length < HeaderSize)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            var extensionType = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(source);
            var extensionFlags = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(2));
            var extensionLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(4));

            if ((extensionFlags & ~WireCriticalFlag) != 0)
            {
                error = NnrpParseError.UnsupportedExtension;
                return false;
            }

            if (extensionLength > int.MaxValue)
            {
                error = NnrpParseError.MessageTooLarge;
                return false;
            }

            var paddingLength = GetPaddingLength((int)extensionLength);
            var total = HeaderSize + (int)extensionLength + paddingLength;
            if (source.Length < total)
            {
                error = NnrpParseError.SourceTooShort;
                return false;
            }

            for (var i = 0; i < paddingLength; i++)
            {
                if (source[HeaderSize + (int)extensionLength + i] != 0)
                {
                    error = NnrpParseError.NonZeroReservedField;
                    return false;
                }
            }

            block = new ControlExtensionBlock(
                (ushort)(extensionType | ((extensionFlags & WireCriticalFlag) != 0 ? CriticalFlag : 0)),
                source.Slice(HeaderSize, (int)extensionLength).ToArray());
            bytesConsumed = total;
            return true;
        }

        private static int GetPaddingLength(int payloadLength)
        {
            return (Alignment - (payloadLength % Alignment)) % Alignment;
        }
    }
}
