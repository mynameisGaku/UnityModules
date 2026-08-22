// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectSetup.Editor
{
    internal sealed class ProjectSetupWindow : EditorWindow
    {
        internal const string RootElementName = "project-setup-root";
        internal const string ProfileFieldName = "profile-field";
        internal const string ChangeListName = "change-list";
        internal const string PreviewButtonName = "preview-button";
        internal const string ApplyButtonName = "apply-button";
        internal const string RestoreButtonName = "restore-button";
        private const string MenuPath = "Tools/Project Setup/Open";

        private ProjectSetupProfile _profile;
        private ProjectSetupService _service;
        private ObjectField _profileField;
        private Label _statusLabel;
        private VisualElement _changeList;
        private Button _applyButton;
        private Button _restoreButton;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            var window = GetWindow<ProjectSetupWindow>();
            window.titleContent = new GUIContent("Project Setup");
            window.minSize = new Vector2(720f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            _service = new ProjectSetupService(new UnityProjectSetupEnvironment(), new ProjectSetupBackupStore());
            EnsureTransientProfile();
        }

        private void OnDisable()
        {
            if (_profile != null && !AssetDatabase.Contains(_profile))
            {
                DestroyImmediate(_profile);
            }
        }

        public void CreateGUI()
        {
            EnsureTransientProfile();
            rootVisualElement.Clear();
            rootVisualElement.name = RootElementName;
            rootVisualElement.style.paddingLeft = 18f;
            rootVisualElement.style.paddingRight = 18f;
            rootVisualElement.style.paddingTop = 14f;
            rootVisualElement.style.paddingBottom = 14f;
            rootVisualElement.style.minWidth = 680f;

            var title = new Label("Prepare project settings safely");
            title.style.fontSize = 24f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4f;
            rootVisualElement.Add(title);

            var description = new Label("Store repeated Project Settings in a reusable profile. Preview every difference before applying, then restore the last backup when needed.");
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginBottom = 10f;
            rootVisualElement.Add(description);

            _statusLabel = new Label();
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.paddingLeft = 10f;
            _statusLabel.style.paddingRight = 10f;
            _statusLabel.style.paddingTop = 7f;
            _statusLabel.style.paddingBottom = 7f;
            _statusLabel.style.marginBottom = 10f;
            _statusLabel.style.backgroundColor = new Color(0.16f, 0.19f, 0.23f, 1f);
            rootVisualElement.Add(_statusLabel);

            rootVisualElement.Add(CreateProfileToolbar());
            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1f;
            scrollView.style.marginTop = 8f;
            scrollView.Add(CreateSettingsContent());
            rootVisualElement.Add(scrollView);
            rootVisualElement.Add(CreateActionBar());
            RefreshPreview();
        }

        private VisualElement CreateProfileToolbar()
        {
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.alignItems = Align.Center;
            toolbar.style.marginBottom = 2f;

            _profileField = new ObjectField("Profile")
            {
                name = ProfileFieldName,
                objectType = typeof(ProjectSetupProfile),
                value = AssetDatabase.Contains(_profile) ? _profile : null
            };
            _profileField.style.flexGrow = 1f;
            _profileField.RegisterValueChangedCallback(change => SetProfile(change.newValue as ProjectSetupProfile));
            toolbar.Add(_profileField);

            var newButton = new Button(CreateRecommendedProfile) { text = "New recommended profile" };
            newButton.style.marginLeft = 8f;
            toolbar.Add(newButton);

            var captureButton = new Button(CaptureCurrent) { text = "Capture current" };
            captureButton.style.marginLeft = 4f;
            toolbar.Add(captureButton);

            var saveButton = new Button(SaveProfileAs) { text = "Save profile as" };
            saveButton.style.marginLeft = 4f;
            toolbar.Add(saveButton);
            return toolbar;
        }

        private VisualElement CreateSettingsContent()
        {
            var content = new VisualElement();
            content.Add(CreateEnumCard(
                "asset-serialization",
                "Asset Serialization",
                "Use text assets when teams need readable diffs and stable merges.",
                _profile.ConfigureAssetSerialization,
                _profile.AssetSerialization,
                value => _profile.ConfigureAssetSerialization = value,
                value => _profile.AssetSerialization = (SerializationMode)value));
            content.Add(CreateTextCard(
                "version-control",
                "Version Control",
                "Visible Meta Files keeps Unity references reproducible in version control.",
                _profile.ConfigureVersionControl,
                _profile.VersionControlMode,
                value => _profile.ConfigureVersionControl = value,
                value => _profile.VersionControlMode = value));
            content.Add(CreateEnterPlayModeCard());
            content.Add(CreateEnumCard(
                "color-space",
                "Color Space",
                "Changing Color Space can trigger asset reimports. Keep this disabled unless the profile owns it.",
                _profile.ConfigureColorSpace,
                _profile.ColorSpace,
                value => _profile.ConfigureColorSpace = value,
                value => _profile.ColorSpace = (ColorSpace)value));
            content.Add(CreateToggleCard(
                "run-in-background",
                "Run In Background",
                "Choose whether the Player continues updating after it loses focus.",
                _profile.ConfigureRunInBackground,
                _profile.RunInBackground,
                value => _profile.ConfigureRunInBackground = value,
                value => _profile.RunInBackground = value));
            content.Add(CreateTextCard("company-name", "Company Name", "Shared Player identity value.", _profile.ConfigureCompanyName, _profile.CompanyName, value => _profile.ConfigureCompanyName = value, value => _profile.CompanyName = value));
            content.Add(CreateTextCard("product-name", "Product Name", "Shared Player product name.", _profile.ConfigureProductName, _profile.ProductName, value => _profile.ConfigureProductName = value, value => _profile.ProductName = value));
            content.Add(CreateTextCard("bundle-version", "Bundle Version", "Shared application version string.", _profile.ConfigureBundleVersion, _profile.BundleVersion, value => _profile.ConfigureBundleVersion = value, value => _profile.BundleVersion = value));

            var heading = new Label("Preview");
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.fontSize = 16f;
            heading.style.marginTop = 10f;
            heading.style.marginBottom = 5f;
            content.Add(heading);

            _changeList = new VisualElement { name = ChangeListName };
            _changeList.style.marginBottom = 10f;
            content.Add(_changeList);
            return content;
        }

        private VisualElement CreateEnterPlayModeCard()
        {
            var card = CreateCard("enter-play-mode", "Enter Play Mode", "Optional fast iteration settings. Disabled Domain Reload requires explicit static reset code.");
            var enabled = new Toggle("Apply this setting") { value = _profile.ConfigureEnterPlayMode };
            var useOptions = new Toggle("Use custom reload options") { value = _profile.EnterPlayModeOptionsEnabled };
            var flags = new EnumFlagsField("Disabled reloads", _profile.EnterPlayModeOptions);
            enabled.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.ConfigureEnterPlayMode = change.newValue));
            useOptions.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.EnterPlayModeOptionsEnabled = change.newValue));
            flags.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.EnterPlayModeOptions = (EnterPlayModeOptions)change.newValue));
            card.Add(enabled);
            card.Add(useOptions);
            card.Add(flags);
            return card;
        }

        private VisualElement CreateEnumCard(string name, string title, string description, bool configured, Enum value, Action<bool> setConfigured, Action<Enum> setValue)
        {
            var card = CreateCard(name, title, description);
            var enabled = new Toggle("Apply this setting") { value = configured };
            var field = new EnumField("Desired value", value);
            enabled.RegisterValueChangedCallback(change => ChangeProfile(() => setConfigured(change.newValue)));
            field.RegisterValueChangedCallback(change => ChangeProfile(() => setValue(change.newValue)));
            card.Add(enabled);
            card.Add(field);
            return card;
        }

        private VisualElement CreateTextCard(string name, string title, string description, bool configured, string value, Action<bool> setConfigured, Action<string> setValue)
        {
            var card = CreateCard(name, title, description);
            var enabled = new Toggle("Apply this setting") { value = configured };
            var field = new TextField("Desired value") { value = value ?? string.Empty };
            enabled.RegisterValueChangedCallback(change => ChangeProfile(() => setConfigured(change.newValue)));
            field.RegisterValueChangedCallback(change => ChangeProfile(() => setValue(change.newValue)));
            card.Add(enabled);
            card.Add(field);
            return card;
        }

        private VisualElement CreateToggleCard(string name, string title, string description, bool configured, bool value, Action<bool> setConfigured, Action<bool> setValue)
        {
            var card = CreateCard(name, title, description);
            var enabled = new Toggle("Apply this setting") { value = configured };
            var field = new Toggle("Desired value") { value = value };
            enabled.RegisterValueChangedCallback(change => ChangeProfile(() => setConfigured(change.newValue)));
            field.RegisterValueChangedCallback(change => ChangeProfile(() => setValue(change.newValue)));
            card.Add(enabled);
            card.Add(field);
            return card;
        }

        private static VisualElement CreateCard(string name, string title, string description)
        {
            var card = new VisualElement { name = name };
            card.style.paddingLeft = 10f;
            card.style.paddingRight = 10f;
            card.style.paddingTop = 8f;
            card.style.paddingBottom = 8f;
            card.style.marginBottom = 7f;
            card.style.borderTopWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftWidth = 1f;
            var border = new Color(0.28f, 0.31f, 0.35f, 1f);
            card.style.borderTopColor = border;
            card.style.borderRightColor = border;
            card.style.borderBottomColor = border;
            card.style.borderLeftColor = border;
            card.style.borderTopLeftRadius = 4f;
            card.style.borderTopRightRadius = 4f;
            card.style.borderBottomLeftRadius = 4f;
            card.style.borderBottomRightRadius = 4f;

            var heading = new Label(title);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.fontSize = 14f;
            card.Add(heading);
            var detail = new Label(description);
            detail.style.whiteSpace = WhiteSpace.Normal;
            detail.style.marginBottom = 4f;
            card.Add(detail);
            return card;
        }

        private VisualElement CreateActionBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.justifyContent = Justify.FlexEnd;
            bar.style.paddingTop = 8f;

            var previewButton = new Button(RefreshPreview) { name = PreviewButtonName, text = "Preview changes" };
            bar.Add(previewButton);
            _restoreButton = new Button(RestoreLast) { name = RestoreButtonName, text = "Restore last backup" };
            _restoreButton.style.marginLeft = 6f;
            bar.Add(_restoreButton);
            _applyButton = new Button(ApplyProfile) { name = ApplyButtonName, text = "Apply profile" };
            _applyButton.style.marginLeft = 6f;
            bar.Add(_applyButton);
            return bar;
        }

        private void EnsureTransientProfile()
        {
            if (_profile != null)
            {
                return;
            }

            _profile = CreateInstance<ProjectSetupProfile>();
            _profile.hideFlags = HideFlags.HideAndDontSave;
            _profile.SetRecommendedDefaults();
        }

        private void SetProfile(ProjectSetupProfile profile)
        {
            if (_profile != null && !AssetDatabase.Contains(_profile))
            {
                DestroyImmediate(_profile);
            }

            _profile = profile;
            EnsureTransientProfile();
            CreateGUI();
        }

        private void CreateRecommendedProfile()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create Project Setup Profile", "ProjectSetupProfile", "asset", "Choose where to save the profile asset.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var profile = CreateInstance<ProjectSetupProfile>();
            profile.SetRecommendedDefaults();
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.SaveAssets();
            SetProfile(profile);
        }

        private void CaptureCurrent()
        {
            if (_service == null)
            {
                return;
            }

            var environment = new UnityProjectSetupEnvironment();
            if (!environment.IsAvailable)
            {
                _statusLabel.text = "Project Settings are unavailable while Unity is busy or entering Play Mode.";
                return;
            }

            _profile.Capture(environment.Capture());
            MarkProfileDirty();
            CreateGUI();
        }

        private void SaveProfileAs()
        {
            var path = EditorUtility.SaveFilePanelInProject("Save Project Setup Profile", "ProjectSetupProfile", "asset", "Choose where to save a copy of the profile.");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var copy = Instantiate(_profile);
            copy.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(copy, path);
            AssetDatabase.SaveAssets();
            SetProfile(copy);
        }

        private void ChangeProfile(Action change)
        {
            change();
            MarkProfileDirty();
            RefreshPreview();
        }

        private void MarkProfileDirty()
        {
            if (AssetDatabase.Contains(_profile))
            {
                EditorUtility.SetDirty(_profile);
            }
        }

        private void RefreshPreview()
        {
            if (_service == null || _changeList == null || _statusLabel == null)
            {
                return;
            }

            _changeList.Clear();
            var plan = _service.Preview(_profile);
            if (!plan.IsValid)
            {
                _statusLabel.text = plan.Errors[0];
                foreach (var error in plan.Errors)
                {
                    AddPreviewLine(error, true);
                }
            }
            else if (!plan.HasChanges)
            {
                _statusLabel.text = "Ready. The current project already matches every enabled profile setting.";
                AddPreviewLine("No changes are required.", false);
            }
            else
            {
                _statusLabel.text = $"Ready. {plan.Changes.Count} setting change(s) will be applied after confirmation.";
                foreach (var change in plan.Changes)
                {
                    AddPreviewLine($"{change.Label}: {change.CurrentValue}  ->  {change.DesiredValue}", false);
                }
            }

            _applyButton?.SetEnabled(plan.IsValid && plan.HasChanges);
            _restoreButton?.SetEnabled(_service.HasBackup);
        }

        private void AddPreviewLine(string text, bool error)
        {
            var row = new Label(text);
            row.style.whiteSpace = WhiteSpace.Normal;
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 8f;
            row.style.paddingTop = 5f;
            row.style.paddingBottom = 5f;
            row.style.marginBottom = 3f;
            row.style.backgroundColor = error ? new Color(0.33f, 0.16f, 0.16f, 1f) : new Color(0.13f, 0.15f, 0.18f, 1f);
            _changeList.Add(row);
        }

        private void ApplyProfile()
        {
            var plan = _service.Preview(_profile);
            if (!plan.IsValid || !plan.HasChanges)
            {
                RefreshPreview();
                return;
            }

            var summary = string.Join("\n", plan.Changes.Take(8).Select(change => $"- {change.Label}: {change.CurrentValue} -> {change.DesiredValue}"));
            if (plan.Changes.Count > 8)
            {
                summary += $"\n- ... and {plan.Changes.Count - 8} more";
            }

            if (!EditorUtility.DisplayDialog("Apply Project Setup Profile", $"The current settings will be backed up before applying:\n\n{summary}", "Apply", "Cancel"))
            {
                return;
            }

            var result = _service.Apply(_profile);
            RefreshPreview();
            _statusLabel.text = result.Message;
        }

        private void RestoreLast()
        {
            var plan = _service.PreviewRestore(out _, out var error);
            if (!plan.IsValid)
            {
                _statusLabel.text = error;
                RefreshPreview();
                return;
            }

            var summary = plan.HasChanges
                ? string.Join("\n", plan.Changes.Take(8).Select(change => $"- {change.Label}: {change.CurrentValue} -> {change.DesiredValue}"))
                : "No settings need to change.";
            if (!EditorUtility.DisplayDialog("Restore Project Setup Backup", summary, "Restore", "Cancel"))
            {
                return;
            }

            var result = _service.RestoreLast();
            RefreshPreview();
            _statusLabel.text = result.Message;
        }
    }
}
