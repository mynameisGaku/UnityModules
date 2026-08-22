// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModuleInstaller.Editor
{
    internal sealed class ModuleInstallerWindow : EditorWindow
    {
        internal const string WindowTitle = "Module Manager";
        internal const string StatusElementName = "module-installer-status";
        internal const string UpdateSummaryElementName = "module-installer-update-summary";
        internal const string UpdateButtonElementName = "module-installer-update-all";
        internal const string BundleListElementName = "module-installer-bundles";
        internal const string SpecializedBundleListElementName = "module-installer-specialized-bundles";
        internal const string PackageListElementName = "module-installer-packages";

        private HelpBox _status;
        private Label _updateSummary;
        private Button _updateButton;
        private readonly List<InstallButtonBinding> _installButtons = new List<InstallButtonBinding>();

        [MenuItem("Tools/Module Manager/Open", priority = 1200)]
        [MenuItem("Tools/Module Installer/Open", priority = 1200)]
        internal static void Open()
        {
            var window = GetWindow<ModuleInstallerWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(620f, 480f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 14f;
            rootVisualElement.style.paddingRight = 14f;
            rootVisualElement.style.paddingTop = 12f;
            rootVisualElement.style.paddingBottom = 12f;

            var title = new Label("Choose a workflow, then install only what it needs");
            title.style.fontSize = 22f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(title);

            var description = new Label(
                "Start with four practical workflows. Each card explains when to use it, the first action after installation, and what can change. Specialized libraries remain available below.");
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginTop = 4f;
            description.style.marginBottom = 8f;
            rootVisualElement.Add(description);

            _status = new HelpBox(string.Empty, HelpBoxMessageType.Info)
            {
                name = StatusElementName
            };
            rootVisualElement.Add(_status);

            var updateRow = new VisualElement();
            updateRow.style.flexDirection = FlexDirection.Row;
            updateRow.style.alignItems = Align.Center;
            updateRow.style.marginTop = 8f;
            rootVisualElement.Add(updateRow);

            _updateSummary = new Label { name = UpdateSummaryElementName };
            _updateSummary.style.flexGrow = 1f;
            _updateSummary.style.flexShrink = 1f;
            _updateSummary.style.minWidth = 0f;
            _updateSummary.style.whiteSpace = WhiteSpace.Normal;
            updateRow.Add(_updateSummary);

            _updateButton = new Button(UpdateInstalled)
            {
                text = "Check updates",
                name = UpdateButtonElementName
            };
            _updateButton.style.width = 150f;
            _updateButton.style.flexShrink = 0f;
            updateRow.Add(_updateButton);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1f;
            scrollView.style.marginTop = 8f;
            rootVisualElement.Add(scrollView);

            var bundles = new VisualElement { name = BundleListElementName };
            scrollView.Add(bundles);
            var specialized = new Foldout
            {
                text = "Specialized collections: deterministic simulation and game-rule math",
                value = false,
                name = SpecializedBundleListElementName
            };
            specialized.style.marginTop = 8f;
            scrollView.Add(specialized);
            for (var index = 0; index < ModuleCatalog.Bundles.Count; index++)
            {
                var bundle = ModuleCatalog.Bundles[index];
                if (bundle.Tier == ModuleBundleTier.Recommended)
                {
                    bundles.Add(CreateBundleCard(bundle));
                }
                else
                {
                    specialized.Add(CreateBundleCard(bundle));
                }
            }

            var advanced = new Foldout
            {
                text = "Advanced: read about or install one module",
                value = false,
                name = PackageListElementName
            };
            advanced.style.marginTop = 10f;
            scrollView.Add(advanced);
            for (var index = 0; index < ModuleCatalog.Entries.Count; index++)
            {
                advanced.Add(CreatePackageRow(ModuleCatalog.Entries[index]));
            }

            ModuleInstallDriver.Changed -= RefreshStatus;
            ModuleInstallDriver.Changed += RefreshStatus;
            RefreshStatus();
        }

        private void OnDisable()
        {
            ModuleInstallDriver.Changed -= RefreshStatus;
        }

        private VisualElement CreateBundleCard(ModuleBundle bundle)
        {
            var card = new VisualElement { name = $"bundle-{bundle.Id}" };
            card.style.borderTopWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderTopColor = new Color(0.28f, 0.28f, 0.28f);
            card.style.borderBottomColor = new Color(0.28f, 0.28f, 0.28f);
            card.style.borderLeftColor = new Color(0.28f, 0.28f, 0.28f);
            card.style.borderRightColor = new Color(0.28f, 0.28f, 0.28f);
            card.style.borderTopLeftRadius = 5f;
            card.style.borderTopRightRadius = 5f;
            card.style.borderBottomLeftRadius = 5f;
            card.style.borderBottomRightRadius = 5f;
            card.style.paddingLeft = 10f;
            card.style.paddingRight = 10f;
            card.style.paddingTop = 8f;
            card.style.paddingBottom = 8f;
            card.style.marginBottom = 7f;

            var heading = new Label(bundle.DisplayName)
            {
                name = $"heading-bundle-{bundle.Id}"
            };
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.fontSize = 14f;
            heading.style.flexGrow = 1f;
            card.Add(heading);

            var button = new Button(() => InstallBundle(bundle))
            {
                text = "Install bundle",
                name = $"install-bundle-{bundle.Id}"
            };
            button.style.width = 118f;
            button.style.alignSelf = Align.FlexEnd;
            button.style.marginTop = 7f;

            var summary = new Label(bundle.Summary)
            {
                name = $"summary-bundle-{bundle.Id}"
            };
            summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.marginTop = 4f;
            card.Add(summary);

            var guide = new Foldout
            {
                text = "Quick guide",
                value = false,
                name = $"guide-bundle-{bundle.Id}"
            };
            guide.style.marginTop = 5f;
            guide.Add(CreateGuideLine("Use when", bundle.UseWhen));
            guide.Add(CreateGuideLine("Start here", bundle.FirstStep));
            guide.Add(CreateGuideLine("Change scope", bundle.ChangeScope));
            card.Add(guide);

            var packageSummary = new Label(BuildPackageSummary(bundle.PackageNames))
            {
                name = $"packages-bundle-{bundle.Id}"
            };
            packageSummary.style.whiteSpace = WhiteSpace.Normal;
            packageSummary.style.fontSize = 10f;
            packageSummary.style.opacity = 0.75f;
            packageSummary.style.marginTop = 4f;
            card.Add(packageSummary);
            card.Add(button);
            _installButtons.Add(new InstallButtonBinding(button, bundle.PackageNames, true));
            return card;
        }

        private VisualElement CreatePackageRow(ModuleCatalogEntry entry)
        {
            var row = new VisualElement { name = $"package-{entry.PackageName}" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3f;

            var text = new Label($"{entry.DisplayName} - {entry.Summary}");
            text.style.flexGrow = 1f;
            text.style.flexShrink = 1f;
            text.style.minWidth = 0f;
            text.style.whiteSpace = WhiteSpace.Normal;
            row.Add(text);

            var readmeButton = new Button(() => OpenReadme(entry))
            {
                text = "Read guide",
                name = $"readme-package-{entry.PackageName}",
                tooltip = entry.ReadmeUrl
            };
            readmeButton.style.width = 88f;
            readmeButton.style.flexShrink = 0f;
            readmeButton.style.marginLeft = 5f;
            row.Add(readmeButton);

            var button = new Button(() => InstallPackage(entry))
            {
                text = "Install",
                name = $"install-package-{entry.PackageName}"
            };
            button.style.width = 82f;
            button.style.flexShrink = 0f;
            button.style.marginLeft = 4f;
            row.Add(button);
            _installButtons.Add(new InstallButtonBinding(button, new[] { entry.PackageName }, false));
            return row;
        }

        private static VisualElement CreateGuideLine(string heading, string description)
        {
            var line = new VisualElement();
            line.style.marginBottom = 4f;

            var title = new Label(heading);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            line.Add(title);

            var text = new Label(description);
            text.style.whiteSpace = WhiteSpace.Normal;
            text.style.opacity = 0.85f;
            line.Add(text);
            return line;
        }

        private static string BuildPackageSummary(IReadOnlyList<string> packageNames)
        {
            var names = new string[packageNames.Count];
            for (var index = 0; index < packageNames.Count; index++)
            {
                names[index] = ModuleCatalog.TryFindEntry(packageNames[index], out var entry)
                    ? entry.DisplayName
                    : packageNames[index];
            }

            return string.Join(" / ", names);
        }

        private void InstallBundle(ModuleBundle bundle)
        {
            ModuleInstallDriver.TryInstallBundle(bundle.Id, out var message);
            ShowMessage(message);
        }

        private void InstallPackage(ModuleCatalogEntry entry)
        {
            ModuleInstallDriver.TryInstallPackage(entry.PackageName, out var message);
            ShowMessage(message);
        }

        private static void OpenReadme(ModuleCatalogEntry entry)
        {
            Application.OpenURL(entry.ReadmeUrl);
        }

        private void UpdateInstalled()
        {
            ModuleInstallDriver.TryUpdateInstalled(out var message);
            ShowMessage(message);
        }

        private void ShowMessage(string message)
        {
            if (_status != null)
            {
                _status.text = message;
            }

            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (_status == null)
            {
                return;
            }

            var message = ModuleInstallDriver.LastMessage;
            _status.text = string.IsNullOrEmpty(message)
                ? "Ready. Missing modules can be installed, and outdated installed modules can be updated to pinned releases."
                : message;

            var updatePlan = ModuleInstallDriver.BuildUpdatePlan();
            _updateSummary.text = BuildUpdateSummary(updatePlan);
            _updateButton.tooltip = updatePlan.Issues.Count > 0 ? updatePlan.Issues[0].Message : string.Empty;
            if (ModuleInstallDriver.IsBusy)
            {
                _updateButton.text = "Working...";
                _updateButton.SetEnabled(false);
            }
            else if (updatePlan.Issues.Count > 0)
            {
                _updateButton.text = "Resolve issue";
                _updateButton.SetEnabled(false);
            }
            else if (updatePlan.Entries.Count == 0)
            {
                _updateButton.text = "Up to date";
                _updateButton.SetEnabled(false);
            }
            else
            {
                _updateButton.text = $"Update {updatePlan.Entries.Count}";
                _updateButton.SetEnabled(true);
            }

            for (var index = 0; index < _installButtons.Count; index++)
            {
                var binding = _installButtons[index];
                var plan = ModuleInstallDriver.BuildPlan(binding.PackageNames);
                binding.Button.tooltip = plan.Issues.Count > 0 ? plan.Issues[0].Message : string.Empty;
                if (ModuleInstallDriver.IsBusy)
                {
                    binding.Button.text = "Installing...";
                    binding.Button.SetEnabled(false);
                }
                else if (plan.Issues.Count > 0)
                {
                    binding.Button.text = "Resolve conflict";
                    binding.Button.SetEnabled(false);
                }
                else if (plan.Entries.Count == 0)
                {
                    binding.Button.text = "Installed";
                    binding.Button.SetEnabled(false);
                }
                else
                {
                    binding.Button.text = binding.IsBundle ? $"Install {plan.Entries.Count}" : "Install";
                    binding.Button.SetEnabled(true);
                }
            }
        }

        private static string BuildUpdateSummary(ModuleInstallPlan plan)
        {
            if (plan.Issues.Count > 0)
            {
                return plan.Issues[0].Message;
            }

            if (plan.Entries.Count == 0)
            {
                return "Installed catalog modules are up to date.";
            }

            var names = new string[plan.Entries.Count];
            for (var index = 0; index < plan.Entries.Count; index++)
            {
                names[index] = $"{plan.Entries[index].DisplayName} -> {plan.Entries[index].Version}";
            }

            return $"Updates available: {string.Join(", ", names)}";
        }

        private sealed class InstallButtonBinding
        {
            internal InstallButtonBinding(Button button, IReadOnlyList<string> packageNames, bool isBundle)
            {
                Button = button;
                PackageNames = packageNames;
                IsBundle = isBundle;
            }

            internal Button Button { get; }
            internal IReadOnlyList<string> PackageNames { get; }
            internal bool IsBundle { get; }
        }
    }
}
