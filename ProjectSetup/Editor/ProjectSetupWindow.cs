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
        internal const string ProfileToolbarName = "profile-toolbar";
        internal const string ProfileFieldName = "profile-field";
        internal const string ProfileActionsName = "profile-actions";
        internal const string NewProfileButtonName = "new-profile-button";
        internal const string CaptureProfileButtonName = "capture-profile-button";
        internal const string SaveProfileButtonName = "save-profile-button";
        internal const string ChangeListName = "change-list";
        internal const string PreviewButtonName = "preview-button";
        internal const string ApplyButtonName = "apply-button";
        internal const string RestoreButtonName = "restore-button";
        internal const string BuildScenesCardName = "build-scenes";
        internal const string BuildScenesListName = "build-scenes-list";
        internal const string AddBuildSceneButtonName = "add-build-scene-button";
        internal const string PlayModeStartSceneCardName = "play-mode-start-scene";
        internal const string PlayModeStartSceneFieldName = "play-mode-start-scene-field";
        internal const string ScriptingDefineCardName = "scripting-define-symbols";
        internal const string ScriptingDefineFieldName = "scripting-define-symbols-field";
        internal const string RootNamespaceCardName = "root-namespace";
        internal const string NewScriptLineEndingsCardName = "new-script-line-endings";
        internal const string NamingDefaultsCardName = "duplicate-naming";
        internal const string ProjectFoldersCardName = "project-folders";
        internal const string ProjectFoldersFieldName = "project-folders-field";
        internal const string VersionControlFilesCardName = "version-control-files";
        internal const string VersionControlFilesToggleName = "version-control-files-toggle";
        internal const string AssemblyDefinitionsCardName = "script-assemblies";
        internal const string AssemblyNameFieldName = "assembly-name-field";
        internal const string RuntimeAssemblyFolderFieldName = "runtime-assembly-folder-field";
        internal const string EditorAssemblyFolderFieldName = "editor-assembly-folder-field";
        internal const string IncludeTestAssembliesToggleName = "include-test-assemblies-toggle";
        internal const string TestAssemblyRootFolderFieldName = "test-assembly-root-folder-field";
        internal const string ActionBarName = "action-bar";
        private const string MenuPath = "Tools/Project Setup/Open";

        private ProjectSetupProfile _profile;
        private ProjectSetupService _service;
        private ObjectField _profileField;
        private Label _statusLabel;
        private VisualElement _changeList;
        private Button _applyButton;
        private Button _restoreButton;
        private VisualElement _buildSceneList;

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
            var toolbar = new VisualElement { name = ProfileToolbarName };
            toolbar.style.flexDirection = FlexDirection.Column;
            toolbar.style.flexShrink = 0f;
            toolbar.style.marginBottom = 4f;

            _profileField = new ObjectField("Profile")
            {
                name = ProfileFieldName,
                objectType = typeof(ProjectSetupProfile),
                value = AssetDatabase.Contains(_profile) ? _profile : null
            };
            _profileField.style.flexGrow = 1f;
            _profileField.RegisterValueChangedCallback(change => SetProfile(change.newValue as ProjectSetupProfile));
            toolbar.Add(_profileField);

            var actions = new VisualElement { name = ProfileActionsName };
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.justifyContent = Justify.FlexEnd;
            actions.style.flexShrink = 0f;
            actions.style.minHeight = 26f;
            actions.style.marginTop = 5f;
            actions.style.marginBottom = 7f;

            var newButton = new Button(CreateRecommendedProfile)
            {
                name = NewProfileButtonName,
                text = "New recommended profile"
            };
            newButton.style.minHeight = 22f;
            actions.Add(newButton);

            var captureButton = new Button(CaptureCurrent)
            {
                name = CaptureProfileButtonName,
                text = "Capture current"
            };
            captureButton.style.marginLeft = 4f;
            captureButton.style.minHeight = 22f;
            actions.Add(captureButton);

            var saveButton = new Button(SaveProfileAs)
            {
                name = SaveProfileButtonName,
                text = "Save profile as"
            };
            saveButton.style.marginLeft = 4f;
            saveButton.style.minHeight = 22f;
            actions.Add(saveButton);
            toolbar.Add(actions);
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
            content.Add(CreateVersionControlFilesCard());
            content.Add(CreateEnterPlayModeCard());
            content.Add(CreatePlayModeStartSceneCard());
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
            content.Add(CreateScriptingDefineCard());
            content.Add(CreateTextCard(
                RootNamespaceCardName,
                "Root Namespace",
                "Set the default namespace used by generated C# projects. Leave the value empty to clear it.",
                _profile.ConfigureRootNamespace,
                _profile.RootNamespace,
                value => _profile.ConfigureRootNamespace = value,
                value => _profile.RootNamespace = value));
            content.Add(CreateEnumCard(
                NewScriptLineEndingsCardName,
                "New Script Line Endings",
                "Choose the line ending written into new C# scripts created by Unity.",
                _profile.ConfigureNewScriptLineEndings,
                _profile.NewScriptLineEndings,
                value => _profile.ConfigureNewScriptLineEndings = value,
                value => _profile.NewScriptLineEndings = (LineEndingsMode)value));
            content.Add(CreateNamingDefaultsCard());
            content.Add(CreateProjectFoldersCard());
            content.Add(CreateAssemblyDefinitionsCard());
            content.Add(CreateBuildScenesCard());
            content.Add(CreateNameListCard(
                "tags",
                "Tags",
                "Add missing custom Tags without deleting or reordering existing Tags. Enter one name per line.",
                _profile.ConfigureTags,
                _profile.Tags,
                value => _profile.ConfigureTags = value,
                value => _profile.Tags = value));
            content.Add(CreateNameListCard(
                "layers",
                "Layers",
                "Add missing names to free user Layer slots 8 through 31. Enter one name per line.",
                _profile.ConfigureLayers,
                _profile.Layers,
                value => _profile.ConfigureLayers = value,
                value => _profile.Layers = value));
            content.Add(CreateNameListCard(
                "sorting-layers",
                "Sorting Layers",
                "Append missing Sorting Layers while preserving existing names, order, and identifiers. Enter one name per line.",
                _profile.ConfigureSortingLayers,
                _profile.SortingLayers,
                value => _profile.ConfigureSortingLayers = value,
                value => _profile.SortingLayers = value));

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

        private VisualElement CreateNamingDefaultsCard()
        {
            var card = CreateCard(
                NamingDefaultsCardName,
                "Duplicate Naming",
                "Keep duplicated GameObject and Asset names consistent across the project.");
            var enabled = new Toggle("Apply these settings") { value = _profile.ConfigureNamingDefaults };
            var scheme = new EnumField("GameObject suffix", _profile.GameObjectNamingScheme);
            var digits = new IntegerField("Minimum number digits") { value = _profile.GameObjectNamingDigits };
            var assetSpacing = new Toggle("Use a space before Asset copy numbers") { value = _profile.AssetNamingUsesSpace };
            enabled.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.ConfigureNamingDefaults = change.newValue));
            scheme.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.GameObjectNamingScheme = (EditorSettings.NamingScheme)change.newValue));
            digits.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.GameObjectNamingDigits = change.newValue));
            assetSpacing.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.AssetNamingUsesSpace = change.newValue));
            card.Add(enabled);
            card.Add(scheme);
            card.Add(digits);
            card.Add(assetSpacing);
            return card;
        }

        private VisualElement CreateProjectFoldersCard()
        {
            var card = CreateCard(
                ProjectFoldersCardName,
                "Project Folders",
                "Create missing folders under Assets. Restore removes only folders created by this tool that are still empty.");
            var enabled = new Toggle("Create missing folders") { value = _profile.ConfigureProjectFolders };
            var field = new TextField("Folder paths")
            {
                name = ProjectFoldersFieldName,
                multiline = true,
                value = string.Join("\n", _profile.ProjectFolders)
            };
            field.style.minHeight = 84f;
            enabled.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.ConfigureProjectFolders = change.newValue));
            field.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.ProjectFolders = ParseNameList(change.newValue)));
            card.Add(enabled);
            card.Add(field);
            return card;
        }

        private VisualElement CreateAssemblyDefinitionsCard()
        {
            var card = CreateCard(
                AssemblyDefinitionsCardName,
                "Script Assemblies",
                "Create Runtime and Editor Assembly Definitions, with optional EditMode and PlayMode test assemblies. Existing files are never overwritten.");
            var enabled = new Toggle("Create missing Runtime and Editor assemblies")
            {
                value = _profile.ConfigureAssemblyDefinitions
            };
            var assemblyName = new TextField("Assembly name")
            {
                name = AssemblyNameFieldName,
                value = _profile.AssemblyName
            };
            var runtimeFolder = new TextField("Runtime folder")
            {
                name = RuntimeAssemblyFolderFieldName,
                value = _profile.RuntimeAssemblyFolder
            };
            var editorFolder = new TextField("Editor folder")
            {
                name = EditorAssemblyFolderFieldName,
                value = _profile.EditorAssemblyFolder
            };
            var includeTests = new Toggle("Include EditMode and PlayMode test assemblies")
            {
                name = IncludeTestAssembliesToggleName,
                value = _profile.IncludeTestAssemblies
            };
            var testRootFolder = new TextField("Test root folder")
            {
                name = TestAssemblyRootFolderFieldName,
                value = _profile.TestAssemblyRootFolder
            };
            enabled.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.ConfigureAssemblyDefinitions = change.newValue));
            assemblyName.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.AssemblyName = change.newValue));
            runtimeFolder.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.RuntimeAssemblyFolder = change.newValue));
            editorFolder.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.EditorAssemblyFolder = change.newValue));
            includeTests.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.IncludeTestAssemblies = change.newValue));
            testRootFolder.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.TestAssemblyRootFolder = change.newValue));
            card.Add(enabled);
            card.Add(assemblyName);
            card.Add(runtimeFolder);
            card.Add(editorFolder);
            card.Add(includeTests);
            card.Add(testRootFolder);
            return card;
        }

        private VisualElement CreateVersionControlFilesCard()
        {
            var card = CreateCard(
                VersionControlFilesCardName,
                "Version Control Files",
                "Create Unity-ready .gitignore and .gitattributes files. Existing files are never overwritten, and restore removes only unchanged files created by this tool.");
            var enabled = new Toggle("Create missing .gitignore and .gitattributes")
            {
                name = VersionControlFilesToggleName,
                value = _profile.ConfigureVersionControlFiles
            };
            enabled.RegisterValueChangedCallback(change => ChangeProfile(
                () => _profile.ConfigureVersionControlFiles = change.newValue));
            card.Add(enabled);
            return card;
        }

        private VisualElement CreateBuildScenesCard()
        {
            var card = CreateCard(
                BuildScenesCardName,
                "Build Scenes",
                "Replace the active Build Profile scene list in this exact order. The first enabled Scene is the Player startup Scene.");
            var enabled = new Toggle("Apply this scene list") { value = _profile.ConfigureBuildScenes };
            enabled.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.ConfigureBuildScenes = change.newValue));
            card.Add(enabled);

            _buildSceneList = new VisualElement { name = BuildScenesListName };
            _buildSceneList.style.marginTop = 4f;
            card.Add(_buildSceneList);
            RefreshBuildSceneRows();

            var addButton = new Button(() =>
            {
                var scenes = CloneBuildScenes();
                Array.Resize(ref scenes, scenes.Length + 1);
                scenes[scenes.Length - 1] = new ProjectSetupBuildScene();
                _profile.BuildScenes = scenes;
                MarkProfileDirty();
                RefreshBuildSceneRows();
                RefreshPreview();
            })
            {
                name = AddBuildSceneButtonName,
                text = "Add scene"
            };
            addButton.style.alignSelf = Align.FlexStart;
            addButton.style.marginTop = 5f;
            card.Add(addButton);
            return card;
        }

        private void RefreshBuildSceneRows()
        {
            if (_buildSceneList == null)
            {
                return;
            }

            _buildSceneList.Clear();
            var scenes = _profile.BuildScenes;
            if (scenes.Length == 0)
            {
                var empty = new Label("No Scenes are configured. Add the startup Scene first.");
                empty.style.whiteSpace = WhiteSpace.Normal;
                empty.style.marginBottom = 3f;
                _buildSceneList.Add(empty);
                return;
            }

            for (var index = 0; index < scenes.Length; index++)
            {
                AddBuildSceneRow(index, scenes[index]);
            }
        }

        private void AddBuildSceneRow(int index, ProjectSetupBuildScene entry)
        {
            var row = new VisualElement { name = $"build-scene-row-{index}" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4f;

            var sceneField = new ObjectField($"Scene {index + 1}")
            {
                objectType = typeof(SceneAsset),
                value = entry?.SceneAsset
            };
            sceneField.style.flexGrow = 1f;
            sceneField.style.minWidth = 260f;
            sceneField.RegisterValueChangedCallback(change =>
            {
                UpdateBuildScene(index, scene => scene.SceneAsset = change.newValue as SceneAsset, false);
            });
            row.Add(sceneField);

            var sceneEnabled = new Toggle("Enabled") { value = entry?.Enabled ?? true };
            sceneEnabled.style.marginLeft = 5f;
            sceneEnabled.RegisterValueChangedCallback(change =>
            {
                UpdateBuildScene(index, scene => scene.Enabled = change.newValue, false);
            });
            row.Add(sceneEnabled);

            var upButton = new Button(() => MoveBuildScene(index, index - 1)) { text = "Up" };
            upButton.style.marginLeft = 4f;
            upButton.SetEnabled(index > 0);
            row.Add(upButton);

            var downButton = new Button(() => MoveBuildScene(index, index + 1)) { text = "Down" };
            downButton.style.marginLeft = 2f;
            downButton.SetEnabled(index + 1 < _profile.BuildScenes.Length);
            row.Add(downButton);

            var removeButton = new Button(() => RemoveBuildScene(index)) { text = "Remove" };
            removeButton.style.marginLeft = 2f;
            row.Add(removeButton);
            _buildSceneList.Add(row);
        }

        private void UpdateBuildScene(int index, Action<ProjectSetupBuildScene> update, bool rebuildRows)
        {
            var scenes = CloneBuildScenes();
            if (index < 0 || index >= scenes.Length)
            {
                return;
            }

            update(scenes[index]);
            _profile.BuildScenes = scenes;
            MarkProfileDirty();
            if (rebuildRows)
            {
                RefreshBuildSceneRows();
            }

            RefreshPreview();
        }

        private void MoveBuildScene(int sourceIndex, int destinationIndex)
        {
            var scenes = CloneBuildScenes();
            if (sourceIndex < 0 || destinationIndex < 0 || sourceIndex >= scenes.Length || destinationIndex >= scenes.Length)
            {
                return;
            }

            (scenes[sourceIndex], scenes[destinationIndex]) = (scenes[destinationIndex], scenes[sourceIndex]);
            _profile.BuildScenes = scenes;
            MarkProfileDirty();
            RefreshBuildSceneRows();
            RefreshPreview();
        }

        private void RemoveBuildScene(int index)
        {
            var scenes = CloneBuildScenes().Where((_, itemIndex) => itemIndex != index).ToArray();
            _profile.BuildScenes = scenes;
            MarkProfileDirty();
            RefreshBuildSceneRows();
            RefreshPreview();
        }

        private ProjectSetupBuildScene[] CloneBuildScenes()
        {
            return _profile.BuildScenes.Select(scene => scene?.Clone() ?? new ProjectSetupBuildScene()).ToArray();
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

        private VisualElement CreatePlayModeStartSceneCard()
        {
            var card = CreateCard(
                PlayModeStartSceneCardName,
                "Play Mode Start Scene",
                "Start Play Mode from one bootstrap Scene even while another Scene is open. Leave the Scene empty to use the currently open Scenes.");
            var enabled = new Toggle("Apply this setting") { value = _profile.ConfigurePlayModeStartScene };
            var sceneField = new ObjectField("Start Scene (optional)")
            {
                name = PlayModeStartSceneFieldName,
                objectType = typeof(SceneAsset),
                value = _profile.PlayModeStartScene.SceneAsset
            };
            enabled.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.ConfigurePlayModeStartScene = change.newValue));
            sceneField.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.PlayModeStartScene.SceneAsset = change.newValue as SceneAsset));
            card.Add(enabled);
            card.Add(sceneField);
            return card;
        }

        private VisualElement CreateScriptingDefineCard()
        {
            var card = CreateCard(
                ScriptingDefineCardName,
                "Scripting Define Symbols",
                "Add required compile symbols for the active build target without removing existing symbols. Enter one symbol per line.");
            var enabled = new Toggle("Add missing symbols") { value = _profile.ConfigureScriptingDefineSymbols };
            var field = new TextField("Required symbols")
            {
                name = ScriptingDefineFieldName,
                multiline = true,
                value = string.Join("\n", _profile.ScriptingDefineSymbols)
            };
            field.style.minHeight = 58f;
            enabled.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.ConfigureScriptingDefineSymbols = change.newValue));
            field.RegisterValueChangedCallback(change => ChangeProfile(() => _profile.ScriptingDefineSymbols = ParseNameList(change.newValue)));
            card.Add(enabled);
            card.Add(field);
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

        private VisualElement CreateNameListCard(
            string name,
            string title,
            string description,
            bool configured,
            string[] values,
            Action<bool> setConfigured,
            Action<string[]> setValues)
        {
            var card = CreateCard(name, title, description);
            var enabled = new Toggle("Add missing names") { value = configured };
            var field = new TextField("Desired names")
            {
                multiline = true,
                value = string.Join("\n", values ?? Array.Empty<string>())
            };
            field.style.minHeight = 58f;
            enabled.RegisterValueChangedCallback(change => ChangeProfile(() => setConfigured(change.newValue)));
            field.RegisterValueChangedCallback(change => ChangeProfile(() => setValues(ParseNameList(change.newValue))));
            card.Add(enabled);
            card.Add(field);
            return card;
        }

        private static string[] ParseNameList(string value)
        {
            return (value ?? string.Empty)
                .Replace("\r", string.Empty)
                .Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => item.Length > 0)
                .ToArray();
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
            var bar = new VisualElement { name = ActionBarName };
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.justifyContent = Justify.FlexEnd;
            bar.style.alignItems = Align.Center;
            bar.style.flexShrink = 0f;
            bar.style.minHeight = 30f;
            bar.style.paddingTop = 8f;

            var previewButton = new Button(RefreshPreview) { name = PreviewButtonName, text = "Preview changes" };
            previewButton.style.minHeight = 22f;
            bar.Add(previewButton);
            _restoreButton = new Button(RestoreLast) { name = RestoreButtonName, text = "Restore last backup" };
            _restoreButton.style.marginLeft = 6f;
            _restoreButton.style.minHeight = 22f;
            bar.Add(_restoreButton);
            _applyButton = new Button(ApplyProfile) { name = ApplyButtonName, text = "Apply profile" };
            _applyButton.style.marginLeft = 6f;
            _applyButton.style.minHeight = 22f;
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
