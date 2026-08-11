using System;
using System.Collections.Generic;
using System.IO;
using Containers;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>保存先の抽象。ファイルにも PlayerPrefs にも、独自の保管庫にも差せる。</summary>
    public interface IDebugMenuStorage
    {
        /// <summary>保存されている文字列を読む。無ければ null。</summary>
        /// <param name="key">保存の単位を表すキー。</param>
        string Load(string key);

        /// <summary>文字列を保存する。</summary>
        /// <param name="key">保存の単位を表すキー。</param>
        /// <param name="value">保存する内容。</param>
        void Save(string key, string value);

        /// <summary>保存されている内容を消す。</summary>
        /// <param name="key">保存の単位を表すキー。</param>
        void Delete(string key);
    }

    /// <summary>PlayerPrefs へ保存する。手軽だが、量が増えると向かない。</summary>
    public sealed class DebugMenuPlayerPrefsStorage : IDebugMenuStorage
    {
        /// <inheritdoc/>
        public string Load(string key) => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null;

        /// <inheritdoc/>
        public void Save(string key, string value)
        {
            PlayerPrefs.SetString(key, value);
            PlayerPrefs.Save();
        }

        /// <inheritdoc/>
        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// <see cref="Application.persistentDataPath"/> 以下のファイルへ保存する。
    /// <para>
    /// 書き込みは一時ファイルを経由してから置き換える。途中で電源が落ちても、
    /// 半端に書けたファイルが残って次回の読み込みを壊すことがないようにするため。
    /// </para>
    /// </summary>
    public sealed class DebugMenuFileStorage : IDebugMenuStorage
    {
        private readonly string _directory;

        /// <summary>保存先のフォルダを指定して作る。</summary>
        /// <param name="directory">保存先。省略すると persistentDataPath/DebugMenu。</param>
        public DebugMenuFileStorage(string directory = null) =>
            _directory = directory ?? Path.Combine(Application.persistentDataPath, "DebugMenu");

        /// <inheritdoc/>
        public string Load(string key)
        {
            var path = PathFor(key);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <inheritdoc/>
        public void Save(string key, string value)
        {
            Directory.CreateDirectory(_directory);

            var path = PathFor(key);
            var temporary = path + ".tmp";

            // 書いてから差し替える。直接上書きすると、書き込み中の中断で
            // 読めないファイルが残る。
            File.WriteAllText(temporary, value);

            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }

        /// <inheritdoc/>
        public void Delete(string key)
        {
            var path = PathFor(key);
            if (File.Exists(path)) File.Delete(path);
        }

        private string PathFor(string key)
        {
            // キーには経路区切りが入るので、ファイル名に使えない文字を潰す。
            var safe = key;
            foreach (var invalid in Path.GetInvalidFileNameChars()) safe = safe.Replace(invalid, '_');

            return Path.Combine(_directory, safe + ".json");
        }
    }

    /// <summary>ディスクへ書き出す形。Unity の JsonUtility が扱えるよう素直な構成にしてある。</summary>
    [Serializable]
    public sealed class DebugMenuSettingsData
    {
        /// <summary>この形式の版。読み込み側が古い保存を判別するために使う。</summary>
        public int Version = 1;

        /// <summary>保存キーの一覧。<see cref="Values"/> と同じ並び。</summary>
        public List<string> Keys = new List<string>();

        /// <summary>値の一覧。<see cref="Keys"/> と同じ並び。</summary>
        public List<string> Values = new List<string>();

        /// <summary>値の種類の一覧。復元時にどう解釈するかを決める。</summary>
        public List<int> Kinds = new List<int>();
    }

    /// <summary>
    /// メニューの値を保存し、次回の起動で戻す。
    /// <para>
    /// 保存の単位は行ではなく<b>保存キー</b>。行を別ページへ移しても表示名を変えても、
    /// キーさえ同じなら復元できる。キーを明示していない行は経路から自動生成するので、
    /// 動かすと復元できなくなる —— 動かす予定のある行には
    /// <see cref="DebugElement.SaveKey"/> を明示すること。
    /// </para>
    /// </summary>
    public sealed class DebugMenuSettings
    {
        private readonly IDebugMenuStorage _storage;
        private readonly string _key;

        /// <summary>保存先と保存の単位を指定して作る。</summary>
        /// <param name="storage">保存先。省略するとファイル保存。</param>
        /// <param name="key">保存の単位を表すキー。</param>
        public DebugMenuSettings(
            IDebugMenuStorage storage = null,
            string key = "debug-menu-settings",
            DebugMenuSettingsFormat format = DebugMenuSettingsFormat.Json)
        {
            _storage = storage ?? new DebugMenuFileStorage();
            _key = key;
            Format = format;
        }

        /// <summary>次回の保存で使う形式。読み込み時は内容から自動判別する。</summary>
        public DebugMenuSettingsFormat Format { get; set; }

        /// <summary>最後に読み込めた内容の形式。</summary>
        public DebugMenuSettingsFormat LastLoadedFormat { get; private set; } = DebugMenuSettingsFormat.Json;

        /// <summary>いまの値を集めて保存する。保存した行の数を返す。</summary>
        /// <param name="menu">対象のメニュー。</param>
        public int Save(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            var data = Capture(menu);
            _storage.Save(_key, DebugMenuSettingsSerializer.Serialize(data, Format));
            return data.Keys.Count;
        }

        /// <summary>保存されている値を戻す。戻せた行の数を返す。</summary>
        /// <param name="menu">対象のメニュー。</param>
        public int Load(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            var serialized = _storage.Load(_key);
            if (!DebugMenuSettingsSerializer.TryDeserialize(serialized, out var data, out var format)) return 0;

            LastLoadedFormat = format;
            return Apply(menu, data);
        }

        /// <summary>現在値を指定形式のファイルへ原子的に書き出す。</summary>
        /// <param name="menu">保存対象。</param>
        /// <param name="path">書き出す絶対または相対パス。</param>
        /// <param name="format">書き出す形式。</param>
        /// <returns>保存した行数。</returns>
        public int SaveAs(DebugMenuRoot menu, string path, DebugMenuSettingsFormat format)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("保存先を指定してください。", nameof(path));

            var data = Capture(menu);
            WriteFileAtomically(path, DebugMenuSettingsSerializer.SerializeFile(data, format));
            return data.Keys.Count;
        }

        /// <summary>指定ファイルの形式を自動判別し、値を適用する。</summary>
        /// <param name="menu">適用先。</param>
        /// <param name="path">読み込むファイル。</param>
        /// <returns>適用できた行数。ファイルが無い、または壊れていれば0。</returns>
        public int LoadFrom(DebugMenuRoot menu, string path)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return 0;

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[DebugMenu] 設定ファイルを読めなかった。\n{exception.Message}");
                return 0;
            }

            if (!DebugMenuSettingsSerializer.TryDeserializeFile(bytes, out var data, out var format)) return 0;
            LastLoadedFormat = format;
            return Apply(menu, data);
        }

        /// <summary>メニューの現在値を保存可能なデータへ集める。</summary>
        public static DebugMenuSettingsData Capture(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            var data = new DebugMenuSettingsData();
            var visited = new HashSet<DebugElement>();
            menu.VisitAll((_, element) =>
            {
                // FavoritesやRecentは元の行を借用する。同じ実体は1回だけ保存する。
                if (!visited.Add(element)) return;
                if (!element.IsSaveable) return;

                var snapshot = DebugValueSnapshot.Capture(element);
                if (!snapshot.HasValue) return;

                data.Keys.Add(element.ResolveSaveKey());
                data.Values.Add(snapshot.ToStorageString());
                data.Kinds.Add((int)snapshot.Kind);
            });
            return data;
        }

        /// <summary>保存データを同じキー・同じ型の行へ適用する。</summary>
        public static int Apply(DebugMenuRoot menu, DebugMenuSettingsData data)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            if (data == null || data.Keys == null || data.Values == null || data.Kinds == null) return 0;

            var count = Mathf.Min(data.Keys.Count, Mathf.Min(data.Values.Count, data.Kinds.Count));
            if (count == 0) return 0;

            var stored = new Dictionary<string, (string Value, DebugValueKind Kind)>(count);
            for (var i = 0; i < count; i++) stored[data.Keys[i]] = (data.Values[i], (DebugValueKind)data.Kinds[i]);

            var applied = 0;
            menu.VisitAll((_, element) =>
            {
                if (!element.IsSaveable) return;
                if (!stored.TryGetValue(element.ResolveSaveKey(), out var entry)) return;
                if (entry.Kind != element.ValueKind) return;
                if (!DebugValueSnapshot.TryParse(entry.Kind, entry.Value, out var snapshot)) return;
                if (snapshot.Apply(element)) applied++;
            });
            return applied;
        }

        /// <summary>保存されている内容を消す。</summary>
        public void Delete() => _storage.Delete(_key);

        /// <summary>全ての行を既定値へ戻す。保存の中身には触らない。</summary>
        /// <param name="menu">対象のメニュー。</param>
        public static void ResetAll(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            menu.VisitAll((_, element) => element.ResetToDefault());
        }

        private static void WriteFileAtomically(string path, byte[] bytes)
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var temporary = fullPath + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            if (File.Exists(fullPath)) File.Delete(fullPath);
            File.Move(temporary, fullPath);
        }
    }
}
