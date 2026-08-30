// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 有効なビルド対象シーン、または取得済みの選択シーンアセットにビルドガードの検査規則を適用します。
    /// </summary>
    internal static class BuildGuardManualScanner
    {
        /// <summary>1回の取得で受け付ける選択アセット候補の最大数です。</summary>
        internal const int MaximumSelectedAssetCandidates = 4096;

        /// <summary>1回の検査で受け付ける選択シーンアセットの最大数です。</summary>
        internal const int MaximumSelectedScenes = 256;

        /// <summary>現在有効なビルドプロファイルから、有効なシーンのパスを返します。</summary>
        internal static IReadOnlyList<string> GetEnabledBuildScenePaths()
        {
            var scenes = EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>();
            var paths = new List<string>(scenes.Length);
            for (var index = 0; index < scenes.Length; index++)
            {
                var scene = scenes[index];
                if (scene != null && scene.enabled && !string.IsNullOrWhiteSpace(scene.path))
                {
                    paths.Add(scene.path.Replace('\\', '/'));
                }
            }

            return paths;
        }

        /// <summary>「Assets」配下で直接選択されたシーンアセットを、パスの文字順で取得します。</summary>
        internal static bool TryGetSelectedScenePaths(
            out IReadOnlyList<string> scenePaths,
            out string errorMessage)
        {
            try
            {
                return TryResolveSelectedScenePaths(
                    Selection.assetGUIDs,
                    AssetDatabase.GUIDToAssetPath,
                    path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null,
                    out scenePaths,
                    out errorMessage);
            }
            catch (Exception exception)
            {
                scenePaths = Array.Empty<string>();
                errorMessage = $"選択シーンの取得に失敗しました: {exception.Message}";
                return false;
            }
        }

        /// <summary>選択項目の識別子をパスへ変換し、「Assets」配下のシーンアセットだけを残します。</summary>
        internal static bool TryResolveSelectedScenePaths(
            IReadOnlyList<string> selectedAssetGuids,
            Func<string, string> guidToAssetPath,
            Func<string, bool> isSceneAsset,
            out IReadOnlyList<string> scenePaths,
            out string errorMessage)
        {
            scenePaths = Array.Empty<string>();
            errorMessage = string.Empty;
            if (selectedAssetGuids == null || guidToAssetPath == null || isSceneAsset == null)
            {
                errorMessage = "選択シーンの取得元を利用できません。";
                return false;
            }

            if (selectedAssetGuids.Count > MaximumSelectedAssetCandidates)
            {
                errorMessage = $"選択中のアセットが多すぎます。選択できるアセットは最大{MaximumSelectedAssetCandidates}件です。";
                return false;
            }

            var paths = new SortedSet<string>(StringComparer.Ordinal);
            try
            {
                for (var index = 0; index < selectedAssetGuids.Count; index++)
                {
                    var guid = selectedAssetGuids[index];
                    if (string.IsNullOrWhiteSpace(guid))
                    {
                        continue;
                    }

                    var path = (guidToAssetPath(guid) ?? string.Empty).Replace('\\', '/').TrimEnd('/');
                    if (!IsSceneAssetPath(path) || !isSceneAsset(path))
                    {
                        continue;
                    }

                    paths.Add(path);
                    if (paths.Count > MaximumSelectedScenes)
                    {
                        errorMessage = $"選択中のシーンが多すぎます。選択できるシーンアセットは最大{MaximumSelectedScenes}件です。";
                        return false;
                    }
                }
            }
            catch (Exception exception)
            {
                errorMessage = $"選択シーンの取得に失敗しました: {exception.Message}";
                return false;
            }

            scenePaths = new List<string>(paths);
            return true;
        }

        /// <summary>保存状態と読み込み状態を変えずに、指定されたシーンのパスを検査します。</summary>
        internal static BuildGuardManualScanResult Scan(
            IReadOnlyList<string> scenePaths,
            Func<int, int, string, bool> shouldCancel = null)
        {
            var issues = new List<BuildGuardScanIssue>();
            var scannedCount = BuildGuardScenePathVisitor.Visit(
                scenePaths,
                shouldCancel,
                scene => AppendIssues(scene, BuildGuardSceneInspector.Inspect(scene), issues),
                out var cancelled);
            return new BuildGuardManualScanResult(issues, scannedCount, cancelled);
        }

        /// <summary>取得済みのシーンを再確認し、選択状態が古い場合は途中結果を破棄します。</summary>
        internal static bool TryScanSelectedScenes(
            IReadOnlyList<string> scenePaths,
            Func<int, int, string, bool> shouldCancel,
            out BuildGuardManualScanResult result,
            out string errorMessage)
        {
            result = new BuildGuardManualScanResult(Array.Empty<BuildGuardScanIssue>(), 0, false);
            try
            {
                if (!TryNormalizeCapturedScenePaths(scenePaths, out var normalizedPaths, out errorMessage))
                {
                    return false;
                }

                var scanResult = Scan(normalizedPaths, shouldCancel);
                if (!TryNormalizeCapturedScenePaths(
                        normalizedPaths,
                        out var finalPaths,
                        out _)
                    || !HaveSamePaths(normalizedPaths, finalPaths)
                    || (!scanResult.Cancelled && scanResult.ScannedSceneCount != normalizedPaths.Count))
                {
                    errorMessage = "選択シーンの状態が変わりました。「現在の選択を使用」を押してから、もう一度検査してください。";
                    return false;
                }

                result = scanResult;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = $"選択シーンの検査に失敗しました: {exception.Message}";
                return false;
            }
        }

        /// <summary>取得済みの各パスが、現在もシーンアセットとして参照できることを確認します。</summary>
        private static bool TryNormalizeCapturedScenePaths(
            IReadOnlyList<string> scenePaths,
            out IReadOnlyList<string> normalizedPaths,
            out string errorMessage)
        {
            normalizedPaths = Array.Empty<string>();
            errorMessage = string.Empty;
            if (scenePaths == null || scenePaths.Count == 0)
            {
                errorMessage = "プロジェクトウィンドウでシーンアセットを1件以上選択してください。";
                return false;
            }

            if (scenePaths.Count > MaximumSelectedScenes)
            {
                errorMessage = $"選択中のシーンが多すぎます。選択できるシーンアセットは最大{MaximumSelectedScenes}件です。";
                return false;
            }

            var paths = new SortedSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < scenePaths.Count; index++)
            {
                var path = (scenePaths[index] ?? string.Empty).Replace('\\', '/').TrimEnd('/');
                if (!IsSceneAssetPath(path) || AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                {
                    errorMessage = "選択シーンの状態が変わりました。「現在の選択を使用」を押してから、もう一度検査してください。";
                    return false;
                }

                paths.Add(path);
            }

            normalizedPaths = new List<string>(paths);
            return true;
        }

        /// <summary>指定されたパスが「Assets」配下の保存済みシーンアセットを示すか確認します。</summary>
        private static bool IsSceneAssetPath(string path)
        {
            return path.StartsWith("Assets/", StringComparison.Ordinal)
                && path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>2つのシーンパス一覧が、同じ順序で完全に一致するか確認します。</summary>
        private static bool HaveSamePaths(
            IReadOnlyList<string> expected,
            IReadOnlyList<string> actual)
        {
            if (expected.Count != actual.Count)
            {
                return false;
            }

            for (var index = 0; index < expected.Count; index++)
            {
                if (!string.Equals(expected[index], actual[index], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>シーンの検査結果を、画面表示用の問題一覧へ追加します。</summary>
        private static void AppendIssues(
            Scene scene,
            BuildGuardSceneInspection inspection,
            ICollection<BuildGuardScanIssue> issues)
        {
            foreach (var finding in inspection.MissingScripts)
            {
                issues.Add(new BuildGuardScanIssue(
                    BuildGuardIssueKind.MissingScript,
                    scene.path.Replace('\\', '/'),
                    finding.HierarchyPath,
                    $"欠落スクリプト: {finding.MissingScriptCount}"));
            }

            foreach (var finding in inspection.MissingObjectReferences)
            {
                issues.Add(new BuildGuardScanIssue(
                    BuildGuardIssueKind.MissingObjectReference,
                    scene.path.Replace('\\', '/'),
                    finding.HierarchyPath,
                    $"{finding.ComponentTypeName}[{finding.ComponentIndex}].{finding.PropertyPath}"));
            }
        }
    }
}
