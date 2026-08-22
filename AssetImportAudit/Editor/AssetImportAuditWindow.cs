using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetImportAudit.Editor
{
    /// <summary>Provides the ordered Preview, selection, and Apply workflow for texture import settings.</summary>
    public sealed class AssetImportAuditWindow : EditorWindow
    {
        private string _rootFolder = "Assets";
        private int _maxTextureSize = 2048;
        private TextureImporterCompression _compression = TextureImporterCompression.Compressed;
        private bool _mipmapEnabled;
        private bool _sRgbTexture = true;
        private bool _readable;
        private FilterMode _filterMode = FilterMode.Bilinear;
        private int _anisoLevel = 1;
        private AssetImportAuditPlan _plan;
        private readonly HashSet<string> _selectedPaths = new HashSet<string>(StringComparer.Ordinal);
        private Vector2 _scrollPosition;
        private string _message;

        private void OnEnable()
        {
            minSize = new Vector2(520f, 520f);
        }

        /// <summary>Opens the audit window.</summary>
        [MenuItem("Tools/Asset Import Audit/Open")]
        public static void Open()
        {
            GetWindow<AssetImportAuditWindow>("Asset Import Audit");
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Choose a folder, set expected values, Preview differences, then Apply selected or all assets.", MessageType.Info);
            _rootFolder = EditorGUILayout.TextField("Root Folder", _rootFolder);
            if (GUILayout.Button("Use Selection", GUILayout.Width(110f)))
                UseSelection();

            EditorGUILayout.Space(4f);
            _maxTextureSize = EditorGUILayout.IntField("Max Texture Size", _maxTextureSize);
            _compression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", _compression);
            _mipmapEnabled = EditorGUILayout.Toggle("Mipmaps", _mipmapEnabled);
            _sRgbTexture = EditorGUILayout.Toggle("sRGB", _sRgbTexture);
            _readable = EditorGUILayout.Toggle("Read/Write", _readable);
            _filterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", _filterMode);
            _anisoLevel = EditorGUILayout.IntSlider("Aniso Level", _anisoLevel, 0, 16);

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Preview", GUILayout.Height(24f)))
                Preview();

            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.Warning);
            if (_plan == null)
                return;

            EditorGUILayout.LabelField($"{_plan.Issues.Count} mismatches in {_plan.Entries.Count} assets", EditorStyles.boldLabel);
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scroll.scrollPosition;
                foreach (var group in _plan.Issues.GroupBy(issue => issue.AssetPath, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
                {
                    var selected = _selectedPaths.Contains(group.Key);
                    var next = EditorGUILayout.ToggleLeft(group.Key, selected);
                    if (next)
                        _selectedPaths.Add(group.Key);
                    else
                        _selectedPaths.Remove(group.Key);
                    foreach (var issue in group)
                        EditorGUILayout.LabelField($"    {issue.SettingName}: {issue.CurrentValue} -> {issue.ExpectedValue}", EditorStyles.miniLabel);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_plan.IsEmpty))
                {
                    if (GUILayout.Button("Apply Selected"))
                        ApplySelected();
                    if (GUILayout.Button("Apply All"))
                        ApplyAll();
                }
                if (GUILayout.Button("Clear"))
                {
                    _plan = null;
                    _selectedPaths.Clear();
                    _message = null;
                }
            }
        }

        private void UseSelection()
        {
            var selectedPath = Selection.activeObject == null ? string.Empty : AssetDatabase.GetAssetPath(Selection.activeObject);
            if (AssetDatabase.IsValidFolder(selectedPath))
                _rootFolder = selectedPath;
        }

        private void Preview()
        {
            try
            {
                var settings = new AssetImportAuditTextureSettings(_maxTextureSize, _compression, _mipmapEnabled, _sRgbTexture, _readable, _filterMode, _anisoLevel);
                _plan = AssetImportAuditService.Preview(_rootFolder, settings);
                _selectedPaths.Clear();
                foreach (var issue in _plan.Issues)
                    _selectedPaths.Add(issue.AssetPath);
                _message = _plan.IsEmpty ? "No differences found." : null;
            }
            catch (Exception exception)
            {
                _plan = null;
                _message = exception.Message;
            }
        }

        private void ApplySelected()
        {
            Apply(AssetImportAuditService.Apply(_plan, _selectedPaths));
        }

        private void ApplyAll()
        {
            Apply(AssetImportAuditService.Apply(_plan));
        }

        private void Apply(AssetImportAuditApplyResult result)
        {
            _message = result.Succeeded ? $"Applied {result.AppliedAssetCount} assets." : $"Apply failed: {result.Error}. Preview again before retrying.";
            if (result.Succeeded)
                Preview();
        }
    }
}
