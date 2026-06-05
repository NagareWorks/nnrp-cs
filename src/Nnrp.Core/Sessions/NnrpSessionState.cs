namespace Nnrp.Core
{
    public enum NnrpSessionState : byte
    {
        Init = 0,
        Negotiating = 1,
        Active = 2,
        Draining = 3,
        Closed = 4,
    }
}
