using System;
using System.Threading;
using UnityEngine;

namespace SceneFlow
{
    /// <summary>Unity自身の初期化callbackで確定したメインスレッドをScene API境界で検査する。</summary>
    internal static class SceneFlowMainThread
    {
        private static int _threadId;

        /// <summary>現在がUnity callbackで確定済みのメインスレッドならtrue。</summary>
        public static bool IsCurrent =>
            _threadId != 0 &&
            Thread.CurrentThread.ManagedThreadId == _threadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BindRuntimeThread() => BindFromUnityCallback();

        /// <summary>Unityのmain-thread初期化callbackから現在のthread idを記録する。</summary>
        internal static void BindFromUnityCallback()
        {
            var context = SynchronizationContext.Current;
            if (context == null) throw new InvalidOperationException("Unityのメインスレッド同期contextを確認できません。");

            _threadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>現在がUnity callbackで確定済みのメインスレッドでなければ生成を拒否する。</summary>
        internal static void RequireCurrent()
        {
            if (!IsCurrent) throw new InvalidOperationException("SceneFlowServiceはUnityメインスレッドから生成してください。");
        }
    }
}
