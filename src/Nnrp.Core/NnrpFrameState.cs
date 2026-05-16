namespace Nnrp.Core
{
    public enum NnrpFrameState : byte
    {
        Announced = 0,
        Submitted = 1,
        Processing = 2,
        Ready = 3,
        Delivered = 4,
        Dropped = 5,
        Cancelled = 6,
        Expired = 7,
    }
}
