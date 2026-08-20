using System.Threading;
using UnityEngine;

namespace StartupFlow
{
    /// <summary>Unity初期化callbackで確定したメインスレッドをfail-closedで判定する。</summary>
    internal static class StartupFlowMainThread
    {
        private static int _threadId;

        /// <summary>現在のthreadが初期化済みUnityメインスレッドならtrue。</summary>
        internal static bool IsCurrent => Volatile.Read(ref _threadId) != 0 && Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref _threadId);

        /// <summary>Domain Reloadを無効にしたPlay開始でも前回のthread情報を破棄する。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Volatile.Write(ref _threadId, 0);

        /// <summary>最初のScene処理より前にUnityメインスレッドを記録する。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bind() => Volatile.Write(ref _threadId, Thread.CurrentThread.ManagedThreadId);

        /// <summary>PlayMode回帰testから初期化状態を再現する。</summary>
        internal static void BindForTests() => Bind();
    }
}
