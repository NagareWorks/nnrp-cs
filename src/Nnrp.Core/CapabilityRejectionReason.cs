namespace Nnrp.Core
{
    public enum CapabilityRejectionReason
    {
        None = 0,
        InvalidClientCapabilities = 1,
        InvalidServerCapabilities = 2,
        NoCommonCodec = 3,
        NoCommonDType = 4,
        NoCommonTensorLayout = 5,
        MaxViewsExceeded = 6,
        NoCommonPayloadKind = 7,
    }
}
