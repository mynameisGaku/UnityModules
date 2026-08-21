using System.Collections.Generic;

namespace GameplayThreat
{
    /// <summary>
    /// 非負threat scoreへ有限増減を入力順に適用し、0下限と安定した首位を決定論的に解決します。
    /// </summary>
    public static class ThreatScoreResolver
    {
        /// <summary>1回に受け付ける初期entryの最大数です。</summary>
        public const int MaximumEntryCount = 32;
        /// <summary>1回に受け付ける増減の最大数です。</summary>
        public const int MaximumAdjustmentCount = 64;

        /// <summary>
        /// 初期entryと増減を変更せず、全最終score・全明細・首位を解決します。
        /// entry由来の失敗ではfailureIndexがentry index、増減由来では増減index、それ以外では-1になります。
        /// </summary>
        /// <param name="entries">1〜32件の正ID・有限非負scoreです。</param>
        /// <param name="adjustments">0〜64件の既存ID向け有限増減です。</param>
        /// <param name="resolution">成功時の不変な解決結果です。</param>
        /// <param name="error">失敗理由です。</param>
        /// <param name="failureIndex">失敗したentryまたは増減のindexです。</param>
        /// <returns>全入力を安全に解決できた場合はtrueです。</returns>
        public static bool TryResolve(
            IReadOnlyList<ThreatScoreEntry> entries,
            IReadOnlyList<ThreatScoreAdjustment> adjustments,
            out ThreatScoreResolution resolution,
            out ThreatScoreError error,
            out int failureIndex)
        {
            return ThreatScoreEngine.TryResolve(entries, adjustments, out resolution, out error, out failureIndex);
        }
    }
}
