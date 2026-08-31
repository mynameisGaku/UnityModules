// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 選択されたプレハブアセットを、保存や変更をせずに検査します。
    /// </summary>
    internal static class BuildGuardPrefabScanner
    {
        /// <summary>「Assets」配下にある保存済みプレハブのパスを正規化し、利用可能か確認します。</summary>
        internal static IReadOnlyList<string> NormalizePrefabPaths(IReadOnlyList<string> prefabPaths)
        {
            if (prefabPaths == null)
            {
                throw new ArgumentNullException(
                    nameof(prefabPaths),
                    "プレハブのパス一覧を指定してください。");
            }

            var normalized = new SortedSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < prefabPaths.Count; index++)
            {
                var path = (prefabPaths[index] ?? string.Empty).Replace('\\', '/').TrimEnd('/');
                if (!path.StartsWith("Assets/", StringComparison.Ordinal)
                    || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
                    || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    throw new ArgumentException(
                        $"指定されたパスは「Assets」配下のプレハブアセットではありません: {path}",
                        nameof(prefabPaths));
                }

                normalized.Add(path);
            }

            return new List<string>(normalized);
        }

        /// <summary>プレハブをパスの文字順で検査し、一時的に読み込んだ内容を必ず解放します。</summary>
        internal static BuildGuardPrefabScanResult Scan(
            IReadOnlyList<string> prefabPaths,
            Func<int, int, string, bool> shouldCancel = null)
        {
            var paths = NormalizePrefabPaths(prefabPaths);
            var issues = new List<BuildGuardPrefabScanIssue>();
            var scannedCount = 0;
            var cancelled = false;
            for (var index = 0; index < paths.Count; index++)
            {
                var path = paths[index];
                if (shouldCancel != null && shouldCancel(index, paths.Count, path))
                {
                    cancelled = true;
                    break;
                }

                GameObject contentsRoot = null;
                try
                {
                    contentsRoot = PrefabUtility.LoadPrefabContents(path);
                    AppendIssues(
                        path,
                        contentsRoot.scene,
                        BuildGuardSceneInspector.Inspect(contentsRoot.scene),
                        issues);
                    scannedCount++;
                }
                finally
                {
                    if (contentsRoot != null)
                    {
                        PrefabUtility.UnloadPrefabContents(contentsRoot);
                    }
                }
            }

            return new BuildGuardPrefabScanResult(issues, scannedCount, cancelled);
        }

        /// <summary>
        /// 現在のプレハブ内容を再検査し、記録時と同じ対象に同じ問題が残っている場合だけ返します。
        /// </summary>
        internal static bool TryFindCurrentTarget(
            Scene scene,
            BuildGuardPrefabScanIssue snapshot,
            out GameObject target)
        {
            target = null;
            if (!scene.IsValid()
                || !scene.isLoaded
                || string.IsNullOrWhiteSpace(snapshot.TargetGlobalObjectId))
            {
                return false;
            }

            // 階層名が同じでも置換済みの対象を採用しないよう、現在の問題を識別値まで再構築します。
            var currentIssues = new List<BuildGuardPrefabScanIssue>();
            AppendIssues(
                snapshot.PrefabPath,
                scene,
                BuildGuardSceneInspector.Inspect(scene),
                currentIssues);
            for (var index = 0; index < currentIssues.Count; index++)
            {
                var current = currentIssues[index];
                if (!MatchesSnapshot(snapshot, current))
                {
                    continue;
                }

                var currentTarget = BuildGuardHierarchyPath.Find(scene, current.HierarchyPath);
                if (currentTarget == null
                    || !string.Equals(
                        current.TargetGlobalObjectId,
                        GetGlobalObjectId(currentTarget),
                        StringComparison.Ordinal))
                {
                    continue;
                }

                target = currentTarget;
                return true;
            }

            return false;
        }

        /// <summary>記録時と現在の問題が、修復対象として同一か確認します。</summary>
        private static bool MatchesSnapshot(
            BuildGuardPrefabScanIssue snapshot,
            BuildGuardPrefabScanIssue current)
        {
            return snapshot.Kind == current.Kind
                && string.Equals(snapshot.PrefabPath, current.PrefabPath, StringComparison.Ordinal)
                && string.Equals(snapshot.HierarchyPath, current.HierarchyPath, StringComparison.Ordinal)
                && string.Equals(
                    snapshot.TargetGlobalObjectId,
                    current.TargetGlobalObjectId,
                    StringComparison.Ordinal);
        }

        /// <summary>プレハブの検査結果を、画面表示用の問題一覧へ追加します。</summary>
        private static void AppendIssues(
            string prefabPath,
            Scene scene,
            BuildGuardSceneInspection inspection,
            ICollection<BuildGuardPrefabScanIssue> issues)
        {
            foreach (var finding in inspection.MissingScripts)
            {
                var target = BuildGuardHierarchyPath.Find(scene, finding.HierarchyPath);
                issues.Add(new BuildGuardPrefabScanIssue(
                    BuildGuardIssueKind.MissingScript,
                    prefabPath,
                    finding.HierarchyPath,
                    GetGlobalObjectId(target),
                    $"欠落スクリプト: {finding.MissingScriptCount}"));
            }

            foreach (var finding in inspection.MissingObjectReferences)
            {
                var target = BuildGuardHierarchyPath.Find(scene, finding.HierarchyPath);
                issues.Add(new BuildGuardPrefabScanIssue(
                    BuildGuardIssueKind.MissingObjectReference,
                    prefabPath,
                    finding.HierarchyPath,
                    GetGlobalObjectId(target),
                    $"{finding.ComponentTypeName}[{finding.ComponentIndex}].{finding.PropertyPath}"));
            }
        }

        /// <summary>対象ゲームオブジェクトの安定識別値を文字列で返します。</summary>
        private static string GetGlobalObjectId(UnityEngine.Object value)
        {
            return value == null
                ? string.Empty
                : GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
        }
    }
}
