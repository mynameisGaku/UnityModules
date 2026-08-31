using System;
using System.Collections.Generic;
using System.Linq;

namespace PlayModeTuning.Editor
{
    /// <summary>
    /// 選択した調整差分を不変データとして保持する、一度だけ反映できる確認計画です。
    /// 作成元と同じオブジェクト、作業識別子、一時識別子、改訂値が一致しない場合は反映できません。
    /// </summary>
    public sealed class PlayModeTuningPlan
    {
        internal PlayModeTuningPlan(PlayModeTuningError error, string message, Guid sessionId, Guid nonce, string revision, IEnumerable<PlayModeTuningChange> changes)
        {
            Error = error;
            Message = message ?? string.Empty;
            SessionId = sessionId;
            Nonce = nonce;
            Revision = revision ?? string.Empty;
            Changes = Array.AsReadOnly((changes ?? Enumerable.Empty<PlayModeTuningChange>()).ToArray());
        }

        /// <summary>計画作成の失敗理由を取得します。反映可能な場合は<see cref="PlayModeTuningError.None"/>です。</summary>
        public PlayModeTuningError Error { get; }

        /// <summary>計画作成の結果を補足する説明を取得します。</summary>
        public string Message { get; }

        /// <summary>計画が属する調整作業の識別子を取得します。</summary>
        public Guid SessionId { get; }

        /// <summary>同じ計画の複製や再使用を拒否するための一時識別子を取得します。</summary>
        public Guid Nonce { get; }

        /// <summary>確認時の作業データと対象状態から計算した改訂値を取得します。</summary>
        public string Revision { get; }

        /// <summary>項目識別順に並んだ、変更前後の差分を読み取り専用で取得します。</summary>
        public IReadOnlyList<PlayModeTuningChange> Changes { get; }

        /// <summary>計画作成が成功し、反映前の再検証へ進めるかどうかを取得します。</summary>
        public bool IsReady => Error == PlayModeTuningError.None;
    }
}
