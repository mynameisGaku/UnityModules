using System;
using System.Collections.Generic;

namespace PlayModeTuning.Editor
{
    /// <summary>
    /// 再生前の対象固定、再生中の手動記録、再生後の差分確認、反映または破棄を明示的に進める編集用入口です。
    /// 各操作は失敗理由を結果型で返し、確認なしの自動記録や自動反映は行いません。
    /// </summary>
    public static class PlayModeTuningService
    {
        private static readonly PlayModeTuningOperations Operations = new PlayModeTuningOperations(new UnityPlayModeTuningGateway(), new UnityPlayModeTuningSessionStore(), new PlayModeTuningPlanRegistry(), PlayModeTuningDomain.Token);

        /// <summary>編集状態で選択内容と開始値を検証し、不変な調整作業として固定します。</summary>
        /// <param name="selections">保存済みシーン上の対象コンポーネントと最上位項目の一覧です。</param>
        /// <returns>開始の成否、失敗理由、開始後の作業状態を含む結果です。</returns>
        /// <remarks>
        /// 編集状態でない、別作業が進行中、選択が無効または未対応、対象数・項目数・記録量が上限超過の場合は、
        /// 対象値を変更せず失敗結果を返します。
        /// </remarks>
        public static PlayModeTuningStartResult Start(IReadOnlyList<PlayModeTuningPropertySelection> selections)
        {
            return Operations.Start(selections);
        }

        /// <summary>値の記録、差分確認、反映を行わず、現在の調整作業状態だけを取得します。</summary>
        /// <returns>作業がない場合は待機状態、保存データが不正な場合は継続不能状態を表す不変結果です。</returns>
        public static PlayModeTuningSession GetCurrentSession()
        {
            return Operations.GetCurrentSession();
        }

        /// <summary>再生中の明示的な呼び出しにより、開始時に固定した選択項目だけを記録します。</summary>
        /// <param name="sessionId"><see cref="Start"/>が返した調整作業の識別子です。</param>
        /// <returns>記録の成否、記録項目数、記録量、記録後の作業状態を含む結果です。</returns>
        /// <remarks>
        /// 再生中でない、段階または識別子が不一致、対象識別情報や再生設定が変化、記録量が上限超過の場合は失敗し、
        /// 編集状態の値は変更しません。
        /// </remarks>
        public static PlayModeTuningCaptureResult CaptureDuringPlay(Guid sessionId)
        {
            return Operations.CaptureDuringPlay(sessionId);
        }

        /// <summary>再生終了後に開始値と記録値を再検証し、シーン値を変更せず一度だけ使える反映計画を作成します。</summary>
        /// <param name="sessionId"><see cref="Start"/>が返した調整作業の識別子です。</param>
        /// <returns>失敗理由、改訂値、項目ごとの差分を含む不変計画です。</returns>
        /// <remarks>
        /// 編集状態でない、手動記録を終えていない、識別子や対象状態が変化、または差分がない場合は反映不能な計画を返します。
        /// </remarks>
        public static PlayModeTuningPlan PreviewAfterPlay(Guid sessionId)
        {
            return Operations.PreviewAfterPlay(sessionId);
        }

        /// <summary>確認済みの同じ計画を一度だけ消費し、再検証、反映、反映後確認を行い、失敗時は復元を試みます。</summary>
        /// <param name="plan"><see cref="PreviewAfterPlay"/>が返した同一オブジェクトの反映可能な計画です。</param>
        /// <returns>反映と復元それぞれの実行有無、成否、失敗理由を含む結果です。</returns>
        /// <remarks>
        /// 計画が<c>null</c>、古い、複製、使用済み、別環境由来、または対象状態が変化した場合は値を書き換えません。
        /// 書き換え後の確認やシーンの変更済み設定に失敗した場合は、Unityの取り消し記録に含まれるシリアル化値の復元を試みます。
        /// オブジェクトまたはコンポーネントの追加・削除など、階層構造の副作用は検出しますが、自動復元を保証しません。
        /// </remarks>
        public static PlayModeTuningApplyResult Apply(PlayModeTuningPlan plan)
        {
            return Operations.Apply(plan);
        }

        /// <summary>記録値をシーンへ反映せず、指定した調整作業を終了します。</summary>
        /// <param name="sessionId">終了する調整作業の識別子です。</param>
        /// <returns>破棄後の作業状態です。識別子が一致しない場合は失敗理由を含みます。</returns>
        public static PlayModeTuningSession Discard(Guid sessionId)
        {
            return Operations.Discard(sessionId);
        }

        internal static PlayModeTuningOperations InternalOperations => Operations;
    }
}
