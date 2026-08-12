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
        private string _defaultValue = string.Empty;
        private bool _hasDefaultValue;
        private readonly List<string> _extensions = new List<string>();
        private readonly IReadOnlyList<string> _readOnlyExtensions;

        private string _stored;
        private string _currentDirectory = string.Empty;

        /// <summary>ゲーム側の文字列を直接読み書きするパス行を作る。</summary>
        /// <param name="label">表示名。</param>
        /// <param name="mode">ファイルとフォルダーのどちらを受け付けるか。</param>
        /// <param name="getter">現在値を返す関数。</param>
        /// <param name="setter">値を書き込む関数。</param>
        public DebugPath(string label, DebugPathMode mode, Func<string> getter, Action<string> setter) : base(label)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
            if (TryReadExternalValue(getter, out var initialValue))
            {
                _defaultValue = initialValue ?? string.Empty;
                _hasDefaultValue = true;
            }
            _readOnlyExtensions = _extensions.AsReadOnly();
            Mode = mode;
            MarkerVisibility = DebugMarkerVisibility.Always;
        }

        /// <summary>行自身が値を抱えるパス行を作る。</summary>
        /// <param name="label">表示名。</param>
        /// <param name="mode">ファイルとフォルダーのどちらを受け付けるか。</param>
        /// <param name="initialValue">初期値。</param>
        public DebugPath(string label, DebugPathMode mode, string initialValue = null) : base(label)
        {
            _stored = initialValue ?? string.Empty;
            _defaultValue = _stored;
            _hasDefaultValue = true;
            _readOnlyExtensions = _extensions.AsReadOnly();
            Mode = mode;
            MarkerVisibility = DebugMarkerVisibility.Always;
        }

        /// <summary>ファイルとフォルダーのどちらを受け付けるか。</summary>
        public DebugPathMode Mode { get; }

        /// <summary>入力確定時に対象の存在を必須にするか。</summary>
        public bool RequireExisting { get; set; }

        /// <summary>ファイルモードで受け付ける拡張子。空なら全て受け付ける。</summary>
        public IReadOnlyList<string> Extensions => _readOnlyExtensions;

        /// <summary>最後に入力を拒んだ理由。直近の入力が有効なら空。</summary>
        public string LastValidationError { get; private set; } = string.Empty;

        /// <summary>ブラウザーが現在表示しているフォルダー。未展開なら空。</summary>
        public string CurrentDirectory => _currentDirectory;

        /// <summary>ブラウザー候補が組み直されたときに呼ばれる。</summary>
        public event Action StructureChanged;

        /// <summary>現在のパス。代入時にも設定済みの検証を行う。</summary>
        public string Value
        {
            get
            {
                var value = (_getter != null ? _getter() : _stored) ?? string.Empty;
                CaptureDefaultIfNeeded(value);
                return value;
            }
            set => TrySetValue(value);
        }

        /// <inheritdoc/>
        public override DebugValueKind ValueKind => DebugValueKind.Text;

        /// <inheritdoc/>
        public override bool CanTypeValue => true;

        /// <summary>行決定は直接入力よりブラウザー展開を優先する。</summary>
        public override bool PrefersDecide => true;

        /// <inheritdoc/>
        public override bool IsModified => TryGetCurrent(out var value) && !string.Equals(value, _defaultValue, StringComparison.Ordinal);

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
            if (extensions != null)
            {
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
            }

            if (IsExpanded) RebuildBrowser();

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
            return SetValueUnchecked(next);
        }

        /// <inheritdoc/>
        public override string GetValueText() => Value;

        /// <inheritdoc/>
        public override string GetEditText() => Value;

        /// <summary>ブラウザーの展開と折り畳みを切り替える。</summary>
        public override void OnDecide()
        {
            if (IsExpanded)
            {
                CollapseBrowser();
                return;
            }

            if (!TryResolveStartDirectory(out _currentDirectory)) return;

            IsExpanded = true;
            RebuildBrowser();
        }

        /// <inheritdoc/>
        public override bool CommitEditText(string text) => TrySetValue(text);

        /// <inheritdoc/>
        public override void ResetToDefault()
        {
            LastValidationError = string.Empty;
            if (!TryGetCurrent(out var current)) return;
            SetValueUnchecked(_defaultValue, current);
        }

        private void RebuildBrowser()
        {
            ClearChildren();

            if (string.IsNullOrEmpty(_currentDirectory) && !TryResolveStartDirectory(out _currentDirectory)) return;

            AddParentRow();
            if (Mode == DebugPathMode.Folder)
            {
                Add(new DebugPathBrowserRow("Use This Folder", _currentDirectory, SelectCurrentFolder));
            }

            AddDirectoryRows();
            if (Mode == DebugPathMode.File) AddFileRows();

            StructureChanged?.Invoke();
        }

        private void AddParentRow()
        {
            try
            {
                var parent = Directory.GetParent(_currentDirectory);
                if (parent == null) return;

                var path = parent.FullName;
                Add(new DebugPathBrowserRow("[..] Parent", path, () => NavigateTo(path)));
            }
            catch (Exception exception)
            {
                AddErrorRow("Parent", exception);
            }
        }

        private void AddDirectoryRows()
        {
            try
            {
                var directories = Directory.GetDirectories(_currentDirectory);
                Array.Sort(directories, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < directories.Length; i++)
                {
                    var path = directories[i];
                    var name = Path.GetFileName(path);
                    Add(new DebugPathBrowserRow("[Folder] " + name, path, () => NavigateTo(path)));
                }
            }
            catch (Exception exception)
            {
                AddErrorRow("Folders", exception);
            }
        }

        private void AddFileRows()
        {
            try
            {
                var files = Directory.GetFiles(_currentDirectory);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < files.Length; i++)
                {
                    var path = files[i];
                    if (!IsAcceptedFile(path)) continue;

                    var name = Path.GetFileName(path);
                    Add(new DebugPathBrowserRow("[File] " + name, path, () => SelectFile(path)));
                }
            }
            catch (Exception exception)
            {
                AddErrorRow("Files", exception);
            }
        }

        private void NavigateTo(string directory)
        {
            _currentDirectory = directory ?? string.Empty;
            RebuildBrowser();
        }

        private void SelectFile(string path)
        {
            if (TrySetValue(path)) CollapseBrowser();
            else RebuildBrowser();
        }

        private void SelectCurrentFolder()
        {
            if (TrySetValue(_currentDirectory)) CollapseBrowser();
            else RebuildBrowser();
        }

        private void CollapseBrowser()
        {
            IsExpanded = false;
            _currentDirectory = string.Empty;
            ClearChildren();
            StructureChanged?.Invoke();
        }

        private void AddErrorRow(string operation, Exception exception)
        {
            var message = exception?.Message ?? "Unknown error";
            Add(new DebugPathBrowserRow("[Error] " + operation, message));
        }

        private bool TryResolveStartDirectory(out string directory)
        {
            if (!TryGetCurrent(out var value))
            {
                directory = string.Empty;
                return false;
            }

            var candidate = value;

            try
            {
                if (Mode == DebugPathMode.File && !Directory.Exists(candidate)) candidate = Path.GetDirectoryName(candidate);
                if (string.IsNullOrWhiteSpace(candidate)) candidate = Directory.GetCurrentDirectory();

                var fullPath = Path.GetFullPath(candidate);
                while (!Directory.Exists(fullPath))
                {
                    var parent = Directory.GetParent(fullPath);
                    if (parent == null) break;
                    fullPath = parent.FullName;
                }

                if (Directory.Exists(fullPath))
                {
                    directory = fullPath;
                    return true;
                }
            }
            catch
            {
                // 無効な文字や存在しないドライブは、現在フォルダーへ戻して表示を続ける。
            }

            directory = Directory.GetCurrentDirectory();
            return true;
        }

        private bool IsAcceptedFile(string path)
        {
            if (_extensions.Count == 0) return true;

            for (var i = 0; i < _extensions.Count; i++)
            {
                if (path.EndsWith(_extensions[i], StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private bool SetValueUnchecked(string value)
        {
            if (!TryGetCurrent(out var current)) return false;

            return SetValueUnchecked(value, current);
        }

        private bool SetValueUnchecked(string value, string current)
        {
            if (string.Equals(current, value, StringComparison.Ordinal))
            {
                ClearReadError("値設定");
                return true;
            }

            if (_setter != null)
            {
                if (!TryWriteExternalValue(_setter, value)) return false;
            }
            else
            {
                _stored = value;
                ClearReadError("値設定");
            }

            NotifyChanged();
            return true;
        }

        private bool TryGetCurrent(out string value)
        {
            if (_getter == null)
            {
                value = _stored ?? string.Empty;
                return true;
            }

            if (!TryReadExternalValue(_getter, out value)) return false;

            value ??= string.Empty;
            CaptureDefaultIfNeeded(value);
            return true;
        }

        private void CaptureDefaultIfNeeded(string value)
        {
            if (_hasDefaultValue) return;

            _defaultValue = value ?? string.Empty;
            _hasDefaultValue = true;
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
