using System;

namespace PlayModeTuning.Editor
{
    /// <summary>
    /// 利用者が明示的に開始した1回の再生調整作業について、現在段階と失敗理由を不変データとして報告します。
    /// 保存データや対象状態が不正になった場合は、継続不能な段階と失敗理由を保持します。
    /// </summary>
    public sealed class PlayModeTuningSession
    {
        internal PlayModeTuningSession(Guid sessionId, PlayModeTuningPhase phase, PlayModeTuningError error, string message, int componentCount, int propertyCount)
        {
            SessionId = sessionId;
            Phase = phase;
            Error = error;
            Message = message ?? string.Empty;
            ComponentCount = componentCount;
            PropertyCount = propertyCount;
        }

        /// <summary>調整作業を他の作業と区別する識別子を取得します。作業がない場合は<see cref="Guid.Empty"/>です。</summary>
        public Guid SessionId { get; }

        /// <summary>調整作業の現在段階を取得します。</summary>
        public PlayModeTuningPhase Phase { get; }

        /// <summary>調整作業を継続できない理由を取得します。問題がない場合は<see cref="PlayModeTuningError.None"/>です。</summary>
        public PlayModeTuningError Error { get; }

        /// <summary>現在状態または失敗理由を補足する説明を取得します。</summary>
        public string Message { get; }

        /// <summary>調整作業で固定した対象コンポーネント数を取得します。</summary>
        public int ComponentCount { get; }

        /// <summary>調整作業で固定した対象項目数を取得します。</summary>
        public int PropertyCount { get; }

        /// <summary>反映、復元、差分なし、破棄、または無効化によって作業が終了しているかどうかを取得します。</summary>
        public bool IsTerminal => Phase == PlayModeTuningPhase.Completed || Phase == PlayModeTuningPhase.Stale;
    }
}
