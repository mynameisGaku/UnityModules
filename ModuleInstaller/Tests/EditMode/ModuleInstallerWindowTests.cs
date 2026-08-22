// SPDX-License-Identifier: MIT

using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModuleInstaller.Editor.Tests
{
    internal sealed class ModuleInstallerWindowTests
    {
        [Test]
        public void CreateGUI_ExposesBundlesAndAdvancedPackageRows()
        {
            var window = ScriptableObject.CreateInstance<ModuleInstallerWindow>();
            try
            {
                window.CreateGUI();
                var bundles = window.rootVisualElement.Q<VisualElement>(ModuleInstallerWindow.BundleListElementName);
                var specialized = window.rootVisualElement.Q<Foldout>(ModuleInstallerWindow.SpecializedBundleListElementName);
                var packages = window.rootVisualElement.Q<Foldout>(ModuleInstallerWindow.PackageListElementName);

                Assert.That(bundles, Is.Not.Null);
                Assert.That(bundles.childCount, Is.EqualTo(4));
                Assert.That(specialized, Is.Not.Null);
                Assert.That(specialized.value, Is.False);
                Assert.That(specialized.childCount, Is.EqualTo(2));
                Assert.That(packages, Is.Not.Null);
                Assert.That(packages.value, Is.False);
                Assert.That(packages.childCount, Is.EqualTo(ModuleCatalog.Entries.Count));
                Assert.That(window.rootVisualElement.Q<Button>("install-bundle-game-rules"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Button>("install-package-com.studiogaku.build-guard"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Button>("readme-package-com.studiogaku.build-guard"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Foldout>("guide-bundle-project-maintenance"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Label>(ModuleInstallerWindow.UpdateSummaryElementName), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Button>(ModuleInstallerWindow.UpdateButtonElementName), Is.Not.Null);

                var projectMaintenanceCard = window.rootVisualElement.Q<VisualElement>("bundle-project-maintenance");
                Assert.That(projectMaintenanceCard, Is.Not.Null);
                var projectMaintenanceHeading = projectMaintenanceCard.Q<Label>("heading-bundle-project-maintenance");
                var projectMaintenanceSummary = projectMaintenanceCard.Q<Label>("summary-bundle-project-maintenance");
                var projectMaintenanceGuide = projectMaintenanceCard.Q<Foldout>("guide-bundle-project-maintenance");
                var projectMaintenancePackages = projectMaintenanceCard.Q<Label>("packages-bundle-project-maintenance");
                var projectMaintenanceInstall = projectMaintenanceCard.Q<Button>("install-bundle-project-maintenance");
                Assert.That(projectMaintenanceHeading, Is.Not.Null);
                Assert.That(projectMaintenanceSummary, Is.Not.Null);
                Assert.That(projectMaintenanceGuide, Is.Not.Null);
                Assert.That(projectMaintenancePackages, Is.Not.Null);
                Assert.That(projectMaintenanceInstall, Is.Not.Null);
                Assert.That(projectMaintenancePackages.text, Does.Contain("Texture Import Settings"));
                Assert.That(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenanceHeading), Is.LessThan(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenanceSummary)));
                Assert.That(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenanceSummary), Is.LessThan(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenanceGuide)));
                Assert.That(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenanceGuide), Is.LessThan(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenancePackages)));
                Assert.That(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenancePackages), Is.LessThan(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenanceInstall)));

                var projectSetupRow = window.rootVisualElement.Q<VisualElement>("package-com.studiogaku.project-setup");
                var projectSetupLabel = projectSetupRow.Q<Label>();
                var projectSetupReadmeButton = projectSetupRow.Q<Button>("readme-package-com.studiogaku.project-setup");
                var projectSetupInstallButton = projectSetupRow.Q<Button>("install-package-com.studiogaku.project-setup");
                Assert.That(projectSetupLabel.style.minWidth.value.value, Is.EqualTo(0f));
                Assert.That(projectSetupLabel.style.flexShrink.value, Is.EqualTo(1f));
                Assert.That(projectSetupReadmeButton.style.flexShrink.value, Is.EqualTo(0f));
                Assert.That(projectSetupInstallButton.style.flexShrink.value, Is.EqualTo(0f));
                Assert.That(projectSetupReadmeButton.tooltip, Does.Contain("project-setup-v1.15.0/ProjectSetup/README.md"));

                var assetImportRow = window.rootVisualElement.Q<VisualElement>("package-com.studiogaku.asset-import-audit");
                var assetImportLabel = assetImportRow.Q<Label>();
                var assetImportReadmeButton = assetImportRow.Q<Button>("readme-package-com.studiogaku.asset-import-audit");
                var assetImportInstallButton = assetImportRow.Q<Button>("install-package-com.studiogaku.asset-import-audit");
                Assert.That(assetImportLabel.text, Does.Contain("Texture Import Settings").And.Contain("Standalone").And.Contain("Android").And.Contain("iOS"));
                Assert.That(assetImportReadmeButton.tooltip, Does.Contain("asset-import-audit-v1.1.0/AssetImportAudit/README.md"));
                Assert.That(assetImportInstallButton, Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }
    }
}
