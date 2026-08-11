using System;
using System.Collections.Generic;
using System.IO;

namespace DebugMenu
{
    /// <summary>
    /// ファイルまたはフォルダーのパスを直接入力する行。
    /// 存在確認とファイル拡張子の制限は必要な場合だけ有効にできる。
    /// </summary>
    public sealed class DebugPath : DebugElement
    {
        private readonly Func<string> _getter;
        private readonly Action<string> _setter;
        private readonly string _defaultValue;
        private readonly List<string> _extensions = new List<string>();
        private readonly IReadOnlyList<string> _readOnlyExtensions;

        private string _stored;

        /// <summary>ゲーム側の文字列を直接読み書きするパス行を作る。</summary>
        /// <param name="label">表示名。</param>
        /// <param name="mode">ファイルとフォルダーのどちらを受け付けるか。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public DebugPath(string label, DebugPathMode mode, Func<string> getter, Action<string> setter) : base(label)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            _defaultValue = getter() ?? string.Empty;
            _readOnlyExtensions = _extensions.AsReadOnly();
            Mode = mode;
            IsExpandable = false;
        }

        /// <summary>行自身が値を抱えるパス行を作る。</summary>
        /// <param name="label">表示名。</param>
        /// <param name="mode">ファイルとフォルダーのどちらを受け付けるか。</param>
        /// <param name="initialValue">初期値。</param>
        public DebugPath(string label, DebugPathMode mode, string initialValue = null) : base(label)
        {
            _stored = initialValue ?? string.Empty;
            _defaultValue = _stored;
            _readOnlyExtensions = _extensions.AsReadOnly();
            Mode = mode;
            IsExpandable = false;
        }

        /// <summary>ファイルとフォルダーのどちらを受け付けるか。</summary>
        public DebugPathMode Mode { get; }

        /// <summary>入力確定時に対象の存在を必須にするか。</summary>
        public bool RequireExisting { get; set; }

        /// <summary>ファイルモードで受け付ける拡張子。空なら全て受け付ける。</summary>
        public IReadOnlyList<string> Extensions => _readOnlyExtensions;

        /// <summary>最後に入力を拒んだ理由。直近の入力が有効なら空。</summary>
        public string LastValidationError { get; private set; } = string.Empty;

        /// <summary>現在のパス。代入時にも設定済みの検証を行う。</summary>
        public string Value
        {
            get => (_getter != null ? _getter() : _stored) ?? string.Empty;
            set => TrySetValue(value);
        }

        /// <inheritdoc/>
        public override DebugValueKind ValueKind => DebugValueKind.Text;

        /// <inheritdoc/>
        public override bool CanTypeValue => true;

        /// <inheritdoc/>
        public override bool IsModified => !string.Equals(Value, _defaultValue, StringComparison.Ordinal);

        /// <summary>存在確認の有無を設定して、そのまま返す。</summary>
        /// <param name="required">存在を必須にするなら true。</param>
        public DebugPath WithExistingPathRequired(bool required = true)
        {
            RequireExisting = required;
            return this;
        }

        /// <summary>
        /// ファイルモードで受け付ける拡張子を設定する。
        /// <c>txt</c>、<c>.txt</c>、<c>*.txt</c> は全て <c>.txt</c> として扱う。
        /// </summary>
        /// <param name="extensions">受け付ける拡張子。空なら制限を解除する。</param>
        public DebugPath WithExtensions(params string[] extensions)
        {
            _extensions.Clear();
            if (extensions == null) return this;

            for (var i = 0; i < extensions.Length; i++)
            {
                var normalized = NormalizeExtension(extensions[i]);
                if (string.IsNullOrEmpty(normalized)) continue;

                var duplicate = false;
                for (var j = 0; j < _extensions.Count; j++)
                {
                    if (!string.Equals(_extensions[j], normalized, StringComparison.OrdinalIgnoreCase)) continue;
                    duplicate = true;
                    break;
                }

                if (!duplicate) _extensions.Add(normalized);
            }

            return this;
        }

        /// <summary>現在の検証設定でパスを受け付けられるか調べる。</summary>
        /// <param name="path">調べるパス。</param>
        /// <param name="error">受け付けられない理由。有効なら空。</param>
        public bool IsValidPath(string path, out string error)
        {
            var candidate = path ?? string.Empty;

            if (RequireExisting)
            {
                var exists = Mode == DebugPathMode.File ? File.Exists(candidate) : Directory.Exists(candidate);
                if (!exists)
                {
                    error = Mode == DebugPathMode.File ? "ファイルが存在しません。" : "フォルダーが存在しません。";
                    return false;
                }
            }

            if (Mode == DebugPathMode.File && _extensions.Count > 0)
            {
                var accepted = false;
                for (var i = 0; i < _extensions.Count; i++)
                {
                    if (!candidate.EndsWith(_extensions[i], StringComparison.OrdinalIgnoreCase)) continue;
                    accepted = true;
                    break;
                }

                if (!accepted)
                {
                    error = "許可されていない拡張子です。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        /// <summary>検証に成功した場合だけ値を書き込む。</summary>
        /// <param name="path">書き込むパス。</param>
        public bool TrySetValue(string path)
        {
            var next = path ?? string.Empty;
            if (!IsValidPath(next, out var error))
            {
                LastValidationError = error;
                return false;
            }

            LastValidationError = string.Empty;
            SetValueUnchecked(next);
            return true;
        }

        /// <inheritdoc/>
        public override string GetValueText() => Value;

        /// <inheritdoc/>
        public override string GetEditText() => Value;

        /// <inheritdoc/>
        public override bool CommitEditText(string text) => TrySetValue(text);

        /// <inheritdoc/>
        public override void ResetToDefault()
        {
            LastValidationError = string.Empty;
            SetValueUnchecked(_defaultValue);
        }

        private void SetValueUnchecked(string value)
        {
            if (string.Equals(Value, value, StringComparison.Ordinal)) return;

            if (_setter != null) _setter(value);
            else _stored = value;
            NotifyChanged();
        }

        private static string NormalizeExtension(string extension)
        {
            var normalized = extension?.Trim();
            if (string.IsNullOrEmpty(normalized) || normalized == "*" || normalized == "*.*") return string.Empty;
            if (normalized.StartsWith("*", StringComparison.Ordinal)) normalized = normalized.Substring(1);
            if (!normalized.StartsWith(".", StringComparison.Ordinal)) normalized = "." + normalized;
            return normalized;
        }
    }
}
