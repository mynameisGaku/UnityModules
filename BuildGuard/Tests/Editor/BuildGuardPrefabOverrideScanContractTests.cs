// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Verifies pure limits, source classification, snapshot and ordering contracts.
    /// </summary>
    internal sealed class BuildGuardPrefabOverrideScanContractTests
    {
        [Test]
        public void DefaultLimits_AreFixedProductionBounds()
        {
            var limits = BuildGuardPrefabOverrideScanLimits.Default;

            Assert.That(limits.MaxVisitedGameObjects, Is.EqualTo(250000));
            Assert.That(limits.MaxPrefabInstances, Is.EqualTo(25000));
            Assert.That(limits.MaxFindings, Is.EqualTo(10000));
            Assert.That(limits.TryValidate(out var message), Is.True);
            Assert.That(message, Is.Empty);
        }

        [TestCase(0, 1, 1, "MaxVisitedGameObjects")]
        [TestCase(1, 0, 1, "MaxPrefabInstances")]
        [TestCase(1, 1, 0, "MaxFindings")]
        public void TryValidate_NonPositiveLimit_ReturnsExactOwner(
            int maxGameObjects,
            int maxInstances,
            int maxFindings,
            string expectedOwner)
        {
            var limits = new BuildGuardPrefabOverrideScanLimits(
                maxGameObjects,
                maxInstances,
                maxFindings);

            Assert.That(limits.TryValidate(out var message), Is.False);
            Assert.That(message, Does.StartWith(expectedOwner));
        }

        [TestCase(PrefabInstanceStatus.MissingAsset)]
        [TestCase(PrefabInstanceStatus.NotAPrefab)]
        public void ClassifyPrefabSource_NonConnectedStatus_Fails(PrefabInstanceStatus status)
        {
            var error = BuildGuardPrefabOverrideSceneScanner.ClassifyPrefabSource(
                status,
                PrefabAssetType.Regular,
                "Assets/Source.prefab");

            Assert.That(error, Is.EqualTo(BuildGuardPrefabOverrideScanError.UnsupportedPrefabInstanceStatus));
        }

        [Test]
        public void ClassifyPrefabSource_LegacyDisconnectedValue_Fails()
        {
            var disconnected = (PrefabInstanceStatus)2;

            var error = BuildGuardPrefabOverrideSceneScanner.ClassifyPrefabSource(
                disconnected,
                PrefabAssetType.Regular,
                "Assets/Source.prefab");

            Assert.That(error, Is.EqualTo(BuildGuardPrefabOverrideScanError.UnsupportedPrefabInstanceStatus));
        }

        [Test]
        public void ClassifyPrefabSource_MissingTypeOrPath_Fails()
        {
            Assert.That(
                BuildGuardPrefabOverrideSceneScanner.ClassifyPrefabSource(
                    PrefabInstanceStatus.Connected,
                    PrefabAssetType.MissingAsset,
                    "Assets/Source.prefab"),
                Is.EqualTo(BuildGuardPrefabOverrideScanError.MissingPrefabSource));
            Assert.That(
                BuildGuardPrefabOverrideSceneScanner.ClassifyPrefabSource(
                    PrefabInstanceStatus.Connected,
                    PrefabAssetType.Regular,
                    string.Empty),
                Is.EqualTo(BuildGuardPrefabOverrideScanError.MissingPrefabSource));
            Assert.That(
                BuildGuardPrefabOverrideSceneScanner.ClassifyPrefabSource(
                    PrefabInstanceStatus.Connected,
                    PrefabAssetType.NotAPrefab,
                    "Assets/Source.prefab"),
                Is.EqualTo(BuildGuardPrefabOverrideScanError.MissingPrefabSource));
        }

        [Test]
        public void Success_DetachesFindingSnapshot()
        {
            var source = new List<BuildGuardPrefabOverrideFinding>
            {
                CreateFinding(BuildGuardPrefabOverrideKind.AddedComponent, "Root[0]", "UnityEngine.BoxCollider", 1),
            };

            var result = BuildGuardPrefabOverrideScanResult.Success(source, 1, 1);
            source.Clear();

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.Error, Is.EqualTo(BuildGuardPrefabOverrideScanError.None));
            Assert.That(result.Findings, Has.Count.EqualTo(1));
        }

        [Test]
        public void Failure_NeverExposesPartialFindings()
        {
            var result = BuildGuardPrefabOverrideScanResult.Failure(
                BuildGuardPrefabOverrideScanError.TooManyFindings,
                "limit",
                10,
                2);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Findings, Is.Empty);
            Assert.That(result.VisitedGameObjectCount, Is.EqualTo(10));
            Assert.That(result.ScannedPrefabInstanceCount, Is.EqualTo(2));
        }

        [Test]
        public void CompareFindings_UsesKindPathTypeAndIndexDeterministically()
        {
            var sphere = CreateFinding(
                BuildGuardPrefabOverrideKind.AddedComponent,
                "Root[0]/Target[0]",
                "UnityEngine.SphereCollider",
                1);
            var box = CreateFinding(
                BuildGuardPrefabOverrideKind.AddedComponent,
                "Root[0]/Target[0]",
                "UnityEngine.BoxCollider",
                2);
            var removed = CreateFinding(
                BuildGuardPrefabOverrideKind.RemovedComponent,
                "Root[0]/A[0]",
                "UnityEngine.BoxCollider",
                1);
            var findings = new List<BuildGuardPrefabOverrideFinding> { removed, sphere, box };

            findings.Sort(BuildGuardPrefabOverrideSceneScanner.CompareFindings);

            Assert.That(findings[0].ComponentTypeName, Is.EqualTo("UnityEngine.BoxCollider"));
            Assert.That(findings[1].ComponentTypeName, Is.EqualTo("UnityEngine.SphereCollider"));
            Assert.That(findings[2].Kind, Is.EqualTo(BuildGuardPrefabOverrideKind.RemovedComponent));
        }

        private static BuildGuardPrefabOverrideFinding CreateFinding(
            BuildGuardPrefabOverrideKind kind,
            string targetPath,
            string componentType,
            int componentIndex)
        {
            return new BuildGuardPrefabOverrideFinding(
                kind,
                "Assets/Scene.unity",
                "scene-guid",
                "Assets/Source.prefab",
                "prefab-guid",
                PrefabAssetType.Regular,
                "Assets/Source.prefab",
                PrefabAssetType.Regular,
                false,
                "Root[0]",
                targetPath,
                string.Empty,
                componentType,
                componentIndex,
                "root-id",
                "target-id",
                string.Empty);
        }
    }
}
