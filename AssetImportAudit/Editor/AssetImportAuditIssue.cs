namespace AssetImportAudit.Editor
{
    /// <summary>テクスチャー取り込み設定で、同じ条件なら一貫して検出される1件の不一致を表します。</summary>
    public readonly struct AssetImportAuditIssue
    {
        /// <summary>設定不一致の情報を作成します。</summary>
        /// <param name="assetPath">対象アセットのプロジェクト相対パスです。</param>
        /// <param name="settingName">不一致がある設定項目の内部識別名です。</param>
        /// <param name="currentValue">現在の取込設定値です。</param>
        /// <param name="expectedValue">要求された取込設定値です。</param>
        public AssetImportAuditIssue(string assetPath, string settingName, string currentValue, string expectedValue)
            : this(assetPath, AssetImportAuditTexturePlatform.None, settingName, currentValue, expectedValue)
        {
        }

        /// <summary>対象機種情報を含む設定不一致の情報を作成します。</summary>
        /// <param name="platform">不一致がある対象機種です。</param>
        /// <param name="assetPath">対象アセットのプロジェクト相対パスです。</param>
        /// <param name="settingName">不一致がある設定項目の内部識別名です。</param>
        /// <param name="currentValue">現在の取込設定値です。</param>
        /// <param name="expectedValue">要求された取込設定値です。</param>
        public AssetImportAuditIssue(AssetImportAuditTexturePlatform platform, string assetPath, string settingName, string currentValue, string expectedValue)
            : this(assetPath, platform, settingName, currentValue, expectedValue)
        {
        }

        private AssetImportAuditIssue(string assetPath, AssetImportAuditTexturePlatform platform, string settingName, string currentValue, string expectedValue)
        {
            AssetPath = assetPath;
            Platform = platform;
            SettingName = settingName;
            CurrentValue = currentValue;
            ExpectedValue = expectedValue;
        }

        /// <summary>プロジェクト相対のアセットパス。</summary>
        public string AssetPath { get; }

        /// <summary>この不一致に対応する対象機種。共通の取込設定の場合は、対象機種なしを示す列挙値。</summary>
        public AssetImportAuditTexturePlatform Platform { get; }

        /// <summary>対象機種別の個別設定に属する不一致かどうか。</summary>
        public bool IsPlatformSetting => Platform != AssetImportAuditTexturePlatform.None;

        /// <summary>設定項目を一意に識別する、変更されない名前。</summary>
        public string SettingName { get; }

        /// <summary>現在の取り込み設定値。</summary>
        public string CurrentValue { get; }

        /// <summary>要求された取り込み設定値。</summary>
        public string ExpectedValue { get; }
    }
}
