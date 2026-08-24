// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Tests
{
    [Parallelizable(ParallelScope.None)]
    internal sealed class ProjectSetupLayerCollisionIntegrationTests
    {
        [Test]
        public void ApplyAndRestore_NewLayerAndBothCollisionMatricesRoundTripExactly()
        {
            var paths = new[]
            {
                Path.GetFullPath("ProjectSettings/TagManager.asset"),
                Path.GetFullPath("ProjectSettings/DynamicsManager.asset"),
                Path.GetFullPath("ProjectSettings/Physics2DSettings.asset")
            };
            var originalBytes = paths.Select(File.ReadAllBytes).ToArray();
            var layer = $"ProjectSetupCollision{Guid.NewGuid():N}".Substring(0, 31);
            var profile = ScriptableObject.CreateInstance<ProjectSetupProfile>();
            try
            {
                var environment = new UnityProjectSetupEnvironment();
                var snapshot = environment.Capture();
                var newLayerIndex = Array.FindIndex(snapshot.Layers, 8, string.IsNullOrEmpty);
                Assert.That(newLayerIndex, Is.GreaterThanOrEqualTo(8), "The integration Project needs one free user Layer slot.");

                var desired3D = !ProjectSetupLayerCollisionStore.IsCollisionEnabled(
                    snapshot.PhysicsLayerCollisionMasks,
                    0,
                    newLayerIndex);
                var desired2D = !ProjectSetupLayerCollisionStore.IsCollisionEnabled(
                    snapshot.Physics2DLayerCollisionMasks,
                    0,
                    newLayerIndex);

                profile.SetRecommendedDefaults();
                profile.ConfigureAssetSerialization = false;
                profile.ConfigureVersionControl = false;
                profile.ConfigureLayers = true;
                profile.Layers = new[] { layer };
                profile.ConfigurePhysicsLayerCollisions = true;
                profile.PhysicsLayerCollisions = new[]
                {
                    new ProjectSetupLayerCollision("Default", layer, desired3D)
                };
                profile.ConfigurePhysics2DLayerCollisions = true;
                profile.Physics2DLayerCollisions = new[]
                {
                    new ProjectSetupLayerCollision(layer, "Default", desired2D)
                };

                environment.Apply(profile);
                Assert.That(File.ReadAllBytes(paths[1]), Is.Not.EqualTo(originalBytes[1]), "Physics settings were not persisted.");
                Assert.That(File.ReadAllBytes(paths[2]), Is.Not.EqualTo(originalBytes[2]), "Physics 2D settings were not persisted.");
                var changed = environment.Capture();
                var actualLayerIndex = ProjectSetupLayerCollisionStore.FindLayerIndex(changed.Layers, layer);

                Assert.That(actualLayerIndex, Is.EqualTo(newLayerIndex));
                Assert.That(
                    ProjectSetupLayerCollisionStore.IsCollisionEnabled(changed.PhysicsLayerCollisionMasks, 0, actualLayerIndex),
                    Is.EqualTo(desired3D));
                Assert.That(
                    ProjectSetupLayerCollisionStore.IsCollisionEnabled(changed.Physics2DLayerCollisionMasks, 0, actualLayerIndex),
                    Is.EqualTo(desired2D));

                environment.Apply(snapshot);
                var restored = environment.Capture();

                Assert.That(restored.PhysicsLayerCollisionMasks, Is.EqualTo(snapshot.PhysicsLayerCollisionMasks));
                Assert.That(restored.Physics2DLayerCollisionMasks, Is.EqualTo(snapshot.Physics2DLayerCollisionMasks));

                for (var index = 0; index < paths.Length; index++)
                {
                    CollectionAssert.AreEqual(originalBytes[index], File.ReadAllBytes(paths[index]), paths[index]);
                }
            }
            finally
            {
                for (var index = 0; index < paths.Length; index++)
                {
                    if (!File.ReadAllBytes(paths[index]).SequenceEqual(originalBytes[index]))
                    {
                        File.WriteAllBytes(paths[index], originalBytes[index]);
                    }
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
