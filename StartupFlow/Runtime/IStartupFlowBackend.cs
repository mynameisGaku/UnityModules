using System;
using System.Threading;

namespace StartupFlow
{
    /// <summary>main-thread、終了token、observer例外ログをテストから置き換える内部境界。</summary>
    internal interface IStartupFlowBackend
    {
        /// <summary>現在のthreadがUnityメインスレッドならtrue。</summary>
        bool IsMainThread { get; }

        /// <summary>Play Modeまたはアプリケーション終了時にcancelされるtoken。</summary>
        CancellationToken ExitToken { get; }

        /// <summary>利用側observerの例外をflow本体へ戻さず記録する。</summary>
        void LogObserverException(Exception exception);
    }
}
