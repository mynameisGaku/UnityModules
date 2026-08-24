// SPDX-License-Identifier: MIT

using System;
using UnityEngine;

namespace PlayerOptions
{
    /// <summary>一つのPlayerPrefs keyへversion付きJSON文書を保存する標準backend。</summary>
    public sealed class PlayerPrefsPlayerOptionsStorage : IPlayerOptionsStorage
    {
        private const int MaximumKeyLength = 256;

        /// <summary>保存に使用するkeyを指定する。</summary>
        /// <param name="key">空白以外で256文字以下のproject内key。</param>
        public PlayerPrefsPlayerOptionsStorage(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("PlayerPrefs keyが空です。", nameof(key));
            if (key.Length > MaximumKeyLength) throw new ArgumentException("PlayerPrefs keyは256文字以下にしてください。", nameof(key));
            Key = key;
        }

        /// <summary>一つのJSON文書を保持するPlayerPrefs key。</summary>
        public string Key { get; }

        /// <inheritdoc/>
        public bool TryRead(out string contents)
        {
            if (!PlayerPrefs.HasKey(Key))
            {
                contents = null;
                return false;
            }

            contents = PlayerPrefs.GetString(Key, null);
            return true;
        }

        /// <inheritdoc/>
        public void Write(string contents)
        {
            if (contents == null) throw new ArgumentNullException(nameof(contents));
            PlayerPrefs.SetString(Key, contents);
            PlayerPrefs.Save();
        }
    }
}
