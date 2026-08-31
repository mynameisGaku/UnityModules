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
                Assert.That(ModuleInstallerWindow.WindowTitle, Is.EqualTo("モジュール管理"));
                Assert.That(ModuleInstallerWindow.MenuPath, Is.EqualTo("Tools/モジュール管理/開く"));
                Assert.That(window.rootVisualElement.Q<Label>(ModuleInstallerWindow.TitleElementName).text, Does.Contain("必要なモジュールだけを導入"));
                Assert.That(window.rootVisualElement.Q<Label>(ModuleInstallerWindow.DescriptionElementName).text, Does.Contain("四つの用途別セット"));
                Assert.That(specialized.text, Does.Contain("専門機能").And.Contain("ゲーム規則"));
                Assert.That(packages.text, Does.Contain("個別導入").And.Contain("説明を読む"));

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
                Assert.That(projectMaintenancePackages.text, Does.Contain("テクスチャー取込設定監査").And.Contain("ビルド実行アシスタント"));
                Assert.That(projectMaintenanceCard.childCount, Is.EqualTo(5));
                Assert.That(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenanceHeading), Is.EqualTo(0));
                Assert.That(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenanceSummary), Is.EqualTo(1));
                Assert.That(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenanceGuide), Is.EqualTo(2));
                Assert.That(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenancePackages), Is.EqualTo(3));
                Assert.That(projectMaintenanceCard.hierarchy.IndexOf(projectMaintenanceInstall), Is.EqualTo(4));

                var sceneAndUiCard = window.rootVisualElement.Q<VisualElement>("bundle-scene-and-ui");
                Assert.That(sceneAndUiCard, Is.Not.Null);
                var sceneAndUiHeading = sceneAndUiCard.Q<Label>("heading-bundle-scene-and-ui");
                var sceneAndUiSummary = sceneAndUiCard.Q<Label>("summary-bundle-scene-and-ui");
                var sceneAndUiGuide = sceneAndUiCard.Q<Foldout>("guide-bundle-scene-and-ui");
                var sceneAndUiPackages = sceneAndUiCard.Q<Label>("packages-bundle-scene-and-ui");
                var sceneAndUiInstall = sceneAndUiCard.Q<Button>("install-bundle-scene-and-ui");
                Assert.That(sceneAndUiHeading, Is.Not.Null);
                Assert.That(sceneAndUiSummary, Is.Not.Null);
                Assert.That(sceneAndUiGuide, Is.Not.Null);
                Assert.That(sceneAndUiPackages, Is.Not.Null);
                Assert.That(sceneAndUiInstall, Is.Not.Null);
                Assert.That(sceneAndUiPackages.text, Does.Contain("シーン作業セット").And.Contain("実行中調整").And.Contain("シーン切り替え"));
                Assert.That(sceneAndUiCard.childCount, Is.EqualTo(5));
                Assert.That(sceneAndUiCard.hierarchy.IndexOf(sceneAndUiHeading), Is.EqualTo(0));
                Assert.That(sceneAndUiCard.hierarchy.IndexOf(sceneAndUiSummary), Is.EqualTo(1));
                Assert.That(sceneAndUiCard.hierarchy.IndexOf(sceneAndUiGuide), Is.EqualTo(2));
                Assert.That(sceneAndUiCard.hierarchy.IndexOf(sceneAndUiPackages), Is.EqualTo(3));
                Assert.That(sceneAndUiCard.hierarchy.IndexOf(sceneAndUiInstall), Is.EqualTo(4));

                var projectSetupRow = window.rootVisualElement.Q<VisualElement>("package-com.studiogaku.project-setup");
                var projectSetupLabel = projectSetupRow.Q<Label>();
                var projectSetupReadmeButton = projectSetupRow.Q<Button>("readme-package-com.studiogaku.project-setup");
                var projectSetupInstallButton = projectSetupRow.Q<Button>("install-package-com.studiogaku.project-setup");
                Assert.That(projectSetupLabel.style.minWidth.value.value, Is.EqualTo(0f));
                Assert.That(projectSetupLabel.style.flexShrink.value, Is.EqualTo(1f));
                Assert.That(projectSetupReadmeButton.style.flexShrink.value, Is.EqualTo(0f));
                Assert.That(projectSetupInstallButton.style.flexShrink.value, Is.EqualTo(0f));
                Assert.That(projectSetupReadmeButton.tooltip, Does.Contain("project-setup-v1.15.0/ProjectSetup/Documentation~/index.md"));

                var assetImportRow = window.rootVisualElement.Q<VisualElement>("package-com.studiogaku.asset-import-audit");
                var assetImportLabel = assetImportRow.Q<Label>();
                var assetImportReadmeButton = assetImportRow.Q<Button>("readme-package-com.studiogaku.asset-import-audit");
                var assetImportInstallButton = assetImportRow.Q<Button>("install-package-com.studiogaku.asset-import-audit");
                Assert.That(assetImportLabel.text, Does.Contain("テクスチャー取込設定監査").And.Contain("パソコン").And.Contain("Android").And.Contain("iOS"));
                Assert.That(assetImportLabel.text, Does.Not.Contain("Standalone"));
                Assert.That(assetImportReadmeButton.tooltip, Does.Contain("asset-import-audit-v1.2.0/AssetImportAudit/README.md"));
                Assert.That(assetImportInstallButton, Is.Not.Null);

                var buildAssistantRow = window.rootVisualElement.Q<VisualElement>("package-com.studiogaku.build-assistant");
                Assert.That(buildAssistantRow, Is.Not.Null);
                var buildAssistantLabel = buildAssistantRow.Q<Label>();
                var buildAssistantReadmeButton = buildAssistantRow.Q<Button>("readme-package-com.studiogaku.build-assistant");
                var buildAssistantInstallButton = buildAssistantRow.Q<Button>("install-package-com.studiogaku.build-assistant");
                Assert.That(buildAssistantLabel.text, Does.Contain("ビルド実行アシスタント").And.Contain("デスクトップ向けビルド計画").And.Contain("件数を制限した履歴"));
                Assert.That(buildAssistantReadmeButton.tooltip, Does.Contain("build-assistant-v1.1.0/BuildAssistant/README.md"));
                Assert.That(buildAssistantInstallButton, Is.Not.Null);
                Assert.That(packages.contentContainer.hierarchy.IndexOf(buildAssistantRow), Is.EqualTo(packages.contentContainer.hierarchy.IndexOf(assetImportRow) + 1));

                var sceneWorkspaceRow = window.rootVisualElement.Q<VisualElement>("package-com.studiogaku.scene-workspace");
                Assert.That(sceneWorkspaceRow, Is.Not.Null);
                var sceneWorkspaceLabel = sceneWorkspaceRow.Q<Label>();
                var sceneWorkspaceReadmeButton = sceneWorkspaceRow.Q<Button>("readme-package-com.studiogaku.scene-workspace");
                var sceneWorkspaceInstallButton = sceneWorkspaceRow.Q<Button>("install-package-com.studiogaku.scene-workspace");
                Assert.That(sceneWorkspaceLabel.text, Does.Contain("シーン作業セット").And.Contain("複数シーンの順番").And.Contain("計画の有効性確認"));
                Assert.That(sceneWorkspaceReadmeButton.tooltip, Does.Contain("scene-workspace-v1.0.0/SceneWorkspace/README.md"));
                Assert.That(sceneWorkspaceInstallButton, Is.Not.Null);
                Assert.That(sceneWorkspaceRow.childCount, Is.EqualTo(3));
                Assert.That(sceneWorkspaceRow.hierarchy.IndexOf(sceneWorkspaceLabel), Is.EqualTo(0));
                Assert.That(sceneWorkspaceRow.hierarchy.IndexOf(sceneWorkspaceReadmeButton), Is.EqualTo(1));
                Assert.That(sceneWorkspaceRow.hierarchy.IndexOf(sceneWorkspaceInstallButton), Is.EqualTo(2));

                var playModeTuningRow = window.rootVisualElement.Q<VisualElement>("package-com.studiogaku.play-mode-tuning");
                Assert.That(playModeTuningRow, Is.Not.Null);
                var playModeTuningLabel = playModeTuningRow.Q<Label>();
                var playModeTuningReadmeButton = playModeTuningRow.Q<Button>("readme-package-com.studiogaku.play-mode-tuning");
                var playModeTuningInstallButton = playModeTuningRow.Q<Button>("install-package-com.studiogaku.play-mode-tuning");
                Assert.That(playModeTuningLabel.text, Does.Contain("実行中調整").And.Contain("プロパティー変更").And.Contain("計画の有効性確認"));
                Assert.That(playModeTuningReadmeButton.tooltip, Does.Contain("play-mode-tuning-v1.0.0/PlayModeTuning/README.md"));
                Assert.That(playModeTuningInstallButton, Is.Not.Null);
                Assert.That(playModeTuningRow.childCount, Is.EqualTo(3));
                Assert.That(playModeTuningRow.hierarchy.IndexOf(playModeTuningLabel), Is.EqualTo(0));
                Assert.That(playModeTuningRow.hierarchy.IndexOf(playModeTuningReadmeButton), Is.EqualTo(1));
                Assert.That(playModeTuningRow.hierarchy.IndexOf(playModeTuningInstallButton), Is.EqualTo(2));

                var sceneFlowRow = window.rootVisualElement.Q<VisualElement>("package-com.studiogaku.scene-flow");
                Assert.That(sceneFlowRow, Is.Not.Null);
                Assert.That(packages.contentContainer.hierarchy.IndexOf(sceneWorkspaceRow), Is.EqualTo(packages.contentContainer.hierarchy.IndexOf(buildAssistantRow) + 1));
                Assert.That(packages.contentContainer.hierarchy.IndexOf(playModeTuningRow), Is.EqualTo(packages.contentContainer.hierarchy.IndexOf(sceneWorkspaceRow) + 1));
                Assert.That(packages.contentContainer.hierarchy.IndexOf(sceneFlowRow), Is.EqualTo(packages.contentContainer.hierarchy.IndexOf(playModeTuningRow) + 1));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }
    }
}
