// SPDX-License-Identifier: MIT

using BuildGuard.Editor;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BuildGuard.Tests
{
    /// <summary>
    /// Verifies deterministic hierarchy path creation and resolution.
    /// </summary>
    [Parallelizable(ParallelScope.None)]
    internal sealed class BuildGuardHierarchyPathTests
    {
        [Test]
        public void CreateAndFind_DuplicateEscapedNames_ReturnsExactSibling()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Root/Group");
            var first = new GameObject("Target");
            var second = new GameObject("Target");
            first.transform.SetParent(root.transform, false);
            second.transform.SetParent(root.transform, false);

            var path = BuildGuardHierarchyPath.Create(second.transform);
            var resolved = BuildGuardHierarchyPath.Find(scene, path);

            Assert.That(path, Is.EqualTo("Root\\/Group[0]/Target[1]"));
            Assert.That(resolved, Is.SameAs(second));
        }

        [Test]
        public void Find_UnknownPath_ReturnsNull()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Root");

            Assert.That(BuildGuardHierarchyPath.Find(scene, "Root[1]"), Is.Null);
        }
    }
}
