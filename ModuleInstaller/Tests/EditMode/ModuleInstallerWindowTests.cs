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
                var packages = window.rootVisualElement.Q<Foldout>(ModuleInstallerWindow.PackageListElementName);

                Assert.That(bundles, Is.Not.Null);
                Assert.That(bundles.childCount, Is.EqualTo(ModuleCatalog.Bundles.Count));
                Assert.That(packages, Is.Not.Null);
                Assert.That(packages.childCount, Is.EqualTo(ModuleCatalog.Entries.Count));
                Assert.That(window.rootVisualElement.Q<Button>("install-bundle-game-rules"), Is.Not.Null);
                Assert.That(window.rootVisualElement.Q<Button>("install-package-com.studiogaku.build-guard"), Is.Not.Null);

                var projectSetupRow = window.rootVisualElement.Q<VisualElement>("package-com.studiogaku.project-setup");
                var projectSetupLabel = projectSetupRow.Q<Label>();
                var projectSetupButton = projectSetupRow.Q<Button>();
                Assert.That(projectSetupLabel.style.minWidth.value.value, Is.EqualTo(0f));
                Assert.That(projectSetupLabel.style.flexShrink.value, Is.EqualTo(1f));
                Assert.That(projectSetupButton.style.flexShrink.value, Is.EqualTo(0f));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }
    }
}
