using System;
using System.Threading;

namespace BuildAssistant.Editor
{
    internal static class ExecutionGuard
    {
        private static int running;

        internal static bool TryEnter(out IDisposable lease)
        {
            if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
            {
                lease = null;
                return false;
            }

            lease = new Lease();
            return true;
        }

        internal static bool IsRunning => Volatile.Read(ref running) != 0;

        private sealed class Lease : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                Interlocked.Exchange(ref running, 0);
            }
        }
    }
}
