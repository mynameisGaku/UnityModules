// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 有効なビルド対象シーンから得た、途中結果を含まない不変なプレハブ構造差分一覧を保持します。
    /// </summary>
    internal readonly struct BuildGuardPrefabOverrideReviewScanResult
    {
        private BuildGuardPrefabOverrideReviewScanResult(
            bool succeeded,
            bool cancelled,
            IReadOnlyList<BuildGuardPrefabOverrideFinding> findings,
            IReadOnlyList<BuildGuardPrefabOverrideReviewFailure> failures,
            int scannedSceneCount,
            long totalFindingCount,
            bool wasTruncated)
        {
            Succeeded = succeeded;
            Cancelled = cancelled;
            Findings = findings;
            Failures = failures;
            ScannedSceneCount = scannedSceneCount;
            TotalFindingCount = totalFindingCount;
            WasTruncated = wasTruncated;
        }

        /// <summary>すべての対象シーンを検査できたかを表します。</summary>
        internal bool Succeeded { get; }

        /// <summary>利用者が検査を中止したかを表します。</summary>
        internal bool Cancelled { get; }

        /// <summary>表示用に保持した構造差分の不変な一覧です。</summary>
        internal IReadOnlyList<BuildGuardPrefabOverrideFinding> Findings { get; }

        /// <summary>シーンごとの検査失敗を保持する不変な一覧です。</summary>
        internal IReadOnlyList<BuildGuardPrefabOverrideReviewFailure> Failures { get; }

        /// <summary>完全に検査できたシーンの件数です。</summary>
        internal int ScannedSceneCount { get; }

        /// <summary>表示上限を適用する前の構造差分総数です。</summary>
        internal long TotalFindingCount { get; }

        /// <summary>表示上限によって一覧を省略したかを表します。</summary>
        internal bool WasTruncated { get; }

        /// <summary>呼出元の一覧から切り離した、成功結果を作成します。</summary>
        internal static BuildGuardPrefabOverrideReviewScanResult Success(
            IReadOnlyList<BuildGuardPrefabOverrideFinding> findings,
            int scannedSceneCount,
            long totalFindingCount)
        {
            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            if (scannedSceneCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scannedSceneCount));
            }

            if (totalFindingCount < findings.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(totalFindingCount));
            }

            return new BuildGuardPrefabOverrideReviewScanResult(
                true,
                false,
                CopyFindings(findings),
                Array.Empty<BuildGuardPrefabOverrideReviewFailure>(),
                scannedSceneCount,
                totalFindingCount,
                totalFindingCount > findings.Count);
        }

        /// <summary>途中の構造差分を公開しない中止結果を作成します。</summary>
        internal static BuildGuardPrefabOverrideReviewScanResult Cancellation(int scannedSceneCount)
        {
            if (scannedSceneCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scannedSceneCount));
            }

            return new BuildGuardPrefabOverrideReviewScanResult(
                false,
                true,
                Array.Empty<BuildGuardPrefabOverrideFinding>(),
                Array.Empty<BuildGuardPrefabOverrideReviewFailure>(),
                scannedSceneCount,
                0,
                false);
        }

        /// <summary>途中の構造差分を公開しない失敗結果を作成します。</summary>
        internal static BuildGuardPrefabOverrideReviewScanResult Failure(
            IReadOnlyList<BuildGuardPrefabOverrideReviewFailure> failures,
            int scannedSceneCount)
        {
            if (failures == null)
            {
                throw new ArgumentNullException(nameof(failures));
            }

            if (failures.Count == 0)
            {
                throw new ArgumentException("失敗結果には、1件以上の失敗理由が必要です。", nameof(failures));
            }

            if (scannedSceneCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(scannedSceneCount));
            }

            return new BuildGuardPrefabOverrideReviewScanResult(
                false,
                false,
                Array.Empty<BuildGuardPrefabOverrideFinding>(),
                CopyFailures(failures),
                scannedSceneCount,
                0,
                false);
        }

        private static IReadOnlyList<BuildGuardPrefabOverrideFinding> CopyFindings(
            IReadOnlyList<BuildGuardPrefabOverrideFinding> source)
        {
            var snapshot = new BuildGuardPrefabOverrideFinding[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                snapshot[index] = source[index];
            }

            return Array.AsReadOnly(snapshot);
        }

        private static IReadOnlyList<BuildGuardPrefabOverrideReviewFailure> CopyFailures(
            IReadOnlyList<BuildGuardPrefabOverrideReviewFailure> source)
        {
            var snapshot = new BuildGuardPrefabOverrideReviewFailure[source.Count];
            for (var index = 0; index < source.Count; index++)
            {
                snapshot[index] = source[index];
            }

            return Array.AsReadOnly(snapshot);
        }
    }
}
