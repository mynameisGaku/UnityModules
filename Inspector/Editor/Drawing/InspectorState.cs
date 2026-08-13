using System;
using System.Collections.Generic;
using UnityEditor;

namespace Inspector.Editor
{
    /// <summary>
    /// 折りたたみの開閉と、選んでいるタブを覚えておく。
    /// <para>
    /// 型とグループパスで覚えるので、同じコンポーネントを別のオブジェクトで開いても状態が揃う。
    /// 開き直すたびに全部閉じているのは、階層の深い設定ではかなり煩わしい。
    /// </para>
    /// <para>
    /// 保存先は <see cref="EditorPrefs"/>。Windows ではレジストリなので読み書きが安くはなく、
    /// 描画のたびに触ると効いてくる。手前に辞書を置いて、実際の読み書きは変化したときだけにする。
    /// </para>
    /// </summary>
    public static class InspectorState
    {
        private const string Prefix = "StudioGaku.Inspector.";

        private static readonly Dictionary<string, bool> Foldouts = new Dictionary<string, bool>();
        private static readonly Dictionary<string, int> Tabs = new Dictionary<string, int>();

        public static bool GetFoldout(Type type, string path, bool fallback)
        {
            var key = Key(type, path);

            if (Foldouts.TryGetValue(key, out var cached)) return cached;

            var stored = EditorPrefs.GetBool(key, fallback);
            Foldouts[key] = stored;
            return stored;
        }

        public static void SetFoldout(Type type, string path, bool value)
        {
            var key = Key(type, path);

            if (Foldouts.TryGetValue(key, out var cached) && cached == value) return;

            Foldouts[key] = value;
            EditorPrefs.SetBool(key, value);
        }

        public static int GetTab(Type type, string path)
        {
            var key = Key(type, path);

            if (Tabs.TryGetValue(key, out var cached)) return cached;

            var stored = EditorPrefs.GetInt(key, 0);
            Tabs[key] = stored;
            return stored;
        }

        public static void SetTab(Type type, string path, int value)
        {
            var key = Key(type, path);

            if (Tabs.TryGetValue(key, out var cached) && cached == value) return;

            Tabs[key] = value;
            EditorPrefs.SetInt(key, value);
        }

        private static string Key(Type type, string path) => Prefix + (type?.FullName ?? "?") + "." + path;
    }
}
