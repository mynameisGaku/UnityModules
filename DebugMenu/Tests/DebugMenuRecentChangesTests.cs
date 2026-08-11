using NUnit.Framework;

namespace DebugMenu.Tests
{
    /// <summary>最近変更した項目の順序、重複排除、借用表示、解除を検証する。</summary>
    public sealed class DebugMenuRecentChangesTests
    {
        [Test]
        public void RecentChanges_KeepsUniqueNewestFirstOrder()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var first = page.Root.Int("First", 0);
            var second = page.Root.Int("Second", 0);

            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);

            first.Value = 1;
            second.Value = 1;
            first.Value = 2;

            Assert.AreEqual(2, recent.Count);
            Assert.AreSame(first, recent.Entries[0].Element);
            Assert.AreSame(second, recent.Entries[1].Element);
            Assert.AreSame(page, recent.Entries[0].Page);
            Assert.AreSame(first, recent.Page.Root.Children[0]);
            Assert.AreSame(second, recent.Page.Root.Children[1]);
        }

        [Test]
        public void RecentChanges_BorrowedRowsKeepOriginalParent()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var group = page.Root.Add(new DebugGroup("Player"));
            var value = group.Add(new DebugBool("Invincible", false));

            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);
            value.Value = true;

            Assert.AreSame(group, value.Parent);
            recent.Page.Root.Children[0].OnDecide();
            Assert.IsFalse(value.Value, "借用ページから同じ行を操作できていない。");
            Assert.AreSame(group, value.Parent, "借用表示で元ページの親が変わっている。");
        }

        [Test]
        public void RecentChanges_EvictsOldestAtCapacity()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            var first = root.Int("First", 0);
            var second = root.Int("Second", 0);
            var third = root.Int("Third", 0);

            using var recent = new DebugMenuRecentChanges(2);
            recent.Attach(menu);

            first.Value = 1;
            second.Value = 1;
            third.Value = 1;

            Assert.AreEqual(2, recent.Count);
            Assert.AreSame(third, recent.Entries[0].Element);
            Assert.AreSame(second, recent.Entries[1].Element);
        }

        [Test]
        public void RecentChanges_IgnoresElementsFromAnotherMenu()
        {
            var tracked = new DebugMenuRoot();
            var trackedValue = tracked.AddPage("Tracked").Root.Int("Value", 0);
            var other = new DebugMenuRoot();
            var otherValue = other.AddPage("Other").Root.Int("Value", 0);

            using var recent = new DebugMenuRecentChanges();
            recent.Attach(tracked);

            otherValue.Value = 1;
            Assert.AreEqual(0, recent.Count);

            trackedValue.Value = 1;
            Assert.AreEqual(1, recent.Count);
        }

        [Test]
        public void RecentChanges_RefreshTracksDynamicallyAddedRows()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");

            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);

            var added = page.Root.Int("Added", 0);
            added.Value = 1;
            Assert.AreEqual(0, recent.Count);

            recent.Refresh(menu);
            added.Value = 2;

            Assert.AreEqual(1, recent.Count);
            Assert.AreSame(added, recent.Entries[0].Element);
        }

        [Test]
        public void RecentChanges_DisposeDetachesListenerAndBorrowedRows()
        {
            var menu = new DebugMenuRoot();
            var value = menu.AddPage("Gameplay").Root.Int("Value", 0);
            var recent = new DebugMenuRecentChanges();
            var changedCount = 0;
            recent.Changed += () => changedCount++;
            recent.Attach(menu);
            value.Value = 1;
            Assert.AreEqual(1, changedCount);

            recent.Dispose();
            value.Value = 2;

            Assert.IsFalse(recent.IsAttached);
            Assert.AreEqual(1, changedCount, "破棄後も変更通知を受け取っている。");
            Assert.AreEqual(0, recent.Page.Root.Children.Count, "破棄後も借用行が残っている。");
        }

        [Test]
        public void RecentChanges_MultipleInstancesDetachIndependently()
        {
            var menu = new DebugMenuRoot();
            var value = menu.AddPage("Gameplay").Root.Int("Value", 0);
            using var first = new DebugMenuRecentChanges();
            using var second = new DebugMenuRecentChanges();
            first.Attach(menu);
            second.Attach(menu);

            first.Dispose();
            value.Value = 1;

            Assert.AreEqual(0, first.Count);
            Assert.AreEqual(1, second.Count);
        }
    }
}
