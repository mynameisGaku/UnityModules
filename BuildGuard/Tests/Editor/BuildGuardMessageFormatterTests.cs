// SPDX-License-Identifier: MIT

using System;
using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Verifies deterministic combined build failure messages.
    /// </summary>
    internal sealed class BuildGuardMessageFormatterTests
    {
        [Test]
        public void Format_MixedFindings_IsSortedAndComplete()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Message Scene";
            var scripts = new[]
            {
                new MissingScriptFinding("Root[1]", 2),
                new MissingScriptFinding("Root[0]", 1)
            };
            var references = new[]
            {
                new MissingObjectReferenceFinding("Root[1]", "Example.Second", 2, "m_Z"),
                new MissingObjectReferenceFinding("Root[0]", "Example.First", 1, "m_A")
            };

            var message = BuildGuardMessageFormatter.Format(scene, scripts, references);

            Assert.That(message, Is.EqualTo(
                "Build Guard found build-blocking issues in a Player build Scene.\n" +
                "Scene: <unsaved:Message Scene>\n" +
                "Missing Scripts: 3\n" +
                "- Root[0]: 1\n" +
                "- Root[1]: 2\n" +
                "Missing Object References: 2\n" +
                "- Root[0] :: Example.First[1].m_A\n" +
                "- Root[1] :: Example.Second[2].m_Z\n" +
                "Repair or remove the listed missing references before building again."));
        }

        [Test]
        public void Format_NoFindings_ThrowsArgumentException()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Assert.Throws<ArgumentException>(() => BuildGuardMessageFormatter.Format(
                scene,
                Array.Empty<MissingScriptFinding>(),
                Array.Empty<MissingObjectReferenceFinding>()));
        }
    }
}
