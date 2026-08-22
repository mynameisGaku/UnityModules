using System.Collections.Generic;

namespace AssetImportAudit.Editor
{
    /// <summary>Reports the result of applying a preview plan.</summary>
    public readonly struct AssetImportAuditApplyResult
    {
        /// <summary>Creates an application result.</summary>
        public AssetImportAuditApplyResult(bool succeeded, AssetImportAuditError error, int appliedAssetCount, IReadOnlyList<string> staleAssetPaths)
        {
            Succeeded = succeeded;
            Error = error;
            AppliedAssetCount = appliedAssetCount;
            StaleAssetPaths = staleAssetPaths ?? new string[0];
        }

        /// <summary>Whether all requested assets were applied.</summary>
        public bool Succeeded { get; }

        /// <summary>Failure reason, or None.</summary>
        public AssetImportAuditError Error { get; }

        /// <summary>Number of assets reimported.</summary>
        public int AppliedAssetCount { get; }

        /// <summary>Assets that changed after preview.</summary>
        public IReadOnlyList<string> StaleAssetPaths { get; }
    }
}
