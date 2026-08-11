using System;
using System.IO;
using UnityEngine;

namespace DebugMenu
{
    /// <summary>プロファイルと任意ファイルへの保存・読み込みを通常メニューから操作する。</summary>
    public sealed class DebugMenuSettingsPage : IDisposable
    {
        private readonly DebugMenuRoot _menu;
        private readonly DebugMenuSettings _settings;
        private readonly DebugMenuProfiles _profiles;
        private readonly TransientTextElement _profileNameElement;
        private readonly TransientTextElement _filePathElement;
        private readonly SettingsFormatElement _formatElement;

        private string _profileName = "Default";
        private string _filePath;

        /// <summary>対象メニュー、通常保存、プロファイルサービスを指定して作る。</summary>
        public DebugMenuSettingsPage(DebugMenuRoot menu, DebugMenuSettings settings, DebugMenuProfiles profiles)
        {
            _menu = menu ?? throw new ArgumentNullException(nameof(menu));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));

            Page = new DebugPage("Settings")
            {
                Description = "名前付きプロファイル、保存形式、任意ファイルへの保存と読み込み。",
            };

            _filePath = Path.Combine(
                Application.persistentDataPath,
                "DebugMenu",
                "debug-menu-settings" + DebugMenuSettingsSerializer.GetExtension(_settings.Format));
            _profileNameElement = new TransientTextElement("Profile Name", () => _profileName, value => _profileName = value);
            _filePathElement = new TransientTextElement("File", () => _filePath, value => _filePath = value);
            _formatElement = new SettingsFormatElement(_settings.Format, SetFormat);

            _profiles.Changed += Rebuild;
            Rebuild();
        }

        /// <summary>トップレベルへ登録する設定ページ。</summary>
        public DebugPage Page { get; }

        /// <summary>現在入力されているプロファイル名。</summary>
        public string ProfileName
        {
            get => _profileName;
            set => _profileName = value?.Trim() ?? string.Empty;
        }

        /// <summary>Save As / Load From の対象パス。</summary>
        public string FilePath
        {
            get => _filePath;
            set => _filePath = value ?? string.Empty;
        }

        /// <summary>最後の操作結果。設定ページの説明欄にも表示する。</summary>
        public string LastResult { get; private set; } = string.Empty;

        /// <summary>現在値を入力名のプロファイルへ保存する。</summary>
        public int SaveProfile()
        {
            try
            {
                var count = _profiles.Save(_profileName, _menu);
                LastResult = $"Profile '{_profileName}' saved ({count})";
                Page.Description = LastResult;
                return count;
            }
            catch (Exception exception)
            {
                LastResult = exception.Message;
                Page.Description = LastResult;
                return 0;
            }
        }

        /// <summary>入力名のプロファイルを適用する。</summary>
        public int LoadProfile()
        {
            var applied = _profiles.TryApply(_profileName, _menu, out var count) ? count : 0;
            LastResult = applied > 0 ? $"Profile '{_profileName}' loaded ({applied})" : $"Profile '{_profileName}' not found";
            Page.Description = LastResult;
            return applied;
        }

        /// <summary>入力名のプロファイルを削除する。</summary>
        public bool DeleteProfile()
        {
            var deleted = _profiles.Delete(_profileName);
            LastResult = deleted ? $"Profile '{_profileName}' deleted" : $"Profile '{_profileName}' not found";
            Page.Description = LastResult;
            return deleted;
        }

        /// <summary>現在値を指定パスへ現在形式で保存する。</summary>
        public int SaveAs()
        {
            try
            {
                var count = _settings.SaveAs(_menu, _filePath, _settings.Format);
                LastResult = $"Saved {count}: {_filePath}";
                Page.Description = LastResult;
                return count;
            }
            catch (Exception exception)
            {
                LastResult = exception.Message;
                Page.Description = LastResult;
                return 0;
            }
        }

        /// <summary>指定パスから形式を自動判別して読み込む。</summary>
        public int LoadFrom()
        {
            var count = _settings.LoadFrom(_menu, _filePath);
            LastResult = count > 0 ? $"Loaded {count}: {_filePath}" : $"Could not load: {_filePath}";
            Page.Description = LastResult;
            return count;
        }

        /// <summary>イベント購読を解除する。</summary>
        public void Dispose() => _profiles.Changed -= Rebuild;

        private void SetFormat(DebugMenuSettingsFormat format)
        {
            var oldExtension = Path.GetExtension(_filePath);
            _settings.Format = format;
            _profiles.Format = format;

            if (string.Equals(oldExtension, ".json", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(oldExtension, ".txt", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(oldExtension, ".bin", StringComparison.OrdinalIgnoreCase))
            {
                _filePath = Path.ChangeExtension(_filePath, DebugMenuSettingsSerializer.GetExtension(format));
            }
        }

        private void Rebuild()
        {
            Page.Root.ClearChildren();
            Page.Root.Add(_profileNameElement);
            Page.Root.Add(_formatElement);
            Page.Root.Add(new DebugAction("Save Profile", () => SaveProfile()));
            Page.Root.Add(new DebugAction("Load Profile", () => LoadProfile()));
            Page.Root.Add(new DebugAction("Delete Profile", () => DeleteProfile()));
            Page.Root.Add(_filePathElement);
            Page.Root.Add(new DebugAction("Save As", () => SaveAs()));
            Page.Root.Add(new DebugAction("Load From", () => LoadFrom()));
            Page.Root.Add(new DebugAction("Reset All", () => DebugMenuSettings.ResetAll(_menu)));

            var group = Page.Root.Add(new DebugGroup("Saved Profiles") { IsExpanded = true });
            if (_profiles.Count == 0)
            {
                group.Add(new SettingsMessageElement("No profiles"));
            }
            else
            {
                for (var i = 0; i < _profiles.Names.Count; i++)
                {
                    var name = _profiles.Names[i];
                    group.Add(new ProfileEntryElement(name, () =>
                    {
                        _profileName = name;
                        LoadProfile();
                    }));
                }
            }

            Page.Invalidate();
        }

        /// <summary>保存対象にしない一時文字列入力。</summary>
        private sealed class TransientTextElement : DebugElement
        {
            private readonly Func<string> _getter;
            private readonly Action<string> _setter;

            public TransientTextElement(string label, Func<string> getter, Action<string> setter) : base(label)
            {
                _getter = getter;
                _setter = setter;
                IsExpandable = false;
                MarkerVisibility = DebugMarkerVisibility.Never;
            }

            public override bool CanTypeValue => true;
            public override bool IsSaveable => false;
            public override bool IsSearchable => false;
            public override string GetValueText() => _getter() ?? string.Empty;
            public override string GetEditText() => _getter() ?? string.Empty;

            public override bool CommitEditText(string text)
            {
                _setter(text ?? string.Empty);
                return true;
            }
        }

        /// <summary>保存形式を左右送りまたは候補一覧から選ぶ。</summary>
        private sealed class SettingsFormatElement : DebugElement
        {
            private readonly Action<DebugMenuSettingsFormat> _setter;
            private DebugMenuSettingsFormat _format;

            public SettingsFormatElement(DebugMenuSettingsFormat format, Action<DebugMenuSettingsFormat> setter) : base("Format")
            {
                _format = format;
                _setter = setter;
                for (var i = 0; i < 3; i++) Add(new FormatOptionElement(this, (DebugMenuSettingsFormat)i));
            }

            public override bool IsAdjustable => true;
            public override bool IsSaveable => false;
            public override bool IsSearchable => false;
            public override string GetValueText() => _format.ToString();

            public override void OnAdjust(int delta)
            {
                var count = 3;
                var next = ((int)_format + delta) % count;
                Set(next < 0 ? (DebugMenuSettingsFormat)(next + count) : (DebugMenuSettingsFormat)next);
            }

            public override bool TryGetSelection(out int index, out int count)
            {
                index = (int)_format;
                count = 3;
                return true;
            }

            public void Set(DebugMenuSettingsFormat format)
            {
                if (_format == format) return;
                _format = format;
                _setter(format);
            }
        }

        /// <summary>保存形式一覧の1行。</summary>
        private sealed class FormatOptionElement : DebugElement
        {
            private readonly SettingsFormatElement _owner;
            private readonly DebugMenuSettingsFormat _format;

            public FormatOptionElement(SettingsFormatElement owner, DebugMenuSettingsFormat format) : base(format.ToString())
            {
                _owner = owner;
                _format = format;
                IsExpandable = false;
                MarkerVisibility = DebugMarkerVisibility.Never;
            }

            public override bool IsSaveable => false;
            public override bool IsSearchable => false;
            public override void OnDecide()
            {
                _owner.Set(_format);
                _owner.IsExpanded = false;
            }
        }

        /// <summary>保存済みプロファイルを即座に適用する行。</summary>
        private sealed class ProfileEntryElement : DebugElement
        {
            private readonly Action _load;

            public ProfileEntryElement(string name, Action load) : base(name, "Load")
            {
                _load = load;
                IsExpandable = false;
                MarkerVisibility = DebugMarkerVisibility.Never;
            }

            public override bool IsSaveable => false;
            public override void OnDecide() => _load();
        }

        /// <summary>プロファイルが無い場合の案内行。</summary>
        private sealed class SettingsMessageElement : DebugElement
        {
            public SettingsMessageElement(string label) : base(label)
            {
                IsExpandable = false;
                MarkerVisibility = DebugMarkerVisibility.Never;
            }

            public override bool IsSaveable => false;
            public override bool IsSearchable => false;
        }
    }
}
