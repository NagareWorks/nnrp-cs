using System.Threading;
using System.Threading.Tasks;

namespace Nnrp.Core
{
    /// <summary>
    /// Transport-neutral asynchronous sender for framed NNRP messages.
    /// Implementations define their own concurrency guarantees and should observe cancellation before starting new I/O.
    /// </summary>
    public interface INnrpMessageSender
    {
        ValueTask SendAsync(NnrpFramedMessage message, CancellationToken cancellationToken);
    }
}
