using System;
using UnityEditor;
using UnityEngine;

namespace ReferenceFinder
{
    internal sealed class ReferenceFinderWindow : EditorWindow
    {
        private UnityEngine.Object _target;
        private AssetReferenceSearchResult _result;
        private Vector2 _scroll;
        private string _error = string.Empty;

        [MenuItem("Tools/Reference Finder")]
        internal static void Open()
        {
            GetWindow<ReferenceFinderWindow>("Reference Finder");
        }

        internal static void Open(UnityEngine.Object target)
        {
            var window = GetWindow<ReferenceFinderWindow>("Reference Finder");
            window._target = target;
            window.RunSearch();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Find Direct Asset References", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select a project or package asset. The search scans Assets and lists assets that directly depend on it.",
                MessageType.Info);

            _target = EditorGUILayout.ObjectField("Target Asset", _target, typeof(UnityEngine.Object), false);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selection"))
                {
                    _target = Selection.activeObject;
                }

                using (new EditorGUI.DisabledScope(!CanSearch(_target)))
                {
                    if (GUILayout.Button("Search"))
                    {
                        RunSearch();
                    }
                }
            }

            if (!string.IsNullOrEmpty(_error))
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }

            if (_result == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Target", _result.TargetAssetPath);
            EditorGUILayout.LabelField(
                "Scanned",
                $"{_result.ScannedAssetCount} / {_result.CandidateAssetCount} assets");
            EditorGUILayout.LabelField("Direct References", _result.ReferenceAssetPaths.Count.ToString());
            if (_result.WasCanceled)
            {
                EditorGUILayout.HelpBox("The search was canceled. Results are incomplete.", MessageType.Warning);
            }

            if (_result.FailedAssetPaths.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    $"{_result.FailedAssetPaths.Count} assets could not be inspected.",
                    MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(_result.ReferenceAssetPaths.Count == 0))
            {
                if (GUILayout.Button("Copy All Paths"))
                {
                    EditorGUIUtility.systemCopyBuffer = string.Join(Environment.NewLine, _result.ReferenceAssetPaths);
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var path in _result.ReferenceAssetPaths)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(path, EditorStyles.linkLabel))
                    {
                        var asset = AssetDatabase.LoadMainAssetAtPath(path);
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }

                    if (GUILayout.Button("Open", GUILayout.Width(52f)))
                    {
                        AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(path));
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void RunSearch()
        {
            _error = string.Empty;
            _result = null;
            try
            {
                var targetPath = AssetDatabase.GetAssetPath(_target);
                _result = AssetReferenceFinder.FindDirectReferencesInternal(
                    targetPath,
                    null,
                    (index, count, path) => !EditorUtility.DisplayCancelableProgressBar(
                        "Reference Finder",
                        path,
                        count == 0 ? 1f : (float)index / count));
            }
            catch (Exception exception)
            {
                _error = exception.Message;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Repaint();
        }

        internal static bool CanSearch(UnityEngine.Object target)
        {
            if (target == null)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(target);
            return !string.IsNullOrEmpty(path) && !AssetDatabase.IsValidFolder(path);
        }
    }
}
