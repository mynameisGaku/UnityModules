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

                var projectSetupRow = window.rootVisualElement.Q<VisualElement>("package-com.studiogaku.project-setup");
                var projectSetupLabel = projectSetupRow.Q<Label>();
                var projectSetupReadmeButton = projectSetupRow.Q<Button>("readme-package-com.studiogaku.project-setup");
                var projectSetupInstallButton = projectSetupRow.Q<Button>("install-package-com.studiogaku.project-setup");
                Assert.That(projectSetupLabel.style.minWidth.value.value, Is.EqualTo(0f));
                Assert.That(projectSetupLabel.style.flexShrink.value, Is.EqualTo(1f));
                Assert.That(projectSetupReadmeButton.style.flexShrink.value, Is.EqualTo(0f));
                Assert.That(projectSetupInstallButton.style.flexShrink.value, Is.EqualTo(0f));
                Assert.That(projectSetupReadmeButton.tooltip, Does.Contain("project-setup-v1.14.0/ProjectSetup/README.md"));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }
    }
}
