// SPDX-License-Identifier: MIT

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace ProjectSetup.Editor.Tests
{
    internal sealed class ProjectSetupVersionControlFileTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "ProjectSetupVersionControlFileTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, true);
            }
        }

        [Test]
        public void BuildMissingFiles_CreatesDeterministicUnityTemplates()
        {
            var plans = ProjectSetupVersionControlFileUtility.BuildMissingFiles(Array.Empty<string>());

            Assert.That(plans.Select(plan => plan.Path), Is.EqualTo(new[] { ".gitignore", ".gitattributes" }));
            Assert.That(plans[0].Content, Does.Contain("/[Ll]ibrary/").And.Contain("/ProjectSettings/ProjectSetupLastBackup.json"));
            Assert.That(plans[1].Content, Does.Contain("*.cs text eol=lf").And.Contain("*.unity text eol=lf"));
            Assert.That(plans.All(plan => plan.Content.EndsWith("\n", StringComparison.Ordinal)), Is.True);
            Assert.That(plans.All(plan => !plan.Content.Contains("\r")), Is.True);
        }

        [Test]
        public void BuildMissingFiles_PreservesExistingTargets()
        {
            var plans = ProjectSetupVersionControlFileUtility.BuildMissingFiles(new[] { ".gitignore" });

            Assert.That(plans.Select(plan => plan.Path), Is.EqualTo(new[] { ".gitattributes" }));
        }

        [Test]
        public void CreateAndRestore_RemovesOnlyUnchangedCreatedFiles()
        {
            var store = new ProjectSetupVersionControlFileStore(_root);
            var plans = ProjectSetupVersionControlFileUtility.BuildMissingFiles(store.CapturePaths());
            var created = store.Create(plans);
            File.AppendAllText(Path.Combine(_root, ".gitignore"), "# user change\n", new UTF8Encoding(false));

            store.Restore(created);

            Assert.That(File.Exists(Path.Combine(_root, ".gitignore")), Is.True);
            Assert.That(File.Exists(Path.Combine(_root, ".gitattributes")), Is.False);
        }

        [Test]
        public void Create_DoesNotOverwriteOccupiedTarget()
        {
            Directory.CreateDirectory(Path.Combine(_root, ".gitignore"));
            var store = new ProjectSetupVersionControlFileStore(_root);
            var plans = ProjectSetupVersionControlFileUtility.BuildMissingFiles(Array.Empty<string>());

            Assert.Throws<InvalidOperationException>(() => store.Create(plans));
            Assert.That(Directory.Exists(Path.Combine(_root, ".gitignore")), Is.True);
            Assert.That(File.Exists(Path.Combine(_root, ".gitattributes")), Is.False);
        }

        [Test]
        public void Restore_RejectsUnsupportedBackupPath()
        {
            var store = new ProjectSetupVersionControlFileStore(_root);
            var created = new[] { new ProjectSetupCreatedRootFile("ProjectSettings/ProjectVersion.txt", "hash") };

            Assert.Throws<InvalidOperationException>(() => store.Restore(created));
        }
    }
}
