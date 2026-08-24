// SPDX-License-Identifier: MIT

using System.Threading;
using UnityEngine;

namespace PlayerOptions
{
    /// <summary>Unity初期化callbackで確定したmain threadをfail-closedで判定する。</summary>
    internal static class PlayerOptionsMainThread
    {
        private static int _threadId;

        /// <summary>現在threadが初期化済みUnity main threadならtrue。</summary>
        internal static bool IsCurrent
        {
            get
            {
                var threadId = Volatile.Read(ref _threadId);
                return threadId != 0 && Thread.CurrentThread.ManagedThreadId == threadId;
            }
        }

        /// <summary>Domain Reloadを無効にしたPlay開始でも現在のUnity main threadを最初に記録する。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BindAtSubsystemRegistration() => Bind();

        /// <summary>最初のScene処理より前にUnity main threadを記録する。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bind() => Volatile.Write(ref _threadId, Thread.CurrentThread.ManagedThreadId);

        /// <summary>test fixtureからmain thread初期化状態を再現する。</summary>
        internal static void BindForTests() => Bind();
    }
}
