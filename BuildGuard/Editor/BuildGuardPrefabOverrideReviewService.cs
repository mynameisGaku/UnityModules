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
    /// Produces review-only snapshots and safely locates findings without changing Scene content.
    /// </summary>
    internal static class BuildGuardPrefabOverrideReviewService
    {
        /// <summary>Scans Scene paths and returns findings only when every requested visit completes.</summary>
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

        /// <summary>Scans with an injectable Scene-state capture seam for deterministic validation.</summary>
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
                return CreateStateFailure(
                    default,
                    $"Unity could not capture the initial loaded Scene state: {exception.Message}");
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
                return BuildGuardPrefabOverrideReviewScanResult.Failure(
                    new[]
                    {
                        new BuildGuardPrefabOverrideReviewFailure(
                            currentScenePath,
                            BuildGuardPrefabOverrideScanError.UnityApiFailure,
                            $"Unity could not review structural Prefab overrides: {exception.Message}"),
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
                    : CreateStateFailure(scanResult, validationMessage);
            }
            catch (Exception exception)
            {
                return CreateStateFailure(
                    scanResult,
                    $"Unity could not verify the final loaded Scene state: {exception.Message}");
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
                "<loaded Scene state>",
                BuildGuardPrefabOverrideScanError.UnityApiFailure,
                $"Review did not preserve the loaded Scene state: {message}"));
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

        /// <summary>Returns true only when every persisted identity field still matches.</summary>
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
        /// Refreshes one finding, selects a loaded Scene object, or pings a closed Scene asset.
        /// </summary>
        internal static BuildGuardPrefabOverrideNavigationOutcome Locate(
            BuildGuardPrefabOverrideFinding snapshot,
            out string message)
        {
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(snapshot.ScenePath);
            if (sceneAsset == null)
            {
                message = $"Scene is unavailable: {snapshot.ScenePath}";
                return BuildGuardPrefabOverrideNavigationOutcome.SceneUnavailable;
            }

            var currentSceneGuid = AssetDatabase.AssetPathToGUID(snapshot.ScenePath);
            if (!StringEquals(snapshot.SceneGuid, currentSceneGuid))
            {
                message = "This result is stale because the Scene asset identity changed. Refresh the review.";
                return BuildGuardPrefabOverrideNavigationOutcome.Stale;
            }

            var originalActiveScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(snapshot.ScenePath);
            var wasLoaded = scene.IsValid() && scene.isLoaded;
            GameObject navigationTarget = null;
            var outcome = BuildGuardPrefabOverrideNavigationOutcome.Stale;
            message = "This result is stale because the structural override changed. Refresh the review.";
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
                    message = $"Could not refresh {snapshot.ScenePath}: {refreshed.ErrorMessage}";
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
                outcome = BuildGuardPrefabOverrideNavigationOutcome.SceneUnavailable;
                message = $"Unity could not refresh {snapshot.ScenePath}: {exception.Message}";
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
                                restoreMessage = $"Unity could not close the temporary Scene {snapshot.ScenePath}.";
                            }
                        }
                        catch (Exception exception)
                        {
                            restoreSucceeded = false;
                            restoreMessage = $"Unity could not close the temporary Scene: {exception.Message}";
                        }
                    }

                    if (originalActiveScene.IsValid()
                        && originalActiveScene.isLoaded
                        && SceneManager.GetActiveScene() != originalActiveScene
                        && !SceneManager.SetActiveScene(originalActiveScene))
                    {
                        restoreSucceeded = false;
                        restoreMessage = "Unity could not restore the original active Scene.";
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
                    message = "This result became stale before the Scene object could be selected.";
                    return BuildGuardPrefabOverrideNavigationOutcome.Stale;
                }

                Selection.activeGameObject = navigationTarget;
                EditorGUIUtility.PingObject(navigationTarget);
                message = $"Selected current override target: {snapshot.TargetHierarchyPath}";
                return outcome;
            }

            if (outcome == BuildGuardPrefabOverrideNavigationOutcome.PingedSceneAsset)
            {
                Selection.activeObject = sceneAsset;
                EditorGUIUtility.PingObject(sceneAsset);
                message = "The finding is current. The Scene was kept closed, so its Scene asset was selected.";
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
