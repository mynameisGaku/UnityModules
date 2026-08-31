namespace PlayModeTuning.Editor
{
    /// <summary>変更可能なUnityオブジェクトを公開せず、選択した1項目の開始値と記録値の差を表します。</summary>
    public sealed class PlayModeTuningChange
    {
        internal PlayModeTuningChange(string targetName, string componentType, string propertyPath, PlayModeTuningValueKind valueKind, string beforeValue, string afterValue)
        {
            TargetName = targetName ?? string.Empty;
            ComponentType = componentType ?? string.Empty;
            PropertyPath = propertyPath ?? string.Empty;
            ValueKind = valueKind;
            BeforeValue = beforeValue ?? string.Empty;
            AfterValue = afterValue ?? string.Empty;
        }

        /// <summary>確認時に識別情報から解決した対象ゲームオブジェクト名を取得します。</summary>
        public string TargetName { get; }

        /// <summary>対象コンポーネントの型名を取得します。</summary>
        public string ComponentType { get; }

        /// <summary>対象コンポーネント内の最上位シリアル化項目のパスを取得します。</summary>
        public string PropertyPath { get; }

        /// <summary>記録と反映に使用する値形式を取得します。</summary>
        public PlayModeTuningValueKind ValueKind { get; }

        /// <summary>調整作業を開始した編集状態での値を、確認表示用の文字列として取得します。</summary>
        public string BeforeValue { get; }

        /// <summary>再生中に明示的に記録した値を、確認表示用の文字列として取得します。</summary>
        public string AfterValue { get; }
    }
}
