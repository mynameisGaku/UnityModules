using UnityEngine;

namespace PlayModeTuning.Editor
{
    /// <summary>手動記録の対象となる、シーン上の1コンポーネントと1つの最上位シリアル化項目を指定します。</summary>
    public sealed class PlayModeTuningPropertySelection
    {
        /// <summary>コンポーネントと項目パスの組を作成します。妥当性は<see cref="PlayModeTuningService.Start"/>で一括検証されます。</summary>
        /// <param name="target">保存済みシーン上で識別情報を固定する対象コンポーネントです。</param>
        /// <param name="propertyPath">対象コンポーネント内の最上位シリアル化項目のパスです。</param>
        /// <remarks>対象が<c>null</c>、未保存、不安定、入れ子、配列、未対応の場合、開始処理は値を変更せず失敗結果を返します。</remarks>
        public PlayModeTuningPropertySelection(Component target, string propertyPath)
        {
            Target = target;
            PropertyPath = propertyPath ?? string.Empty;
        }

        /// <summary>調整対象として指定したコンポーネントを取得します。</summary>
        public Component Target { get; }

        /// <summary>調整対象として指定した最上位シリアル化項目のパスを取得します。</summary>
        public string PropertyPath { get; }
    }
}
