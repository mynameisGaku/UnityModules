using System;
using System.IO;
using NUnit.Framework;

namespace DebugMenu.Tests
{
    /// <summary>パス行の入力、検証、保存用写し、既定値復元を検証する。</summary>
    public sealed class DebugPathTests
    {
        private string _temporaryDirectory;

        [SetUp]
        public void SetUp()
        {
            _temporaryDirectory = Path.Combine(Path.GetTempPath(), "DebugMenuPath-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_temporaryDirectory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_temporaryDirectory)) Directory.Delete(_temporaryDirectory, true);
        }

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

        [Test]
        public void Path_RowDecideExpandsBrowserWhileDirectInputRemainsAvailable()
        {
            var initial = Path.Combine(_temporaryDirectory, "initial.json");
            var typed = Path.Combine(_temporaryDirectory, "typed.json");
            var path = new DebugPath("Config", DebugPathMode.File, initial);

            Assert.IsTrue(path.PrefersDecide);
            Assert.IsTrue(path.CanTypeValue);

            path.OnDecide();
            Assert.IsTrue(path.IsExpanded);
            Assert.AreEqual(initial, path.Value);

            Assert.IsTrue(path.CommitEditText(typed));
            Assert.AreEqual(typed, path.Value);
        }

        [Test]
        public void Path_FileBrowserShowsFoldersAndFilteredFiles()
        {
            var folder = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Sub")).FullName;
            var accepted = Path.Combine(_temporaryDirectory, "accepted.json");
            var rejected = Path.Combine(_temporaryDirectory, "rejected.txt");
            File.WriteAllText(accepted, "{}");
            File.WriteAllText(rejected, "no");
            var path = new DebugPath("Config", DebugPathMode.File, Path.Combine(_temporaryDirectory, "current.json"))
                .WithExtensions(".json");

            path.OnDecide();

            Assert.AreEqual(Path.GetFullPath(_temporaryDirectory), path.CurrentDirectory);
            Assert.IsNotNull(FindRow(path, "[Folder] " + Path.GetFileName(folder)));
            Assert.IsNotNull(FindRow(path, "[File] accepted.json"));
            Assert.IsNull(FindRow(path, "[File] rejected.txt"));
        }

        [Test]
        public void Path_FileBrowserNavigatesAndSelectsFile()
        {
            var folder = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Sub")).FullName;
            var file = Path.Combine(folder, "selected.json");
            File.WriteAllText(file, "{}");
            var path = new DebugPath("Config", DebugPathMode.File, Path.Combine(_temporaryDirectory, "current.json"))
                .WithExtensions("json");
            path.OnDecide();

            FindRow(path, "[Folder] Sub").OnDecide();
            Assert.AreEqual(folder, path.CurrentDirectory);
            Assert.IsNotNull(FindRow(path, "[..] Parent"));

            FindRow(path, "[File] selected.json").OnDecide();

            Assert.AreEqual(file, path.Value);
            Assert.IsFalse(path.IsExpanded);
            Assert.AreEqual(0, path.Children.Count);
        }

        [Test]
        public void Path_FolderBrowserUsesCurrentFolder()
        {
            var folder = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Sub")).FullName;
            var path = new DebugPath("Output", DebugPathMode.Folder, _temporaryDirectory);
            path.OnDecide();

            FindRow(path, "[Folder] Sub").OnDecide();
            Assert.AreEqual(folder, path.CurrentDirectory);

            FindRow(path, "Use This Folder").OnDecide();

            Assert.AreEqual(folder, path.Value);
            Assert.IsFalse(path.IsExpanded);
        }

        [Test]
        public void Path_BrowserParentRowMovesUp()
        {
            Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Sub"));
            var path = new DebugPath("Output", DebugPathMode.Folder, _temporaryDirectory);
            path.OnDecide();
            FindRow(path, "[Folder] Sub").OnDecide();

            FindRow(path, "[..] Parent").OnDecide();

            Assert.AreEqual(Path.GetFullPath(_temporaryDirectory), path.CurrentDirectory);
        }

        [Test]
        public void Path_BrowserEnumerationFailureAddsErrorRows()
        {
            var folder = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Vanishing")).FullName;
            var path = new DebugPath("Config", DebugPathMode.File, Path.Combine(_temporaryDirectory, "current.json"));
            path.OnDecide();
            var folderRow = FindRow(path, "[Folder] Vanishing");
            Directory.Delete(folder, true);

            Assert.DoesNotThrow(() => folderRow.OnDecide());
            Assert.IsTrue(path.IsExpanded);
            Assert.IsNotNull(FindRowWithPrefix(path, "[Error]"));
        }

        [Test]
        public void Path_BrowserRowsAreNotSavedOrSearched()
        {
            var file = Path.Combine(_temporaryDirectory, "HiddenCandidate.json");
            File.WriteAllText(file, "{}");
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Paths");
            var path = page.FilePath("Config", () => file, _ => { });
            path.OnDecide();
            var candidate = FindRow(path, "[File] HiddenCandidate.json");

            Assert.IsFalse(candidate.IsSaveable);
            Assert.IsFalse(candidate.IsSearchable);

            var search = new DebugMenuSearch();
            search.Rebuild(menu);
            var results = new Containers.FastList<DebugSearchHit>();
            search.Query("HiddenCandidate", results);
            Assert.AreEqual(0, results.Count);
        }

        [Test]
        public void Path_PageExtensionInvalidatesRowsWhenBrowserChanges()
        {
            Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Sub"));
            var value = Path.Combine(_temporaryDirectory, "current.json");
            var page = new DebugPage("Paths");
            var path = page.FilePath("Config", () => value, next => value = next);
            Assert.AreEqual(1, page.VisibleRows.Count);

            path.OnDecide();

            Assert.IsTrue(page.VisibleRows.Count > 1);
        }

        [Test]
        public void Path_NestedBrowserRefreshesPageWithoutPageEvent()
        {
            Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "Sub"));
            var page = new DebugPage("Paths");
            var group = page.Root.Add(new DebugGroup("Nested", true));
            var path = group.Add(new DebugPath(
                "Config",
                DebugPathMode.File,
                Path.Combine(_temporaryDirectory, "current.json")));
            page.Invalidate();
            Assert.AreEqual(2, page.VisibleRows.Count);

            path.OnDecide();

            Assert.IsTrue(page.VisibleRows.Count > 2);
        }

        private static DebugElement FindRow(DebugPath path, string label)
        {
            var children = path.Children;
            for (var i = 0; i < children.Count; i++)
            {
                if (string.Equals(children[i].Label, label, StringComparison.Ordinal)) return children[i];
            }

            return null;
        }

        private static DebugElement FindRowWithPrefix(DebugPath path, string prefix)
        {
            var children = path.Children;
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i].Label.StartsWith(prefix, StringComparison.Ordinal)) return children[i];
            }

            return null;
        }
    }
}
