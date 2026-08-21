// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BuildGuard.Editor
{
    /// <summary>
    /// Scans every loaded Scene component for serialized object references whose targets are missing.
    /// </summary>
    internal static class MissingObjectReferenceSceneScanner
    {
        /// <summary>Scans active and inactive GameObjects in deterministic hierarchy order.</summary>
        internal static IReadOnlyList<MissingObjectReferenceFinding> Scan(Scene scene)
        {
            if (!scene.IsValid())
            {
                throw new ArgumentException("The Scene to scan is invalid.", nameof(scene));
            }

            if (!scene.isLoaded)
            {
                throw new InvalidOperationException("The Scene to scan is not loaded.");
            }

            var findings = new List<MissingObjectReferenceFinding>();
            foreach (var root in BuildGuardHierarchyPath.GetSortedRoots(scene))
            {
                ScanTransform(root.transform, BuildGuardHierarchyPath.FormatSegment(root.transform), findings);
            }

            findings.Sort(CompareFindings);
            return findings;
        }

        private static void ScanTransform(
            Transform current,
            string hierarchyPath,
            ICollection<MissingObjectReferenceFinding> findings)
        {
            var components = current.GetComponents<Component>();
            for (var componentIndex = 0; componentIndex < components.Length; componentIndex++)
            {
                var component = components[componentIndex];
                if (component == null)
                {
                    continue;
                }

                ScanComponent(component, componentIndex, hierarchyPath, findings);
            }

            for (var childIndex = 0; childIndex < current.childCount; childIndex++)
            {
                var child = current.GetChild(childIndex);
                ScanTransform(child, $"{hierarchyPath}/{BuildGuardHierarchyPath.FormatSegment(child)}", findings);
            }
        }

        private static void ScanComponent(
            Component component,
            int componentIndex,
            string hierarchyPath,
            ICollection<MissingObjectReferenceFinding> findings)
        {
            var serializedObject = new SerializedObject(component);
            var property = serializedObject.GetIterator();
            if (!property.Next(true))
            {
                return;
            }

            do
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference
                    && property.objectReferenceValue == null
                    && property.objectReferenceEntityIdValue.IsValid())
                {
                    var componentType = component.GetType();
                    findings.Add(new MissingObjectReferenceFinding(
                        hierarchyPath,
                        componentType.FullName ?? componentType.Name,
                        componentIndex,
                        property.propertyPath));
                }
            }
            while (property.Next(true));
        }

        private static int CompareFindings(
            MissingObjectReferenceFinding left,
            MissingObjectReferenceFinding right)
        {
            var hierarchyOrder = string.CompareOrdinal(left.HierarchyPath, right.HierarchyPath);
            if (hierarchyOrder != 0)
            {
                return hierarchyOrder;
            }

            var componentOrder = left.ComponentIndex.CompareTo(right.ComponentIndex);
            return componentOrder != 0
                ? componentOrder
                : string.CompareOrdinal(left.PropertyPath, right.PropertyPath);
        }

    }
}
