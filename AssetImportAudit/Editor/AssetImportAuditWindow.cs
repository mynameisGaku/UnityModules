using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AssetImportAudit.Editor
{
    /// <summary>テクスチャー取込設定の差分確認、対象選択、反映を順に案内します。</summary>
    public sealed class AssetImportAuditWindow : EditorWindow
    {
        /// <summary>画面上部へ表示する日本語名です。</summary>
        internal const string WindowTitle = "テクスチャー取込設定監査";

        /// <summary>画面を開く日本語メニューの経路です。</summary>
        internal const string MenuPath = "Tools/テクスチャー取込設定監査/開く";

        /// <summary>検査対象に含める設定範囲の表示名です。</summary>
        private static readonly string[] AuditScopeLabels = { "共通設定", "対象機種別設定", "共通設定と対象機種別設定" };

        /// <summary>対応する対象機種の表示名です。</summary>
        private static readonly string[] PlatformLabels = { "パソコン", "Android", "iOS" };

        /// <summary>対応する圧縮方法の表示名です。</summary>
        private static readonly string[] CompressionLabels = { "圧縮なし", "標準圧縮", "高品質圧縮", "低品質圧縮" };

        /// <summary>表示名と同じ順序で並べた圧縮方法です。</summary>
        private static readonly TextureImporterCompression[] CompressionValues =
        {
            TextureImporterCompression.Uncompressed,
            TextureImporterCompression.Compressed,
            TextureImporterCompression.CompressedHQ,
            TextureImporterCompression.CompressedLQ
        };

        /// <summary>対応する画素補間方法の表示名です。</summary>
        private static readonly string[] FilterModeLabels = { "点補間", "二線形補間", "三線形補間" };

        /// <summary>表示名と同じ順序で並べた画素補間方法です。</summary>
        private static readonly FilterMode[] FilterModeValues = { FilterMode.Point, FilterMode.Bilinear, FilterMode.Trilinear };

        /// <summary>最大テクスチャー寸法の表示名です。</summary>
        private static readonly string[] TextureSizeLabels = AssetImportAuditTextureSize.CreateLabels();

        /// <summary>表示名と同じ順序で並べた最大テクスチャー寸法です。</summary>
        private static readonly int[] TextureSizeValues = AssetImportAuditTextureSize.CreateValues();

        /// <summary>表示名と同じ順序で並べた対象機種です。</summary>
        private static readonly AssetImportAuditTexturePlatform[] Platforms =
        {
            AssetImportAuditTexturePlatform.Standalone,
            AssetImportAuditTexturePlatform.Android,
            AssetImportAuditTexturePlatform.iOS
        };

        /// <summary>画面で選べる検査範囲です。</summary>
        private enum AuditScope
        {
            /// <summary>共通設定だけを検査します。</summary>
            SharedSettings = 0,

            /// <summary>対象機種別設定だけを検査します。</summary>
            PlatformOverride = 1,

            /// <summary>共通設定と対象機種別設定をまとめて検査します。</summary>
            SharedAndPlatform = 2
        }

        /// <summary>検査対象のルートフォルダーです。</summary>
        private string _rootFolder = "Assets";

        /// <summary>現在選ばれている検査範囲です。</summary>
        private AuditScope _auditScope = AuditScope.SharedSettings;

        /// <summary>共通設定として期待する最大テクスチャー寸法です。</summary>
        private int _maxTextureSize = 2048;

        /// <summary>共通設定として期待する圧縮方法です。</summary>
        private TextureImporterCompression _compression = TextureImporterCompression.Compressed;

        /// <summary>共通設定でミップマップ生成を期待するかどうかです。</summary>
        private bool _mipmapEnabled;

        /// <summary>共通設定でsRGBとしての取込を期待するかどうかです。</summary>
        private bool _sRgbTexture = true;

        /// <summary>共通設定で読み取り・書き込みを期待するかどうかです。</summary>
        private bool _readable;

        /// <summary>共通設定として期待する画素の補間方法です。</summary>
        private FilterMode _filterMode = FilterMode.Bilinear;

        /// <summary>共通設定として期待する異方性レベルです。</summary>
        private int _anisoLevel = 1;

        /// <summary>検査対象として選ばれている対象機種です。</summary>
        private AssetImportAuditTexturePlatform _platform = AssetImportAuditTexturePlatform.Standalone;

        /// <summary>対象機種別の個別設定を期待するかどうかです。</summary>
        private bool _platformOverrideEnabled = true;

        /// <summary>対象機種別設定として期待する最大テクスチャー寸法です。</summary>
        private int _platformMaxTextureSize = 2048;

        /// <summary>対象機種別設定として期待する圧縮方法です。</summary>
        private TextureImporterCompression _platformCompression = TextureImporterCompression.Compressed;

        /// <summary>直近の差分確認結果です。</summary>
        private AssetImportAuditPlan _plan;

        /// <summary>反映対象として選ばれているアセットパスです。</summary>
        private readonly HashSet<string> _selectedPaths = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>差分一覧の現在のスクロール位置です。</summary>
        private Vector2 _scrollPosition;

        /// <summary>利用者へ表示する処理結果または問題の説明です。</summary>
        private string _message;

        /// <summary>アセットパスを折り返して表示する様式です。</summary>
        private GUIStyle _assetPathStyle;

        /// <summary>設定差分を折り返して表示する様式です。</summary>
        private GUIStyle _issueStyle;

        private void OnEnable()
        {
            minSize = new Vector2(560f, 620f);
        }

        /// <summary>テクスチャー取込設定監査の画面を開きます。</summary>
        [MenuItem(MenuPath)]
        public static void Open()
        {
            GetWindow<AssetImportAuditWindow>(WindowTitle);
        }

        private void OnGUI()
        {
            EnsureStyles();
            EditorGUILayout.HelpBox("上から順に、対象を選び、期待する設定を決め、差分を確認してから選択対象または全対象へ反映します。", MessageType.Info);

            EditorGUILayout.LabelField("1. 対象フォルダー", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _rootFolder = EditorGUILayout.TextField("対象フォルダー", _rootFolder);
            if (EditorGUI.EndChangeCheck())
                ClearPreview();
            if (GUILayout.Button("選択中のフォルダーを使用", GUILayout.Width(180f)))
                UseSelection();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("2. 期待する設定", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            _auditScope = (AuditScope)EditorGUILayout.Popup("検査範囲", (int)_auditScope, AuditScopeLabels);
            if (_auditScope == AuditScope.SharedSettings || _auditScope == AuditScope.SharedAndPlatform)
                DrawSharedSettings();
            if (_auditScope == AuditScope.PlatformOverride || _auditScope == AuditScope.SharedAndPlatform)
                DrawPlatformSettings();
            if (EditorGUI.EndChangeCheck())
                ClearPreview();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("3. 差分確認", EditorStyles.boldLabel);
            if (GUILayout.Button("差分を確認", GUILayout.Height(24f)))
                Preview();

            if (!string.IsNullOrEmpty(_message))
                EditorGUILayout.HelpBox(_message, MessageType.Warning);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("4. 差分一覧", EditorStyles.boldLabel);
            if (_plan == null)
            {
                EditorGUILayout.HelpBox("差分確認の結果がここに表示されます。", MessageType.None);
            }
            else
            {
                EditorGUILayout.LabelField($"{_plan.Entries.Count}個のアセットに{_plan.Issues.Count}件の差分があります。", EditorStyles.boldLabel);
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
                            var prefix = issue.IsPlatformSetting ? $"[{FormatPlatformName(issue.Platform)}] " : string.Empty;
                            var settingName = FormatSettingName(issue.SettingName);
                            var currentValue = FormatSettingValue(issue.SettingName, issue.CurrentValue);
                            var expectedValue = FormatSettingValue(issue.SettingName, issue.ExpectedValue);
                            EditorGUILayout.LabelField($"    {prefix}{settingName}: {currentValue} → {expectedValue}", _issueStyle);
                        }
                        EditorGUILayout.Space(2f);
                    }
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("5. 反映", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_plan == null || _plan.IsEmpty))
                {
                    if (GUILayout.Button("選択対象へ反映"))
                        ApplySelected();
                    if (GUILayout.Button("すべて反映"))
                        ApplyAll();
                }
                using (new EditorGUI.DisabledScope(_plan == null && string.IsNullOrEmpty(_message)))
                {
                    if (GUILayout.Button("結果を消去"))
                        ClearPreview();
                }
            }
        }

        private void DrawSharedSettings()
        {
            EditorGUILayout.LabelField("共通設定", EditorStyles.miniBoldLabel);
            _maxTextureSize = EditorGUILayout.IntPopup("最大テクスチャー寸法", _maxTextureSize, TextureSizeLabels, TextureSizeValues);
            _compression = DrawCompressionPopup("圧縮方法", _compression);
            _mipmapEnabled = EditorGUILayout.Toggle("ミップマップを生成", _mipmapEnabled);
            _sRgbTexture = EditorGUILayout.Toggle("sRGBとして扱う", _sRgbTexture);
            _readable = EditorGUILayout.Toggle("読み取り・書き込みを許可", _readable);
            _filterMode = DrawFilterModePopup("画素の補間方法", _filterMode);
            _anisoLevel = EditorGUILayout.IntSlider("異方性レベル", _anisoLevel, 0, 16);
        }

        private void DrawPlatformSettings()
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("対象機種別設定", EditorStyles.miniBoldLabel);
            var platformIndex = Array.IndexOf(Platforms, _platform);
            if (platformIndex < 0)
            {
                EditorGUILayout.LabelField("対象機種", "対応していない値");
                EditorGUILayout.HelpBox("対象機種に対応していない値が指定されています。別の設定へ置き換えず、差分確認を停止します。", MessageType.Error);
            }
            else
            {
                platformIndex = EditorGUILayout.Popup("対象機種", platformIndex, PlatformLabels);
                _platform = Platforms[platformIndex];
            }
            _platformOverrideEnabled = EditorGUILayout.Toggle("個別設定を使用", _platformOverrideEnabled);
            using (new EditorGUI.DisabledScope(!_platformOverrideEnabled))
            {
                _platformMaxTextureSize = EditorGUILayout.IntPopup("対象機種の最大寸法", _platformMaxTextureSize, TextureSizeLabels, TextureSizeValues);
                _platformCompression = DrawCompressionPopup("対象機種の圧縮方法", _platformCompression);
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
                        throw new InvalidOperationException("対応していない検査範囲です。");
                }
                _selectedPaths.Clear();
                foreach (var issue in _plan.Issues)
                    _selectedPaths.Add(issue.AssetPath);
                _message = _plan.IsEmpty ? "差分はありません。" : null;
            }
            catch (ArgumentException exception)
            {
                _plan = null;
                if (TryFormatInputError(exception, out var message))
                {
                    _message = message;
                }
                else
                {
                    Debug.LogException(exception);
                    _message = "差分確認中に処理できない問題が発生しました。詳しくはコンソールを確認してください。";
                }
            }
            catch (Exception exception)
            {
                _plan = null;
                Debug.LogException(exception);
                _message = "差分確認中に処理できない問題が発生しました。詳しくはコンソールを確認してください。";
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
                ? $"{result.AppliedAssetCount}個のアセットへ反映しました。"
                : result.AppliedAssetCount > 0
                    ? $"{result.AppliedAssetCount}個のアセットへ反映した後に失敗しました。原因: {FormatError(result.Error)}。再度差分を確認してからやり直してください。"
                    : $"反映に失敗しました。原因: {FormatError(result.Error)}。再度差分を確認してからやり直してください。";
            if (result.Succeeded)
                Preview();
        }

        /// <summary>内部の対象機種を利用者向け表示へ変換し、未知の値は対応外と明示します。</summary>
        internal static string FormatPlatformName(AssetImportAuditTexturePlatform platform)
        {
            switch (platform)
            {
                case AssetImportAuditTexturePlatform.None:
                    return "共通";
                case AssetImportAuditTexturePlatform.Standalone:
                    return "パソコン";
                case AssetImportAuditTexturePlatform.Android:
                    return "Android";
                case AssetImportAuditTexturePlatform.iOS:
                    return "iOS";
                default:
                    return "対応していない対象機種";
            }
        }

        /// <summary>内部の設定識別子を利用者向け表示へ変換し、未知の識別子はそのまま返します。</summary>
        internal static string FormatSettingName(string settingName)
        {
            switch (settingName)
            {
                case "maxTextureSize":
                    return "最大テクスチャー寸法";
                case "textureCompression":
                    return "圧縮方法";
                case "mipmapEnabled":
                    return "ミップマップを生成";
                case "sRGBTexture":
                    return "sRGBとして扱う";
                case "isReadable":
                    return "読み取り・書き込みを許可";
                case "filterMode":
                    return "画素の補間方法";
                case "anisoLevel":
                    return "異方性レベル";
                case "overridden":
                    return "個別設定を使用";
                default:
                    return settingName;
            }
        }

        /// <summary>設定識別子に応じて内部値を利用者向け表示へ変換し、未知の値はそのまま返します。</summary>
        internal static string FormatSettingValue(string settingName, string value)
        {
            if (settingName == "textureCompression")
            {
                switch (value)
                {
                    case nameof(TextureImporterCompression.Uncompressed):
                        return "圧縮なし";
                    case nameof(TextureImporterCompression.Compressed):
                        return "標準圧縮";
                    case nameof(TextureImporterCompression.CompressedHQ):
                        return "高品質圧縮";
                    case nameof(TextureImporterCompression.CompressedLQ):
                        return "低品質圧縮";
                }
            }

            if (settingName == "filterMode")
            {
                switch (value)
                {
                    case nameof(FilterMode.Point):
                        return "点補間";
                    case nameof(FilterMode.Bilinear):
                        return "二線形補間";
                    case nameof(FilterMode.Trilinear):
                        return "三線形補間";
                }
            }

            if (value == bool.TrueString)
                return "有効";
            if (value == bool.FalseString)
                return "無効";
            return value;
        }

        /// <summary>反映処理の失敗理由を日本語へ変換し、未知の理由は不明な問題として扱います。</summary>
        internal static string FormatError(AssetImportAuditError error)
        {
            switch (error)
            {
                case AssetImportAuditError.InvalidFolder:
                    return "対象フォルダーが不正です";
                case AssetImportAuditError.InvalidSettings:
                    return "設定値が不正です";
                case AssetImportAuditError.StalePlan:
                    return "差分確認後に対象が変更されました";
                case AssetImportAuditError.NoChanges:
                    return "反映対象がありません";
                case AssetImportAuditError.ImporterUnavailable:
                    return "取込設定を読み込めません";
                case AssetImportAuditError.ApplyFailed:
                    return "取込設定の反映処理で問題が発生しました";
                default:
                    return "不明な問題です";
            }
        }

        /// <summary>利用者が修正できる既知の入力不備だけを日本語へ変換し、変換できたかを戻り値で返します。例外が未指定の場合は失敗します。</summary>
        internal static bool TryFormatInputError(ArgumentException exception, out string message)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception), "入力不備の例外を指定してください。");

            switch (exception.ParamName)
            {
                case "rootFolder":
                    if (exception.Message.StartsWith("対象フォルダーはAssets以下を指定してください。", StringComparison.Ordinal))
                    {
                        message = "対象フォルダーはAssets以下を指定してください。";
                        return true;
                    }
                    if (exception.Message.StartsWith("対象フォルダーが存在しません。", StringComparison.Ordinal))
                    {
                        message = "対象フォルダーが存在しません。";
                        return true;
                    }
                    break;
                case "MaxTextureSize":
                case "maxTextureSize":
                    message = "最大テクスチャー寸法には、32から16384までの表示候補を指定してください。";
                    return true;
                case "Compression":
                case "compression":
                    message = "圧縮方法に対応していない値が指定されています。";
                    return true;
                case "FilterMode":
                case "filterMode":
                    message = "画素の補間方法に対応していない値が指定されています。";
                    return true;
                case "AnisoLevel":
                case "anisoLevel":
                    message = "異方性レベルには0から16までの値を指定してください。";
                    return true;
                case "Platform":
                case "platform":
                    message = "対象機種に対応していない値が指定されています。";
                    return true;
            }

            message = null;
            return false;
        }

        /// <summary>対応する圧縮方法だけを日本語候補で表示し、未知の値は変更せず保持します。</summary>
        private static TextureImporterCompression DrawCompressionPopup(string label, TextureImporterCompression current)
        {
            var index = Array.IndexOf(CompressionValues, current);
            if (index < 0)
            {
                EditorGUILayout.LabelField(label, "対応していない値");
                EditorGUILayout.HelpBox($"{label}に対応していない値が指定されています。別の設定へ置き換えず、差分確認を停止します。", MessageType.Error);
                return current;
            }

            index = EditorGUILayout.Popup(label, index, CompressionLabels);
            return CompressionValues[index];
        }

        /// <summary>対応する画素補間方法だけを日本語候補で表示し、未知の値は変更せず保持します。</summary>
        private static FilterMode DrawFilterModePopup(string label, FilterMode current)
        {
            var index = Array.IndexOf(FilterModeValues, current);
            if (index < 0)
            {
                EditorGUILayout.LabelField(label, "対応していない値");
                EditorGUILayout.HelpBox($"{label}に対応していない値が指定されています。別の設定へ置き換えず、差分確認を停止します。", MessageType.Error);
                return current;
            }

            index = EditorGUILayout.Popup(label, index, FilterModeLabels);
            return FilterModeValues[index];
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
