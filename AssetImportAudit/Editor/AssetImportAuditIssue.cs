namespace AssetImportAudit.Editor
{
    /// <summary>Describes one deterministic mismatch in a texture import setting.</summary>
    public readonly struct AssetImportAuditIssue
    {
        /// <summary>Creates a mismatch description.</summary>
        public AssetImportAuditIssue(string assetPath, string settingName, string currentValue, string expectedValue)
            : this(assetPath, AssetImportAuditTexturePlatform.None, settingName, currentValue, expectedValue)
        {
        }

        /// <summary>Creates a mismatch description with platform metadata.</summary>
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

        /// <summary>Project-relative asset path.</summary>
        public string AssetPath { get; }

        /// <summary>Platform owning this mismatch, or None for default importer settings.</summary>
        public AssetImportAuditTexturePlatform Platform { get; }

        /// <summary>Whether this mismatch belongs to a platform override.</summary>
        public bool IsPlatformSetting => Platform != AssetImportAuditTexturePlatform.None;

        /// <summary>Stable setting identifier.</summary>
        public string SettingName { get; }

        /// <summary>Current importer value.</summary>
        public string CurrentValue { get; }

        /// <summary>Requested importer value.</summary>
        public string ExpectedValue { get; }
    }
}
