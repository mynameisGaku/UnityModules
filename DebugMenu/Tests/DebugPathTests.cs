using System.IO;
using NUnit.Framework;

namespace DebugMenu.Tests
{
    /// <summary>パス行の入力、検証、保存用写し、既定値復元を検証する。</summary>
    public sealed class DebugPathTests
    {
        [Test]
        public void Path_WritesThroughToBoundString()
        {
            var value = "before.txt";
            var path = new DebugPath("Output", DebugPathMode.File, () => value, next => value = next);

            Assert.IsTrue(path.CommitEditText("after.txt"));
            Assert.AreEqual("after.txt", value);
            Assert.AreEqual(DebugValueKind.Text, path.ValueKind);
            Assert.IsTrue(path.CanTypeValue);
        }

        [Test]
        public void Path_FileModeValidatesExistenceAndExtension()
        {
            var existing = typeof(DebugPathTests).Assembly.Location;
            var value = string.Empty;
            var path = new DebugPath("Assembly", DebugPathMode.File, () => value, next => value = next)
                .WithExistingPathRequired()
                .WithExtensions("*.DLL", ".dll");

            Assert.IsTrue(path.CommitEditText(existing));
            Assert.AreEqual(existing, value);
            Assert.AreEqual(1, path.Extensions.Count, "同じ拡張子が重複している。");

            var missing = Path.Combine(Path.GetDirectoryName(existing), "missing-file.dll");
            Assert.IsFalse(path.CommitEditText(missing));
            Assert.AreEqual(existing, value, "存在しないパスで値が変わっている。");
            Assert.IsTrue(!string.IsNullOrEmpty(path.LastValidationError));
        }

        [Test]
        public void Path_FileModeRejectsUnlistedExtension()
        {
            var path = new DebugPath("Config", DebugPathMode.File, "initial.json").WithExtensions("json");

            Assert.IsFalse(path.CommitEditText("config.txt"));
            Assert.AreEqual("initial.json", path.Value);

            Assert.IsTrue(path.CommitEditText("config.JSON"));
            Assert.AreEqual("config.JSON", path.Value);
        }

        [Test]
        public void Path_FileModeAcceptsMultiPartExtension()
        {
            var path = new DebugPath("Archive", DebugPathMode.File, string.Empty).WithExtensions("tar.gz");

            Assert.IsTrue(path.CommitEditText("capture.TAR.GZ"));
            Assert.IsFalse(path.CommitEditText("capture.gz"));
        }

        [Test]
        public void Path_FolderModeRequiresDirectoryInsteadOfFile()
        {
            var file = typeof(DebugPathTests).Assembly.Location;
            var folder = Path.GetDirectoryName(file);
            var path = new DebugPath("Folder", DebugPathMode.Folder, string.Empty).WithExistingPathRequired();

            Assert.IsFalse(path.CommitEditText(file));
            Assert.IsTrue(path.CommitEditText(folder));
            Assert.AreEqual(folder, path.Value);
        }

        [Test]
        public void Path_ResetRestoresDefaultEvenIfValidationChanges()
        {
            var value = "default.txt";
            var path = new DebugPath("Output", DebugPathMode.File, () => value, next => value = next);
            path.Value = "changed.txt";
            path.WithExistingPathRequired();

            path.ResetToDefault();

            Assert.AreEqual("default.txt", value);
            Assert.IsFalse(path.IsModified);
        }

        [Test]
        public void Path_SnapshotRoundTripsAsText()
        {
            var source = new DebugPath("Source", DebugPathMode.File, "saved/path.txt");
            var target = new DebugPath("Target", DebugPathMode.File, "other.txt");

            var snapshot = DebugValueSnapshot.Capture(source);

            Assert.IsTrue(snapshot.HasValue);
            Assert.IsTrue(snapshot.Apply(target));
            Assert.AreEqual("saved/path.txt", target.Value);
        }

        [Test]
        public void Path_AuthoringExtensionsAddFileAndFolderRows()
        {
            var file = string.Empty;
            var folder = string.Empty;
            var page = new DebugPage("Paths");

            var fileRow = page.FilePath("File", () => file, value => file = value);
            var folderRow = page.FolderPath("Folder", () => folder, value => folder = value);

            Assert.AreEqual(DebugPathMode.File, fileRow.Mode);
            Assert.AreEqual(DebugPathMode.Folder, folderRow.Mode);
            Assert.AreEqual(2, page.Root.Children.Count);
        }
    }
}
