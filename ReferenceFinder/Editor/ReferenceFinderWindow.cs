using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ReferenceFinder
{
    internal sealed class ReferenceFinderWindow : EditorWindow
    {
        private UnityEngine.Object _target;
        private UnityEngine.Object _replacement;
        [SerializeField] private AssetReferenceSearchMode _searchMode = AssetReferenceSearchMode.Direct;
        [SerializeField] private DefaultAsset _searchRoot;
        private AssetReferenceSearchResult _result;
        private AssetReferenceReplacementPlan _replacementPlan;
        private string _resultSearchRoot = string.Empty;
        private Vector2 _scroll;
        private string _error = string.Empty;
        private string _replacementMessage = string.Empty;

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
            EditorGUILayout.LabelField("Manage Asset References", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Find assets that use the target, then preview exact serialized properties before an Undo-supported replacement.",
                MessageType.Info);

            _target = EditorGUILayout.ObjectField("Target Asset", _target, typeof(UnityEngine.Object), false);
            _searchMode = (AssetReferenceSearchMode)EditorGUILayout.EnumPopup("Search Mode", _searchMode);
            _searchRoot = (DefaultAsset)EditorGUILayout.ObjectField(
                "Search Root",
                _searchRoot,
                typeof(DefaultAsset),
                false);

            EditorGUILayout.HelpBox(
                _searchMode == AssetReferenceSearchMode.Direct
                    ? "Direct finds immediate asset dependencies."
                    : "Recursive also finds assets that depend on the target through other assets.",
                MessageType.None);

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

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Selection Folder"))
                {
                    _searchRoot = ResolveSelectionFolder(Selection.activeObject);
                }

                if (GUILayout.Button("Search All Assets"))
                {
                    _searchRoot = null;
                }
            }

            if (!string.IsNullOrEmpty(_error))
            {
                EditorGUILayout.HelpBox(_error, MessageType.Error);
            }

            if (_result == null)
            {
                DrawReplacementSection();
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Target", _result.TargetAssetPath);
            EditorGUILayout.LabelField("Search Mode", _result.SearchMode.ToString());
            EditorGUILayout.LabelField(
                "Search Root",
                _resultSearchRoot);
            EditorGUILayout.LabelField(
                "Scanned",
                $"{_result.ScannedAssetCount} / {_result.CandidateAssetCount} assets");
            EditorGUILayout.LabelField("Matching Assets", _result.ReferenceAssetPaths.Count.ToString());
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

            DrawReplacementSection();

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

        private void DrawReplacementSection()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Safe Replacement", EditorStyles.boldLabel);
            _replacement = EditorGUILayout.ObjectField(
                "Replacement Asset",
                _replacement,
                typeof(UnityEngine.Object),
                false);
            EditorGUILayout.HelpBox(
                "Preview scans direct serialized object-reference properties only. Scenes and unsupported asset formats are listed but never changed.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(!CanSearch(_target) || !CanSearch(_replacement)))
            {
                if (GUILayout.Button("Preview Replacement"))
                {
                    PreviewReplacement();
                }
            }

            if (!string.IsNullOrEmpty(_replacementMessage))
            {
                EditorGUILayout.HelpBox(_replacementMessage, MessageType.Info);
            }

            if (_replacementPlan == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Exact References", _replacementPlan.Occurrences.Count.ToString());
            EditorGUILayout.LabelField("Unsupported Assets", _replacementPlan.UnsupportedAssetPaths.Count.ToString());
            EditorGUILayout.LabelField("Inspection Failures", _replacementPlan.FailedAssetPaths.Count.ToString());

            foreach (var occurrence in _replacementPlan.Occurrences)
            {
                EditorGUILayout.LabelField(
                    occurrence.AssetPath,
                    $"{occurrence.OwnerName} / {occurrence.PropertyPath}");
            }

            if (_replacementPlan.UnsupportedAssetPaths.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Unsupported assets remain unchanged:\n" + string.Join("\n", _replacementPlan.UnsupportedAssetPaths),
                    MessageType.Warning);
            }

            if (_replacementPlan.FailedAssetPaths.Count > 0)
            {
                EditorGUILayout.HelpBox(
                    "Replacement is blocked because some assets could not be inspected:\n"
                    + string.Join("\n", _replacementPlan.FailedAssetPaths),
                    MessageType.Error);
            }

            using (new EditorGUI.DisabledScope(
                _replacementPlan.Occurrences.Count == 0 || _replacementPlan.FailedAssetPaths.Count > 0))
            {
                if (GUILayout.Button("Replace Previewed References"))
                {
                    ApplyReplacement();
                }
            }
        }

        private void RunSearch(bool clearReplacementMessage = true)
        {
            _error = string.Empty;
            _result = null;
            _replacementPlan = null;
            if (clearReplacementMessage)
            {
                _replacementMessage = string.Empty;
            }
            try
            {
                var targetPath = AssetDatabase.GetAssetPath(_target);
                var searchFolders = GetSearchFolders();
                _resultSearchRoot = searchFolders == null ? "Assets" : string.Join(", ", searchFolders);
                _result = AssetReferenceFinder.FindReferencesInternal(
                    targetPath,
                    searchFolders,
                    _searchMode,
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

        private void PreviewReplacement()
        {
            _error = string.Empty;
            _replacementMessage = string.Empty;
            _replacementPlan = null;
            try
            {
                _replacementPlan = AssetReferenceReplacer.Preview(_target, _replacement, GetSearchFolders());
                _replacementMessage = _replacementPlan.Occurrences.Count == 0
                    ? "No replaceable serialized references were found."
                    : $"Previewed {_replacementPlan.Occurrences.Count} exact serialized references.";
            }
            catch (Exception exception)
            {
                _error = exception.Message;
            }

            Repaint();
        }

        private void ApplyReplacement()
        {
            var unsupportedCount = _replacementPlan.UnsupportedAssetPaths.Count;
            var detail = unsupportedCount == 0
                ? "Every previewed reference will be changed."
                : $"{unsupportedCount} unsupported assets will remain unchanged.";
            if (!EditorUtility.DisplayDialog(
                "Replace Asset References",
                $"Replace {_replacementPlan.Occurrences.Count} serialized references?\n\n{detail}\n\nThe operation can be reverted with Undo.",
                "Replace",
                "Cancel"))
            {
                return;
            }

            try
            {
                var replacementResult = AssetReferenceReplacer.Apply(_replacementPlan);
                var successMessage = $"Replaced {replacementResult.ReplacedReferenceCount} references in {replacementResult.ChangedAssetPaths.Count} assets.";
                _replacementPlan = null;
                RunSearch(false);
                _replacementMessage = successMessage;
            }
            catch (Exception exception)
            {
                _error = exception.Message;
            }

            Repaint();
        }

        private string[] GetSearchFolders()
        {
            if (_searchRoot == null)
            {
                return null;
            }

            var path = AssetDatabase.GetAssetPath(_searchRoot);
            if (!AssetDatabase.IsValidFolder(path)
                || !(string.Equals(path, "Assets", StringComparison.Ordinal)
                    || path.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                throw new ArgumentException("Search Root must be a folder inside Assets.");
            }

            return new[] { path };
        }

        internal static DefaultAsset ResolveSelectionFolder(UnityEngine.Object selection)
        {
            var path = AssetDatabase.GetAssetPath(selection);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var folderPath = AssetDatabase.IsValidFolder(path)
                ? path
                : Path.GetDirectoryName(path)?.Replace('\\', '/');
            return string.IsNullOrEmpty(folderPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<DefaultAsset>(folderPath);
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
