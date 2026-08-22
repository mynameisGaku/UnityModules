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
        internal const string WindowTitle = "Module Installer";
        internal const string StatusElementName = "module-installer-status";
        internal const string BundleListElementName = "module-installer-bundles";
        internal const string PackageListElementName = "module-installer-packages";

        private HelpBox _status;
        private readonly List<InstallButtonBinding> _installButtons = new List<InstallButtonBinding>();

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

            var title = new Label("Install modules by task");
            title.style.fontSize = 22f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(title);

            var description = new Label(
                "Choose a practical bundle instead of copying Git URLs one by one. Existing Assets/Modules copies are detected before installation.");
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginTop = 4f;
            description.style.marginBottom = 8f;
            rootVisualElement.Add(description);

            _status = new HelpBox(string.Empty, HelpBoxMessageType.Info)
            {
                name = StatusElementName
            };
            rootVisualElement.Add(_status);

            var scrollView = new ScrollView(ScrollViewMode.Vertical);
            scrollView.style.flexGrow = 1f;
            scrollView.style.marginTop = 8f;
            rootVisualElement.Add(scrollView);

            var bundles = new VisualElement { name = BundleListElementName };
            scrollView.Add(bundles);
            for (var index = 0; index < ModuleCatalog.Bundles.Count; index++)
            {
                bundles.Add(CreateBundleCard(ModuleCatalog.Bundles[index]));
            }

            var advanced = new Foldout
            {
                text = "Advanced: install one module",
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

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            card.Add(header);

            var heading = new Label(bundle.DisplayName);
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.style.fontSize = 14f;
            heading.style.flexGrow = 1f;
            header.Add(heading);

            var button = new Button(() => InstallBundle(bundle))
            {
                text = "Install bundle",
                name = $"install-bundle-{bundle.Id}"
            };
            button.style.width = 118f;
            header.Add(button);
            _installButtons.Add(new InstallButtonBinding(button, bundle.PackageNames, true));

            var summary = new Label(bundle.Summary);
            summary.style.whiteSpace = WhiteSpace.Normal;
            summary.style.marginTop = 4f;
            card.Add(summary);

            var packageSummary = new Label(BuildPackageSummary(bundle.PackageNames));
            packageSummary.style.whiteSpace = WhiteSpace.Normal;
            packageSummary.style.fontSize = 10f;
            packageSummary.style.opacity = 0.75f;
            packageSummary.style.marginTop = 4f;
            card.Add(packageSummary);
            return card;
        }

        private VisualElement CreatePackageRow(ModuleCatalogEntry entry)
        {
            var row = new VisualElement { name = $"package-{entry.PackageName}" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 3f;

            var text = new Label($"{entry.DisplayName}  —  {entry.Summary}");
            text.style.flexGrow = 1f;
            text.style.whiteSpace = WhiteSpace.Normal;
            row.Add(text);

            var button = new Button(() => InstallPackage(entry))
            {
                text = "Install",
                name = $"install-package-{entry.PackageName}"
            };
            button.style.width = 82f;
            row.Add(button);
            _installButtons.Add(new InstallButtonBinding(button, new[] { entry.PackageName }, false));
            return row;
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

            return string.Join(" · ", names);
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
                ? "Ready. Installed packages are skipped automatically."
                : message;
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
