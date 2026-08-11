using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>保存済みプロファイルの名前一覧。Unity の JSON 形式へ変換するための入れ物。</summary>
    [Serializable]
    internal sealed class DebugMenuProfileCatalog
    {
        /// <summary>保存形式の版。</summary>
        public int Version = 1;

        /// <summary>作成順のプロファイル名。</summary>
        public List<string> Names = new List<string>();
    }

    /// <summary>
    /// 名前付きの設定スナップショットを保存し、必要な状態へまとめて切り替える。
    /// <para>
    /// 値の収集と復元は <see cref="DebugMenuSettings"/> に任せる。
    /// この型は名前一覧と、名前ごとに分けた保存キーだけを管理する。
    /// </para>
    /// </summary>
    public sealed class DebugMenuProfiles
    {
        private readonly IDebugMenuStorage _storage;
        private readonly string _keyPrefix;
        private readonly List<string> _names = new List<string>();
        private readonly IReadOnlyList<string> _readOnlyNames;

        /// <summary>保存先とキーの先頭部分を指定して作る。</summary>
        /// <param name="storage">保存先。省略するとファイル保存。</param>
        /// <param name="keyPrefix">プロファイル保存に使うキーの先頭部分。</param>
        public DebugMenuProfiles(
            IDebugMenuStorage storage = null,
            string keyPrefix = "debug-menu-profile",
            DebugMenuSettingsFormat format = DebugMenuSettingsFormat.Json)
        {
            if (string.IsNullOrWhiteSpace(keyPrefix)) throw new ArgumentException("保存キーを指定してください。", nameof(keyPrefix));

            _storage = storage ?? new DebugMenuFileStorage();
            _keyPrefix = keyPrefix;
            Format = format;
            _readOnlyNames = _names.AsReadOnly();
            Reload();
        }

        /// <summary>保存されているプロファイル名。作成順で並ぶ。</summary>
        public IReadOnlyList<string> Names => _readOnlyNames;

        /// <summary>保存されているプロファイル数。</summary>
        public int Count => _names.Count;

        /// <summary>次回保存するプロファイル値の形式。読み込みは内容から自動判別する。</summary>
        public DebugMenuSettingsFormat Format { get; set; }

        /// <summary>プロファイルの保存・削除・再読み込みで一覧が変わったときに呼ばれる。</summary>
        public event Action Changed;

        /// <summary>指定名のプロファイルが保存されているか。</summary>
        /// <param name="name">調べるプロファイル名。</param>
        public bool Contains(string name) => FindNameIndex(NormalizeName(name, false)) >= 0;

        /// <summary>現在のメニュー値を指定名で保存する。同名なら上書きする。</summary>
        /// <param name="name">保存するプロファイル名。</param>
        /// <param name="menu">保存対象のメニュー。</param>
        /// <returns>保存した行数。</returns>
        public int Save(string name, DebugMenuRoot menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            var normalized = NormalizeName(name, true);
            var existingIndex = FindNameIndex(normalized);
            var storageName = existingIndex >= 0 ? _names[existingIndex] : normalized;
            var dataKey = ProfileDataKey(storageName);
            var previousData = _storage.Load(dataKey);
            var previousCatalog = existingIndex < 0 ? _storage.Load(CatalogKey) : null;
            int saved;
            try
            {
                saved = CreateSettings(storageName).Save(menu);

                if (existingIndex < 0)
                {
                    _names.Add(normalized);
                    SaveCatalog();
                }
            }
            catch
            {
                if (existingIndex < 0)
                {
                    var addedIndex = FindNameIndex(normalized);
                    if (addedIndex >= 0) _names.RemoveAt(addedIndex);
                }

                // 値と一覧は別の保存単位なので、途中失敗時は両方を開始前へ戻す。
                RestoreStorageBestEffort(dataKey, previousData);
                if (existingIndex < 0) RestoreStorageBestEffort(CatalogKey, previousCatalog);
                throw;
            }

            Changed?.Invoke();
            return saved;
        }

        /// <summary>指定名の値をメニューへ適用する。</summary>
        /// <param name="name">適用するプロファイル名。</param>
        /// <param name="menu">適用先のメニュー。</param>
        /// <param name="appliedCount">適用できた行数。</param>
        /// <returns>指定名のプロファイルが存在すれば true。</returns>
        public bool TryApply(string name, DebugMenuRoot menu, out int appliedCount)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));

            var index = FindNameIndex(NormalizeName(name, false));
            if (index < 0)
            {
                appliedCount = 0;
                return false;
            }

            appliedCount = CreateSettings(_names[index]).Load(menu);
            return true;
        }

        /// <summary>指定名のプロファイルを削除する。</summary>
        /// <param name="name">削除するプロファイル名。</param>
        /// <returns>存在して削除できたなら true。</returns>
        public bool Delete(string name)
        {
            var index = FindNameIndex(NormalizeName(name, false));
            if (index < 0) return false;

            var storedName = _names[index];
            var dataKey = ProfileDataKey(storedName);
            var previousData = _storage.Load(dataKey);
            var previousCatalog = _storage.Load(CatalogKey);
            _names.RemoveAt(index);
            try
            {
                // 一覧を先に外す。途中停止しても、存在しない値を一覧から指さない。
                SaveCatalog();
                _storage.Delete(dataKey);
            }
            catch
            {
                _names.Insert(index, storedName);
                RestoreStorageBestEffort(dataKey, previousData);
                RestoreStorageBestEffort(CatalogKey, previousCatalog);
                throw;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>全プロファイルを削除する。</summary>
        public void Clear()
        {
            if (_names.Count == 0) return;

            var previousNames = new List<string>(_names);
            var previousCatalog = _storage.Load(CatalogKey);
            var dataKeys = new List<string>(previousNames.Count);
            var previousData = new List<string>(previousNames.Count);
            for (var i = 0; i < previousNames.Count; i++)
            {
                var dataKey = ProfileDataKey(previousNames[i]);
                dataKeys.Add(dataKey);
                previousData.Add(_storage.Load(dataKey));
            }

            _names.Clear();
            try
            {
                SaveCatalog();
                for (var i = 0; i < dataKeys.Count; i++) _storage.Delete(dataKeys[i]);
            }
            catch
            {
                _names.AddRange(previousNames);
                for (var i = 0; i < dataKeys.Count; i++)
                {
                    RestoreStorageBestEffort(dataKeys[i], previousData[i]);
                }

                RestoreStorageBestEffort(CatalogKey, previousCatalog);
                throw;
            }

            Changed?.Invoke();
        }

        /// <summary>保存先から名前一覧を読み直す。壊れた項目と重複名は除く。</summary>
        /// <returns>読み込めたプロファイル数。</returns>
        public int Reload()
        {
            var hadNames = _names.Count > 0;
            _names.Clear();

            DebugMenuProfileCatalog catalog;
            try
            {
                var json = _storage.Load(CatalogKey);
                if (string.IsNullOrEmpty(json))
                {
                    if (hadNames) Changed?.Invoke();
                    return 0;
                }

                catalog = JsonUtility.FromJson<DebugMenuProfileCatalog>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[DebugMenu] プロファイル一覧を読めなかった。空の一覧で続行する。\n{exception.Message}");
                if (hadNames) Changed?.Invoke();
                return 0;
            }

            if (catalog?.Names == null)
            {
                if (hadNames) Changed?.Invoke();
                return 0;
            }

            for (var i = 0; i < catalog.Names.Count; i++)
            {
                var name = NormalizeName(catalog.Names[i], false);
                if (string.IsNullOrEmpty(name) || FindNameIndex(name) >= 0) continue;
                _names.Add(name);
            }

            Changed?.Invoke();
            return _names.Count;
        }

        private string CatalogKey => _keyPrefix + ".catalog";

        private string ProfileDataKey(string name) => _keyPrefix + ".data." + EncodeName(name);

        private DebugMenuSettings CreateSettings(string name) =>
            new DebugMenuSettings(_storage, ProfileDataKey(name), Format);

        private void SaveCatalog()
        {
            if (_names.Count == 0)
            {
                _storage.Delete(CatalogKey);
                return;
            }

            var catalog = new DebugMenuProfileCatalog();
            catalog.Names.AddRange(_names);
            _storage.Save(CatalogKey, JsonUtility.ToJson(catalog, true));
        }

        private void RestoreStorageBestEffort(string key, string previousValue)
        {
            try
            {
                if (previousValue == null) _storage.Delete(key);
                else _storage.Save(key, previousValue);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[DebugMenu] プロファイル保存の巻き戻しに失敗した: {key}\n{exception.Message}");
            }
        }

        private int FindNameIndex(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;

            for (var i = 0; i < _names.Count; i++)
            {
                if (string.Equals(_names[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            }

            return -1;
        }

        private static string NormalizeName(string name, bool throwIfEmpty)
        {
            var normalized = name?.Trim();
            if (!string.IsNullOrEmpty(normalized) || !throwIfEmpty) return normalized;
            throw new ArgumentException("プロファイル名を指定してください。", nameof(name));
        }

        private static string EncodeName(string name)
        {
            var bytes = Encoding.UTF8.GetBytes(name);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
