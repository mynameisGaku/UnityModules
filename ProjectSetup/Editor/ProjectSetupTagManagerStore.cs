// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditorInternal;

namespace ProjectSetup.Editor
{
    internal static class ProjectSetupTagManagerStore
    {
        private const string TagManagerPath = "ProjectSettings/TagManager.asset";

        internal static void Capture(
            out string[] availableTags,
            out string[] customTags,
            out string[] layers,
            out ProjectSetupSortingLayer[] sortingLayers,
            out string tagManagerFileText)
        {
            var serialized = Load();
            serialized.Update();
            customTags = ReadStrings(Require(serialized, "tags"));
            availableTags = InternalEditorUtility.tags ?? Array.Empty<string>();
            layers = ReadStrings(Require(serialized, "layers"));
            sortingLayers = ReadSortingLayers(Require(serialized, "m_SortingLayers"));
            tagManagerFileText = File.ReadAllText(Path.GetFullPath(TagManagerPath), new UTF8Encoding(false, true));
        }

        internal static void Apply(ProjectSetupProfile profile)
        {
            var serialized = Load();
            serialized.Update();
            var changed = false;
            if (profile.ConfigureTags)
            {
                changed |= AppendMissingStrings(Require(serialized, "tags"), profile.Tags);
            }

            if (profile.ConfigureLayers)
            {
                changed |= FillMissingLayers(Require(serialized, "layers"), profile.Layers);
            }

            if (profile.ConfigureSortingLayers)
            {
                changed |= AppendMissingSortingLayers(Require(serialized, "m_SortingLayers"), profile.SortingLayers);
            }

            if (changed)
            {
                Apply(serialized);
            }
        }

        internal static void Restore(ProjectSetupSnapshot snapshot)
        {
            if (!snapshot.HasTagManagerData)
            {
                return;
            }

            if (!string.IsNullOrEmpty(snapshot.TagManagerFileText))
            {
                RestoreFileText(snapshot.TagManagerFileText);
                return;
            }

            var serialized = Load();
            serialized.Update();
            WriteStrings(Require(serialized, "tags"), snapshot.CustomTags);
            WriteStrings(Require(serialized, "layers"), snapshot.Layers);
            WriteSortingLayers(Require(serialized, "m_SortingLayers"), snapshot.SortingLayers);
            Apply(serialized);
        }

        private static void RestoreFileText(string text)
        {
            var path = Path.GetFullPath(TagManagerPath);
            if (File.Exists(path)
                && string.Equals(File.ReadAllText(path), text, StringComparison.Ordinal))
            {
                return;
            }

            var temporaryPath = path + ".project-setup.tmp";
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(text);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }

                AssetDatabase.ImportAsset(TagManagerPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }

        private static SerializedObject Load()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
            if (assets == null || assets.Length == 0 || assets[0] == null)
            {
                throw new InvalidOperationException("TagManager.asset is unavailable.");
            }

            return new SerializedObject(assets[0]);
        }

        private static SerializedProperty Require(SerializedObject serialized, string name)
        {
            return serialized.FindProperty(name) ?? throw new InvalidOperationException($"TagManager property '{name}' is unavailable.");
        }

        private static string[] ReadStrings(SerializedProperty property)
        {
            var values = new string[property.arraySize];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = property.GetArrayElementAtIndex(index).stringValue ?? string.Empty;
            }

            return values;
        }

        private static ProjectSetupSortingLayer[] ReadSortingLayers(SerializedProperty property)
        {
            var values = new ProjectSetupSortingLayer[property.arraySize];
            for (var index = 0; index < values.Length; index++)
            {
                var item = property.GetArrayElementAtIndex(index);
                values[index] = new ProjectSetupSortingLayer(
                    item.FindPropertyRelative("name").stringValue,
                    item.FindPropertyRelative("uniqueID").intValue,
                    item.FindPropertyRelative("locked").boolValue);
            }

            return values;
        }

        private static bool AppendMissingStrings(SerializedProperty property, IReadOnlyList<string> requested)
        {
            var existing = new HashSet<string>(ReadStrings(property), StringComparer.Ordinal);
            var changed = false;
            for (var index = 0; index < requested.Count; index++)
            {
                var value = requested[index];
                if (!existing.Add(value))
                {
                    continue;
                }

                property.InsertArrayElementAtIndex(property.arraySize);
                property.GetArrayElementAtIndex(property.arraySize - 1).stringValue = value;
                changed = true;
            }

            return changed;
        }

        private static bool FillMissingLayers(SerializedProperty property, IReadOnlyList<string> requested)
        {
            var existing = new HashSet<string>(ReadStrings(property), StringComparer.Ordinal);
            var changed = false;
            for (var requestedIndex = 0; requestedIndex < requested.Count; requestedIndex++)
            {
                var value = requested[requestedIndex];
                if (!existing.Add(value))
                {
                    continue;
                }

                var freeIndex = -1;
                for (var layerIndex = 8; layerIndex < property.arraySize; layerIndex++)
                {
                    if (string.IsNullOrEmpty(property.GetArrayElementAtIndex(layerIndex).stringValue))
                    {
                        freeIndex = layerIndex;
                        break;
                    }
                }

                if (freeIndex < 0)
                {
                    throw new InvalidOperationException("No free user Layer slot is available.");
                }

                property.GetArrayElementAtIndex(freeIndex).stringValue = value;
                changed = true;
            }

            return changed;
        }

        private static bool AppendMissingSortingLayers(SerializedProperty property, IReadOnlyList<string> requested)
        {
            var existingNames = new HashSet<string>(StringComparer.Ordinal);
            var existingIds = new HashSet<int>();
            for (var index = 0; index < property.arraySize; index++)
            {
                var item = property.GetArrayElementAtIndex(index);
                existingNames.Add(item.FindPropertyRelative("name").stringValue ?? string.Empty);
                existingIds.Add(item.FindPropertyRelative("uniqueID").intValue);
            }

            var changed = false;
            for (var requestedIndex = 0; requestedIndex < requested.Count; requestedIndex++)
            {
                var value = requested[requestedIndex];
                if (!existingNames.Add(value))
                {
                    continue;
                }

                var uniqueId = 1;
                while (existingIds.Contains(uniqueId))
                {
                    if (uniqueId == int.MaxValue)
                    {
                        throw new InvalidOperationException("No unique Sorting Layer identifier is available.");
                    }

                    uniqueId++;
                }

                existingIds.Add(uniqueId);
                property.InsertArrayElementAtIndex(property.arraySize);
                var item = property.GetArrayElementAtIndex(property.arraySize - 1);
                item.FindPropertyRelative("name").stringValue = value;
                item.FindPropertyRelative("uniqueID").intValue = uniqueId;
                item.FindPropertyRelative("locked").boolValue = false;
                changed = true;
            }

            return changed;
        }

        private static void WriteStrings(SerializedProperty property, IReadOnlyList<string> values)
        {
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                property.GetArrayElementAtIndex(index).stringValue = values[index];
            }
        }

        private static void WriteSortingLayers(SerializedProperty property, IReadOnlyList<ProjectSetupSortingLayer> values)
        {
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++)
            {
                var source = values[index];
                var item = property.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("name").stringValue = source.Name;
                item.FindPropertyRelative("uniqueID").intValue = source.UniqueId;
                item.FindPropertyRelative("locked").boolValue = source.Locked;
            }
        }

        private static void Apply(SerializedObject serialized)
        {
            if (!serialized.ApplyModifiedPropertiesWithoutUndo())
            {
                throw new InvalidOperationException("TagManager changes could not be applied.");
            }

            AssetDatabase.SaveAssets();
        }
    }
}
