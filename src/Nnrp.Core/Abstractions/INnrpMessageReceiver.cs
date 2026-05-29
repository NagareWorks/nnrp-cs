using System.Threading;
using System.Threading.Tasks;

namespace Nnrp.Core
{
    /// <summary>
    /// Transport-neutral asynchronous receiver for framed NNRP messages.
    /// Implementations define their own concurrency guarantees and should observe cancellation while waiting for I/O.
    /// </summary>
    public interface INnrpMessageReceiver
    {
        ValueTask<NnrpFramedMessage> ReceiveAsync(CancellationToken cancellationToken);
    }
}
