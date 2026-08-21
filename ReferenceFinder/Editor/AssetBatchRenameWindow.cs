using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ReferenceFinder
{
    internal sealed class AssetBatchRenameWindow : EditorWindow
    {
        private const string ToolMenuPath = "Tools/Asset Management/Batch Rename";
        private const string AssetMenuPath = "Assets/Batch Rename Selected Assets";

        private UnityEngine.Object[] _assets = Array.Empty<UnityEngine.Object>();
        private string _findText = string.Empty;
        private string _replacementText = string.Empty;
        private string _prefix = string.Empty;
        private string _suffix = string.Empty;
        private AssetRenamePlan _plan;
        private Vector2 _scroll;
        private string _message = string.Empty;
        private bool _messageIsError;

        [MenuItem(ToolMenuPath, priority = 2010)]
        private static void OpenFromTools()
        {
            Open();
        }

        [MenuItem(AssetMenuPath, false, 2001)]
        private static void OpenFromAssets()
        {
            Open();
        }

        [MenuItem(AssetMenuPath, true)]
        private static bool ValidateOpenFromAssets()
        {
            return Selection.objects.Length > 0;
        }

        private static void Open()
        {
            var window = GetWindow<AssetBatchRenameWindow>("Batch Rename");
            window.UseCurrentSelection();
            window.minSize = new Vector2(640f, 320f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Batch Rename Assets", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Select non-script main assets below Assets, preview every final path, then apply GUID-preserving renames. Scripts, folders, package assets, sub-assets, case-only changes, and collisions are rejected.",
                MessageType.Info);
            EditorGUILayout.LabelField("Selected Assets", _assets.Length.ToString());

            _findText = EditorGUILayout.TextField("Find", _findText);
            _replacementText = EditorGUILayout.TextField("Replace", _replacementText);
            _prefix = EditorGUILayout.TextField("Prefix", _prefix);
            _suffix = EditorGUILayout.TextField("Suffix", _suffix);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Current Selection"))
                {
                    UseCurrentSelection();
                }

                using (new EditorGUI.DisabledScope(_assets.Length == 0))
                {
                    if (GUILayout.Button("Preview"))
                    {
                        Preview();
                    }
                }
            }

            if (!string.IsNullOrEmpty(_message))
            {
                EditorGUILayout.HelpBox(_message, _messageIsError ? MessageType.Error : MessageType.Info);
            }

            if (_plan == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Planned Renames", _plan.Entries.Count.ToString());
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var entry in _plan.Entries)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(entry.OriginalPath, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("To: " + entry.NewPath, EditorStyles.wordWrappedLabel);
                }
            }

            EditorGUILayout.EndScrollView();
            using (new EditorGUI.DisabledScope(_plan.Entries.Count == 0))
            {
                if (GUILayout.Button("Apply Previewed Renames", GUILayout.Height(28f)))
                {
                    Apply();
                }
            }
        }

        private void UseCurrentSelection()
        {
            _assets = Selection.objects.Where(asset => asset != null).ToArray();
            _plan = null;
            _message = _assets.Length == 0
                ? "Select one or more main assets in the Project window."
                : $"Captured {_assets.Length} selected object(s).";
            _messageIsError = false;
            Repaint();
        }

        private void Preview()
        {
            try
            {
                _plan = AssetBatchRenamer.Preview(_assets, _findText, _replacementText, _prefix, _suffix);
                _message = _plan.Entries.Count == 0
                    ? "The current rules do not change any selected asset names."
                    : $"Previewed {_plan.Entries.Count} GUID-preserving rename(s).";
                _messageIsError = false;
            }
            catch (Exception exception)
            {
                _plan = null;
                _message = exception.Message;
                _messageIsError = true;
            }

            Repaint();
        }

        private void Apply()
        {
            if (!EditorUtility.DisplayDialog(
                "Batch Rename Assets",
                $"Rename {_plan.Entries.Count} asset(s)?\n\nReferences are preserved by GUID, but this operation is not added to Unity Undo. Review the preview and use version control for recovery.",
                "Rename",
                "Cancel"))
            {
                return;
            }

            try
            {
                var result = AssetBatchRenamer.Apply(_plan);
                _plan = null;
                _message = $"Renamed {result.RenamedAssetCount} asset(s).";
                _messageIsError = false;
                _assets = result.RenamedAssetPaths
                    .Select(AssetDatabase.LoadMainAssetAtPath)
                    .Where(asset => asset != null)
                    .ToArray();
                Selection.objects = _assets;
            }
            catch (Exception exception)
            {
                _message = exception.Message;
                _messageIsError = true;
            }

            Repaint();
        }
    }
}
