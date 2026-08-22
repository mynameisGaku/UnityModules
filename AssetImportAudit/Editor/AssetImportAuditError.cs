namespace AssetImportAudit.Editor
{
    /// <summary>Describes why an audit operation could not be completed.</summary>
    public enum AssetImportAuditError
    {
        None = 0,
        InvalidFolder = 1,
        InvalidSettings = 2,
        StalePlan = 3,
        NoChanges = 4,
        ImporterUnavailable = 5,
        ApplyFailed = 6
    }
}
