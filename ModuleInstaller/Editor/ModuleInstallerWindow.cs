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
        internal const string WindowTitle = "モジュール管理";
        internal const string MenuPath = "Tools/モジュール管理/開く";
        internal const string TitleElementName = "module-installer-title";
        internal const string DescriptionElementName = "module-installer-description";
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

        [MenuItem(MenuPath, priority = 1200)]
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

            var title = new Label("用途を選び、必要なモジュールだけを導入します")
            {
                name = TitleElementName
            };
            title.style.fontSize = 22f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(title);

            var description = new Label(
                "まず四つの用途別セットから選びます。各項目で、向いている状況、導入後の最初の操作、変更される範囲を確認できます。専門的な計算部品は下部にまとめています。")
            {
                name = DescriptionElementName
            };
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
                text = "更新を確認",
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
                text = "専門機能：決定論的シミュレーションとゲーム規則の計算",
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
                text = "個別導入：説明を読む、または一つだけ導入する",
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
                text = "一括導入",
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
                text = "簡単な案内",
                value = false,
                name = $"guide-bundle-{bundle.Id}"
            };
            guide.style.marginTop = 5f;
            guide.Add(CreateGuideLine("向いている状況", bundle.UseWhen));
            guide.Add(CreateGuideLine("最初の操作", bundle.FirstStep));
            guide.Add(CreateGuideLine("変更される範囲", bundle.ChangeScope));
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
                text = "説明を読む",
                name = $"readme-package-{entry.PackageName}",
                tooltip = entry.GuideUrl
            };
            readmeButton.style.width = 88f;
            readmeButton.style.flexShrink = 0f;
            readmeButton.style.marginLeft = 5f;
            row.Add(readmeButton);

            var button = new Button(() => InstallPackage(entry))
            {
                text = "導入",
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
            Application.OpenURL(entry.GuideUrl);
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
                ? "準備できました。未導入のモジュールを追加し、固定版より古い導入済みモジュールを更新できます。"
                : message;

            var updatePlan = ModuleInstallDriver.BuildUpdatePlan();
            _updateSummary.text = BuildUpdateSummary(updatePlan);
            _updateButton.tooltip = updatePlan.Issues.Count > 0 ? updatePlan.Issues[0].Message : string.Empty;
            if (ModuleInstallDriver.IsBusy)
            {
                _updateButton.text = "処理中…";
                _updateButton.SetEnabled(false);
            }
            else if (updatePlan.Issues.Count > 0)
            {
                _updateButton.text = "問題を解消";
                _updateButton.SetEnabled(false);
            }
            else if (updatePlan.Entries.Count == 0)
            {
                _updateButton.text = "更新不要";
                _updateButton.SetEnabled(false);
            }
            else
            {
                _updateButton.text = $"{updatePlan.Entries.Count}件を更新";
                _updateButton.SetEnabled(true);
            }

            for (var index = 0; index < _installButtons.Count; index++)
            {
                var binding = _installButtons[index];
                var plan = ModuleInstallDriver.BuildPlan(binding.PackageNames);
                binding.Button.tooltip = plan.Issues.Count > 0 ? plan.Issues[0].Message : string.Empty;
                if (ModuleInstallDriver.IsBusy)
                {
                    binding.Button.text = "導入中…";
                    binding.Button.SetEnabled(false);
                }
                else if (plan.Issues.Count > 0)
                {
                    binding.Button.text = "競合を解消";
                    binding.Button.SetEnabled(false);
                }
                else if (plan.Entries.Count == 0)
                {
                    binding.Button.text = "導入済み";
                    binding.Button.SetEnabled(false);
                }
                else
                {
                    binding.Button.text = binding.IsBundle ? $"{plan.Entries.Count}件を導入" : "導入";
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
                return "導入済みの一覧掲載モジュールは固定版と一致しています。";
            }

            var names = new string[plan.Entries.Count];
            for (var index = 0; index < plan.Entries.Count; index++)
            {
                names[index] = $"{plan.Entries[index].DisplayName} → {plan.Entries[index].Version}";
            }

            return $"更新可能：{string.Join("、", names)}";
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
