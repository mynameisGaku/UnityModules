using System;
using System.Threading;
using UnityEngine;

namespace StartupFlow
{
    /// <summary>Unity runtimeのthread、終了token、Consoleへ接続する内部境界。</summary>
    internal sealed class UnityStartupFlowBackend : IStartupFlowBackend
    {
        /// <summary>現在のthreadがUnityメインスレッドならtrue。</summary>
        public bool IsMainThread => StartupFlowMainThread.IsCurrent;

        /// <summary>Play Modeまたはアプリケーション終了時にcancelされるtoken。</summary>
        public CancellationToken ExitToken => Application.exitCancellationToken;

        /// <summary>observer例外をUnity Consoleへ記録する。</summary>
        public void LogObserverException(Exception exception) => Debug.LogException(exception);
    }
}
