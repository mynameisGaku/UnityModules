// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// 確認専用のプレハブ構造差分一覧を作り、シーン内容を変えず安全に対象を特定します。
    /// </summary>
    internal static class BuildGuardPrefabOverrideReviewService
    {
        /// <summary>指定した全シーンを検査できた場合だけ、構造差分一覧を返します。</summary>
        internal static BuildGuardPrefabOverrideReviewScanResult Scan(
            IReadOnlyList<string> scenePaths,
            int maximumDisplayedFindings,
            Func<int, int, string, bool> shouldCancel = null)
        {
            return Scan(
                scenePaths,
                maximumDisplayedFindings,
                shouldCancel,
                CaptureSceneState);
        }

        /// <summary>決定論的な試験でシーン状態取得処理を差し替えられる検査入口です。</summary>
        internal static BuildGuardPrefabOverrideReviewScanResult Scan(
            IReadOnlyList<string> scenePaths,
            int maximumDisplayedFindings,
            Func<int, int, string, bool> shouldCancel,
            Func<BuildGuardPrefabOverrideReviewSceneState> captureSceneState)
        {
            if (scenePaths == null)
            {
                throw new ArgumentNullException(nameof(scenePaths));
            }

            if (maximumDisplayedFindings <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumDisplayedFindings));
            }

            if (captureSceneState == null)
            {
                throw new ArgumentNullException(nameof(captureSceneState));
            }

            BuildGuardPrefabOverrideReviewSceneState expectedState;
            try
            {
                expectedState = captureSceneState();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return CreateStateFailure(
                    default,
                    "検査前のシーン状態を取得できませんでした。Unityのログで原因を確認してください。");
            }

            var scanResult = ExecuteScan(
                scenePaths,
                maximumDisplayedFindings,
                shouldCancel);
            return ValidateFinalSceneState(scanResult, expectedState, captureSceneState);
        }

        private static BuildGuardPrefabOverrideReviewScanResult ExecuteScan(
            IReadOnlyList<string> scenePaths,
            int maximumDisplayedFindings,
            Func<int, int, string, bool> shouldCancel)
        {
            var findings = new List<BuildGuardPrefabOverrideFinding>(
                Math.Min(maximumDisplayedFindings, 256));
            var scannedSceneCount = 0;
            var totalFindingCount = 0L;
            var currentScenePath = string.Empty;
            BuildGuardPrefabOverrideReviewFailure failure = default;
            var cancelled = false;

            try
            {
                BuildGuardScenePathVisitor.Visit(
                    scenePaths,
                    (index, total, scenePath) =>
                    {
                        currentScenePath = scenePath;
                        return shouldCancel != null && shouldCancel(index, total, scenePath);
                    },
                    scene =>
                    {
                        currentScenePath = NormalizePath(scene.path);
                        var sceneResult = BuildGuardPrefabOverrideSceneScanner.Scan(scene);
                        if (!sceneResult.Succeeded)
                        {
                            failure = new BuildGuardPrefabOverrideReviewFailure(
                                currentScenePath,
                                sceneResult.Error,
                                sceneResult.ErrorMessage);
                            throw new ReviewScanFailedException();
                        }

                        scannedSceneCount++;
                        totalFindingCount += sceneResult.Findings.Count;
                        for (var findingIndex = 0;
                            findingIndex < sceneResult.Findings.Count;
                            findingIndex++)
                        {
                            AddBoundedFinding(
                                findings,
                                sceneResult.Findings[findingIndex],
                                maximumDisplayedFindings);
                        }
                    },
                    out cancelled);
            }
            catch (ReviewScanFailedException)
            {
                return BuildGuardPrefabOverrideReviewScanResult.Failure(
                    new[] { failure },
                    scannedSceneCount);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return BuildGuardPrefabOverrideReviewScanResult.Failure(
                    new[]
                    {
                        new BuildGuardPrefabOverrideReviewFailure(
                            currentScenePath,
                            BuildGuardPrefabOverrideScanError.UnityApiFailure,
                            "プレハブ構造差分を検査できませんでした。Unityのログで原因を確認してください。"),
                    },
                    scannedSceneCount);
            }

            if (cancelled)
            {
                return BuildGuardPrefabOverrideReviewScanResult.Cancellation(scannedSceneCount);
            }
            return BuildGuardPrefabOverrideReviewScanResult.Success(
                findings,
                scannedSceneCount,
                totalFindingCount);
        }

        private static BuildGuardPrefabOverrideReviewScanResult ValidateFinalSceneState(
            BuildGuardPrefabOverrideReviewScanResult scanResult,
            BuildGuardPrefabOverrideReviewSceneState expectedState,
            Func<BuildGuardPrefabOverrideReviewSceneState> captureSceneState)
        {
            try
            {
                var currentState = captureSceneState();
                return BuildGuardPrefabOverrideReviewSceneState.TryValidate(
                    expectedState,
                    currentState,
                    out var validationMessage)
                    ? scanResult
                    : CreateStateFailure(
                        scanResult,
                        $"検査前の読込済みシーン状態を保てませんでした: {validationMessage}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return CreateStateFailure(
                    scanResult,
                    "検査後のシーン状態を確認できませんでした。Unityのログで原因を確認してください。");
            }
        }

        private static BuildGuardPrefabOverrideReviewScanResult CreateStateFailure(
            BuildGuardPrefabOverrideReviewScanResult scanResult,
            string message)
        {
            var failures = new List<BuildGuardPrefabOverrideReviewFailure>();
            if (!scanResult.Succeeded && !scanResult.Cancelled && scanResult.Failures != null)
            {
                failures.AddRange(scanResult.Failures);
            }

            failures.Add(new BuildGuardPrefabOverrideReviewFailure(
                "<読込済みシーンの状態>",
                BuildGuardPrefabOverrideScanError.UnityApiFailure,
                message));
            return BuildGuardPrefabOverrideReviewScanResult.Failure(
                failures,
                scanResult.ScannedSceneCount);
        }

        private static BuildGuardPrefabOverrideReviewSceneState CaptureSceneState()
        {
            var scenes = new BuildGuardPrefabOverrideReviewSceneState.SceneEntry[SceneManager.sceneCount];
            for (var index = 0; index < scenes.Length; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                scenes[index] = new BuildGuardPrefabOverrideReviewSceneState.SceneEntry(
                    scene.handle.GetRawData(),
                    NormalizePath(scene.path),
                    scene.isDirty);
            }

            var activeScene = SceneManager.GetActiveScene();
            return new BuildGuardPrefabOverrideReviewSceneState(
                scenes,
                activeScene.IsValid() ? activeScene.handle.GetRawData() : 0,
                activeScene.IsValid() ? NormalizePath(activeScene.path) : string.Empty);
        }

        /// <summary>保存済みの識別項目がすべて現在値と一致する場合だけ真を返します。</summary>
        internal static bool MatchesSnapshot(
            BuildGuardPrefabOverrideFinding snapshot,
            BuildGuardPrefabOverrideFinding current)
        {
            return snapshot.Kind == current.Kind
                && StringEquals(snapshot.ScenePath, current.ScenePath)
                && StringEquals(snapshot.SceneGuid, current.SceneGuid)
                && StringEquals(snapshot.PrefabAssetPath, current.PrefabAssetPath)
                && StringEquals(snapshot.PrefabAssetGuid, current.PrefabAssetGuid)
                && snapshot.PrefabAssetType == current.PrefabAssetType
                && StringEquals(snapshot.NearestPrefabAssetPath, current.NearestPrefabAssetPath)
                && snapshot.NearestPrefabAssetType == current.NearestPrefabAssetType
                && snapshot.IsNestedPrefabObject == current.IsNestedPrefabObject
                && StringEquals(snapshot.InstanceRootHierarchyPath, current.InstanceRootHierarchyPath)
                && StringEquals(snapshot.TargetHierarchyPath, current.TargetHierarchyPath)
                && StringEquals(snapshot.SourceObjectPath, current.SourceObjectPath)
                && StringEquals(snapshot.ComponentTypeName, current.ComponentTypeName)
                && snapshot.ComponentIndex == current.ComponentIndex
                && StringEquals(snapshot.InstanceRootGlobalObjectId, current.InstanceRootGlobalObjectId)
                && StringEquals(snapshot.NavigationTargetGlobalObjectId, current.NavigationTargetGlobalObjectId)
                && StringEquals(snapshot.SourceObjectGlobalObjectId, current.SourceObjectGlobalObjectId);
        }

        /// <summary>
        /// 1件の差分を再検査し、読込済みシーンの対象、または閉じたシーンアセットを選択します。
        /// </summary>
        internal static BuildGuardPrefabOverrideNavigationOutcome Locate(
            BuildGuardPrefabOverrideFinding snapshot,
            out string message)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(snapshot.ScenePath);
            if (sceneAsset == null)
            {
                message = $"シーンを利用できません: {snapshot.ScenePath}";
                return BuildGuardPrefabOverrideNavigationOutcome.SceneUnavailable;
            }

            var currentSceneGuid = AssetDatabase.AssetPathToGUID(snapshot.ScenePath);
            if (!StringEquals(snapshot.SceneGuid, currentSceneGuid))
            {
                message = "シーンアセットの識別情報が変化したため、この結果は古くなっています。「更新して検査」を押してください。";
                return BuildGuardPrefabOverrideNavigationOutcome.Stale;
            }

            var originalActiveScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(snapshot.ScenePath);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            GameObject navigationTarget = null;
            var outcome = BuildGuardPrefabOverrideNavigationOutcome.Stale;
            message = "プレハブ構造差分が変化したため、この結果は古くなっています。「更新して検査」を押してください。";
            var restoreSucceeded = true;
            var restoreMessage = string.Empty;

            try
            {
                if (!wasLoaded)
                {
                    scene = EditorSceneManager.OpenScene(snapshot.ScenePath, OpenSceneMode.Additive);
                }

                var refreshed = BuildGuardPrefabOverrideSceneScanner.Scan(scene);
                if (!refreshed.Succeeded)
                {
                    outcome = BuildGuardPrefabOverrideNavigationOutcome.ScanFailed;
                    message = $"{snapshot.ScenePath} を再検査できませんでした: {refreshed.ErrorMessage}";
                }
                else if (TryFindMatchingFinding(snapshot, refreshed.Findings, out var current))
                {
                    navigationTarget = BuildGuardHierarchyPath.Find(scene, current.TargetHierarchyPath);
                    if (navigationTarget != null
                        && StringEquals(
                            current.NavigationTargetGlobalObjectId,
                            GetGlobalObjectId(navigationTarget)))
                    {
                        outcome = wasLoaded
                            ? BuildGuardPrefabOverrideNavigationOutcome.SelectedSceneObject
                            : BuildGuardPrefabOverrideNavigationOutcome.PingedSceneAsset;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                outcome = BuildGuardPrefabOverrideNavigationOutcome.SceneUnavailable;
                message = $"{snapshot.ScenePath} を再検査できませんでした。Unityのログで原因を確認してください。";
            }
            finally
            {
                if (!wasLoaded)
                {
                    var openedScene = SceneManager.GetSceneByPath(snapshot.ScenePath);
                    if (openedScene.IsValid() && openedScene.isLoaded)
                    {
                        try
                        {
                            restoreSucceeded = EditorSceneManager.CloseScene(openedScene, true);
                            if (!restoreSucceeded)
                            {
                                restoreMessage = $"一時的に開いたシーンを閉じられませんでした: {snapshot.ScenePath}";
                            }
                        }
                        catch (Exception exception)
                        {
                            Debug.LogException(exception);
                            restoreSucceeded = false;
                            restoreMessage = "一時的に開いたシーンを閉じられませんでした。Unityのログで原因を確認してください。";
                        }
                    }

                    if (originalActiveScene.IsValid()
                        && originalActiveScene.isLoaded
                        && SceneManager.GetActiveScene() != originalActiveScene
                        && !SceneManager.SetActiveScene(originalActiveScene))
                    {
                        restoreSucceeded = false;
                        restoreMessage = "検査前のアクティブシーンへ戻せませんでした。";
                    }
                }
            }

            if (!restoreSucceeded)
            {
                message = restoreMessage;
                return BuildGuardPrefabOverrideNavigationOutcome.SceneStateRestoreFailed;
            }

            if (outcome == BuildGuardPrefabOverrideNavigationOutcome.SelectedSceneObject)
            {
                if (!scene.IsValid() || !scene.isLoaded || navigationTarget == null)
                {
                    message = "シーン内の対象を選択する前に結果が古くなりました。「更新して検査」を押してください。";
                    return BuildGuardPrefabOverrideNavigationOutcome.Stale;
                }

                Selection.activeGameObject = navigationTarget;
                EditorGUIUtility.PingObject(navigationTarget);
                message = $"現在の構造差分の対象を選択しました: {snapshot.TargetHierarchyPath}";
                return outcome;
            }

            if (outcome == BuildGuardPrefabOverrideNavigationOutcome.PingedSceneAsset)
            {
                Selection.activeObject = sceneAsset;
                EditorGUIUtility.PingObject(sceneAsset);
                message = "構造差分が現在も存在することを確認しました。シーンは閉じたまま、そのシーンアセットを選択しました。";
            }

            return outcome;
        }

        private static bool TryFindMatchingFinding(
            BuildGuardPrefabOverrideFinding snapshot,
            IReadOnlyList<BuildGuardPrefabOverrideFinding> findings,
            out BuildGuardPrefabOverrideFinding current)
        {
            for (var index = 0; index < findings.Count; index++)
            {
                if (MatchesSnapshot(snapshot, findings[index]))
                {
                    current = findings[index];
                    return true;
                }
            }

            current = default;
            return false;
        }

        private static void AddBoundedFinding(
            List<BuildGuardPrefabOverrideFinding> findings,
            BuildGuardPrefabOverrideFinding finding,
            int maximumCount)
        {
            var minimum = 0;
            var maximum = findings.Count;
            while (minimum < maximum)
            {
                var middle = minimum + ((maximum - minimum) / 2);
                if (BuildGuardPrefabOverrideSceneScanner.CompareFindings(findings[middle], finding) <= 0)
                {
                    minimum = middle + 1;
                }
                else
                {
                    maximum = middle;
                }
            }

            if (findings.Count >= maximumCount && minimum >= maximumCount)
            {
                return;
            }

            findings.Insert(minimum, finding);
            if (findings.Count > maximumCount)
            {
                findings.RemoveAt(maximumCount);
            }
        }

        private static string GetGlobalObjectId(UnityEngine.Object value)
        {
            return value == null
                ? string.Empty
                : GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static bool StringEquals(string left, string right)
        {
            return string.Equals(left, right, StringComparison.Ordinal);
        }

        private sealed class ReviewScanFailedException : Exception
        {
        }
    }
}
