// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;

namespace BuildGuard.Editor
{
    /// <summary>
    /// プレハブ構造差分検査1回分の、途中結果を含まない不変な結果を保持します。
    /// </summary>
    internal readonly struct BuildGuardPrefabOverrideScanResult
    {
        private BuildGuardPrefabOverrideScanResult(
            bool succeeded,
            BuildGuardPrefabOverrideScanError error,
            string errorMessage,
            IReadOnlyList<BuildGuardPrefabOverrideFinding> findings,
            int visitedGameObjectCount,
            int scannedPrefabInstanceCount)
        {
            Succeeded = succeeded;
            Error = error;
            ErrorMessage = errorMessage ?? string.Empty;
            Findings = findings ?? Array.Empty<BuildGuardPrefabOverrideFinding>();
            VisitedGameObjectCount = visitedGameObjectCount;
            ScannedPrefabInstanceCount = scannedPrefabInstanceCount;
        }

        /// <summary>検査が完全に成功したかを表します。</summary>
        internal bool Succeeded { get; }

        /// <summary>検査に失敗した原因の種類です。</summary>
        internal BuildGuardPrefabOverrideScanError Error { get; }

        /// <summary>検査に失敗した理由です。</summary>
        internal string ErrorMessage { get; }

        /// <summary>検査で見つかった構造差分の不変な一覧です。</summary>
        internal IReadOnlyList<BuildGuardPrefabOverrideFinding> Findings { get; }

        /// <summary>検査中に確認したゲームオブジェクトの総数です。</summary>
        internal int VisitedGameObjectCount { get; }

        /// <summary>検査した最上位プレハブ実体の総数です。</summary>
        internal int ScannedPrefabInstanceCount { get; }

        /// <summary>呼出元の一覧から切り離した、成功結果を作成します。</summary>
        internal static BuildGuardPrefabOverrideScanResult Success(
            IReadOnlyList<BuildGuardPrefabOverrideFinding> findings,
            int visitedGameObjectCount,
            int scannedPrefabInstanceCount)
        {
            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            var snapshot = new BuildGuardPrefabOverrideFinding[findings.Count];
            for (var index = 0; index < findings.Count; index++)
            {
                snapshot[index] = findings[index];
            }

            return new BuildGuardPrefabOverrideScanResult(
                true,
                BuildGuardPrefabOverrideScanError.None,
                string.Empty,
                Array.AsReadOnly(snapshot),
                visitedGameObjectCount,
                scannedPrefabInstanceCount);
        }

        /// <summary>途中の構造差分を公開しない失敗結果を作成します。</summary>
        internal static BuildGuardPrefabOverrideScanResult Failure(
            BuildGuardPrefabOverrideScanError error,
            string errorMessage,
            int visitedGameObjectCount,
            int scannedPrefabInstanceCount)
        {
            if (error == BuildGuardPrefabOverrideScanError.None)
            {
                throw new ArgumentException("失敗結果には、成功以外の原因が必要です。", nameof(error));
            }

            return new BuildGuardPrefabOverrideScanResult(
                false,
                error,
                errorMessage,
                Array.Empty<BuildGuardPrefabOverrideFinding>(),
                visitedGameObjectCount,
                scannedPrefabInstanceCount);
        }
    }
}
