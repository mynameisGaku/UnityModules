using System;
using System.Threading;

namespace SceneWorkspace.Editor
{
    /// <summary>エディター領域内で、切り替えまたは復元処理を同時に一つだけ許可します。</summary>
    internal static class SceneWorkspaceExecutionGuard
    {
        /// <summary>処理中の場合は1、待機中の場合は0です。</summary>
        private static int entered;

        /// <summary>未使用の場合だけ処理権を取得し、終了時に解放する所有物を返します。</summary>
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

        /// <summary>処理権を一度だけ解放する所有物です。</summary>
        private sealed class Lease : IDisposable
        {
            /// <summary>すでに解放した場合は1、未解放の場合は0です。</summary>
            private int disposed;

            /// <summary>処理権を一度だけ解放します。</summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                    Interlocked.Exchange(ref entered, 0);
            }
        }
    }
}
