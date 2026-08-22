// SPDX-License-Identifier: MIT

using NUnit.Framework;
using ProjectSetup.Editor;
using UnityEditor;
using UnityEngine;

namespace ProjectSetup.Tests
{
    internal sealed class ProjectSetupCodeGenerationIntegrationTests
    {
        [Test]
        public void ApplyAndRestore_CodeGenerationSettingsRoundTripExactly()
        {
            var originalNamespace = EditorSettings.projectGenerationRootNamespace;
            var originalLineEndings = EditorSettings.lineEndingsForNewScripts;
            var desiredNamespace = originalNamespace == "ProjectSetup.Gate" ? "ProjectSetup.GateAlternate" : "ProjectSetup.Gate";
            var desiredLineEndings = originalLineEndings == LineEndingsMode.Unix
                ? LineEndingsMode.Windows
                : LineEndingsMode.Unix;
            var environment = new UnityProjectSetupEnvironment();
            var snapshot = environment.Capture();
            var profile = ScriptableObject.CreateInstance<ProjectSetupProfile>();
            profile.SetRecommendedDefaults();
            profile.ConfigureAssetSerialization = false;
            profile.ConfigureVersionControl = false;
            profile.ConfigureRootNamespace = true;
            profile.RootNamespace = desiredNamespace;
            profile.ConfigureNewScriptLineEndings = true;
            profile.NewScriptLineEndings = desiredLineEndings;

            try
            {
                environment.Apply(profile);

                Assert.That(EditorSettings.projectGenerationRootNamespace, Is.EqualTo(desiredNamespace));
                Assert.That(EditorSettings.lineEndingsForNewScripts, Is.EqualTo(desiredLineEndings));

                environment.Apply(snapshot);

                Assert.That(EditorSettings.projectGenerationRootNamespace, Is.EqualTo(originalNamespace));
                Assert.That(EditorSettings.lineEndingsForNewScripts, Is.EqualTo(originalLineEndings));
            }
            finally
            {
                EditorSettings.projectGenerationRootNamespace = originalNamespace;
                EditorSettings.lineEndingsForNewScripts = originalLineEndings;
                Object.DestroyImmediate(profile);
            }
        }
    }
}
