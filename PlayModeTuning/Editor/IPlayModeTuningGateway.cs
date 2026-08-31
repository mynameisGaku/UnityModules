using System.Collections.Generic;

namespace PlayModeTuning.Editor
{
    /// <summary>決定論的な作業規則と、Unity上の対象解決・変更処理を分離します。</summary>
    internal interface IPlayModeTuningGateway
    {
        /// <summary>現在の再生状態とエディター状態を取得します。</summary>
        PlayModeTuningEnvironment GetEnvironment();

        /// <summary>利用者の選択を、保存可能な対象情報と初期値へ解決します。</summary>
        PlayModeTuningGatewayResult ResolveSelections(IReadOnlyList<PlayModeTuningPropertySelection> selections);

        /// <summary>保存済みの対象情報から現在値と選択外状態を記録します。</summary>
        PlayModeTuningGatewayResult Capture(IReadOnlyList<PlayModeTuningPropertyRecord> properties);

        /// <summary>全対象を事前確認し、一つの取り消し単位として値の反映を開始します。</summary>
        PlayModeTuningMutationResult Apply(IReadOnlyList<PlayModeTuningWrite> writes);

        /// <summary>進行中の反映を一度で取り消せる履歴へまとめ、失敗時の復元所有権は維持します。</summary>
        PlayModeTuningMutationResult CompleteApply();

        /// <summary>結果保存まで成功した反映について、内部の復元所有権だけを解放します。</summary>
        void ReleaseApply();

        /// <summary>進行中または履歴へまとめた直後の反映が所有する履歴だけを戻します。</summary>
        PlayModeTuningMutationResult RevertApply();

        /// <summary>対象シーンを保存前の変更済み状態へ移します。</summary>
        PlayModeTuningMutationResult MarkScenesDirty(IReadOnlyList<string> scenePaths);
    }
}
