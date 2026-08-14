using System.Threading;
using UnityEngine;

namespace DiagnosticsContext
{
    /// <summary>Unity初期化中に記録したメインスレッドを副作用なしで判定する。</summary>
    internal static class DiagnosticsMainThread
    {
        /// <summary>Unityメインスレッドのmanaged thread ID。未初期化時は0。</summary>
        private static int _threadId;

#if UNITY_EDITOR
        /// <summary>Play前のEditMode API利用に備えてEditorメインスレッドを記録する。</summary>
        [UnityEditor.InitializeOnLoadMethod]
        private static void BindEditorMainThread()
        {
            BindCurrentThread();
        }
#endif

        /// <summary>domain reload設定にかかわらずPlay開始前に現在threadを記録する。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BindRuntimeMainThread()
        {
            BindCurrentThread();
        }

        /// <summary>現在threadが記録済みUnityメインスレッドならtrueを返す。</summary>
        internal static bool IsCurrent => Thread.CurrentThread.ManagedThreadId == Volatile.Read(ref _threadId);

        /// <summary>現在threadをメインスレッドとして上書き記録する。</summary>
        private static void BindCurrentThread()
        {
            Volatile.Write(ref _threadId, Thread.CurrentThread.ManagedThreadId);
        }

        /// <summary>独立した決定論的testで現在threadを明示的に再記録する。</summary>
        internal static void BindCurrentThreadForTesting()
        {
            BindCurrentThread();
        }

        /// <summary>first-touch回帰testで明示初期化前のfail-closed状態へ戻す。</summary>
        internal static void ResetForTesting()
        {
            Volatile.Write(ref _threadId, 0);
        }
    }
}
