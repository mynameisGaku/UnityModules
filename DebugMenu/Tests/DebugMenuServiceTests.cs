using System.Collections.Generic;
using Containers;
using NUnit.Framework;
using UnityEngine;

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
        public void Settings_ResetAllRestoresDefaults()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var flag = page.Root.Bool("flag", false);
            var count = page.Root.Int("count", 5).WithRange(0, 100);

            flag.Value = true;
            count.Value = 99;

            DebugMenuSettings.ResetAll(menu);

            Assert.IsFalse(flag.Value);
            Assert.AreEqual(5, count.Value);
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
    }
}
