// SPDX-License-Identifier: MIT

namespace BuildGuard.Editor
{
    /// <summary>
    /// プレハブ構造差分を、決定論的な表示文と複写文へ整形します。
    /// </summary>
    internal static class BuildGuardPrefabOverrideReviewPresentation
    {
        /// <summary>構造差分の種類を日本語へ整形します。</summary>
        internal static string FormatKind(BuildGuardPrefabOverrideKind kind)
        {
            switch (kind)
            {
                case BuildGuardPrefabOverrideKind.AddedGameObject:
                    return "ゲームオブジェクトの追加";
                case BuildGuardPrefabOverrideKind.RemovedGameObject:
                    return "ゲームオブジェクトの削除";
                case BuildGuardPrefabOverrideKind.AddedComponent:
                    return "コンポーネントの追加";
                case BuildGuardPrefabOverrideKind.RemovedComponent:
                    return "コンポーネントの削除";
                default:
                    return $"不明な構造差分（{(int)kind}）";
            }
        }

        /// <summary>検査失敗の種類を日本語へ整形します。</summary>
        internal static string FormatScanError(BuildGuardPrefabOverrideScanError error)
        {
            switch (error)
            {
                case BuildGuardPrefabOverrideScanError.None:
                    return "なし";
                case BuildGuardPrefabOverrideScanError.InvalidScene:
                    return "無効なシーン";
                case BuildGuardPrefabOverrideScanError.SceneNotLoaded:
                    return "未読込のシーン";
                case BuildGuardPrefabOverrideScanError.InvalidLimits:
                    return "検査上限が不正";
                case BuildGuardPrefabOverrideScanError.UnsupportedPrefabInstanceStatus:
                    return "未対応のプレハブ状態";
                case BuildGuardPrefabOverrideScanError.MissingPrefabSource:
                    return "プレハブ参照元が見つからない";
                case BuildGuardPrefabOverrideScanError.TooManyGameObjects:
                    return "ゲームオブジェクト数が上限超過";
                case BuildGuardPrefabOverrideScanError.TooManyPrefabInstances:
                    return "プレハブ実体数が上限超過";
                case BuildGuardPrefabOverrideScanError.TooManyFindings:
                    return "構造差分数が上限超過";
                case BuildGuardPrefabOverrideScanError.UnityApiFailure:
                    return "Unity APIの処理に失敗";
                default:
                    return $"不明な検査エラー（{(int)error}）";
            }
        }

        /// <summary>対象コンポーネントの型と並び位置を整形します。</summary>
        internal static string FormatComponent(BuildGuardPrefabOverrideFinding finding)
        {
            return string.IsNullOrEmpty(finding.ComponentTypeName)
                ? "-"
                : $"{finding.ComponentTypeName}[{finding.ComponentIndex}]";
        }

        /// <summary>最も近いプレハブアセットと参照元パスを整形します。</summary>
        internal static string FormatSource(BuildGuardPrefabOverrideFinding finding)
        {
            var assetPath = string.IsNullOrEmpty(finding.NearestPrefabAssetPath)
                ? finding.PrefabAssetPath
                : finding.NearestPrefabAssetPath;
            return string.IsNullOrEmpty(finding.SourceObjectPath)
                ? assetPath
                : $"{assetPath} :: {finding.SourceObjectPath}";
        }

        /// <summary>1件の構造差分を日本語の項目名付き複写文へ整形します。</summary>
        internal static string FormatClipboardText(BuildGuardPrefabOverrideFinding finding)
        {
            return $"種類: {FormatKind(finding.Kind)} | シーン: {finding.ScenePath} | "
                + $"対象パス: {finding.TargetHierarchyPath} | "
                + $"コンポーネント: {FormatComponent(finding)} | 参照元: {FormatSource(finding)}";
        }
    }
}
