namespace Nnrp.Core
{
    public enum NnrpParseError
    {
        None = 0,
        SourceTooShort = 1,
        DestinationTooShort = 2,
        InvalidMagic = 3,
        InvalidHeaderLength = 4,
        UnsupportedVersion = 5,
        UnknownWireFormat = 6,
        UnknownMessageType = 7,
        ReservedFlagsSet = 8,
        MessageTooLarge = 9,
        NonZeroReservedField = 10,
        InconsistentSectionDescriptor = 11,
        InvalidTileIndexBlock = 12,
        InvalidMessageLayout = 13,

        /// <summary>Unknown critical control extension.</summary>
        UnsupportedExtension = 14,
    }
}
