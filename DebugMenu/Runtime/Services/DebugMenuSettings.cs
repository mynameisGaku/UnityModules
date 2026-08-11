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
        public DebugMenuSettings(IDebugMenuStorage storage = null, string key = "debug-menu-settings")
        {
            _storage = storage ?? new DebugMenuFileStorage();
            _key = key;
        }

        /// <summary>いまの値を集めて保存する。保存した行の数を返す。</summary>
        /// <param name="menu">対象のメニュー。</param>
        public int Save(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            var data = new DebugMenuSettingsData();

            menu.VisitAll((_, element) =>
            {
                if (!element.IsSaveable) return;

                var snapshot = DebugValueSnapshot.Capture(element);
                if (!snapshot.HasValue) return;

                data.Keys.Add(element.ResolveSaveKey());
                data.Values.Add(snapshot.ToStorageString());
                data.Kinds.Add((int)snapshot.Kind);
            });

            _storage.Save(_key, JsonUtility.ToJson(data, true));
            return data.Keys.Count;
        }

        /// <summary>保存されている値を戻す。戻せた行の数を返す。</summary>
        /// <param name="menu">対象のメニュー。</param>
        public int Load(DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            var json = _storage.Load(_key);
            if (string.IsNullOrEmpty(json)) return 0;

            DebugMenuSettingsData data;
            try
            {
                data = JsonUtility.FromJson<DebugMenuSettingsData>(json);
            }
            catch (Exception exception)
            {
                // 壊れた保存で起動が止まる方が困る。捨てて既定値で続ける。
                Debug.LogWarning($"[DebugMenu] 保存された設定を読めなかった。既定値で続行する。\n{exception.Message}");
                return 0;
            }

            if (data == null || data.Keys == null) return 0;

            // 3 本のリストが揃っていない保存は信用しない。
            var count = Mathf.Min(data.Keys.Count, Mathf.Min(data.Values.Count, data.Kinds.Count));
            if (count == 0) return 0;

            var stored = new Dictionary<string, (string Value, DebugValueKind Kind)>(count);
            for (var i = 0; i < count; i++) stored[data.Keys[i]] = (data.Values[i], (DebugValueKind)data.Kinds[i]);

            var applied = 0;
            menu.VisitAll((_, element) =>
            {
                if (!element.IsSaveable) return;
                if (!stored.TryGetValue(element.ResolveSaveKey(), out var entry)) return;

                // 保存時と種類が変わっていたら書き戻さない。
                // 型を変えた行に古い値を押し込むと、意味の違う値が入る。
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
    }
}
