// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Editor
{
    internal static class ProjectSetupLayerCollisionStore
    {
        internal const int LayerCount = 32;
        private const string PhysicsSettingsPath = "ProjectSettings/DynamicsManager.asset";
        private const string Physics2DSettingsPath = "ProjectSettings/Physics2DSettings.asset";

        internal static void Capture(out int[] physicsMasks, out int[] physics2DMasks)
        {
            physicsMasks = CaptureMasks(Physics.GetIgnoreLayerCollision);
            physics2DMasks = CaptureMasks(Physics2D.GetIgnoreLayerCollision);
        }

        internal static void Apply(ProjectSetupProfile profile, IReadOnlyList<string> layers)
        {
            if (profile.ConfigurePhysicsLayerCollisions
                && ApplyRules(
                    profile.PhysicsLayerCollisions,
                    layers,
                    Physics.GetIgnoreLayerCollision,
                    Physics.IgnoreLayerCollision))
            {
                SaveSettingsAsset(PhysicsSettingsPath);
            }

            if (profile.ConfigurePhysics2DLayerCollisions
                && ApplyRules(
                    profile.Physics2DLayerCollisions,
                    layers,
                    Physics2D.GetIgnoreLayerCollision,
                    Physics2D.IgnoreLayerCollision))
            {
                SaveSettingsAsset(Physics2DSettingsPath);
            }
        }

        internal static void Restore(ProjectSetupSnapshot snapshot)
        {
            if (snapshot.HasPhysicsLayerCollisionData
                && RestoreMasks(snapshot.PhysicsLayerCollisionMasks, Physics.GetIgnoreLayerCollision, Physics.IgnoreLayerCollision))
            {
                SaveSettingsAsset(PhysicsSettingsPath);
            }

            if (snapshot.HasPhysics2DLayerCollisionData
                && RestoreMasks(snapshot.Physics2DLayerCollisionMasks, Physics2D.GetIgnoreLayerCollision, Physics2D.IgnoreLayerCollision))
            {
                SaveSettingsAsset(Physics2DSettingsPath);
            }
        }

        internal static ProjectSetupLayerCollision[] CreateNamedRules(
            IReadOnlyList<string> layers,
            IReadOnlyList<int> masks)
        {
            if (layers == null || layers.Count != LayerCount || !IsSymmetric(masks))
            {
                return Array.Empty<ProjectSetupLayerCollision>();
            }

            var rules = new List<ProjectSetupLayerCollision>();
            for (var first = 0; first < LayerCount; first++)
            {
                if (string.IsNullOrEmpty(layers[first]))
                {
                    continue;
                }

                for (var second = first; second < LayerCount; second++)
                {
                    if (string.IsNullOrEmpty(layers[second]))
                    {
                        continue;
                    }

                    rules.Add(new ProjectSetupLayerCollision(
                        layers[first],
                        layers[second],
                        IsCollisionEnabled(masks, first, second)));
                }
            }

            return rules.ToArray();
        }

        internal static bool IsCollisionEnabled(IReadOnlyList<int> masks, int firstLayer, int secondLayer)
        {
            return masks != null
                && masks.Count == LayerCount
                && firstLayer >= 0
                && firstLayer < LayerCount
                && secondLayer >= 0
                && secondLayer < LayerCount
                && (masks[firstLayer] & (1 << secondLayer)) != 0;
        }

        internal static bool IsSymmetric(IReadOnlyList<int> masks)
        {
            if (masks == null || masks.Count != LayerCount)
            {
                return false;
            }

            for (var first = 0; first < LayerCount; first++)
            {
                for (var second = first; second < LayerCount; second++)
                {
                    if (IsCollisionEnabled(masks, first, second)
                        != IsCollisionEnabled(masks, second, first))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        internal static int FindLayerIndex(IReadOnlyList<string> layers, string name)
        {
            if (layers == null || string.IsNullOrEmpty(name))
            {
                return -1;
            }

            for (var index = 0; index < layers.Count; index++)
            {
                if (string.Equals(layers[index], name, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private static int[] CaptureMasks(Func<int, int, bool> getIgnore)
        {
            var masks = new int[LayerCount];
            for (var first = 0; first < LayerCount; first++)
            {
                var mask = 0;
                for (var second = 0; second < LayerCount; second++)
                {
                    if (!getIgnore(first, second))
                    {
                        mask |= 1 << second;
                    }
                }

                masks[first] = mask;
            }

            return masks;
        }

        private static bool ApplyRules(
            IReadOnlyList<ProjectSetupLayerCollision> rules,
            IReadOnlyList<string> layers,
            Func<int, int, bool> getIgnore,
            Action<int, int, bool> setIgnore)
        {
            var changed = false;
            for (var index = 0; index < rules.Count; index++)
            {
                var rule = rules[index] ?? throw new InvalidOperationException($"Layer collision rule {index + 1} is missing.");
                var first = FindLayerIndex(layers, rule.FirstLayer);
                var second = FindLayerIndex(layers, rule.SecondLayer);
                if (first < 0 || second < 0)
                {
                    throw new InvalidOperationException($"Layer collision rule {index + 1} refers to an unavailable Layer.");
                }

                var desiredIgnore = !rule.CollisionsEnabled;
                if (getIgnore(first, second) == desiredIgnore)
                {
                    continue;
                }

                setIgnore(first, second, desiredIgnore);
                changed = true;
            }

            return changed;
        }

        private static bool RestoreMasks(
            IReadOnlyList<int> masks,
            Func<int, int, bool> getIgnore,
            Action<int, int, bool> setIgnore)
        {
            if (!IsSymmetric(masks))
            {
                throw new InvalidOperationException($"Layer collision backup must contain exactly {LayerCount} symmetric rows.");
            }

            var changed = false;
            for (var first = 0; first < LayerCount; first++)
            {
                for (var second = first; second < LayerCount; second++)
                {
                    var desiredIgnore = !IsCollisionEnabled(masks, first, second);
                    if (getIgnore(first, second) == desiredIgnore)
                    {
                        continue;
                    }

                    setIgnore(first, second, desiredIgnore);
                    changed = true;
                }
            }

            return changed;
        }

        private static void SaveSettingsAsset(string path)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0 || assets[0] == null)
            {
                throw new InvalidOperationException($"Physics settings asset '{path}' is unavailable.");
            }

            EditorUtility.SetDirty(assets[0]);
            AssetDatabase.SaveAssetIfDirty(assets[0]);
        }
    }
}
