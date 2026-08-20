using System;
using System.Threading;

namespace StartupFlow
{
    /// <summary>1回のstep実行へキャンセル状態と進捗通知を渡す、実行期間限定のcontext。</summary>
    public sealed class StartupStepContext
    {
        private Func<StartupStepContext, float, StartupFlowError> _reportProgress;

        internal StartupStepContext(string stepId, CancellationToken cancellationToken, Func<StartupStepContext, float, StartupFlowError> reportProgress)
        {
            StepId = stepId ?? string.Empty;
            CancellationToken = cancellationToken;
            _reportProgress = reportProgress ?? throw new ArgumentNullException(nameof(reportProgress));
        }

        /// <summary>このcontextが属するstep識別子。</summary>
        public string StepId { get; }

        /// <summary>利用側の中止要求とアプリケーション終了をまとめたtoken。</summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>利用側の中止要求またはアプリケーション終了を受け取った場合にtrue。</summary>
        public bool IsCancellationRequested => CancellationToken.IsCancellationRequested;

        /// <summary>現在stepの進捗を通知する。0以上1以下で、同じstep内では減少できない。</summary>
        /// <param name="progress">有限の0以上1以下の進捗。</param>
        /// <returns>通知できた場合はNone。無効値、thread違反、通知中、または期限切れなら理由を返す。</returns>
        public StartupFlowError ReportProgress(float progress)
        {
            var reporter = Volatile.Read(ref _reportProgress);
            return reporter == null ? StartupFlowError.StepNotActive : reporter(this, progress);
        }

        internal void Deactivate() => Interlocked.Exchange(ref _reportProgress, null);
    }
}
