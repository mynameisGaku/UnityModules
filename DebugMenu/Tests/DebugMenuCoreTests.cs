using System;
using System.Collections.Generic;
using Containers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DebugMenu.Tests
{
    /// <summary>
    /// 行・ページ・移動の検証。
    /// UI も入力デバイスも使わずに済むよう層を分けてあるので、ここで挙動を確定できる。
    /// </summary>
    public sealed class DebugMenuCoreTests
    {
        [Test]
        public void ElementAdd_RejectsOwnershipCyclesAndIgnoresSameParentDuplicate()
        {
            var root = new DebugElement("root");
            var child = root.Add(new DebugElement("child"));
            var versionProperty = typeof(DebugElement).GetProperty(
                "OwnedSubtreeVersion",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(versionProperty);
            var version = (uint)versionProperty.GetValue(root);

            Assert.AreSame(child, root.Add(child));
            Assert.AreEqual(1, root.Children.Count);
            Assert.AreEqual(version, (uint)versionProperty.GetValue(root), "同じ親への再追加で所有版が進んでいる");
            Assert.Throws<InvalidOperationException>(() => root.Add(root));
            Assert.Throws<InvalidOperationException>(() => child.Add(root));
            Assert.AreSame(root, child.Parent);
        }

        private sealed class EmptyIntegerElement : DebugElement
        {
            public EmptyIntegerElement() : base("Empty") { }

            public override DebugValueKind ValueKind => DebugValueKind.Int;

            public override bool TryGetInt(out int value)
            {
                value = 0;
                return false;
            }
        }

        private sealed class FailingNonValueElement : DebugElement
        {
            public FailingNonValueElement() : base("Status") { }

            public override string GetValueText() => throw new InvalidOperationException("provider failed");
        }

        private sealed class RecoverableWriteElement : DebugElement
        {
            public RecoverableWriteElement() : base("Recoverable Write") { }

            public bool ThrowOnWrite { get; set; }
            public int Value { get; private set; }
            public override DebugValueKind ValueKind => DebugValueKind.Int;

            public override bool TrySetInt(int value)
            {
                if (ThrowOnWrite) throw new InvalidOperationException("custom setter failed");

                Value = value;
                return true;
            }
        }

        private sealed class ThrowingOperationElement : DebugElement
        {
            public ThrowingOperationElement() : base("Throwing Operation")
            {
                IsExpandable = false;
                IsAdjustableValue = true;
            }

            public bool ThrowOnOperation { get; set; }
            public bool IsAdjustableValue { get; set; }
            public int DecideCount { get; private set; }
            public int AdjustCount { get; private set; }
            public int ResetCount { get; private set; }
            public override bool IsAdjustable => IsAdjustableValue;

            public override void OnDecide()
            {
                if (ThrowOnOperation) throw new InvalidOperationException("decide failed");
                DecideCount++;
            }

            public override void OnAdjust(int delta)
            {
                if (ThrowOnOperation) throw new InvalidOperationException("adjust failed");
                AdjustCount += delta;
            }

            public override void ResetToDefault()
            {
                if (ThrowOnOperation) throw new InvalidOperationException("reset failed");
                ResetCount++;
            }
        }

        private sealed class ThrowingAdjustabilityElement : DebugElement
        {
            public ThrowingAdjustabilityElement() : base("Throwing Adjustability") { }

            public bool ThrowOnCheck { get; set; }
            public int AdjustCount { get; private set; }
            public override bool IsAdjustable => ThrowOnCheck
                ? throw new InvalidOperationException("adjustability failed")
                : true;
            public override void OnAdjust(int delta) => AdjustCount += delta;
        }

        [Test]
        public void CollectionApi_UsesReadOnlyBclContracts()
        {
            Assert.AreEqual(
                typeof(IReadOnlyList<DebugPage>),
                typeof(DebugMenuRoot).GetProperty(nameof(DebugMenuRoot.Pages))?.PropertyType);
            Assert.AreEqual(
                typeof(IReadOnlyList<DebugElement>),
                typeof(DebugElement).GetProperty(nameof(DebugElement.Children))?.PropertyType);
            Assert.AreEqual(
                typeof(IReadOnlyList<DebugRow>),
                typeof(DebugPage).GetProperty(nameof(DebugPage.VisibleRows))?.PropertyType);
        }

        [Test]
        public void CollectionViews_RejectMutationAndStaySynchronized()
        {
            var menu = new DebugMenuRoot();
            var pages = menu.Pages;
            Assert.Throws<NotSupportedException>(() => ((IList<DebugPage>)pages).Add(new DebugPage("Blocked")));

            var page = menu.AddPage("Root");
            Assert.AreEqual(1, pages.Count, "取得済みのページ一覧へ追加が反映されていない");

            var children = page.Root.Children;
            Assert.Throws<NotSupportedException>(() => ((IList<DebugElement>)children).Add(new DebugElement("Blocked")));

            page.Root.Add(new DebugElement("Child"));
            Assert.AreEqual(1, children.Count, "取得済みの子行一覧へ追加が反映されていない");

            var rows = page.VisibleRows;
            page.Root.Add(new DebugElement("Second"));
            Assert.AreSame(rows, page.VisibleRows, "可視行を読むたびに別の一覧が作られている");
            Assert.AreEqual(2, rows.Count, "取得済みの可視行一覧へ再構築結果が反映されていない");
        }

        [Test]
        public void InlinePageLink_ExposesTargetReadOnlyChildren()
        {
            var target = new DebugPage("Target");
            target.Root.Add(new DebugElement("Child"));
            var link = new DebugPageLink("Inline", target, DebugAttachMode.Inline);

            Assert.AreSame(target.Root.Children, link.Children);
            Assert.Throws<NotSupportedException>(() => ((IList<DebugElement>)link.Children).Clear());
            Assert.AreEqual(1, target.Root.Children.Count);
        }

        [Test]
        public void PageRegistration_AddIsIdempotentAndRemoveMiddleKeepsCurrentPath()
        {
            var menu = new DebugMenuRoot();
            var first = menu.AddPage("First");
            var middle = menu.AddPage("Middle");
            var last = menu.AddPage("Last");
            var version = ReadPageVersion(menu);
            var pages = menu.Pages;

            Assert.AreSame(middle, menu.AddPage(middle));
            Assert.AreEqual(version, ReadPageVersion(menu));
            Assert.AreEqual(3, pages.Count);

            var child = new DebugPage("Child");
            menu.PushPage(child);
            var changedCount = 0;
            menu.PageChanged += _ => changedCount++;
            Assert.IsTrue(menu.RemovePage(middle));

            CollectionAssert.AreEqual(new[] { first, last }, pages);
            Assert.AreSame(child, menu.CurrentPage);
            Assert.AreEqual(1, menu.Depth);
            Assert.AreEqual(unchecked(version + 1u), ReadPageVersion(menu));
            Assert.AreEqual(0, changedCount);
        }

        [Test]
        public void RemovePage_CurrentRootUsesNextThenPreviousFallback()
        {
            var menu = new DebugMenuRoot();
            var first = menu.AddPage("First");
            var middle = menu.AddPage("Middle");
            var last = menu.AddPage("Last");
            var changed = new List<DebugPage>();
            menu.PageChanged += changed.Add;

            menu.SetRootPage(middle);
            changed.Clear();
            Assert.IsTrue(menu.RemovePage(middle));
            Assert.AreSame(last, menu.CurrentPage);
            CollectionAssert.AreEqual(new[] { last }, changed);

            changed.Clear();
            Assert.IsTrue(menu.RemovePage(last));
            Assert.AreSame(first, menu.CurrentPage);
            CollectionAssert.AreEqual(new[] { first }, changed);
        }

        [Test]
        public void RemovePage_LinkedCurrentPageKeepsReachableNavigationPath()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var linked = menu.AddPage("Linked");
            root.AddChildPage(linked);
            menu.SetRootPage(root);
            menu.PushPage(linked);
            var changedCount = 0;
            menu.PageChanged += _ => changedCount++;

            Assert.IsTrue(menu.RemovePage(linked));

            Assert.AreSame(linked, menu.CurrentPage);
            Assert.AreEqual(1, menu.Depth);
            Assert.AreEqual(0, changedCount);
        }

        [Test]
        public void RemovePage_LastClosesVisibleMenuAndNoOpsStayStable()
        {
            var menu = new DebugMenuRoot();
            var only = menu.AddPage("Only");
            var pages = menu.Pages;
            var version = ReadPageVersion(menu);
            var notifications = new List<string>();
            menu.PageChanged += page => notifications.Add(page == null ? "page:null" : "page:value");
            menu.VisibilityChanged += visible => notifications.Add("visible:" + visible);
            menu.SetVisible(true);
            notifications.Clear();

            Assert.IsFalse(menu.RemovePage(null));
            Assert.IsFalse(menu.RemovePage(new DebugPage("Unknown")));
            Assert.AreEqual(version, ReadPageVersion(menu));
            Assert.IsTrue(menu.RemovePage(only));

            Assert.AreEqual(0, pages.Count);
            Assert.IsNull(menu.CurrentPage);
            Assert.AreEqual(0, menu.Depth);
            Assert.IsFalse(menu.IsVisible);
            Assert.AreEqual(unchecked(version + 1u), ReadPageVersion(menu));
            CollectionAssert.AreEqual(new[] { "page:null", "visible:False" }, notifications);

            Assert.IsFalse(menu.RemovePage(only));
            menu.ClearPages();
            Assert.AreEqual(unchecked(version + 1u), ReadPageVersion(menu));
            Assert.AreEqual(2, notifications.Count);
        }

        [Test]
        public void ClearPages_ClearsLiveViewAndTransientPathWithPreciseVersioning()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("First");
            var second = menu.AddPage("Second");
            menu.SetRootPage(second);
            var pages = menu.Pages;
            var version = ReadPageVersion(menu);
            var changedCount = 0;
            menu.PageChanged += _ => changedCount++;

            menu.ClearPages();

            Assert.AreEqual(0, pages.Count);
            Assert.IsNull(menu.CurrentPage);
            Assert.AreEqual(0, menu.Depth);
            Assert.AreEqual(unchecked(version + 1u), ReadPageVersion(menu));
            Assert.AreEqual(1, changedCount);

            menu.SetRootPage(new DebugPage("Transient"));
            changedCount = 0;
            var emptyVersion = ReadPageVersion(menu);
            menu.ClearPages();
            Assert.IsNull(menu.CurrentPage);
            Assert.AreEqual(emptyVersion, ReadPageVersion(menu));
            Assert.AreEqual(1, changedCount);

            menu.ClearPages();
            Assert.AreEqual(emptyVersion, ReadPageVersion(menu));
            Assert.AreEqual(1, changedCount);
        }

        [Test]
        public void Decide_ActionCanClearPagesWithoutInvalidatingMissingCurrentPage()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Root");
            page.Root.Action("Clear", menu.ClearPages);

            Assert.DoesNotThrow(menu.Decide);
            Assert.IsNull(menu.CurrentPage);
        }

        [Test]
        public void Flatten_HidesChildrenOfCollapsedGroup()
        {
            var page = new DebugPage("Test");
            var group = page.Group("親", g => g.Bool("子", false));
            group.IsExpanded = false;
            page.Invalidate();

            Assert.AreEqual(1, page.VisibleRows.Count, "閉じているのに子が出ている");
            Assert.AreEqual("親", page.VisibleRows[0].Element.Label);
        }

        [Test]
        public void Flatten_ShowsChildrenOfExpandedGroup()
        {
            var page = new DebugPage("Test");
            page.Group("親", g => g.Bool("子", false));

            var rows = page.VisibleRows;

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(0, rows[0].Depth);
            Assert.AreEqual(1, rows[1].Depth, "字下げの深さが伝わっていない");
        }

        [Test]
        public void Flatten_ReportsDepthForNestedGroups()
        {
            var page = new DebugPage("Test");
            page.Group("A", a => a.Group("B", b => b.Bool("C", false)));

            var rows = page.VisibleRows;

            Assert.AreEqual(3, rows.Count);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, new[] { rows[0].Depth, rows[1].Depth, rows[2].Depth });
        }

        // ── カーソル ────────────────────────────────────────────────────────

        [Test]
        public void Cursor_StopsAtEnds()
        {
            var page = new DebugPage("Test");
            page.Root.Bool("a", false);
            page.Root.Bool("b", false);

            page.MoveCursor(-5);
            Assert.AreEqual(0, page.CursorIndex);

            page.MoveCursor(99);
            Assert.AreEqual(1, page.CursorIndex, "末尾を越えている");
        }

        [Test]
        public void Cursor_WrappedMoveConnectsEnds()
        {
            var page = new DebugPage("Test");
            page.Root.Bool("a", false);
            page.Root.Bool("b", false);
            page.Root.Bool("c", false);

            page.MoveCursorWrapped(-1);
            Assert.AreEqual(2, page.CursorIndex, "負方向の折り返しが壊れている");

            page.MoveCursorWrapped(1);
            Assert.AreEqual(0, page.CursorIndex);
        }

        [Test]
        public void Cursor_ClampsWhenRowsDisappear()
        {
            var page = new DebugPage("Test");
            var group = page.Group("親", g =>
            {
                g.Bool("a", false);
                g.Bool("b", false);
            });

            page.CursorIndex = 2;
            Assert.AreEqual(2, page.CursorIndex);

            group.IsExpanded = false;
            page.Invalidate();

            Assert.AreEqual(0, page.CursorIndex, "消えた行を指したままになっている");
        }

        [Test]
        public void Cursor_EmptyPageIsSafe()
        {
            var page = new DebugPage("Empty");

            Assert.DoesNotThrow(() => page.MoveCursor(1));
            Assert.DoesNotThrow(() => page.MoveCursorWrapped(-1));
            Assert.IsNull(page.CurrentElement);
        }

        // ── ページ遷移 ──────────────────────────────────────────────────────

        [Test]
        public void RootPageMove_NoPagesIsSafe()
        {
            var menu = new DebugMenuRoot();

            Assert.DoesNotThrow(() => menu.MoveRootPage(1));
            Assert.IsNull(menu.CurrentPage);
        }

        [Test]
        public void RootPageMove_ZeroKeepsCurrentPage()
        {
            var menu = new DebugMenuRoot();
            var first = menu.AddPage("First");
            menu.AddPage("Second");

            menu.MoveRootPage(0);

            Assert.AreSame(first, menu.CurrentPage);
        }

        [Test]
        public void RootPageMove_OnePageStaysAtRoot()
        {
            var menu = new DebugMenuRoot();
            var only = menu.AddPage("Only");
            var changed = 0;
            menu.PageChanged += _ => changed++;

            menu.MoveRootPage(1);
            menu.MoveRootPage(-1);

            Assert.AreSame(only, menu.CurrentPage);
            Assert.AreEqual(0, changed);
        }

        [Test]
        public void RootPageMove_WrapsForwardAndBackward()
        {
            var menu = new DebugMenuRoot();
            var first = menu.AddPage("First");
            var second = menu.AddPage("Second");
            var third = menu.AddPage("Third");

            menu.MoveRootPage(-1);
            Assert.AreSame(third, menu.CurrentPage);

            menu.MoveRootPage(1);
            Assert.AreSame(first, menu.CurrentPage);

            menu.MoveRootPage(1);
            Assert.AreSame(second, menu.CurrentPage);
        }

        [Test]
        public void RootPageMove_FromChildClearsHistory()
        {
            var menu = new DebugMenuRoot();
            var first = menu.AddPage("First");
            var child = new DebugPage("Child");
            first.AddChildPage(child);
            var second = menu.AddPage("Second");

            menu.Decide();
            Assert.AreSame(child, menu.CurrentPage);

            menu.MoveRootPage(1);

            Assert.AreSame(second, menu.CurrentPage);
            Assert.AreEqual(0, menu.Depth);
        }

        [Test]
        public void RootPageMove_OnePageReturnsFromChild()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var child = new DebugPage("Child");
            root.AddChildPage(child);

            menu.Decide();
            menu.MoveRootPage(1);

            Assert.AreSame(root, menu.CurrentPage);
            Assert.AreEqual(0, menu.Depth);
        }

        [Test]
        public void PageLink_DecideNavigatesToChildPage()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var child = new DebugPage("Child");
            root.AddChildPage(child);

            menu.Decide();

            Assert.AreSame(child, menu.CurrentPage, "遷移していない");
            Assert.AreEqual(1, menu.Depth);
        }

        [Test]
        public void Cancel_ReturnsToPreviousPage()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var child = new DebugPage("Child");
            root.AddChildPage(child);

            menu.SetVisible(true);
            menu.Decide();
            menu.Cancel();

            Assert.AreSame(root, menu.CurrentPage);
            Assert.IsTrue(menu.IsVisible, "最上位でないのに閉じている");
        }

        [Test]
        public void Cancel_ClosesMenuAtRoot()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("Root");
            menu.SetVisible(true);

            menu.Cancel();

            Assert.IsFalse(menu.IsVisible);
        }

        [Test]
        public void InlineAttach_DoesNotSwitchPage()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var child = new DebugPage("Child");
            child.Root.Bool("子の値", false);
            root.AddChildPage(child, DebugAttachMode.Inline);

            menu.Decide();

            Assert.AreSame(root, menu.CurrentPage, "インラインなのにページが変わった");
            Assert.AreEqual(2, root.VisibleRows.Count, "その場に展開されていない");
        }

        [Test]
        public void Shortcut_WorksFromAnyPage()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var other = menu.AddPage("Other");

            var fired = false;
            other.Root.Action("撃つ", () => fired = true).WithShortcut(KeyCode.F5);

            menu.SetRootPage(root);

            Assert.IsTrue(menu.TryInvokeShortcut(KeyCode.F5));
            Assert.IsTrue(fired, "別ページの行が呼ばれていない");
        }

        // ── 値の行 ──────────────────────────────────────────────────────────

        [Test]
        public void Shortcut_PredicateInvokesFirstPressedKey()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Root");
            var firstFired = false;
            var secondFired = false;
            page.Root.Action("First", () => firstFired = true).WithShortcut(KeyCode.F5);
            page.Root.Action("Second", () => secondFired = true).WithShortcut(KeyCode.F6);
            var visited = new List<KeyCode>();

            var invoked = menu.TryInvokeShortcut(key =>
            {
                visited.Add(key);
                return key == KeyCode.F6;
            });

            Assert.IsTrue(invoked);
            Assert.IsFalse(firstFired);
            Assert.IsTrue(secondFired);
            CollectionAssert.AreEqual(new[] { KeyCode.F5, KeyCode.F6 }, visited);
        }

        [Test]
        public void Shortcut_PredicateRejectsNull()
        {
            var menu = new DebugMenuRoot();

            Assert.Throws<System.ArgumentNullException>(() => menu.TryInvokeShortcut((System.Func<KeyCode, bool>)null));
        }

        [Test]
        public void Shortcut_InvalidatesOwningPage()
        {
            var menu = new DebugMenuRoot();
            var current = menu.AddPage("Current");
            var target = menu.AddPage("Target");
            var group = target.Group("Group", child => child.Bool("Value", false)).WithShortcut(KeyCode.F5);
            group.IsExpanded = false;
            target.Invalidate();
            Assert.AreEqual(1, target.VisibleRows.Count);
            menu.SetRootPage(current);

            Assert.IsTrue(menu.TryInvokeShortcut(KeyCode.F5));

            Assert.IsTrue(group.IsExpanded);
            Assert.AreEqual(2, target.VisibleRows.Count);
            Assert.AreSame(current, menu.CurrentPage);
        }

        [Test]
        public void Shortcut_FindsActionInsidePageModeChild()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var child = new DebugPage("Child");
            var fired = false;
            child.Root.Action("Action", () => fired = true).WithShortcut(KeyCode.F5);
            root.AddChildPage(child, DebugAttachMode.Page);

            Assert.IsTrue(menu.TryInvokeShortcut(KeyCode.F5));
            Assert.IsTrue(fired);
            Assert.AreSame(root, menu.CurrentPage);
        }

        [Test]
        public void Shortcut_OnPageLinkOpensItsTarget()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var child = new DebugPage("Child");
            root.AddChildPage(child, DebugAttachMode.Page).WithShortcut(KeyCode.F5);

            Assert.IsTrue(menu.TryInvokeShortcut(KeyCode.F5));
            Assert.AreSame(child, menu.CurrentPage);
            Assert.AreEqual(1, menu.Depth);
        }

        [Test]
        public void Shortcut_PageCycleTerminatesAndContinuesSearch()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var child = new DebugPage("Child");
            var fired = false;
            root.AddChildPage(child, DebugAttachMode.Page);
            child.AddChildPage(root, DebugAttachMode.Page);
            child.Root.Action("Action", () => fired = true).WithShortcut(KeyCode.F6);

            Assert.IsTrue(menu.TryInvokeShortcut(KeyCode.F6));
            Assert.IsTrue(fired);
        }

        [Test]
        public void Bool_WritesThroughToBoundVariable()
        {
            var backing = false;
            var element = new DebugBool("flag", () => backing, v => backing = v);

            element.OnDecide();

            Assert.IsTrue(backing, "ゲーム側の変数に届いていない");
            Assert.AreEqual("ON", element.GetValueText());
        }

        [Test]
        public void Int_ClampsToRange()
        {
            var element = new DebugInt("count", 5).WithRange(0, 10).WithStep(4);

            element.OnAdjust(1);
            element.OnAdjust(1);

            Assert.AreEqual(10, element.Value, "上限を越えている");

            element.OnAdjust(-1);
            Assert.AreEqual(6, element.Value);
        }

        [Test]
        public void Int_RatioOnlyAvailableWithRange()
        {
            var unbounded = new DebugInt("free", 5);
            Assert.IsFalse(unbounded.TryGetRatio(out _), "範囲が無いのに比率を返している");

            var bounded = new DebugInt("bounded", 5).WithRange(0, 10);
            Assert.IsTrue(bounded.TryGetRatio(out var ratio));
            Assert.AreEqual(0.5f, ratio, 0.0001f);
        }

        [Test]
        public void Float_EditTextKeepsFullPrecision()
        {
            var element = new DebugFloat("speed", 1f / 3f).WithDigits(2);

            Assert.AreEqual("0.33", element.GetValueText(), "表示は丸めるべき");
            Assert.AreNotEqual("0.33", element.GetEditText(), "打ち込みでは精度を落とすべきでない");
        }

        [Test]
        public void Float_RejectsUnparsableText()
        {
            var element = new DebugFloat("speed", 1f);

            Assert.IsFalse(element.CommitEditText("abc"));
            Assert.AreEqual(1f, element.Value, "拒んだのに値が変わっている");

            Assert.IsTrue(element.CommitEditText("2.5"));
            Assert.AreEqual(2.5f, element.Value, 0.0001f);
        }

        [Test]
        public void Enum_CyclesThroughOptions()
        {
            var element = new DebugEnum("mode", new[] { "A", "B", "C" });

            element.OnAdjust(-1);
            Assert.AreEqual("C", element.GetValueText(), "負方向の折り返しが壊れている");

            element.OnAdjust(1);
            Assert.AreEqual("A", element.GetValueText());
        }

        [Test]
        public void Enum_ReportsSelectionAndCount()
        {
            var element = new DebugEnum("mode", new[] { "A", "B", "C" }, 1);

            Assert.IsTrue(element.TryGetSelection(out var index, out var count));
            Assert.AreEqual(1, index);
            Assert.AreEqual(3, count);
        }

        [Test]
        public void Enum_UsesDeclarationOrderForSparseEnums()
        {
            var value = Sparse.Second;
            var element = DebugEnum.OfEnum("sparse", () => value, v => value = v);

            element.OnAdjust(1);

            Assert.AreEqual(Sparse.Third, value, "enum の値ではなく宣言順で送るべき");
        }

        [Test]
        public void Enum_AdjustUsesOverflowSafeWrapping()
        {
            var element = new DebugEnum("mode", new[] { "A", "B", "C" }, 1);

            element.OnAdjust(int.MaxValue);
            Assert.AreEqual("C", element.GetValueText());
            element.OnAdjust(int.MinValue);
            Assert.AreEqual("A", element.GetValueText());
        }

        [Test]
        public void EnumOption_SetterFailureMarksOptionAndAllowsRetry()
        {
            var throwOnWrite = true;
            var value = 0;
            var owner = new DebugEnum("mode", new[] { "A", "B" }, () => value, next =>
            {
                if (throwOnWrite) throw new InvalidOperationException("enum option failed");
                value = next;
            });
            var option = owner.Children[1];
            option.Shortcut = KeyCode.F7;
            var menu = new DebugMenuRoot();
            menu.AddPage("Enum").Root.Add(owner);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.IsFalse(menu.TryInvokeShortcut(KeyCode.F7));
            Assert.AreEqual(0, value);
            Assert.IsTrue(option.HasError);

            throwOnWrite = false;
            Assert.IsTrue(menu.TryInvokeShortcut(KeyCode.F7));
            Assert.AreEqual(1, value);
            Assert.IsFalse(option.HasError);
        }

        // ── 共通の飾り ──────────────────────────────────────────────────────

        [Test]
        public void WarnRange_FlagsValueOutsideRange()
        {
            var element = new DebugFloat("fps", 60f);
            element.SetWarnRange(30f, 999f);

            Assert.IsFalse(element.IsValueWarned);

            element.Value = 12f;
            Assert.IsTrue(element.IsValueWarned, "範囲外なのに印が立たない");
        }

        [Test]
        public void SaveKey_IsBuiltFromPath()
        {
            var page = new DebugPage("Gameplay");
            DebugBool leaf = null;
            page.Group("Player", g => leaf = g.Bool("Invincible", false));

            Assert.AreEqual("Gameplay/Player/Invincible", leaf.ResolveSaveKey());
        }

        [Test]
        public void SaveKey_ExplicitKeyIgnoresPath()
        {
            var page = new DebugPage("Gameplay");
            DebugBool leaf = null;
            page.Group("Player", g => leaf = g.Bool("Invincible", false).WithSaveKey("player.invincible"));

            Assert.AreEqual("player.invincible", leaf.ResolveSaveKey());
        }

        [Test]
        public void LabelProvider_OverridesDisplayLabel()
        {
            var hp = 120;
            var element = new DebugWatch("HP", () => hp.ToString());
            element.SetLabelProvider(() => $"HP {hp}/200");

            Assert.AreEqual("HP 120/200", element.DisplayLabel);
            Assert.AreEqual("HP", element.Label, "保存キー用の名前まで変わってはいけない");
        }

        [Test]
        public void DisplayLabel_ExceptionUsesFallbackAndRecoversWithoutRepeatedLogs()
        {
            var shouldThrow = true;
            var failureCount = 0;
            var loggedWarning = string.Empty;
            var element = new DebugElement("HP");
            element.SetLabelProvider(() => shouldThrow
                ? throw new System.InvalidOperationException("label failed " + ++failureCount)
                : "HP 120/200");
            Application.LogCallback captureWarning = (condition, stackTrace, type) =>
            {
                if (type == LogType.Warning && condition.Contains("[DebugMenu]")) loggedWarning = condition;
            };

            Application.logMessageReceived += captureWarning;
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*ラベル取得"));
                Assert.IsFalse(element.TryGetDisplayLabel(out var fallback));
                Assert.AreEqual("HP", fallback);
                Assert.IsTrue(element.HasReadError);

                // 例外文言が毎回変わっても、同じ行は5秒間に1回だけログへ出す。
                Assert.IsFalse(element.TryGetDisplayLabel(out _));
                LogAssert.NoUnexpectedReceived();

                shouldThrow = false;
                Assert.IsTrue(element.TryGetDisplayLabel(out var recovered));
                Assert.AreEqual("HP 120/200", recovered);
                Assert.IsFalse(element.HasReadError);
            }
            finally
            {
                Application.logMessageReceived -= captureWarning;
            }

            StringAssert.Contains("InvalidOperationException: label failed 1", loggedWarning);
            StringAssert.Contains(" at ", loggedWarning, "警告ログから例外の発生位置を追えない");
        }

        [Test]
        public void ValueGetter_ExceptionDoesNotStopConstructionOrRangeConfiguration()
        {
            var shouldThrow = true;
            var value = 7;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            DebugInt element = null;
            Assert.DoesNotThrow(() => element = new DebugInt(
                "Count",
                () => shouldThrow ? throw new System.InvalidOperationException("value failed") : value,
                next => value = next).WithRange(0, 10));
            Assert.IsTrue(element.HasReadError);

            shouldThrow = false;
            Assert.IsTrue(element.TryGetDisplayValueText(out var text));
            Assert.AreEqual("7", text);
            Assert.IsFalse(element.HasReadError);
        }

        [Test]
        public void RecoveredGetter_DisplayCapturesRealDefaultAcrossAllValueKinds()
        {
            var shouldThrow = true;
            var writeCount = 0;
            var booleanValue = true;
            var intValue = 7;
            var floatValue = 0.75f;
            var enumValue = 1;
            var textValue = "baseline";
            var pathValue = "C:\\Baseline";
            var vectorValue = new Vector4(1f, 2f, 3f, 4f);
            var colorValue = new Color(0.8f, 0.3f, 0.2f, 0.6f);

            ExpectValueGetterWarnings(10);
            var boolean = new DebugBool("Boolean", () => ReadOrThrow(booleanValue, shouldThrow), value => { booleanValue = value; writeCount++; });
            var integer = new DebugInt("Integer", () => ReadOrThrow(intValue, shouldThrow), value => { intValue = value; writeCount++; });
            var number = new DebugFloat("Float", () => ReadOrThrow(floatValue, shouldThrow), value => { floatValue = value; writeCount++; });
            var choice = new DebugEnum("Choice", new[] { "A", "B" }, () => ReadOrThrow(enumValue, shouldThrow), value => { enumValue = value; writeCount++; });
            var text = new DebugText("Text", () => ReadOrThrow(textValue, shouldThrow), value => { textValue = value; writeCount++; });
            var path = new DebugPath("Path", DebugPathMode.Folder, () => ReadOrThrow(pathValue, shouldThrow), value => { pathValue = value; writeCount++; });
            var vector = new DebugVector("Vector", 2, () => ReadOrThrow(vectorValue, shouldThrow), value => { vectorValue = value; writeCount++; });
            var color = new DebugColor("Color", () => ReadOrThrow(colorValue, shouldThrow), value => { colorValue = value; writeCount++; });

            shouldThrow = false;
            var elements = new DebugElement[] { boolean, integer, number, choice, text, path, vector, color };
            for (var i = 0; i < elements.Length; i++) Assert.IsTrue(elements[i].TryGetDisplayValueText(out _));

            booleanValue = false;
            intValue = 11;
            floatValue = 0.25f;
            enumValue = 0;
            textValue = "changed";
            pathValue = "C:\\Changed";
            vectorValue = new Vector4(8f, 7f, 6f, 5f);
            colorValue = Color.green;

            for (var i = 0; i < elements.Length; i++) Assert.IsTrue(elements[i].IsModified, elements[i].Label);
            for (var i = 0; i < elements.Length; i++) elements[i].ResetToDefault();

            Assert.AreEqual(8, writeCount);
            Assert.IsTrue(booleanValue);
            Assert.AreEqual(7, intValue);
            Assert.That(floatValue, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.AreEqual(1, enumValue);
            Assert.AreEqual("baseline", textValue);
            Assert.AreEqual("C:\\Baseline", pathValue);
            Assert.AreEqual(new Vector4(1f, 2f, 3f, 4f), vectorValue);
            Assert.AreEqual(new Color(0.8f, 0.3f, 0.2f, 0.6f), colorValue);
        }

        [Test]
        public void RecoveredGetter_ResetBeforeDisplayDoesNotWriteFallbackAcrossAllValueKinds()
        {
            var shouldThrow = true;
            var writeCount = 0;
            var booleanValue = true;
            var intValue = 7;
            var floatValue = 0.75f;
            var enumValue = 1;
            var textValue = "baseline";
            var pathValue = "C:\\Baseline";
            var vectorValue = new Vector4(1f, 2f, 3f, 4f);
            var colorValue = new Color(0.8f, 0.3f, 0.2f, 0.6f);

            ExpectValueGetterWarnings(10);
            var boolean = new DebugBool("Boolean", () => ReadOrThrow(booleanValue, shouldThrow), value => { booleanValue = value; writeCount++; });
            var integer = new DebugInt("Integer", () => ReadOrThrow(intValue, shouldThrow), value => { intValue = value; writeCount++; });
            var number = new DebugFloat("Float", () => ReadOrThrow(floatValue, shouldThrow), value => { floatValue = value; writeCount++; });
            var choice = new DebugEnum("Choice", new[] { "A", "B" }, () => ReadOrThrow(enumValue, shouldThrow), value => { enumValue = value; writeCount++; });
            var text = new DebugText("Text", () => ReadOrThrow(textValue, shouldThrow), value => { textValue = value; writeCount++; });
            var path = new DebugPath("Path", DebugPathMode.Folder, () => ReadOrThrow(pathValue, shouldThrow), value => { pathValue = value; writeCount++; });
            var vector = new DebugVector("Vector", 2, () => ReadOrThrow(vectorValue, shouldThrow), value => { vectorValue = value; writeCount++; });
            var color = new DebugColor("Color", () => ReadOrThrow(colorValue, shouldThrow), value => { colorValue = value; writeCount++; });
            var elements = new DebugElement[] { boolean, integer, number, choice, text, path, vector, color };

            shouldThrow = false;
            for (var i = 0; i < elements.Length; i++) elements[i].ResetToDefault();
            Assert.AreEqual(0, writeCount, "最初の正常値を既定値として覚えるだけのResetがsetterを呼んだ");

            booleanValue = false;
            intValue = 11;
            floatValue = 0.25f;
            enumValue = 0;
            textValue = "changed";
            pathValue = "C:\\Changed";
            vectorValue = new Vector4(8f, 7f, 6f, 5f);
            colorValue = Color.green;
            for (var i = 0; i < elements.Length; i++) elements[i].ResetToDefault();

            Assert.AreEqual(8, writeCount);
            Assert.IsTrue(booleanValue);
            Assert.AreEqual(7, intValue);
            Assert.That(floatValue, Is.EqualTo(0.75f).Within(0.0001f));
            Assert.AreEqual(1, enumValue);
            Assert.AreEqual("baseline", textValue);
            Assert.AreEqual("C:\\Baseline", pathValue);
            Assert.AreEqual(new Vector4(1f, 2f, 3f, 4f), vectorValue);
            Assert.AreEqual(new Color(0.8f, 0.3f, 0.2f, 0.6f), colorValue);
        }

        [Test]
        public void ValueOperations_GetterExceptionsSkipWritesWithoutEscaping()
        {
            var shouldThrow = false;
            var writeCount = 0;

            var integer = new DebugInt(
                "Integer",
                () => shouldThrow ? throw new InvalidOperationException("integer failed") : 4,
                _ => writeCount++).WithRange(0, 10);
            var number = new DebugFloat(
                "Float",
                () => shouldThrow ? throw new InvalidOperationException("float failed") : 0.4f,
                _ => writeCount++).WithRange(0f, 1f);
            var boolean = new DebugBool(
                "Boolean",
                () => shouldThrow ? throw new InvalidOperationException("boolean failed") : true,
                _ => writeCount++);
            var choice = new DebugEnum(
                "Choice",
                new[] { "A", "B" },
                () => shouldThrow ? throw new InvalidOperationException("choice failed") : 0,
                _ => writeCount++);
            var text = new DebugText(
                "Text",
                () => shouldThrow ? throw new InvalidOperationException("text failed") : "before",
                _ => writeCount++);
            var path = new DebugPath(
                "Path",
                DebugPathMode.Folder,
                () => shouldThrow ? throw new InvalidOperationException("path failed") : string.Empty,
                _ => writeCount++);
            var vector = new DebugVector(
                "Vector",
                2,
                () => shouldThrow ? throw new InvalidOperationException("vector failed") : Vector4.one,
                _ => writeCount++);
            var color = new DebugColor(
                "Color",
                () => shouldThrow ? throw new InvalidOperationException("color failed") : Color.red,
                _ => writeCount++);

            shouldThrow = true;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(() => integer.OnAdjust(1));
            Assert.IsFalse(integer.CommitEditText("7"));
            Assert.IsFalse(integer.TrySetRatio(0.8f));
            Assert.DoesNotThrow(integer.ResetToDefault);
            Assert.IsFalse(integer.IsModified);
            Assert.IsFalse(integer.TryGetInt(out _));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(() => number.OnAdjust(1));
            Assert.IsFalse(number.CommitEditText("0.8"));
            Assert.IsFalse(number.TrySetRatio(0.8f));
            Assert.DoesNotThrow(number.ResetToDefault);
            Assert.IsFalse(number.IsModified);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(boolean.OnDecide);
            Assert.DoesNotThrow(() => boolean.OnAdjust(1));
            Assert.IsFalse(boolean.TrySetBool(false));
            Assert.DoesNotThrow(boolean.ResetToDefault);
            Assert.IsFalse(boolean.IsModified);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(() => choice.OnAdjust(1));
            Assert.IsFalse(choice.TryGetSelection(out _, out _));
            Assert.DoesNotThrow(choice.Children[1].OnDecide);
            Assert.IsFalse(choice.IsModified);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.IsFalse(text.CommitEditText("after"));
            Assert.DoesNotThrow(text.ResetToDefault);
            Assert.IsFalse(text.IsModified);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(path.OnDecide);
            Assert.IsFalse(path.IsExpanded);
            Assert.IsFalse(path.CommitEditText("C:\\Temp"));
            Assert.DoesNotThrow(path.ResetToDefault);
            Assert.IsFalse(path.IsModified);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.IsFalse(vector.CommitEditText("2, 3"));
            Assert.DoesNotThrow(vector.ResetToDefault);
            Assert.IsFalse(vector.IsModified);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(() => vector.GetComponent(0).OnAdjust(1));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(() => color.SetHsv(0.5f, 1f, 1f));
            Assert.DoesNotThrow(() => color.SetAlpha(0.5f));
            Assert.IsFalse(color.CommitEditText("#00FF00"));
            Assert.DoesNotThrow(color.ResetToDefault);
            Assert.IsFalse(color.IsModified);

            Assert.AreEqual(0, writeCount, "取得失敗中に利用側 setter が呼ばれている");

            shouldThrow = false;
            integer.OnAdjust(1);
            Assert.AreEqual(1, writeCount, "取得元の回復後も値操作が再開されていない");
            Assert.IsFalse(integer.HasReadError);
        }

        [Test]
        public void ValueOperations_SetterExceptionsAreIsolatedForAllValueTypesAndRecover()
        {
            var throwOnWrite = false;
            var changedCount = 0;
            var booleanValue = true;
            var integerValue = 4;
            var floatValue = 0.4f;
            var enumValue = 0;
            var textValue = "before";
            var pathValue = "before.txt";
            var vectorValue = new Vector4(1f, 1f, 0f, 0f);
            var colorValue = Color.red;

            var boolean = new DebugBool("Boolean", () => booleanValue, value =>
            {
                if (throwOnWrite) throw new InvalidOperationException("boolean setter failed");
                booleanValue = value;
            });
            var integer = new DebugInt("Integer", () => integerValue, value =>
            {
                if (throwOnWrite) throw new InvalidOperationException("integer setter failed");
                integerValue = value;
            }).WithRange(0, 10);
            var number = new DebugFloat("Float", () => floatValue, value =>
            {
                if (throwOnWrite) throw new InvalidOperationException("float setter failed");
                floatValue = value;
            }).WithRange(0f, 1f);
            var choice = new DebugEnum("Choice", new[] { "A", "B" }, () => enumValue, value =>
            {
                if (throwOnWrite) throw new InvalidOperationException("enum setter failed");
                enumValue = value;
            });
            var text = new DebugText("Text", () => textValue, value =>
            {
                if (throwOnWrite) throw new InvalidOperationException("text setter failed");
                textValue = value;
            });
            var path = new DebugPath("Path", DebugPathMode.File, () => pathValue, value =>
            {
                if (throwOnWrite) throw new InvalidOperationException("path setter failed");
                pathValue = value;
            });
            var vector = new DebugVector("Vector", 2, () => vectorValue, value =>
            {
                if (throwOnWrite) throw new InvalidOperationException("vector setter failed");
                vectorValue = value;
            });
            var vectorComponent = vector.GetComponent(0);
            var color = new DebugColor("Color", () => colorValue, value =>
            {
                if (throwOnWrite) throw new InvalidOperationException("color setter failed");
                colorValue = value;
            });

            var elements = new DebugElement[] { boolean, integer, number, choice, text, path, vector, color };
            var writes = new Func<bool>[]
            {
                () => boolean.TrySetBool(false),
                () => integer.TrySetRatio(0.8f),
                () => number.CommitEditText("0.8"),
                () => choice.TrySetInt(1),
                () => text.CommitEditText("after"),
                () => path.CommitEditText("after.txt"),
                () => vector.CommitEditText("2, 3"),
                () => color.CommitEditText("#00FF00"),
            };
            for (var i = 0; i < elements.Length; i++) elements[i].Changed += () => changedCount++;

            var versionBeforeFailure = DebugElement.ValueVersion;
            throwOnWrite = true;
            ExpectValueSetterWarnings(elements.Length);
            for (var i = 0; i < writes.Length; i++)
            {
                var applied = true;
                Assert.DoesNotThrow(() => applied = writes[i]());
                Assert.IsFalse(applied, $"{elements[i].Label} が設定失敗を成功扱いした");
                Assert.AreEqual("ERROR: 値設定", elements[i].ReadErrorText);
            }

            Assert.AreEqual(0, changedCount, "設定失敗でChangedが発火した");
            Assert.AreEqual(versionBeforeFailure, DebugElement.ValueVersion, "設定失敗で値の版数が進んだ");
            Assert.IsTrue(booleanValue);
            Assert.AreEqual(4, integerValue);
            Assert.That(floatValue, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.AreEqual(0, enumValue);
            Assert.AreEqual("before", textValue);
            Assert.AreEqual("before.txt", pathValue);
            Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), vectorValue);
            Assert.AreEqual(Color.red, colorValue);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.IsFalse(vectorComponent.TrySetFloat(5f));
            Assert.IsTrue(vectorComponent.HasReadError);
            Assert.AreEqual(0, changedCount, "親の設定失敗を子成分が成功通知した");
            Assert.AreEqual(versionBeforeFailure, DebugElement.ValueVersion, "子成分の設定失敗で値の版数が進んだ");

            throwOnWrite = false;
            for (var i = 0; i < writes.Length; i++)
            {
                Assert.IsTrue(writes[i](), $"{elements[i].Label} が設定元の回復後も書き込めない");
                Assert.IsFalse(elements[i].HasReadError, $"{elements[i].Label} の設定エラーが回復後も残っている");
            }

            Assert.AreEqual(elements.Length, changedCount);
            Assert.AreEqual(versionBeforeFailure + (uint)elements.Length, DebugElement.ValueVersion);
            Assert.IsFalse(booleanValue);
            Assert.AreEqual(8, integerValue);
            Assert.That(floatValue, Is.EqualTo(0.8f).Within(0.0001f));
            Assert.AreEqual(1, enumValue);
            Assert.AreEqual("after", textValue);
            Assert.AreEqual("after.txt", pathValue);
            Assert.AreEqual(new Vector4(2f, 3f, 0f, 0f), vectorValue);
            Assert.AreEqual(Color.green, colorValue);
            Assert.IsTrue(vectorComponent.TrySetFloat(6f));
            Assert.IsFalse(vectorComponent.HasReadError);
            Assert.That(vectorValue.x, Is.EqualTo(6f).Within(0.0001f));
        }

        [Test]
        public void ValueOperations_RangeClampSetterExceptionsDoNotNotifyAndRecover()
        {
            var throwOnWrite = true;
            var integerValue = 20;
            var floatValue = 2f;
            var changedCount = 0;
            var integer = new DebugInt("Integer", () => integerValue, value =>
            {
                if (throwOnWrite) throw new InvalidOperationException("integer range setter failed");
                integerValue = value;
            });
            var number = new DebugFloat("Float", () => floatValue, value =>
            {
                if (throwOnWrite) throw new InvalidOperationException("float range setter failed");
                floatValue = value;
            });
            integer.Changed += () => changedCount++;
            number.Changed += () => changedCount++;
            var version = DebugElement.ValueVersion;

            ExpectValueSetterWarnings(2);
            Assert.DoesNotThrow(() => integer.WithRange(0, 10));
            Assert.DoesNotThrow(() => number.WithRange(0f, 1f));

            Assert.AreEqual(20, integerValue);
            Assert.That(floatValue, Is.EqualTo(2f).Within(0.0001f));
            Assert.AreEqual(0, changedCount);
            Assert.AreEqual(version, DebugElement.ValueVersion);

            throwOnWrite = false;
            integer.WithRange(0, 10);
            number.WithRange(0f, 1f);

            Assert.AreEqual(10, integerValue);
            Assert.That(floatValue, Is.EqualTo(1f).Within(0.0001f));
            Assert.AreEqual(2, changedCount);
            Assert.IsFalse(integer.HasReadError);
            Assert.IsFalse(number.HasReadError);
        }

        [Test]
        public void ValueSnapshot_CustomSetterExceptionReturnsFalseAndCanRecover()
        {
            var snapshot = DebugValueSnapshot.Capture(new DebugInt("Source", 7));
            var target = new RecoverableWriteElement { ThrowOnWrite = true };

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            var applied = true;
            Assert.DoesNotThrow(() => applied = snapshot.Apply(target));

            Assert.IsFalse(applied);
            Assert.IsTrue(target.HasReadError);

            target.ThrowOnWrite = false;
            Assert.IsTrue(snapshot.Apply(target));
            Assert.AreEqual(7, target.Value);
            Assert.IsFalse(target.HasReadError);
        }

        [Test]
        public void Root_CustomOperationExceptionsAreIsolatedAndRecover()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Operations");
            var element = page.Root.Add(new ThrowingOperationElement { ThrowOnOperation = true });
            element.Shortcut = KeyCode.F5;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.DoesNotThrow(menu.Decide);
            Assert.DoesNotThrow(() => menu.Adjust(1));
            Assert.IsFalse(menu.TryInvokeShortcut(KeyCode.F5), "例外で実行できていないショートカットを成功扱いした");

            Assert.IsTrue(element.HasReadError);
            Assert.AreEqual(0, element.DecideCount);
            Assert.AreEqual(0, element.AdjustCount);

            element.ThrowOnOperation = false;
            menu.Decide();
            menu.Adjust(2);
            Assert.IsTrue(menu.TryInvokeShortcut(KeyCode.F5));

            Assert.IsFalse(element.HasReadError);
            Assert.AreEqual(2, element.DecideCount);
            Assert.AreEqual(2, element.AdjustCount);
        }

        [Test]
        public void Action_ExceptionUsesCommonRowErrorBoundaryAndRecovers()
        {
            var throwOnAction = true;
            var callCount = 0;
            var menu = new DebugMenuRoot();
            var action = menu.AddPage("Actions").Root.Action("Run", () =>
            {
                if (throwOnAction) throw new InvalidOperationException("action failed");
                callCount++;
            }).WithShortcut(KeyCode.F6);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.DoesNotThrow(menu.Decide);
            Assert.IsFalse(menu.TryInvokeShortcut(KeyCode.F6), "同じ行の抑制期間中でも失敗結果は返す必要がある");
            Assert.IsTrue(action.HasError);
            Assert.AreEqual(0, callCount);

            throwOnAction = false;
            Assert.IsTrue(menu.TryInvokeShortcut(KeyCode.F6));
            Assert.IsFalse(action.HasError);
            Assert.AreEqual(1, callCount);
        }

        [Test]
        public void Root_BuiltInSetterFailuresRemainVisibleAndRecover()
        {
            var throwOnWrite = true;
            var boolValue = false;
            var intValue = 1;
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Built In");
            var boolean = page.Root.Add(new DebugBool("Boolean", () => boolValue, value =>
            {
                if (throwOnWrite) throw new InvalidOperationException("decide setter failed");
                boolValue = value;
            }));
            var integer = page.Root.Add(new DebugInt("Integer", () => intValue, value =>
            {
                if (throwOnWrite) throw new InvalidOperationException("adjust setter failed");
                intValue = value;
            }));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            menu.Decide();
            Assert.AreEqual("ERROR: 値設定", boolean.ReadErrorText, "OnDecide内部のsetter失敗を安全入口が消した");
            Assert.IsFalse(boolValue);

            page.MoveCursor(1);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            menu.Adjust(1);
            Assert.AreEqual("ERROR: 値設定", integer.ReadErrorText, "OnAdjust内部のsetter失敗を安全入口が消した");
            Assert.AreEqual(1, intValue);

            throwOnWrite = false;
            page.MoveCursor(-1);
            menu.Decide();
            page.MoveCursor(1);
            menu.Adjust(1);

            Assert.IsTrue(boolValue);
            Assert.AreEqual(2, intValue);
            Assert.IsFalse(boolean.HasReadError);
            Assert.IsFalse(integer.HasReadError);
        }

        [Test]
        public void WriteError_LogRateLimitResetsAfterSuccessfulRecovery()
        {
            var throwOnWrite = true;
            var value = 1;
            var element = new DebugInt("Count", () => value, next =>
            {
                if (throwOnWrite) throw new InvalidOperationException("rate limited setter failed");
                value = next;
            });

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.IsFalse(element.TrySetInt(2));
            Assert.IsFalse(element.TrySetInt(2), "連続失敗は結果だけfalseでログを抑制する");

            throwOnWrite = false;
            Assert.IsTrue(element.TrySetInt(2));
            throwOnWrite = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.IsFalse(element.TrySetInt(3), "回復後の再発は新しい失敗として報告する");
        }

        [Test]
        public void Root_GetterFailureDuringAdjustRemainsFailureUntilRecovery()
        {
            var throwOnRead = false;
            var value = 1;
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Getter").Root.Add(new DebugInt(
                "Integer",
                () => throwOnRead ? throw new InvalidOperationException("adjust getter failed") : value,
                next => value = next));
            throwOnRead = true;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(() => menu.Adjust(1));
            Assert.IsTrue(element.HasReadError);
            Assert.AreEqual(1, value);

            throwOnRead = false;
            menu.Adjust(1);
            Assert.AreEqual(2, value);
            Assert.IsFalse(element.HasReadError);
        }

        [Test]
        public void Root_AdjustabilityExceptionIsIsolatedAndRecovers()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Metadata").Root.Add(new ThrowingAdjustabilityElement { ThrowOnCheck = true });

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.DoesNotThrow(() => menu.Adjust(2));
            Assert.IsTrue(element.HasError);
            Assert.AreEqual(0, element.AdjustCount);

            element.ThrowOnCheck = false;
            menu.Adjust(2);
            Assert.IsFalse(element.HasError);
            Assert.AreEqual(2, element.AdjustCount);
        }

        [Test]
        public void ValueSnapshot_ExceptionSkipsOnlyFailingElementAndCanRecover()
        {
            var shouldThrow = false;
            var element = new DebugInt(
                "Count",
                () => shouldThrow ? throw new System.InvalidOperationException("snapshot failed") : 3,
                _ => { });
            shouldThrow = true;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.IsFalse(DebugValueSnapshot.TryCapture(element, out var failed));
            Assert.IsFalse(failed.HasValue);

            shouldThrow = false;
            Assert.IsTrue(DebugValueSnapshot.TryCapture(element, out var recovered));
            Assert.IsTrue(recovered.HasValue);
            Assert.IsFalse(element.HasReadError);
        }

        [Test]
        public void ValueSnapshot_ValueKindWithoutValueFailsAndKeepsDiagnostic()
        {
            var element = new EmptyIntegerElement();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.IsFalse(DebugValueSnapshot.TryCapture(element, out var snapshot));
            Assert.IsFalse(snapshot.HasValue);
            Assert.IsTrue(element.HasReadError);
            StringAssert.Contains("Int", element.ReadErrorMessage);
        }

        [Test]
        public void ValueSnapshot_NonValueRowDoesNotClearExistingReadError()
        {
            var element = new FailingNonValueElement();
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.IsFalse(element.TryGetDisplayValueText(out _));
            var diagnostic = element.ReadErrorMessage;

            Assert.IsTrue(DebugValueSnapshot.TryCapture(element, out var snapshot));

            Assert.IsFalse(snapshot.HasValue);
            Assert.IsTrue(element.HasReadError);
            Assert.AreEqual(diagnostic, element.ReadErrorMessage);
        }

        [Test]
        public void SearchAndTextSnapshot_BrokenLabelDoesNotHideHealthyRows()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Test");
            var broken = page.Root.Add(new DebugElement("Broken", "fallback"));
            broken.SetLabelProvider(() => throw new System.InvalidOperationException("label failed"));
            page.Root.Add(new DebugBool("Healthy", true));
            var search = new DebugMenuSearch();
            var results = new FastList<DebugSearchHit>();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*ラベル取得"));
            Assert.DoesNotThrow(() => search.Rebuild(menu));
            search.Query("bro", results);
            Assert.AreEqual(1, results.Count, "静的ラベルへのフォールバックが索引へ入っていない");

            var text = string.Empty;
            Assert.DoesNotThrow(() => text = DebugMenuTextSnapshot.Capture(menu));
            StringAssert.Contains("Test / Broken = fallback", text);
            StringAssert.Contains("Test / Healthy = ON", text);
        }

        [Test]
        public void Color_ShowAlphaFalseForcesOpaque()
        {
            var element = new DebugColor("team", new Color(0.1f, 0.2f, 0.3f, 0.25f));

            element.ShowAlpha = false;
            Assert.AreEqual(1f, element.Value.a);

            element.Value = new Color(0.4f, 0.5f, 0.6f, 0.1f);
            Assert.AreEqual(1f, element.Value.a);
        }

        [Test]
        public void Color_ShowAlphaFalseNormalizesLaterGetterChanges()
        {
            var value = new Color(0.1f, 0.2f, 0.3f, 1f);
            var element = new DebugColor("team", () => value, next => value = next)
            {
                ShowAlpha = false,
            };

            value.a = 0.25f;

            Assert.AreEqual(1f, element.Value.a);
            Assert.AreEqual("#1A334C", element.GetValueText());
            Assert.IsFalse(element.IsModified);
        }

        [Test]
        public void Color_ShowAlphaFailureKeepsPreviousModeAndAllowsRetry()
        {
            var throwOnRead = false;
            var throwOnWrite = false;
            var value = new Color(0.1f, 0.2f, 0.3f, 0.25f);
            var element = new DebugColor(
                "team",
                () => throwOnRead ? throw new InvalidOperationException("alpha getter failed") : value,
                next =>
                {
                    if (throwOnWrite) throw new InvalidOperationException("alpha setter failed");
                    value = next;
                });

            throwOnRead = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            element.ShowAlpha = false;
            Assert.IsTrue(element.ShowAlpha);

            throwOnRead = false;
            throwOnWrite = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            element.ShowAlpha = false;
            Assert.IsTrue(element.ShowAlpha);
            Assert.AreEqual(0.25f, value.a, 0.0001f);

            throwOnWrite = false;
            element.ShowAlpha = false;
            Assert.IsFalse(element.ShowAlpha);
            Assert.AreEqual(1f, value.a, 0.0001f);
            Assert.IsFalse(element.HasError);
        }

        [Test]
        public void Int_AdjustSaturatesAtIntegerBoundsWithoutOverflow()
        {
            var positive = new DebugInt("positive", int.MaxValue - 1).WithStep(int.MaxValue);
            var negative = new DebugInt("negative", int.MinValue + 1).WithStep(int.MaxValue);
            var ranged = new DebugInt("ranged", 9).WithRange(0, 10).WithStep(int.MaxValue);

            positive.OnAdjust(int.MaxValue);
            negative.OnAdjust(int.MinValue);
            ranged.OnAdjust(int.MaxValue);

            Assert.AreEqual(int.MaxValue, positive.Value);
            Assert.AreEqual(int.MinValue, negative.Value);
            Assert.AreEqual(10, ranged.Value);
        }

        [Test]
        public void Color_SetAlpha()
        {
            var element = new DebugColor("team", Color.red);

            element.SetAlpha(0.35f);
            Assert.AreEqual(0.35f, element.Value.a, 0.0001f);

            element.SetAlpha(2f);
            Assert.AreEqual(1f, element.Value.a);
        }

        [Test]
        public void Color_OnDecideRespectsIsExpandable()
        {
            var element = new DebugColor("team", Color.cyan) { IsExpandable = false };

            element.OnDecide();
            Assert.IsFalse(element.IsExpanded);

            element.IsExpandable = true;
            element.OnDecide();
            Assert.IsTrue(element.IsExpanded);
        }

        [Test]
        public void Color_DecideTogglesHsvPanel()
        {
            var element = new DebugColor("team", Color.cyan);

            Assert.IsFalse(element.IsExpanded);

            element.OnDecide();
            Assert.IsTrue(element.IsExpanded);

            element.OnDecide();
            Assert.IsFalse(element.IsExpanded);
        }

        [Test]
        public void Color_ValueAssignmentSynchronizesHsv()
        {
            var element = new DebugColor("team", Color.red);

            element.Value = Color.green;

            Assert.AreEqual(1f / 3f, element.Hue, 0.001f);
            Assert.AreEqual(1f, element.Saturation, 0.001f);
            Assert.AreEqual(1f, element.Brightness, 0.001f);
        }

        [Test]
        public void GraphScale_IgnoresNonFiniteSamples()
        {
            var values = new[] { float.NaN, 2f, float.PositiveInfinity, 5f };
            var index = 0;
            var graph = new DebugGraph("frame", () => values[index++], values.Length);

            for (var i = 0; i < values.Length; i++) graph.Tick(0f);
            graph.GetScale(out var min, out var max);

            Assert.AreEqual(2f, min);
            Assert.AreEqual(5f, max);
        }

        [Test]
        public void PageTick_ProviderExceptionDoesNotStopFollowingRows()
        {
            var shouldThrow = true;
            var healthyCalls = 0;
            var page = new DebugPage("Test");
            var failing = page.Root.Add(new DebugGraph("Broken", () => shouldThrow
                ? throw new System.InvalidOperationException("graph failed")
                : 2f));
            var healthy = page.Root.Add(new DebugGraph("Healthy", () => ++healthyCalls));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*更新"));
            Assert.DoesNotThrow(() => page.Tick(0f));
            Assert.IsTrue(failing.HasReadError);
            Assert.AreEqual(1, healthyCalls, "後続行の更新が止まっている");
            Assert.AreEqual(1, healthy.Samples.Count);

            // 同じ失敗は再度ログへ流さず、後続行だけは引き続き更新する。
            page.Tick(0f);
            Assert.AreEqual(2, healthyCalls);

            shouldThrow = false;
            page.Tick(0f);
            Assert.IsFalse(failing.HasReadError, "取得元が回復してもエラー表示が残っている");
        }

        [Test]
        public void Graph_OnDecideRespectsIsExpandable()
        {
            var graph = new DebugGraph("frame", () => 1f) { IsExpandable = false };

            graph.OnDecide();
            Assert.IsTrue(graph.IsExpanded);

            graph.IsExpandable = true;
            graph.OnDecide();
            Assert.IsFalse(graph.IsExpanded);
        }

        [Test]
        public void Vector_PrefersDecide()
        {
            var value = Vector3.one;
            var vector = DebugVector.Of("position", () => value, next => value = next);

            Assert.IsTrue(vector.PrefersDecide);
            Assert.IsFalse(vector.IsExpanded);

            vector.OnDecide();
            Assert.IsTrue(vector.IsExpanded);
        }

        // ── 入力の解釈 ──────────────────────────────────────────────────────

        [Test]
        public void InputState_ReportsRootPageCommands()
        {
            var previous = new DebugMenuInputState { PreviousPage = true };
            var next = new DebugMenuInputState { NextPage = true };

            Assert.IsTrue(previous.IsHeld(DebugMenuCommand.PreviousPage));
            Assert.IsFalse(previous.IsHeld(DebugMenuCommand.NextPage));
            Assert.IsTrue(next.IsHeld(DebugMenuCommand.NextPage));
            Assert.IsFalse(next.IsHeld(DebugMenuCommand.PreviousPage));
        }

        [Test]
        public void InputRepeater_EmitsRootPageCommand()
        {
            var repeater = new DebugMenuInputRepeater();
            var state = new DebugMenuInputState { NextPage = true };

            Assert.AreEqual(DebugMenuCommand.NextPage, repeater.Poll(state, 0f));
        }

        [Test]
        public void InputRepeater_FiresOnceOnPress()
        {
            var repeater = new DebugMenuInputRepeater();
            var state = new DebugMenuInputState { Down = true };

            Assert.AreEqual(DebugMenuCommand.Down, repeater.Poll(state, 0f));
            Assert.AreEqual(DebugMenuCommand.None, repeater.Poll(state, 0.01f), "待ち時間の前に繰り返している");
        }

        [Test]
        public void InputRepeater_RepeatsAfterInitialDelay()
        {
            var repeater = new DebugMenuInputRepeater { InitialDelay = 0.3f, RepeatInterval = 0.1f };
            var state = new DebugMenuInputState { Down = true };

            repeater.Poll(state, 0f);
            Assert.AreEqual(DebugMenuCommand.None, repeater.Poll(state, 0.2f));
            Assert.AreEqual(DebugMenuCommand.Down, repeater.Poll(state, 0.2f), "待ち時間を過ぎても繰り返さない");
        }

        [Test]
        public void InputRepeater_DecideTakesPrecedenceOverDirection()
        {
            var repeater = new DebugMenuInputRepeater();
            var state = new DebugMenuInputState { Down = true, Decide = true };

            Assert.AreEqual(DebugMenuCommand.Decide, repeater.Poll(state, 0f));
        }

        [Test]
        public void Dispatcher_DeliversCommandToMenu()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Root");
            page.Root.Bool("a", false);
            page.Root.Bool("b", false);

            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.Down);

            Assert.AreEqual(1, page.CursorIndex);
        }

        [Test]
        public void Dispatcher_NullMenuIsSafe()
        {
            Assert.DoesNotThrow(() => DebugMenuCommandDispatcher.Dispatch(null, DebugMenuCommand.NextPage));
        }

        [Test]
        public void Dispatcher_SwitchesRootPages()
        {
            var menu = new DebugMenuRoot();
            var first = menu.AddPage("First");
            var second = menu.AddPage("Second");

            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.NextPage);
            Assert.AreSame(second, menu.CurrentPage);

            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.PreviousPage);
            Assert.AreSame(first, menu.CurrentPage);
        }

        [Test]
        public void Dispatcher_PageUpAndDownStillMoveTenRows()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Root");
            for (var i = 0; i < 21; i++) page.Root.Bool(i.ToString(), false);

            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.PageDown);
            Assert.AreEqual(10, page.CursorIndex);

            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.PageUp);
            Assert.AreEqual(0, page.CursorIndex);
        }

        [Test]
        public void Dispatcher_ResetValueRestoresDefault()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Root");
            var element = page.Root.Int("count", 5).WithRange(0, 10);

            element.Value = 9;
            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.ResetValue);

            Assert.AreEqual(5, element.Value, "既定値に戻っていない");
        }

        [Test]
        public void Dispatcher_CustomResetExceptionIsIsolatedAndRecovers()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Root");
            var element = page.Root.Add(new ThrowingOperationElement { ThrowOnOperation = true });

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.DoesNotThrow(() => DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.ResetValue));
            Assert.IsTrue(element.HasReadError);
            Assert.AreEqual(0, element.ResetCount);

            element.ThrowOnOperation = false;
            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.ResetValue);
            Assert.AreEqual(1, element.ResetCount);
            Assert.IsFalse(element.HasReadError);
        }

        private static T ReadOrThrow<T>(T value, bool shouldThrow)
        {
            if (shouldThrow) throw new InvalidOperationException("initial getter failed");
            return value;
        }

        private static uint ReadPageVersion(DebugMenuRoot menu)
        {
            var field = typeof(DebugMenuRoot).GetField(
                "_pageVersion",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(field);
            return (uint)field.GetValue(menu);
        }

        private static void ExpectValueGetterWarnings(int count)
        {
            for (var i = 0; i < count; i++)
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            }
        }

        private static void ExpectValueSetterWarnings(int count)
        {
            for (var i = 0; i < count; i++)
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            }
        }

        private enum Sparse
        {
            First = 10,
            Second = 25,
            Third = 99,
        }
    }
}
