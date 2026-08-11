using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DebugMenu.Editor
{
    /// <summary>
    /// 導入時の面倒を引き受けるエディタ拡張。
    /// <para>
    /// ランタイムの UI Toolkit は <see cref="PanelSettings"/> アセットを要求し、
    /// そのアセットはテーマ（<c>ThemeStyleSheet</c>）を要求する。手で辿ると 3 手かかるうえ、
    /// テーマを付け忘れると<b>何も描かれないのにエラーも出ない</b>という詰まり方をする。
    /// ここで一度に作る。
    /// </para>
    /// </summary>
    public static class DebugMenuSetup
    {
        private const string SettingsDirectory = "Assets/Settings";
        private const string PanelAssetPath = SettingsDirectory + "/DebugMenuPanelSettings.asset";
        private const string ThemeAssetPath = SettingsDirectory + "/DebugMenuTheme.tss";

        /// <summary>
        /// <see cref="PanelSettings"/> を作り、テーマを割り当てて選択状態にする。
        /// 既にあれば作り直さず、それを選ぶ。
        /// </summary>
        [MenuItem("Tools/Debug Menu/Create Panel Settings")]
        public static void CreatePanelSettings()
        {
            var settings = CreateOrLoadPanelSettings();

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);

            Debug.Log($"[DebugMenu] PanelSettings を用意した: {PanelAssetPath}");
        }

        /// <summary>
        /// シーンに <see cref="DebugMenuController"/> を置き、PanelSettings を割り当てる。
        /// 既に置かれていれば、その設定だけを埋める。
        /// </summary>
        [MenuItem("Tools/Debug Menu/Add To Scene")]
        public static void AddToScene()
        {
            var controller = Object.FindAnyObjectByType<DebugMenuController>();

            if (controller == null)
            {
                var host = new GameObject("Debug Menu");
                controller = host.AddComponent<DebugMenuController>();
                Undo.RegisterCreatedObjectUndo(host, "Add Debug Menu");
            }

            var serialized = new SerializedObject(controller);
            var property = serialized.FindProperty("_panelSettings");

            if (property != null && property.objectReferenceValue == null)
            {
                property.objectReferenceValue = CreateOrLoadPanelSettings();
                serialized.ApplyModifiedProperties();
            }

            Selection.activeGameObject = controller.gameObject;
            Debug.Log("[DebugMenu] シーンに配置した。実行して F1 で開く。");
        }

        /// <summary>PanelSettings を作る、または既にあるものを返す。</summary>
        internal static PanelSettings CreateOrLoadPanelSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelAssetPath);
            if (existing != null)
            {
                if (existing.themeStyleSheet == null) existing.themeStyleSheet = ResolveTheme();
                return existing;
            }

            Directory.CreateDirectory(SettingsDirectory);

            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet = ResolveTheme();

            // 解像度が変わっても文字の大きさが暴れないようにする。
            // 既定の ConstantPixelSize だと 4K で豆粒になる。
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 1f;   // 高さ基準。横長でも縦の情報量を保つ

            // デバッグ表示なので最前面に出す。
            settings.sortingOrder = 1000f;

            AssetDatabase.CreateAsset(settings, PanelAssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        /// <summary>
        /// テーマを見つける、または最小限のものを作る。
        /// <para>
        /// Unity 6 では UI Toolkit が組み込みになり、既定のランタイムテーマが
        /// アセットとして project 内に置かれない。プロジェクト内を探して、
        /// 無ければ空のテーマを作る（空でも文字と矩形は描ける）。
        /// </para>
        /// </summary>
        private static ThemeStyleSheet ResolveTheme()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemeAssetPath);
            if (existing != null) return existing;

            // プロジェクトに既にテーマがあれば、それを使う方が見た目が揃う。
            var found = AssetDatabase.FindAssets("t:ThemeStyleSheet");
            for (var i = 0; i < found.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(found[i]);
                var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);
                if (theme != null) return theme;
            }

            Directory.CreateDirectory(SettingsDirectory);

            // 中身は空でよい。このモジュールは色も寸法もコード側で指定するため、
            // テーマに求めるのは「パネルが成立すること」だけ。
            File.WriteAllText(ThemeAssetPath, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(ThemeAssetPath, ImportAssetOptions.ForceSynchronousImport);

            var created = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemeAssetPath);
            if (created == null) Debug.LogWarning($"[DebugMenu] テーマを作れなかった: {ThemeAssetPath}");

            return created;
        }
    }
}
