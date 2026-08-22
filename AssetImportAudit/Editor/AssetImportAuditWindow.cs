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
        private static readonly string[] PlatformLabels = { "Standalone", "Android", "iOS" };
        private static readonly string[] TextureSizeLabels = AssetImportAuditTextureSize.CreateLabels();
        private static readonly int[] TextureSizeValues = AssetImportAuditTextureSize.CreateValues();
        private static readonly AssetImportAuditTexturePlatform[] Platforms =
        {
            AssetImportAuditTexturePlatform.Standalone,
            AssetImportAuditTexturePlatform.Android,
            AssetImportAuditTexturePlatform.iOS
        };

        private enum AuditScope
        {
            SharedSettings = 0,
            PlatformOverride = 1,
            SharedAndPlatform = 2
        }

        private string _rootFolder = "Assets";
        private AuditScope _auditScope = AuditScope.SharedSettings;
        private int _maxTextureSize = 2048;
        private TextureImporterCompression _compression = TextureImporterCompression.Compressed;
        private bool _mipmapEnabled;
        private bool _sRgbTexture = true;
        private bool _readable;
        private FilterMode _filterMode = FilterMode.Bilinear;
        private int _anisoLevel = 1;
        private AssetImportAuditTexturePlatform _platform = AssetImportAuditTexturePlatform.Standalone;
        private bool _platformOverrideEnabled = true;
        private int _platformMaxTextureSize = 2048;
        private TextureImporterCompression _platformCompression = TextureImporterCompression.Compressed;
        private AssetImportAuditPlan _plan;
        private readonly HashSet<string> _selectedPaths = new HashSet<string>(StringComparer.Ordinal);
        private Vector2 _scrollPosition;
        private string _message;
        private GUIStyle _assetPathStyle;
        private GUIStyle _issueStyle;

        private void OnEnable()
        {
            minSize = new Vector2(560f, 620f);
        }

        /// <summary>Opens the audit window.</summary>
        [MenuItem("Tools/Asset Import Audit/Open")]
        public static void Open()
        {
            GetWindow<AssetImportAuditWindow>("Asset Import Audit");
        }

        private void OnGUI()
        {
            EnsureStyles();
            EditorGUILayout.HelpBox("Work from top to bottom: choose assets, set expected values, Preview differences, then Apply selected or all assets.", MessageType.Info);

            EditorGUILayout.LabelField("1. Target Folder", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _rootFolder = EditorGUILayout.TextField("Root Folder", _rootFolder);
            if (EditorGUI.EndChangeCheck())
                ClearPreview();
            if (GUILayout.Button("Use Selection", GUILayout.Width(110f)))
                UseSelection();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("2. Expected Settings", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _auditScope = (AuditScope)EditorGUILayout.EnumPopup("Settings Scope", _auditScope);
            if (_auditScope == AuditScope.SharedSettings || _auditScope == AuditScope.SharedAndPlatform)
                DrawSharedSettings();
            if (_auditScope == AuditScope.PlatformOverride || _auditScope == AuditScope.SharedAndPlatform)
                DrawPlatformSettings();
            if (EditorGUI.EndChangeCheck())
                ClearPreview();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("3. Preview", EditorStyles.boldLabel);
            if (GUILayout.Button("Preview", GUILayout.Height(24f)))
                Preview();

            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.Warning);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("4. Review Differences", EditorStyles.boldLabel);
            if (_plan == null)
            {
                EditorGUILayout.HelpBox("Preview results appear here.", MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField($"{_plan.Issues.Count} mismatches in {_plan.Entries.Count} assets", EditorStyles.boldLabel);
                using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
                {
                    _scrollPosition = scroll.scrollPosition;
                    foreach (var group in _plan.Issues.GroupBy(issue => issue.AssetPath, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
                    {
                        var selected = _selectedPaths.Contains(group.Key);
                        bool next;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            next = EditorGUILayout.Toggle(selected, GUILayout.Width(18f));
                            EditorGUILayout.LabelField(group.Key, _assetPathStyle);
                        }
                        if (next)
                            _selectedPaths.Add(group.Key);
                        else
                            _selectedPaths.Remove(group.Key);
                        foreach (var issue in group)
                        {
                            var prefix = issue.IsPlatformSetting ? $"[{issue.Platform}] " : string.Empty;
                            EditorGUILayout.LabelField($"    {prefix}{issue.SettingName}: {issue.CurrentValue} -> {issue.ExpectedValue}", _issueStyle);
                        }
                        EditorGUILayout.Space(2f);
                    }
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("5. Apply", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_plan == null || _plan.IsEmpty))
                {
                    if (GUILayout.Button("Apply Selected"))
                        ApplySelected();
                    if (GUILayout.Button("Apply All"))
                        ApplyAll();
                }
                using (new EditorGUI.DisabledScope(_plan == null && string.IsNullOrEmpty(_message)))
                {
                    if (GUILayout.Button("Clear"))
                        ClearPreview();
                }
            }
        }

        private void DrawSharedSettings()
        {
            EditorGUILayout.LabelField("Shared Settings", EditorStyles.miniBoldLabel);
            _maxTextureSize = EditorGUILayout.IntPopup("Max Texture Size", _maxTextureSize, TextureSizeLabels, TextureSizeValues);
            _compression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", _compression);
            _mipmapEnabled = EditorGUILayout.Toggle("Mipmaps", _mipmapEnabled);
            _sRgbTexture = EditorGUILayout.Toggle("sRGB", _sRgbTexture);
            _readable = EditorGUILayout.Toggle("Read/Write", _readable);
            _filterMode = (FilterMode)EditorGUILayout.EnumPopup("Filter Mode", _filterMode);
            _anisoLevel = EditorGUILayout.IntSlider("Aniso Level", _anisoLevel, 0, 16);
        }

        private void DrawPlatformSettings()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Platform Override", EditorStyles.miniBoldLabel);
            var platformIndex = Array.IndexOf(Platforms, _platform);
            platformIndex = EditorGUILayout.Popup("Target Platform", Mathf.Max(platformIndex, 0), PlatformLabels);
            _platform = Platforms[platformIndex];
            _platformOverrideEnabled = EditorGUILayout.Toggle("Override", _platformOverrideEnabled);
            using (new EditorGUI.DisabledScope(!_platformOverrideEnabled))
            {
                _platformMaxTextureSize = EditorGUILayout.IntPopup("Platform Max Size", _platformMaxTextureSize, TextureSizeLabels, TextureSizeValues);
                _platformCompression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Platform Compression", _platformCompression);
            }
        }

        private void UseSelection()
        {
            var selectedPath = Selection.activeObject == null ? string.Empty : AssetDatabase.GetAssetPath(Selection.activeObject);
            if (AssetDatabase.IsValidFolder(selectedPath))
            {
                _rootFolder = selectedPath;
                ClearPreview();
            }
        }

        private void Preview()
        {
            try
            {
                switch (_auditScope)
                {
                    case AuditScope.SharedSettings:
                        _plan = AssetImportAuditService.Preview(_rootFolder, AssetImportAuditTextureAuditSettings.ForShared(CreateSharedSettings()));
                        break;
                    case AuditScope.PlatformOverride:
                        _plan = AssetImportAuditService.Preview(_rootFolder, AssetImportAuditTextureAuditSettings.ForPlatform(_platform, CreatePlatformSettings()));
                        break;
                    case AuditScope.SharedAndPlatform:
                        _plan = AssetImportAuditService.Preview(_rootFolder, AssetImportAuditTextureAuditSettings.ForSharedAndPlatform(CreateSharedSettings(), _platform, CreatePlatformSettings()));
                        break;
                    default:
                        throw new InvalidOperationException("Unsupported audit scope.");
                }
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

        private AssetImportAuditTextureSettings CreateSharedSettings()
        {
            return new AssetImportAuditTextureSettings(_maxTextureSize, _compression, _mipmapEnabled, _sRgbTexture, _readable, _filterMode, _anisoLevel);
        }

        private AssetImportAuditTexturePlatformSettings CreatePlatformSettings()
        {
            return new AssetImportAuditTexturePlatformSettings(_platformOverrideEnabled, _platformMaxTextureSize, _platformCompression);
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
            _message = result.Succeeded
                ? $"Applied {result.AppliedAssetCount} assets."
                : result.AppliedAssetCount > 0
                    ? $"Apply failed after {result.AppliedAssetCount} assets: {result.Error}. Preview again before retrying."
                    : $"Apply failed: {result.Error}. Preview again before retrying.";
            if (result.Succeeded)
                Preview();
        }

        private void ClearPreview()
        {
            _plan = null;
            _selectedPaths.Clear();
            _message = null;
        }

        private void EnsureStyles()
        {
            if (_assetPathStyle == null)
                _assetPathStyle = new GUIStyle(EditorStyles.label) { wordWrap = true };
            if (_issueStyle == null)
                _issueStyle = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
        }
    }
}
