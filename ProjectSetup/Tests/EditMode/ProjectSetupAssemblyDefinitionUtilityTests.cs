// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectSetup.Editor;

namespace ProjectSetup.Tests
{
    internal sealed class ProjectSetupAssemblyDefinitionUtilityTests
    {
        [Test]
        public void BuildMissingDefinitions_CreatesDeterministicRuntimeAndEditorContent()
        {
            var errors = new List<string>();

            var plans = ProjectSetupAssemblyDefinitionUtility.BuildMissingDefinitions(
                "Studio.Game",
                "Assets/Game/Scripts",
                "Assets/Game/Scripts/Editor",
                new[] { "Assets", "Assets/Game", "Assets/Game/Scripts", "Assets/Game/Scripts/Editor" },
                Array.Empty<string>(),
                errors);

            Assert.That(errors, Is.Empty);
            Assert.That(plans, Has.Length.EqualTo(2));
            Assert.That(plans[0].Path, Is.EqualTo("Assets/Game/Scripts/Studio.Game.asmdef"));
            Assert.That(plans[0].Content, Does.Contain("\"name\": \"Studio.Game\""));
            Assert.That(plans[0].Content, Does.Contain("\"rootNamespace\": \"Studio.Game\""));
            Assert.That(plans[1].Path, Is.EqualTo("Assets/Game/Scripts/Editor/Studio.Game.Editor.asmdef"));
            Assert.That(plans[1].Content, Does.Contain("\"Studio.Game\""));
            Assert.That(plans[1].Content, Does.Contain("\"Editor\""));
            Assert.That(plans[0].Content, Does.EndWith("\n"));
            Assert.That(plans[1].Content, Does.EndWith("\n"));
            Assert.That(plans[0].ContentHash, Is.EqualTo(ProjectSetupAssemblyDefinitionUtility.ComputeContentHash(plans[0].Content)));
        }

        [Test]
        public void BuildMissingDefinitions_DoesNotOverwriteExistingTargetFiles()
        {
            var errors = new List<string>();
            var existing = new[]
            {
                "Assets/Scripts/Game.asmdef",
                "Assets/Scripts/Editor/Game.Editor.asmdef"
            };

            var plans = ProjectSetupAssemblyDefinitionUtility.BuildMissingDefinitions(
                "Game",
                "Assets/Scripts",
                "Assets/Scripts/Editor",
                new[] { "Assets", "Assets/Scripts", "Assets/Scripts/Editor" },
                existing,
                errors);

            Assert.That(errors, Is.Empty);
            Assert.That(plans, Is.Empty);
        }

        [Test]
        public void BuildMissingDefinitions_IncludesDeterministicEditModeAndPlayModeTests()
        {
            var errors = new List<string>();

            var plans = ProjectSetupAssemblyDefinitionUtility.BuildMissingDefinitions(
                "Studio.Game",
                "Assets/Game/Scripts",
                "Assets/Game/Scripts/Editor",
                true,
                "Assets/Game/Tests",
                Array.Empty<string>(),
                Array.Empty<string>(),
                errors);

            Assert.That(errors, Is.Empty);
            Assert.That(plans.Select(plan => plan.Path), Is.EqualTo(new[]
            {
                "Assets/Game/Scripts/Studio.Game.asmdef",
                "Assets/Game/Scripts/Editor/Studio.Game.Editor.asmdef",
                "Assets/Game/Tests/EditMode/Studio.Game.Tests.asmdef",
                "Assets/Game/Tests/PlayMode/Studio.Game.PlayMode.Tests.asmdef"
            }));
            Assert.That(plans[2].Content, Does.Contain("\"Studio.Game.Editor\"").And.Contain("\"TestAssemblies\"").And.Contain("\"Editor\""));
            Assert.That(plans[3].Content, Does.Contain("\"Studio.Game\"").And.Contain("\"TestAssemblies\"").And.Not.Contain("includePlatforms"));
        }

        [Test]
        public void BuildMissingDefinitions_RejectsDifferentDefinitionInSameFolder()
        {
            var errors = new List<string>();

            var plans = ProjectSetupAssemblyDefinitionUtility.BuildMissingDefinitions(
                "Game",
                "Assets/Scripts",
                "Assets/Scripts/Editor",
                new[] { "Assets", "Assets/Scripts", "Assets/Scripts/Editor" },
                new[] { "Assets/Scripts/Existing.asmdef" },
                errors);

            Assert.That(plans, Is.Empty);
            Assert.That(errors, Has.Some.Contains("different Assembly Definition"));
        }

        [Test]
        public void ComputeContentHash_UsesExactUtf8Bytes()
        {
            const string content = "{\n  \"name\": \"Game\"\n}\n";
            var bytes = new System.Text.UTF8Encoding(false).GetBytes(content);

            Assert.That(
                ProjectSetupAssemblyDefinitionUtility.ComputeContentHash(content),
                Is.EqualTo(ProjectSetupAssemblyDefinitionUtility.ComputeContentHash(bytes)));
        }
    }
}
