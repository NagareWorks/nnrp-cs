namespace Nnrp.Core
{
    /// <summary>
    /// Bidirectional transport-neutral NNRP message boundary.
    /// </summary>
    public interface INnrpMessageTransport : INnrpMessageSender, INnrpMessageReceiver
    {
    }
}
