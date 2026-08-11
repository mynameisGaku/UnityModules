using System.Collections.Generic;
using Containers;
using NUnit.Framework;
using UnityEngine;

namespace DebugMenu.Tests
{
    /// <summary>
    /// 行・ページ・移動の検証。
    /// UI も入力デバイスも使わずに済むよう層を分けてあるので、ここで挙動を確定できる。
    /// </summary>
    public sealed class DebugMenuCoreTests
    {
        // ── 平坦化 ──────────────────────────────────────────────────────────

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

        private enum Sparse
        {
            First = 10,
            Second = 25,
            Third = 99,
        }
    }
}
