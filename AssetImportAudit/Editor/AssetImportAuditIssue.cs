namespace AssetImportAudit.Editor
{
    /// <summary>Describes one deterministic mismatch in a texture import setting.</summary>
    public readonly struct AssetImportAuditIssue
    {
        /// <summary>Creates a mismatch description.</summary>
        public AssetImportAuditIssue(string assetPath, string settingName, string currentValue, string expectedValue)
        {
            AssetPath = assetPath;
            SettingName = settingName;
            CurrentValue = currentValue;
            ExpectedValue = expectedValue;
        }

        /// <summary>Project-relative asset path.</summary>
        public string AssetPath { get; }

        /// <summary>Stable setting identifier.</summary>
        public string SettingName { get; }

        /// <summary>Current importer value.</summary>
        public string CurrentValue { get; }

        /// <summary>Requested importer value.</summary>
        public string ExpectedValue { get; }
    }
}
