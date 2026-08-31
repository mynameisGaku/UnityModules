using System.Collections.Generic;

namespace AssetImportAudit.Editor
{
    /// <summary>差分確認計画の反映結果を表します。</summary>
    public readonly struct AssetImportAuditApplyResult
    {
        /// <summary>成否、失敗理由、反映数、差分確認後に変わったアセットから反映結果を作成します。</summary>
        /// <param name="succeeded">要求されたすべてのアセットへ反映できたかどうか。</param>
        /// <param name="error">処理を完了できなかった理由です。成功時は失敗なしを指定します。</param>
        /// <param name="appliedAssetCount">再取込まで完了したアセット数です。</param>
        /// <param name="staleAssetPaths">差分確認後に取込設定が変わったアセットのパスです。</param>
        public AssetImportAuditApplyResult(bool succeeded, AssetImportAuditError error, int appliedAssetCount, IReadOnlyList<string> staleAssetPaths)
        {
            Succeeded = succeeded;
            Error = error;
            AppliedAssetCount = appliedAssetCount;
            StaleAssetPaths = staleAssetPaths ?? new string[0];
        }

        /// <summary>要求されたすべてのアセットへ反映できたかどうか。</summary>
        public bool Succeeded { get; }

        /// <summary>失敗理由。失敗していない場合は、失敗なしを示す列挙値。</summary>
        public AssetImportAuditError Error { get; }

        /// <summary>再取込まで完了したアセット数。</summary>
        public int AppliedAssetCount { get; }

        /// <summary>差分確認後に変更されたアセット。</summary>
        public IReadOnlyList<string> StaleAssetPaths { get; }
    }
}
