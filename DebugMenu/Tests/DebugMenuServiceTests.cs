using System.Collections.Generic;
using System.Reflection;
using Containers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DebugMenu.Tests
{
    /// <summary>検索・お気に入り・取り消し・設定保存の検証。</summary>
    public sealed class DebugMenuServiceTests
    {
        /// <summary>テスト用の保管庫。ファイルにも PlayerPrefs にも触らない。</summary>
        private sealed class MemoryStorage : IDebugMenuStorage
        {
            private readonly Dictionary<string, string> _entries = new Dictionary<string, string>();

            public string Load(string key) => _entries.TryGetValue(key, out var value) ? value : null;
            public void Save(string key, string value) => _entries[key] = value;
            public void Delete(string key) => _entries.Remove(key);
        }

        /// <summary>既定値へ戻した回数を数える借用行。</summary>
        private sealed class CountingResetElement : DebugElement
        {
            public CountingResetElement() : base("Reset Counter")
            {
                IsExpandable = false;
            }

            public int ResetCount { get; private set; }
            public override DebugValueKind ValueKind => DebugValueKind.Int;
            public override void ResetToDefault() => ResetCount++;
        }

        /// <summary>履歴の読取経路だけを失敗させられる値行。</summary>
        /// <summary>独自行の初期化例外が後続行へ波及しないことを調べる。</summary>
        private sealed class ThrowingResetElement : DebugElement
        {
            public ThrowingResetElement() : base("Throwing Reset") { }

            public override DebugValueKind ValueKind => DebugValueKind.Int;
            public override void ResetToDefault() => throw new System.InvalidOperationException("reset failed");
        }

        /// <summary>保存対象判定または値種別の取得を失敗させる独自行。</summary>
        private sealed class ThrowingMetadataElement : DebugElement
        {
            private int _value = 1;

            public ThrowingMetadataElement(string label) : base(label) { }

            public bool ThrowOnSaveable { get; set; }
            public bool ThrowOnKind { get; set; }
            public override bool IsSaveable => ThrowOnSaveable
                ? throw new System.InvalidOperationException("saveable metadata failed")
                : true;
            public override DebugValueKind ValueKind => ThrowOnKind
                ? throw new System.InvalidOperationException("kind metadata failed")
                : DebugValueKind.Int;
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

            public int RawValue => _value;

            public void Change(int value)
            {
                _value = value;
                NotifyChanged();
            }
        }

        /// <summary>メタデータ取得中に別の値変更を起こせる行。</summary>
        private sealed class ReentrantMetadataElement : DebugElement
        {
            private System.Action _onMetadata;

            public ReentrantMetadataElement(string label, System.Action onMetadata = null) : base(label)
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

        private sealed class ThrowingHistoryElement : DebugElement
        {
            private int _value;

            public ThrowingHistoryElement(string label, int value) : base(label)
            {
                _value = value;
                IsExpandable = false;
            }

            public bool ThrowOnRead { get; set; }
            public bool RejectWrites { get; set; }
            public int RawValue => _value;
            public override DebugValueKind ValueKind => DebugValueKind.Int;

            public override bool TryGetInt(out int value)
            {
                if (ThrowOnRead) throw new System.InvalidOperationException("history getter failed");
                value = _value;
                return true;
            }

            public override bool TrySetInt(int value)
            {
                if (RejectWrites) return false;

                _value = value;
                NotifyChanged();
                return true;
            }

            public void ChangeWithoutReading(int value)
            {
                _value = value;
                NotifyChanged();
            }
        }

        /// <summary>値取得中に一度だけ別処理を呼び出せる履歴用の値行。</summary>
        private sealed class ReentrantHistoryElement : DebugElement
        {
            private int _value;
            private System.Action _onRead;

            public ReentrantHistoryElement(string label, int value) : base(label)
            {
                _value = value;
                IsExpandable = false;
            }

            public int RawValue => _value;
            public override DebugValueKind ValueKind => DebugValueKind.Int;

            public override bool TryGetInt(out int value)
            {
                var action = _onRead;
                _onRead = null;
                action?.Invoke();
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

            public void InvokeOnNextRead(System.Action action) => _onRead = action;
        }

        [TearDown]
        public void TearDown()
        {
            // 変更の受け取り口はメニュー全体で 1 つしか無い。
            // 外し忘れると次のテストへ漏れる。
            DebugElement.SetChangeListener(null);
        }

        // ── お気に入り ──────────────────────────────────────────────────────

        [Test]
        public void Favorites_DoesNotBreakOriginalPagePath()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");

            DebugBool element = null;
            page.Group("Player", g => element = g.Bool("Invincible", false));

            var expectedKey = element.ResolveSaveKey();
            element.SetFavorite(true);

            var favorites = new DebugMenuFavorites();
            favorites.Rebuild(menu);

            Assert.AreEqual(expectedKey, element.ResolveSaveKey(), "お気に入りに集めたら保存キーが変わった");
            Assert.AreEqual(1, favorites.Count);
        }

        [Test]
        public void Favorites_SharesTheSameElementInstance()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var element = page.Root.Bool("flag", false);
            element.SetFavorite(true);

            var favorites = new DebugMenuFavorites();
            favorites.Rebuild(menu);

            // お気に入り側の行を触ると、元の行も変わるのが正しい。
            var mirrored = favorites.Page.Root.Children[0].Children[0];
            mirrored.OnDecide();

            Assert.IsTrue(element.Value, "写しになっていて元へ反映されない");
        }

        [Test]
        public void Favorites_RebuildsOnlyWhenPinningChanges()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var element = page.Root.Bool("flag", false);

            var favorites = new DebugMenuFavorites();
            favorites.Rebuild(menu);

            Assert.IsFalse(favorites.SyncIfDirty(menu), "変化が無いのに組み直している");

            element.SetFavorite(true);
            Assert.IsTrue(favorites.SyncIfDirty(menu), "留めたのに組み直していない");
        }

        [Test]
        public void Favorites_DetachLeavesOriginalPageIntact()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var element = page.Root.Bool("flag", false);
            element.SetFavorite(true);

            var favorites = new DebugMenuFavorites();
            favorites.Rebuild(menu);
            favorites.Detach();

            Assert.AreEqual(1, page.Root.Children.Count, "元ページから行が消えている");
            Assert.AreSame(page.Root, element.Parent, "親が奪われたままになっている");
        }

        // ── 検索 ────────────────────────────────────────────────────────────

        [Test]
        public void Search_MatchesWordPrefix()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            page.Root.Float("Move Speed", () => 0f, _ => { });
            page.Root.Bool("Invincible", false);

            var search = new DebugMenuSearch();
            search.Rebuild(menu);

            using var hits = TempList<DebugSearchHit>.Rent();
            search.Query("speed", hits.List);

            Assert.AreEqual(1, hits.List.Count, "後ろの語から引けていない");
            Assert.AreEqual("Move Speed", hits.List[0].Element.Label);
        }

        [Test]
        public void Search_DoesNotReturnDuplicates()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            page.Root.Bool("Fire Fire", false);   // 同じ語が 2 回出る

            var search = new DebugMenuSearch();
            search.Rebuild(menu);

            using var hits = TempList<DebugSearchHit>.Rent();
            search.Query("fire", hits.List);

            Assert.AreEqual(1, hits.List.Count, "同じ行が複数回返っている");
        }

        [Test]
        public void Search_SkipsGroupHeadings()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            page.Group("Player", g => g.Bool("Player Health", false));

            var search = new DebugMenuSearch();
            search.Rebuild(menu);

            using var hits = TempList<DebugSearchHit>.Rent();
            search.Query("player", hits.List);

            Assert.AreEqual(1, hits.List.Count, "見出しまで検索結果に出ている");
            Assert.AreEqual("Player Health", hits.List[0].Element.Label);
        }

        [Test]
        public void Search_EmptyQueryReturnsNothing()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay").Root.Bool("flag", false);

            var search = new DebugMenuSearch();
            search.Rebuild(menu);

            using var hits = TempList<DebugSearchHit>.Rent();
            search.Query("  ", hits.List);

            Assert.AreEqual(0, hits.List.Count);
        }

        // ── 取り消し ────────────────────────────────────────────────────────

        [Test]
        public void History_UndoRestoresPreviousValue()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var element = page.Root.Int("count", 5).WithRange(0, 100);

            using var history = new DebugMenuHistory();
            history.Attach(menu);

            element.Value = 42;

            Assert.IsTrue(history.CanUndo, "変更が控えられていない");
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(5, element.Value, "元の値に戻っていない");
        }

        [Test]
        public void History_RedoReappliesChange()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var element = page.Root.Int("count", 5).WithRange(0, 100);

            using var history = new DebugMenuHistory();
            history.Attach(menu);

            element.Value = 42;
            history.Undo();

            Assert.IsTrue(history.Redo());
            Assert.AreEqual(42, element.Value);
        }

        [Test]
        public void History_FailedUndoKeepsSourceStackAndCanRetry()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Add(new ThrowingHistoryElement("count", 1));
            using var history = new DebugMenuHistory();
            history.Attach(menu);
            element.ChangeWithoutReading(2);
            element.RejectWrites = true;

            Assert.IsFalse(history.Undo());
            Assert.AreEqual(2, element.RawValue);
            Assert.AreEqual(1, history.Count);
            Assert.IsTrue(history.CanUndo);
            Assert.IsFalse(history.CanRedo);
            Assert.AreEqual("count", history.NextUndoLabel);
            Assert.IsNull(history.NextRedoLabel);

            element.RejectWrites = false;
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(1, element.RawValue);
            Assert.AreEqual(0, history.Count);
            Assert.IsTrue(history.CanRedo);
            Assert.AreEqual("count", history.NextRedoLabel);
        }

        [Test]
        public void History_FailedRedoKeepsSourceStackAndCanRetry()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Add(new ThrowingHistoryElement("count", 1));
            using var history = new DebugMenuHistory();
            history.Attach(menu);
            element.ChangeWithoutReading(2);
            Assert.IsTrue(history.Undo());
            element.RejectWrites = true;

            Assert.IsFalse(history.Redo());
            Assert.AreEqual(1, element.RawValue);
            Assert.AreEqual(0, history.Count);
            Assert.IsFalse(history.CanUndo);
            Assert.IsTrue(history.CanRedo);
            Assert.IsNull(history.NextUndoLabel);
            Assert.AreEqual("count", history.NextRedoLabel);

            element.RejectWrites = false;
            Assert.IsTrue(history.Redo());
            Assert.AreEqual(2, element.RawValue);
            Assert.AreEqual(1, history.Count);
            Assert.IsFalse(history.CanRedo);
            Assert.AreEqual("count", history.NextUndoLabel);
        }

        [Test]
        public void History_UndoIsNotItselfRecorded()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var element = page.Root.Int("count", 0).WithRange(0, 100);

            using var history = new DebugMenuHistory();
            history.Attach(menu);

            element.Value = 1;
            element.Value = 2;
            Assert.AreEqual(2, history.Count);

            history.Undo();
            Assert.AreEqual(1, history.Count, "取り消しの操作まで履歴に積まれている");
        }

        [Test]
        public void History_UndoRestoresTextValue()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var element = page.Root.Add(new DebugText("name", "初期"));

            using var history = new DebugMenuHistory();
            history.Attach(menu);

            element.Value = "変更後";
            history.Undo();

            Assert.AreEqual("初期", element.Value);
        }

        [Test]
        public void History_CapacityEvictsOldestChange()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Int("count", 0);
            using var history = new DebugMenuHistory(2);
            history.Attach(menu);

            element.Value = 1;
            element.Value = 2;
            element.Value = 3;

            Assert.AreEqual(2, history.Count);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(2, element.Value);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(1, element.Value);
            Assert.IsFalse(history.Undo(), "上限を超えた最古の変更が残っている");
        }

        [Test]
        public void History_NewChangeAfterUndoClearsRedoBranch()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Int("count", 0);
            using var history = new DebugMenuHistory();
            history.Attach(menu);

            element.Value = 1;
            element.Value = 2;
            Assert.IsTrue(history.Undo());
            Assert.IsTrue(history.CanRedo);

            element.Value = 7;

            Assert.IsFalse(history.CanRedo, "分岐後も古いやり直しが残っている");
            Assert.IsFalse(history.Redo());
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(1, element.Value);
        }

        [Test]
        public void History_LabelAndClearReflectCurrentBranches()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Int("Enemy Count", 0);
            using var history = new DebugMenuHistory();
            history.Attach(menu);

            element.Value = 4;

            Assert.AreEqual("Enemy Count", history.NextUndoLabel);
            history.Undo();
            Assert.IsTrue(history.CanRedo);

            history.Clear();

            Assert.AreEqual(0, history.Count);
            Assert.IsFalse(history.CanUndo);
            Assert.IsFalse(history.CanRedo);
            Assert.IsNull(history.NextUndoLabel);
        }

        [Test]
        public void History_ClearReseedsCurrentValueAndDropsDeferredWork()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Int("count", 0);
            using var history = new DebugMenuHistory();
            history.Attach(menu);
            element.Value = 1;

            Assert.DoesNotThrow(history.Clear);
            Assert.AreEqual(0, history.Count);
            element.Value = 2;

            Assert.AreEqual(1, history.Count);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(1, element.Value, "Clear完了時の値を基準にしていない");
        }

        [Test]
        public void History_ClearReseedsRowsChangedByOtherGettersAtFinalValues()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            var first = root.Add(new ReentrantHistoryElement("first", 0));
            var second = root.Add(new ReentrantHistoryElement("second", 0));
            using var history = new DebugMenuHistory();
            history.Attach(menu);
            first.Change(9);
            Assert.IsTrue(history.Undo());
            Assert.IsTrue(history.CanRedo);

            first.InvokeOnNextRead(() => second.Change(1));
            second.InvokeOnNextRead(() => first.Change(1));

            Assert.DoesNotThrow(history.Clear);
            Assert.IsFalse(history.CanUndo, "Clear中のGetterが起こした変更を履歴へ積んでいる");
            Assert.IsFalse(history.CanRedo, "Clear前のやり直し枝が残っている");
            first.Change(2);
            second.Change(2);

            Assert.IsTrue(history.Undo());
            Assert.AreEqual(1, second.RawValue);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(1, first.RawValue);
            Assert.IsFalse(history.Undo(), "Clear前の履歴へ戻れている");
        }

        [Test]
        public void History_ClearDefersGetterRefreshAndTracksFinalOwnedRows()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            var first = root.Add(new ReentrantHistoryElement("first", 0));
            var second = root.Add(new ReentrantHistoryElement("second", 0));
            using var history = new DebugMenuHistory();
            history.Attach(menu);
            DebugInt added = null;
            System.Action addAndRefresh = () =>
            {
                if (added != null) return;

                added = root.Int("late", 5);
                history.Refresh();
            };
            first.InvokeOnNextRead(addAndRefresh);
            second.InvokeOnNextRead(addAndRefresh);

            Assert.DoesNotThrow(history.Clear);
            Assert.NotNull(added);
            added.Value = 6;

            Assert.IsTrue(history.Undo());
            Assert.AreEqual(5, added.Value);
            Assert.IsFalse(history.Undo());
        }

        [Test]
        public void History_DisposeDoesNotReadThrowingGetterAgain()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Add(new ThrowingHistoryElement("count", 0));
            var history = new DebugMenuHistory();
            history.Attach(menu);
            element.ThrowOnRead = true;

            Assert.DoesNotThrow(history.Dispose);
        }

        [Test]
        public void History_RejectsNonPositiveCapacity()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new DebugMenuHistory(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new DebugMenuHistory(-1));
        }

        [Test]
        public void History_MultipleInstancesDetachIndependently()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Int("count", 0);

            using var first = new DebugMenuHistory();
            using var second = new DebugMenuHistory();
            first.Attach(menu);
            second.Attach(menu);

            element.Value = 1;
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(1, second.Count);

            first.Detach();
            element.Value = 2;

            Assert.AreEqual(1, first.Count, "外した履歴へ変更が届いている");
            Assert.AreEqual(2, second.Count, "別の履歴を外したときに受け取りまで切れている");
        }

        [Test]
        public void History_IgnoresElementsOwnedByAnotherMenu()
        {
            var ownMenu = new DebugMenuRoot();
            var ownElement = ownMenu.AddPage("Own").Root.Int("count", 0);
            var otherMenu = new DebugMenuRoot();
            var otherElement = otherMenu.AddPage("Other").Root.Int("count", 0);

            using var history = new DebugMenuHistory();
            history.Attach(ownMenu);

            otherElement.Value = 1;
            otherElement.Value = 2;
            Assert.AreEqual(0, history.Count, "別メニューの変更が履歴へ混ざっている");

            ownElement.Value = 1;
            Assert.AreEqual(1, history.Count, "接続したメニューの変更まで除外されている");
        }

        [Test]
        public void History_RefreshTracksElementsAddedAfterAttach()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            using var history = new DebugMenuHistory();
            history.Attach(menu);

            var element = page.Root.Int("late", 3);
            history.Refresh();
            element.Value = 7;

            Assert.IsTrue(history.Undo());
            Assert.AreEqual(3, element.Value, "後から足した行の初期値へ戻っていない");
        }

        [Test]
        public void History_ReattachClearsCommandsFromPreviousMenu()
        {
            var firstMenu = new DebugMenuRoot();
            var firstElement = firstMenu.AddPage("First").Root.Int("count", 0);
            var secondMenu = new DebugMenuRoot();
            var secondElement = secondMenu.AddPage("Second").Root.Int("count", 10);
            using var history = new DebugMenuHistory();

            history.Attach(firstMenu);
            firstElement.Value = 1;
            Assert.IsTrue(history.CanUndo);

            history.Attach(secondMenu);
            Assert.IsFalse(history.CanUndo, "前のメニューの履歴が接続先変更後も残っている");

            secondElement.Value = 11;
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(10, secondElement.Value);
            Assert.AreEqual(1, firstElement.Value, "前のメニューまで書き換えた");
        }

        [Test]
        public void History_AttachSkipsThrowingGetter()
        {
            var menu = new DebugMenuRoot();
            var broken = menu.AddPage("Gameplay").Root.Add(new ThrowingHistoryElement("broken", 1));
            broken.ThrowOnRead = true;
            using var history = new DebugMenuHistory();

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(() => history.Attach(menu));
            Assert.AreEqual(0, history.Count);
        }

        [Test]
        public void History_PendingAttachBaselineRecoversWithoutStructureChange()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Add(new ThrowingHistoryElement("broken", 1));
            element.ThrowOnRead = true;
            using var history = new DebugMenuHistory();

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            history.Attach(menu);
            element.ThrowOnRead = false;

            history.Refresh();
            element.ChangeWithoutReading(2);

            Assert.IsTrue(history.Undo());
            Assert.AreEqual(1, element.RawValue);
        }

        [Test]
        public void History_FirstHealthyNotificationSeedsPendingBaselineBeforeNextChange()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Add(new ThrowingHistoryElement("broken", 1));
            element.ThrowOnRead = true;
            using var history = new DebugMenuHistory();

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            history.Attach(menu);
            element.ThrowOnRead = false;

            element.ChangeWithoutReading(2);
            Assert.IsFalse(history.CanUndo, "回復を検知した通知自体を不正な履歴として積んでいる");

            element.ChangeWithoutReading(3);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(2, element.RawValue);
        }

        [Test]
        public void History_RefreshSkipsNewThrowingGetterAndKeepsHealthyRows()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var healthy = page.Root.Int("healthy", 2);
            using var history = new DebugMenuHistory();
            history.Attach(menu);

            var broken = page.Root.Add(new ThrowingHistoryElement("broken", 1));
            broken.ThrowOnRead = true;
            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(history.Refresh);

            healthy.Value = 3;
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(2, healthy.Value);
        }

        [Test]
        public void History_ChangeNotificationSkipsThrowingGetter()
        {
            var menu = new DebugMenuRoot();
            var broken = menu.AddPage("Gameplay").Root.Add(new ThrowingHistoryElement("broken", 1));
            using var history = new DebugMenuHistory();
            history.Attach(menu);
            broken.ThrowOnRead = true;

            UnityEngine.TestTools.LogAssert.Expect(
                LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(() => broken.ChangeWithoutReading(2));
            Assert.AreEqual(0, history.Count);
        }

        [Test]
        public void History_GetterFailureKeepsHealthyCommandsAndSeedsRecoveredValue()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Add(new ThrowingHistoryElement("count", 1));
            using var history = new DebugMenuHistory();
            history.Attach(menu);
            element.ChangeWithoutReading(2);
            element.ThrowOnRead = true;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            element.ChangeWithoutReading(3);
            Assert.AreEqual(1, history.Count);

            element.ThrowOnRead = false;
            history.Refresh();
            element.ChangeWithoutReading(4);
            Assert.AreEqual(2, history.Count);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(3, element.RawValue);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(1, element.RawValue);
        }

        [Test]
        public void History_MetadataFailureKeepsCommandsAndRecoversWithNewBaseline()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Add(new ThrowingMetadataElement("count"));
            using var history = new DebugMenuHistory();
            history.Attach(menu);
            element.Change(2);
            element.ThrowOnSaveable = true;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*履歴対象確認"));
            element.Change(3);
            Assert.AreEqual(1, history.Count);

            element.ThrowOnSaveable = false;
            history.Refresh();
            element.Change(4);
            Assert.AreEqual(2, history.Count);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(3, element.RawValue);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(1, element.RawValue);
        }

        [Test]
        public void History_PrunesRemovedCommandsFromUndoAndRedo()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            var healthy = root.Int("healthy", 0);
            var removed = root.Int("removed", 0);
            using var history = new DebugMenuHistory();
            history.Attach(menu);
            healthy.Value = 1;
            removed.Value = 1;
            Assert.IsTrue(history.Undo());
            Assert.IsTrue(history.CanRedo);

            Assert.IsTrue(root.Remove(removed));
            history.Refresh();

            Assert.IsFalse(history.CanRedo);
            Assert.AreEqual("healthy", history.NextUndoLabel);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(0, healthy.Value);
        }

        [TestCase(DebugAttachMode.Page)]
        [TestCase(DebugAttachMode.Inline)]
        public void History_TracksRowsAddedToInitiallyEmptyLinkedPage(DebugAttachMode mode)
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var target = new DebugPage("Target");
            root.AddChildPage(target, mode);
            using var history = new DebugMenuHistory();
            history.Attach(menu);

            var added = target.Root.Int("late", 1);
            history.Refresh();
            added.Value = 2;

            Assert.IsTrue(history.Undo());
            Assert.AreEqual(1, added.Value);
        }

        [Test]
        public void History_TracksPrebuiltPageAddedAfterAttach()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("Root");
            var latePage = new DebugPage("Late");
            var element = latePage.Root.Int("late", 1);
            using var history = new DebugMenuHistory();
            history.Attach(menu);

            menu.AddPage(latePage);
            history.Refresh();
            element.Value = 2;

            Assert.IsTrue(history.Undo());
            Assert.AreEqual(1, element.Value);
        }

        [Test]
        public void History_PageRemovalPrunesCommandsAndReaddStartsFromCurrentValue()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var element = page.Root.Int("count", 0);
            using var history = new DebugMenuHistory();
            history.Attach(menu);
            element.Value = 1;
            Assert.IsTrue(history.CanUndo);

            Assert.IsTrue(menu.RemovePage(page));
            history.Refresh();
            Assert.IsFalse(history.CanUndo);

            element.Value = 2;
            Assert.IsFalse(history.CanUndo);
            menu.AddPage(page);
            history.Refresh();
            Assert.IsFalse(history.CanUndo, "再登録で削除前の履歴が復活している");

            element.Value = 3;
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(2, element.Value);
        }

        [Test]
        public void History_PageRemovalKeepsRowsReachableThroughPageLink()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var linked = menu.AddPage("Linked");
            var element = linked.Root.Int("count", 0);
            root.AddChildPage(linked);
            using var history = new DebugMenuHistory();
            history.Attach(menu);
            element.Value = 1;

            Assert.IsTrue(menu.RemovePage(linked));
            history.Refresh();

            Assert.IsTrue(history.Undo());
            Assert.AreEqual(0, element.Value);
        }

        [Test]
        public void History_ReparentAcrossMenusPrunesOldOwnerAndTracksNewOwner()
        {
            var firstMenu = new DebugMenuRoot();
            var firstRoot = firstMenu.AddPage("First").Root;
            var secondMenu = new DebugMenuRoot();
            var secondRoot = secondMenu.AddPage("Second").Root;
            var element = firstRoot.Int("moved", 0);
            using var firstHistory = new DebugMenuHistory();
            using var secondHistory = new DebugMenuHistory();
            firstHistory.Attach(firstMenu);
            secondHistory.Attach(secondMenu);
            element.Value = 1;

            secondRoot.Add(element);
            firstHistory.Refresh();
            secondHistory.Refresh();
            element.Value = 2;

            Assert.AreEqual(0, firstRoot.Children.Count);
            Assert.IsFalse(firstHistory.CanUndo);
            Assert.IsTrue(secondHistory.Undo());
            Assert.AreEqual(1, element.Value);
        }

        [Test]
        public void History_ReentrantRefreshCoalescesSameRowAtLastNotificationPosition()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            var trigger = root.Add(new ReentrantMetadataElement("trigger"));
            var first = root.Int("first", 0);
            var second = root.Int("second", 0);
            using var history = new DebugMenuHistory();
            history.Attach(menu);
            trigger.SetAction(() =>
            {
                first.Value = 1;
                second.Value = 1;
                first.Value = 2;
            });
            root.Add(new DebugElement("late"));

            Assert.DoesNotThrow(history.Refresh);
            Assert.AreEqual(2, history.Count);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(0, first.Value);
            Assert.IsTrue(history.Undo());
            Assert.AreEqual(0, second.Value);
            Assert.IsFalse(history.Undo());
        }

        [Test]
        public void ChangeNotification_ThrowingInstanceObserverDoesNotBlockLaterObserversOrServices()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Int("count", 0);
            using var recent = new DebugMenuRecentChanges();
            using var history = new DebugMenuHistory();
            recent.Attach(menu);
            history.Attach(menu);

            var laterObserverCount = 0;
            element.Changed += () => throw new System.InvalidOperationException("instance observer failed");
            element.Changed += () => laterObserverCount++;

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*変更通知先.*行イベント"));
            Assert.DoesNotThrow(() => element.Value = 1);
            Assert.DoesNotThrow(() => element.Value = 2);

            Assert.AreEqual(2, laterObserverCount);
            Assert.AreEqual(2, history.Count);
            Assert.AreEqual(1, recent.Count);
            Assert.IsFalse(element.HasReadError);
        }

        [Test]
        public void ChangeNotification_ThrowingServiceObserverDoesNotBlockFollowingService()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Int("count", 0);
            using var recent = new DebugMenuRecentChanges();
            using var history = new DebugMenuHistory();
            recent.Attach(menu);
            recent.Changed += () => throw new System.InvalidOperationException("recent observer failed");
            history.Attach(menu);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*変更通知先.*サービスリスナー"));
            Assert.DoesNotThrow(() => element.Value = 1);

            Assert.AreEqual(1, recent.Count);
            Assert.AreEqual(1, history.Count);
            Assert.IsFalse(element.HasReadError);
        }

        [Test]
        public void ChangeNotification_ThrowingCompatibilityListenerDoesNotBlockServices()
        {
            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Int("count", 0);
            using var recent = new DebugMenuRecentChanges();
            using var history = new DebugMenuHistory();
            DebugElement.SetChangeListener(_ => throw new System.InvalidOperationException("listener failed"));
            recent.Attach(menu);
            history.Attach(menu);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*変更通知先.*互換リスナー"));
            Assert.DoesNotThrow(() => element.Value = 1);

            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(1, recent.Count);
            Assert.IsFalse(element.HasReadError);
        }

        // ── 設定の保存と復元 ────────────────────────────────────────────────

        [Test]
        public void Settings_SavedValuesAreRestored()
        {
            var storage = new MemoryStorage();

            var saved = new DebugMenuRoot();
            var savedPage = saved.AddPage("Gameplay");
            var savedFlag = savedPage.Root.Bool("flag", false);
            var savedCount = savedPage.Root.Int("count", 0).WithRange(0, 100);

            savedFlag.Value = true;
            savedCount.Value = 42;

            new DebugMenuSettings(storage).Save(saved);

            // 別に組み直したメニューへ戻す。実際の起動と同じ経路。
            var loaded = new DebugMenuRoot();
            var loadedPage = loaded.AddPage("Gameplay");
            var loadedFlag = loadedPage.Root.Bool("flag", false);
            var loadedCount = loadedPage.Root.Int("count", 0).WithRange(0, 100);

            var applied = new DebugMenuSettings(storage).Load(loaded);

            Assert.AreEqual(2, applied);
            Assert.IsTrue(loadedFlag.Value);
            Assert.AreEqual(42, loadedCount.Value);
        }

        [Test]
        public void Settings_DoesNotApplyToChangedKind()
        {
            var storage = new MemoryStorage();

            var saved = new DebugMenuRoot();
            saved.AddPage("Gameplay").Root.Int("value", 0).WithRange(0, 100).Value = 42;
            new DebugMenuSettings(storage).Save(saved);

            // 同じキーだが型を変えたメニュー。
            var loaded = new DebugMenuRoot();
            var element = loaded.AddPage("Gameplay").Root.Bool("value", false);

            new DebugMenuSettings(storage).Load(loaded);

            Assert.IsFalse(element.Value, "型の違う値が押し込まれている");
        }

        [Test]
        public void Settings_CorruptDataDoesNotThrow()
        {
            var storage = new MemoryStorage();
            storage.Save("debug-menu-settings", "{ これは JSON ではない");

            var menu = new DebugMenuRoot();
            var element = menu.AddPage("Gameplay").Root.Bool("flag", false);

            // 例外が出ればここで止まる。DoesNotThrow で包まないのは、
            // 包むと Unity 外での実行時制約まで「テスト失敗」に化けるため。
            var applied = new DebugMenuSettings(storage).Load(menu);

            Assert.AreEqual(0, applied, "壊れた保存から値が入ってしまっている");
            Assert.IsFalse(element.Value);
        }

        [Test]
        public void Settings_SkipsNonSaveableElements()
        {
            var storage = new MemoryStorage();

            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            page.Root.Action("実行", () => { });
            page.Root.Watch("監視", () => 1f);
            page.Root.Bool("flag", false);

            var count = new DebugMenuSettings(storage).Save(menu);

            Assert.AreEqual(1, count, "値を持たない行まで保存されている");
        }

        [Test]
        public void Settings_ExplicitSaveKeySurvivesRelocation()
        {
            var storage = new MemoryStorage();

            var saved = new DebugMenuRoot();
            saved.AddPage("A").Root.Bool("flag", false).WithSaveKey("shared.flag").Value = true;
            new DebugMenuSettings(storage).Save(saved);

            // ページも見出しも変えた構成へ戻す。
            var loaded = new DebugMenuRoot();
            DebugBool element = null;
            loaded.AddPage("B").Group("別の見出し", g => element = g.Bool("名前も違う", false).WithSaveKey("shared.flag"));

            new DebugMenuSettings(storage).Load(loaded);

            Assert.IsTrue(element.Value, "明示したキーで復元できていない");
        }

        [Test]
        public void Settings_ApplyVisitsBorrowedElementOnlyOnce()
        {
            var menu = new DebugMenuRoot();
            var original = menu.AddPage("Gameplay");
            var borrowed = menu.AddPage("Recent");
            var element = original.Root.Int("count", 0).WithSaveKey("shared.count");
            borrowed.Root.AddBorrowed(element);
            var data = new DebugMenuSettingsData();
            data.Keys.Add("shared.count");
            data.Values.Add("42");
            data.Kinds.Add((int)DebugValueKind.Int);

            var applied = DebugMenuSettings.Apply(menu, data);

            Assert.AreEqual(1, applied, "借用表示した同じ行を複数件として数えている");
            Assert.AreEqual(42, element.Value);
        }

        [Test]
        public void Settings_ResetAllVisitsBorrowedElementOnlyOnce()
        {
            var menu = new DebugMenuRoot();
            var original = menu.AddPage("Gameplay");
            var borrowed = menu.AddPage("Recent");
            var element = original.Root.Add(new CountingResetElement());
            borrowed.Root.AddBorrowed(element);

            var result = DebugMenuSettings.ResetAll(menu);

            Assert.AreEqual(1, element.ResetCount, "借用表示した同じ行を複数回初期化している");
            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual(1, result.SucceededCount);
            Assert.AreEqual(0, result.FailedCount);
        }

        [Test]
        public void Settings_ResetAllRestoresDefaults()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var flag = page.Root.Bool("flag", false);
            var count = page.Root.Int("count", 5).WithRange(0, 100);

            flag.Value = true;
            count.Value = 99;

            var result = DebugMenuSettings.ResetAll(menu);

            Assert.IsFalse(flag.Value);
            Assert.AreEqual(5, count.Value);
            Assert.AreEqual(2, result.TotalCount);
            Assert.AreEqual(2, result.SucceededCount);
            Assert.AreEqual(0, result.FailedCount);
        }

        // ── 値の写し ────────────────────────────────────────────────────────

        [Test]
        public void Snapshot_RefusesMismatchedKind()
        {
            var source = new DebugInt("count", 42);
            var target = new DebugFloat("speed", 1f);

            var snapshot = DebugValueSnapshot.Capture(source);

            Assert.IsFalse(snapshot.Apply(target), "種類が違うのに書き戻している");
            Assert.AreEqual(1f, target.Value);
        }

        [Test]
        public void Snapshot_SurvivesStringRoundTrip()
        {
            var element = new DebugFloat("speed", 1f / 3f);
            var snapshot = DebugValueSnapshot.Capture(element);

            Assert.IsTrue(DebugValueSnapshot.TryParse(DebugValueKind.Float, snapshot.ToStorageString(), out var restored));

            var target = new DebugFloat("speed", 0f);
            Assert.IsTrue(restored.Apply(target));
            Assert.AreEqual(1f / 3f, target.Value, 1e-7f, "保存を経由して精度が落ちている");
        }

        [Test]
        public void Vector_SnapshotRoundTrips()
        {
            var value = new Vector3(1.5f, -2.25f, 3f);
            var element = DebugVector.Of("pos", () => value, v => value = v);

            var snapshot = DebugValueSnapshot.Capture(element);
            Assert.IsTrue(snapshot.HasValue, "Vector が控えられていない");

            value = Vector3.zero;
            Assert.IsTrue(snapshot.Apply(element));
            Assert.AreEqual(new Vector3(1.5f, -2.25f, 3f), value);
        }
        [Test]
        public void Vector_SettingsAndHistoryTrackOnlyParentValue()
        {
            var value = new Vector3(1f, 2f, 3f);
            var menu = new DebugMenuRoot();
            var vector = DebugVector.Of("Position", () => value, next => value = next);
            menu.AddPage("Gameplay").Root.Add(vector);

            var data = DebugMenuSettings.Capture(menu);

            Assert.AreEqual(1, data.Keys.Count, "Vector親と成分を重複保存している");
            Assert.AreEqual(vector.ResolveSaveKey(), data.Keys[0]);
            for (var i = 0; i < vector.Children.Count; i++) Assert.IsFalse(vector.Children[i].IsSaveable);

            using var history = new DebugMenuHistory();
            history.Attach(menu);
            Assert.IsTrue(((DebugFloat)vector.Children[0]).TrySetFloat(8f));
            Assert.AreEqual(1, history.Count, "Vector親と成分を重複して履歴へ積んでいる");
        }

        [Test]
        public void Settings_ApplyContinuesAfterSetterFailure()
        {
            var failingValue = 1;
            var healthyValue = 2;
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            var failing = root.Add(new DebugInt(
                "Failing",
                () => failingValue,
                _ => throw new System.InvalidOperationException("settings setter failed"))
                .WithSaveKey("failing"));
            root.Add(new DebugInt("Healthy", () => healthyValue, value => healthyValue = value).WithSaveKey("healthy"));
            var data = new DebugMenuSettingsData();
            data.Keys.Add("failing");
            data.Values.Add("10");
            data.Kinds.Add((int)DebugValueKind.Int);
            data.Keys.Add("healthy");
            data.Values.Add("20");
            data.Kinds.Add((int)DebugValueKind.Int);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            var applied = 0;
            Assert.DoesNotThrow(() => applied = DebugMenuSettings.Apply(menu, data));

            Assert.AreEqual(1, applied, "失敗した行を適用件数へ含めたか、後続行へ進んでいない");
            Assert.AreEqual(1, failingValue);
            Assert.AreEqual(20, healthyValue);
            Assert.IsTrue(failing.HasReadError);
        }

        [Test]
        public void Settings_ApplySkipsInvalidEntriesAndAppliesLaterValidValue()
        {
            var menu = new DebugMenuRoot();
            var healthy = menu.AddPage("Gameplay").Root.Int("Healthy", 1).WithSaveKey("healthy");
            var data = new DebugMenuSettingsData();
            data.Keys.Add(null);
            data.Values.Add("8");
            data.Kinds.Add((int)DebugValueKind.Int);
            data.Keys.Add("   ");
            data.Values.Add("9");
            data.Kinds.Add((int)DebugValueKind.Int);
            data.Keys.Add("invalid-kind");
            data.Values.Add("10");
            data.Kinds.Add(999);
            data.Keys.Add("healthy");
            data.Values.Add("12");
            data.Kinds.Add((int)DebugValueKind.Int);

            var applied = 0;
            Assert.DoesNotThrow(() => applied = DebugMenuSettings.Apply(menu, data));

            Assert.AreEqual(1, applied);
            Assert.AreEqual(12, healthy.Value);
        }

        [Test]
        public void Settings_MetadataExceptionsSkipOnlyFailingRows()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            var captureFailure = root.Add(new ThrowingMetadataElement("Capture") { ThrowOnSaveable = true });
            var healthy = root.Int("Healthy", 4).WithSaveKey("healthy");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            var captured = DebugMenuSettings.Capture(menu);
            Assert.AreEqual(1, captured.Keys.Count);
            Assert.AreEqual("healthy", captured.Keys[0]);
            Assert.IsTrue(captureFailure.HasError);

            var applyFailure = root.Add(new ThrowingMetadataElement("Apply") { ThrowOnKind = true });
            applyFailure.SaveKey = "bad-kind";
            var data = new DebugMenuSettingsData();
            data.Keys.Add("bad-kind");
            data.Values.Add("9");
            data.Kinds.Add((int)DebugValueKind.Int);
            data.Keys.Add("healthy");
            data.Values.Add("12");
            data.Kinds.Add((int)DebugValueKind.Int);
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));

            var applied = DebugMenuSettings.Apply(menu, data);

            Assert.AreEqual(1, applied);
            Assert.AreEqual(12, healthy.Value);
            Assert.IsTrue(applyFailure.HasError);
        }

        [Test]
        public void Settings_ResetMetadataExceptionCountsFailureAndContinues()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            root.Add(new ThrowingMetadataElement("Metadata") { ThrowOnKind = true });
            var healthy = root.Int("Healthy", 3);
            healthy.Value = 8;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            var result = DebugMenuSettings.ResetAll(menu);

            Assert.AreEqual(2, result.TotalCount);
            Assert.AreEqual(1, result.SucceededCount);
            Assert.AreEqual(1, result.FailedCount);
            Assert.AreEqual(3, healthy.Value);
        }

        [Test]
        public void Settings_ResetAllCountsNoValueRowsAsZeroAndVectorParentOnce()
        {
            var empty = new DebugMenuRoot();
            var emptyRoot = empty.AddPage("Empty").Root;
            emptyRoot.Group("Group", group => group.Action("Action", () => { }));

            var emptyResult = DebugMenuSettings.ResetAll(empty);

            Assert.AreEqual(0, emptyResult.TotalCount);
            Assert.AreEqual(0, emptyResult.SucceededCount);
            Assert.AreEqual(0, emptyResult.FailedCount);

            var value = new Vector3(1f, 2f, 3f);
            var setterCalls = 0;
            var menu = new DebugMenuRoot();
            var vector = DebugVector.Of("Position", () => value, next =>
            {
                setterCalls++;
                value = next;
            });
            menu.AddPage("Gameplay").Root.Add(vector);
            Assert.IsTrue(((DebugFloat)vector.Children[0]).TrySetFloat(8f));
            var beforeReset = setterCalls;

            var result = DebugMenuSettings.ResetAll(menu);

            Assert.AreEqual(1, setterCalls - beforeReset, "Vector親と成分を二重に復元している");
            Assert.AreEqual(1, result.TotalCount);
            Assert.AreEqual(1, result.SucceededCount);
            Assert.AreEqual(0, result.FailedCount);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), value);
        }

        [Test]
        public void Settings_ResetAllContinuesAfterBuiltInAndCustomFailures()
        {
            var throwOnWrite = false;
            var failingValue = 1;
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            root.Add(new ThrowingResetElement());
            var failing = root.Add(new DebugInt("Failing", () => failingValue, value =>
            {
                if (throwOnWrite) throw new System.InvalidOperationException("reset setter failed");
                failingValue = value;
            }));
            var healthy = root.Int("Healthy", 5);
            failingValue = 9;
            healthy.Value = 12;
            throwOnWrite = true;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            DebugMenuResetResult result = default;
            Assert.DoesNotThrow(() => result = DebugMenuSettings.ResetAll(menu));

            Assert.AreEqual(9, failingValue);
            Assert.IsTrue(failing.HasReadError);
            Assert.AreEqual(5, healthy.Value, "失敗行より後ろの既定値復元が止まっている");
            Assert.AreEqual(3, result.TotalCount);
            Assert.AreEqual(1, result.SucceededCount);
            Assert.AreEqual(2, result.FailedCount);
        }

        [Test]
        public void SettingsFormat_CallbackFailureDoesNotCommitAndSameValueCanRetry()
        {
            var nested = typeof(DebugMenuSettingsPage).GetNestedType("SettingsFormatElement", BindingFlags.NonPublic);
            Assert.NotNull(nested);
            var throwOnSet = true;
            var applied = DebugMenuSettingsFormat.Text;
            System.Action<DebugMenuSettingsFormat> setter = format =>
            {
                if (throwOnSet) throw new System.InvalidOperationException("format setter failed");
                applied = format;
            };
            var element = (DebugElement)System.Activator.CreateInstance(
                nested,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new object[] { DebugMenuSettingsFormat.Text, setter },
                null);
            var set = nested.GetMethod("Set", BindingFlags.Instance | BindingFlags.Public);
            Assert.NotNull(set);

            Assert.Throws<TargetInvocationException>(() => set.Invoke(element, new object[] { DebugMenuSettingsFormat.Json }));
            Assert.AreEqual("Text", element.GetValueText());
            Assert.AreEqual(DebugMenuSettingsFormat.Text, applied);

            throwOnSet = false;
            Assert.DoesNotThrow(() => set.Invoke(element, new object[] { DebugMenuSettingsFormat.Json }));
            Assert.AreEqual("Json", element.GetValueText());
            Assert.AreEqual(DebugMenuSettingsFormat.Json, applied);
        }

        [Test]
        public void SettingsPage_ResetAllShowsWarningAndCountsOnPartialFailure()
        {
            var throwOnWrite = false;
            var failingValue = 1;
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay").Root;
            root.Add(new DebugInt("Failing", () => failingValue, next =>
            {
                if (throwOnWrite) throw new System.InvalidOperationException("reset page failed");
                failingValue = next;
            }));
            var healthy = root.Int("Healthy", 5);
            failingValue = 8;
            healthy.Value = 9;
            throwOnWrite = true;
            var storage = new MemoryStorage();
            var toasts = new DebugMenuToastService();
            using var page = new DebugMenuSettingsPage(
                menu,
                new DebugMenuSettings(storage),
                new DebugMenuProfiles(storage),
                toasts);
            DebugElement reset = null;
            for (var i = 0; i < page.Page.Root.Children.Count; i++)
            {
                if (page.Page.Root.Children[i].Label == "Reset All") reset = page.Page.Root.Children[i];
            }
            Assert.NotNull(reset);
            menu.AddPage(page.Page);
            menu.SetRootPage(page.Page);
            Assert.IsTrue(page.Page.FocusOn(reset));

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.DoesNotThrow(menu.Decide, "ResetAll自身は行ごとの失敗を集約して完了する");

            Assert.AreEqual(5, healthy.Value);
            Assert.AreEqual(DebugMenuToastKind.Warning, toasts.Current?.Kind);
            StringAssert.Contains("1/2 succeeded, 1 failed", toasts.Current?.Message);
            StringAssert.Contains("1/2 succeeded, 1 failed", page.LastResult);
        }

    }
}
