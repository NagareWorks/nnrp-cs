using System;
using System.Threading;

namespace Nnrp.NativeBridge
{
    internal static class NnrpRuntimeHandleIdAllocator
    {
        private static long nextId;

        internal static ulong Allocate()
        {
            var allocated = Interlocked.Increment(ref nextId);
            if (allocated <= 0)
            {
                throw new InvalidOperationException("The process-wide native runtime handle id allocator is exhausted.");
            }

            return checked((ulong)allocated);
        }

        internal static uint AllocateSession()
        {
            var allocated = Allocate();
            if (allocated > uint.MaxValue)
            {
                throw new InvalidOperationException("The process-wide native runtime session id allocator is exhausted.");
            }

            return (uint)allocated;
        }
    }
}
