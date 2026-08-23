using System;
using System.Threading;

namespace SceneWorkspace.Editor
{
    /// <summary>Allows at most one workspace switch or recovery sequence in the editor domain.</summary>
    internal static class SceneWorkspaceExecutionGuard
    {
        private static int entered;

        internal static bool TryEnter(out IDisposable lease)
        {
            if (Interlocked.CompareExchange(ref entered, 1, 0) != 0)
            {
                lease = null;
                return false;
            }
            lease = new Lease();
            return true;
        }

        private sealed class Lease : IDisposable
        {
            private int disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                    Interlocked.Exchange(ref entered, 0);
            }
        }
    }
}
