// SPDX-License-Identifier: MIT

using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectSetup.Tests
{
    internal sealed class ProjectSetupWindowTests
    {
        [Test]
        public void CreateGUI_ContainsStableWorkflowElements()
        {
            var window = ScriptableObject.CreateInstance<ProjectSetupWindow>();
            try
            {
                window.CreateGUI();
                var root = window.rootVisualElement;

                Assert.That(root.name, Is.EqualTo(ProjectSetupWindow.RootElementName));
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.ProfileToolbarName), Is.Not.Null);
                Assert.That(root.Q<ObjectField>(ProjectSetupWindow.ProfileFieldName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.ProfileActionsName), Is.Not.Null);
                Assert.That(root.Q<Button>(ProjectSetupWindow.NewProfileButtonName), Is.Not.Null);
                Assert.That(root.Q<Button>(ProjectSetupWindow.CaptureProfileButtonName), Is.Not.Null);
                Assert.That(root.Q<Button>(ProjectSetupWindow.SaveProfileButtonName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.ChangeListName), Is.Not.Null);
                Assert.That(root.Q<Button>(ProjectSetupWindow.PreviewButtonName), Is.Not.Null);
                Assert.That(root.Q<Button>(ProjectSetupWindow.ApplyButtonName), Is.Not.Null);
                Assert.That(root.Q<Button>(ProjectSetupWindow.RestoreButtonName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.ActionBarName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>("asset-serialization"), Is.Not.Null);
                Assert.That(root.Q<VisualElement>("version-control"), Is.Not.Null);
                Assert.That(root.Q<VisualElement>("enter-play-mode"), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.PlayModeStartSceneCardName), Is.Not.Null);
                Assert.That(root.Q<ObjectField>(ProjectSetupWindow.PlayModeStartSceneFieldName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.ScriptingDefineCardName), Is.Not.Null);
                Assert.That(root.Q<TextField>(ProjectSetupWindow.ScriptingDefineFieldName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.RootNamespaceCardName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.NewScriptLineEndingsCardName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.NamingDefaultsCardName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.ProjectFoldersCardName), Is.Not.Null);
                Assert.That(root.Q<TextField>(ProjectSetupWindow.ProjectFoldersFieldName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.AssemblyDefinitionsCardName), Is.Not.Null);
                Assert.That(root.Q<TextField>(ProjectSetupWindow.AssemblyNameFieldName), Is.Not.Null);
                Assert.That(root.Q<TextField>(ProjectSetupWindow.RuntimeAssemblyFolderFieldName), Is.Not.Null);
                Assert.That(root.Q<TextField>(ProjectSetupWindow.EditorAssemblyFolderFieldName), Is.Not.Null);
                Assert.That(root.Q<Toggle>(ProjectSetupWindow.IncludeTestAssembliesToggleName), Is.Not.Null);
                Assert.That(root.Q<TextField>(ProjectSetupWindow.TestAssemblyRootFolderFieldName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>("color-space"), Is.Not.Null);
                Assert.That(root.Q<VisualElement>("run-in-background"), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.BuildScenesCardName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.BuildScenesListName), Is.Not.Null);
                Assert.That(root.Q<Button>(ProjectSetupWindow.AddBuildSceneButtonName), Is.Not.Null);
                Assert.That(root.Q<VisualElement>("tags"), Is.Not.Null);
                Assert.That(root.Q<VisualElement>("layers"), Is.Not.Null);
                Assert.That(root.Q<VisualElement>("sorting-layers"), Is.Not.Null);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.ProfileToolbarName).style.flexShrink.value, Is.Zero);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.ActionBarName).style.flexShrink.value, Is.Zero);
                Assert.That(root.Q<VisualElement>(ProjectSetupWindow.ActionBarName).style.minHeight.value.value, Is.EqualTo(30f));
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }

    }
}
