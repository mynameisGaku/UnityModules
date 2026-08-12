using NUnit.Framework;

namespace DebugMenu.Tests
{
    /// <summary>最近変更した項目の順序、重複排除、借用表示、解除を検証する。</summary>
    public sealed class DebugMenuRecentChangesTests
    {
        private sealed class MetadataElement : DebugElement
        {
            private int _value;

            public MetadataElement(string label) : base(label) { }

            public bool ThrowOnMetadata { get; set; }
            public int RawValue => _value;
            public override bool IsSaveable => ThrowOnMetadata
                ? throw new System.InvalidOperationException("metadata failed")
                : true;
            public override DebugValueKind ValueKind => DebugValueKind.Int;
            public override bool TryGetInt(out int value)
            {
                value = _value;
                return true;
            }
            public override bool TrySetInt(int value)
            {
                _value = value;
                NotifyChanged();
                return true;
            }

            public void Change(int value)
            {
                _value = value;
                NotifyChanged();
            }
        }

        private sealed class ReentrantMetadataElement : DebugElement
        {
            private System.Action _onMetadata;

            public ReentrantMetadataElement(string label, System.Action onMetadata) : base(label)
            {
                _onMetadata = onMetadata;
            }

            public void SetAction(System.Action action) => _onMetadata = action;

            public override bool IsSaveable
            {
                get
                {
                    var action = _onMetadata;
                    _onMetadata = null;
                    action?.Invoke();
                    return false;
                }
            }
        }

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
        public void RecentChanges_AutomaticallyTracksDynamicallyAddedRows()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");

            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);

            var added = page.Root.Int("Added", 0);
            added.Value = 1;
            Assert.AreEqual(1, recent.Count, "構造変更後の最初の通知で追加行を追跡できていない。");
            Assert.AreSame(added, recent.Entries[0].Element);

            recent.Refresh(menu);
            added.Value = 2;

            Assert.AreEqual(1, recent.Count);
            Assert.AreSame(added, recent.Entries[0].Element);
        }

        [Test]
        public void RecentChanges_AttachKeepsOwnershipWhenGetterIsTemporarilyUnavailable()
        {
            var menu = new DebugMenuRoot();
            var value = 1;
            var throwOnRead = false;
            var element = menu.AddPage("Gameplay").Root.Int(
                "Recoverable",
                () => throwOnRead ? throw new System.InvalidOperationException("getter failed") : value,
                next => value = next);
            throwOnRead = true;

            using var recent = new DebugMenuRecentChanges();
            Assert.DoesNotThrow(() => recent.Attach(menu));
            throwOnRead = false;

            Assert.IsTrue(element.TrySetInt(2));
            Assert.AreEqual(1, recent.Count);
            Assert.AreSame(element, recent.Entries[0].Element);
        }

        [Test]
        public void RecentChanges_RefreshRemovesDynamicallyRemovedRows()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            var element = root.Int("Temporary", 0);
            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);
            element.Value = 1;
            Assert.AreEqual(1, recent.Count);

            Assert.IsTrue(root.Remove(element));
            recent.Refresh(menu);

            Assert.AreEqual(0, recent.Count);
            Assert.AreEqual(0, recent.Page.Root.Children.Count);
        }

        [Test]
        public void RecentChanges_PageRemovalPrunesEntryAndReaddDoesNotRestoreIt()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var element = page.Root.Int("Temporary", 0);
            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);
            element.Value = 1;
            Assert.AreEqual(1, recent.Count);

            Assert.IsTrue(menu.RemovePage(page));
            recent.Refresh();
            Assert.AreEqual(0, recent.Count);
            Assert.AreEqual(0, recent.Page.Root.Children.Count);

            menu.AddPage(page);
            recent.Refresh();
            Assert.AreEqual(0, recent.Count, "再登録で削除前の最近項目が復活している");

            element.Value = 2;
            Assert.AreEqual(1, recent.Count);
        }

        [Test]
        public void RecentChanges_PageRemovalDiscardsPendingChangeBeforeReadd()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var element = page.Root.Add(new MetadataElement("Pending"));
            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);
            element.ThrowOnMetadata = true;
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*最近項目対象確認"));
            element.Change(1);

            Assert.IsTrue(menu.RemovePage(page));
            recent.Refresh();
            menu.AddPage(page);
            element.ThrowOnMetadata = false;
            recent.Refresh();

            Assert.AreEqual(0, recent.Count);
            Assert.AreEqual(0, recent.Page.Root.Children.Count);
        }

        [Test]
        public void RecentChanges_PageRemovalKeepsEntryReachableThroughPageLink()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var linked = menu.AddPage("Linked");
            var element = linked.Root.Int("Value", 0);
            root.AddChildPage(linked);
            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);
            element.Value = 1;

            Assert.IsTrue(menu.RemovePage(linked));
            recent.Refresh();

            Assert.AreEqual(1, recent.Count);
            Assert.AreSame(element, recent.Entries[0].Element);
            Assert.AreSame(linked, recent.Entries[0].Page);
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

        [Test]
        public void RecentChanges_MetadataFailureNotificationAppearsAfterRecovery()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Add(new MetadataElement("Recoverable"));
            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);
            element.ThrowOnMetadata = true;

            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*最近項目対象確認"));
            element.Change(1);
            Assert.AreEqual(0, recent.Count);

            element.ThrowOnMetadata = false;
            recent.Refresh();
            Assert.AreEqual(1, recent.Count);
            Assert.AreSame(element, recent.Entries[0].Element);
        }

        [Test]
        public void RecentChanges_MetadataRecoveryPreservesOriginalChangeOrder()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            var first = root.Add(new MetadataElement("First"));
            var second = root.Add(new MetadataElement("Second"));
            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);
            first.ThrowOnMetadata = true;
            second.ThrowOnMetadata = true;

            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*最近項目対象確認"));
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*最近項目対象確認"));
            first.Change(1);
            second.Change(1);
            first.Change(2);

            first.ThrowOnMetadata = false;
            recent.Refresh();
            Assert.AreSame(first, recent.Entries[0].Element);

            second.ThrowOnMetadata = false;
            recent.Refresh();
            Assert.AreEqual(2, recent.Count);
            Assert.AreSame(first, recent.Entries[0].Element);
            Assert.AreSame(second, recent.Entries[1].Element);
        }

        [Test]
        public void RecentChanges_ClearDiscardsPendingUnknownChanges()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Add(new MetadataElement("Pending"));
            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);
            element.ThrowOnMetadata = true;
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*最近項目対象確認"));
            element.Change(1);

            recent.Clear();
            element.ThrowOnMetadata = false;
            recent.Refresh();

            Assert.AreEqual(0, recent.Count);
        }

        [Test]
        public void RecentChanges_RemovalDiscardsPendingUnknownChangeBeforeReadd()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            var element = root.Add(new MetadataElement("Pending"));
            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);
            element.ThrowOnMetadata = true;
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*最近項目対象確認"));
            element.Change(1);

            Assert.IsTrue(root.Remove(element));
            recent.Refresh();
            root.Add(element);
            element.ThrowOnMetadata = false;
            recent.Refresh();

            Assert.AreEqual(0, recent.Count);
        }

        [Test]
        public void RecentChanges_ParameterlessRefreshRemovesDeletedAndBorrowedOrphans()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            var element = root.Int("Temporary", 0);
            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);
            element.Value = 1;

            Assert.IsTrue(root.Remove(element));
            recent.Page.Root.AddBorrowed(element);
            recent.Refresh();

            Assert.AreEqual(0, recent.Count);
            Assert.AreEqual(0, recent.Page.Root.Children.Count);
        }

        [TestCase(DebugAttachMode.Page)]
        [TestCase(DebugAttachMode.Inline)]
        public void RecentChanges_TracksFirstRowAddedToEmptyLinkedPage(DebugAttachMode mode)
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var target = new DebugPage("Target");
            root.AddChildPage(target, mode);
            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);

            var added = target.Root.Int("late", 0);
            added.Value = 1;

            Assert.AreEqual(1, recent.Count);
            Assert.AreSame(target, recent.Entries[0].Page);
        }

        [Test]
        public void RecentChanges_UpdatesPageAfterOwnedRowMoves()
        {
            var menu = new DebugMenuRoot();
            var first = menu.AddPage("First");
            var second = menu.AddPage("Second");
            var element = first.Root.Int("Moved", 0);
            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);
            element.Value = 1;

            second.Root.Add(element);
            recent.Refresh();

            Assert.AreEqual(0, first.Root.Children.Count);
            Assert.AreSame(second, recent.Entries[0].Page);
        }

        [Test]
        public void RecentChanges_PublicRefreshQueuesReentrantNotificationsInOrder()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            var first = root.Int("first", 0);
            var second = root.Int("second", 0);
            var trigger = root.Add(new ReentrantMetadataElement("trigger", null));
            using var recent = new DebugMenuRecentChanges();
            recent.Attach(menu);
            trigger.SetAction(() =>
            {
                first.Value = 1;
                second.Value = 1;
                first.Value = 2;
            });

            Assert.DoesNotThrow(() => recent.Refresh(menu));
            Assert.AreEqual(2, recent.Count);
            Assert.AreSame(first, recent.Entries[0].Element);
            Assert.AreSame(second, recent.Entries[1].Element);
        }
    }
}
