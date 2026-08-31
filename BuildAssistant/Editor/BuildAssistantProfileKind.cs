namespace BuildAssistant.Editor
{
    /// <summary>Unityが機種別プロファイルと明示的な独自ビルドプロファイル素材のどちらを使っているかを表します。</summary>
    public enum BuildAssistantProfileKind
    {
        /// <summary>独自プロファイルが有効でないため、機種別プロファイルを使っています。</summary>
        Platform = 0,
        /// <summary>明示的に指定された独自ビルドプロファイル素材を使っています。</summary>
        Custom = 1
    }
}
