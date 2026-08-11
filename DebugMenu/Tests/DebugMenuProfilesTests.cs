using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace DebugMenu.Tests
{
    /// <summary>名前付きプロファイルの保存・適用・一覧管理を検証する。</summary>
    public sealed class DebugMenuProfilesTests
    {
        /// <summary>外部へ書き込まないテスト用保存先。</summary>
        private sealed class MemoryStorage : IDebugMenuStorage
        {
            private readonly Dictionary<string, string> _entries = new Dictionary<string, string>();

            public int Count => _entries.Count;
            public string Load(string key) => _entries.TryGetValue(key, out var value) ? value : null;
            public void Save(string key, string value) => _entries[key] = value;
            public void Delete(string key) => _entries.Remove(key);
        }

        /// <summary>書き込み後または削除後に一度だけ失敗する保存先。</summary>
        private sealed class FaultingStorage : IDebugMenuStorage
        {
            private readonly Dictionary<string, string> _entries = new Dictionary<string, string>();

            public string FailAfterSaveKey { get; set; }
            public string FailAfterDeleteKey { get; set; }
            public string FailOnLoadKey { get; set; }
            public int Count => _entries.Count;

            public string Load(string key)
            {
                if (string.Equals(FailOnLoadKey, key, System.StringComparison.Ordinal)) throw new IOException("load failed");
                return _entries.TryGetValue(key, out var value) ? value : null;
            }

            public void Save(string key, string value)
            {
                _entries[key] = value;
                if (!string.Equals(FailAfterSaveKey, key, System.StringComparison.Ordinal)) return;

                FailAfterSaveKey = null;
                throw new IOException("save failed");
            }

            public void Delete(string key)
            {
                _entries.Remove(key);
                if (!string.Equals(FailAfterDeleteKey, key, System.StringComparison.Ordinal)) return;

                FailAfterDeleteKey = null;
                throw new IOException("delete failed");
            }

            public string FindKeyContaining(string text)
            {
                foreach (var key in _entries.Keys)
                {
                    if (key.Contains(text)) return key;
                }

                return null;
            }
        }

        [Test]
        public void Profiles_SaveAndApplyNamedSnapshots()
        {
            var storage = new MemoryStorage();
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var flag = page.Root.Bool("Invincible", false);
            var speed = page.Root.Float("Speed", 1f);
            var profiles = new DebugMenuProfiles(storage);

            flag.Value = true;
            speed.Value = 3.5f;
            Assert.AreEqual(2, profiles.Save("Fast", menu));

            flag.Value = false;
            speed.Value = 0.25f;
            Assert.AreEqual(2, profiles.Save("Slow", menu));

            Assert.IsTrue(profiles.TryApply("Fast", menu, out var applied));
            Assert.AreEqual(2, applied);
            Assert.IsTrue(flag.Value);
            Assert.AreEqual(3.5f, speed.Value);
            CollectionAssert.AreEqual(new[] { "Fast", "Slow" }, profiles.Names);
        }

        [Test]
        public void Profiles_OverwriteDoesNotDuplicateName()
        {
            var storage = new MemoryStorage();
            var menu = new DebugMenuRoot();
            var value = menu.AddPage("Gameplay").Root.Int("Count", 1);
            var profiles = new DebugMenuProfiles(storage);

            profiles.Save("Boss", menu);
            value.Value = 42;
            profiles.Save(" boss ", menu);
            value.Value = 0;

            Assert.AreEqual(1, profiles.Count);
            Assert.IsTrue(profiles.TryApply("BOSS", menu, out var applied));
            Assert.AreEqual(1, applied);
            Assert.AreEqual(42, value.Value);
        }

        [Test]
        public void Profiles_CatalogSurvivesServiceRecreation()
        {
            var storage = new MemoryStorage();
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay").Root.Bool("Flag", true);

            var saved = new DebugMenuProfiles(storage, "profiles");
            saved.Save("Default", menu);
            saved.Save("Visual", menu);

            var restored = new DebugMenuProfiles(storage, "profiles");

            CollectionAssert.AreEqual(new[] { "Default", "Visual" }, restored.Names);
        }

        [Test]
        public void Profiles_DeleteRemovesSnapshotAndCatalogEntry()
        {
            var storage = new MemoryStorage();
            var menu = new DebugMenuRoot();
            var value = menu.AddPage("Gameplay").Root.Int("Count", 7);
            var profiles = new DebugMenuProfiles(storage);

            profiles.Save("Temporary", menu);
            Assert.AreEqual(2, storage.Count, "一覧と値の 2 件が保存されていない。");
            Assert.IsTrue(profiles.Delete("temporary"));
            Assert.AreEqual(0, storage.Count, "削除したプロファイルの値が保存先に残っている。");
            Assert.IsFalse(profiles.Contains("Temporary"));
            Assert.IsFalse(profiles.TryApply("Temporary", menu, out var applied));
            Assert.AreEqual(0, applied);

            value.Value = 99;
            var restored = new DebugMenuProfiles(storage);
            Assert.AreEqual(0, restored.Count);
        }

        [Test]
        public void Profiles_EncodedNamesRemainIndependent()
        {
            var storage = new MemoryStorage();
            var menu = new DebugMenuRoot();
            var value = menu.AddPage("Gameplay").Root.Int("Count", 0);
            var profiles = new DebugMenuProfiles(storage);

            value.Value = 10;
            profiles.Save("A/B", menu);
            value.Value = 20;
            profiles.Save("A_B", menu);
            value.Value = 0;

            Assert.IsTrue(profiles.TryApply("A/B", menu, out _));
            Assert.AreEqual(10, value.Value);
            Assert.IsTrue(profiles.TryApply("A_B", menu, out _));
            Assert.AreEqual(20, value.Value);
        }

        [Test]
        public void Profiles_RejectsEmptyName()
        {
            var profiles = new DebugMenuProfiles(new MemoryStorage());
            var menu = new DebugMenuRoot();

            Assert.Throws<System.ArgumentException>(() => profiles.Save("   ", menu));
        }

        [Test]
        public void Profiles_NewSaveRollsBackDataAndNameWhenCatalogWriteFails()
        {
            var storage = new FaultingStorage
            {
                FailAfterSaveKey = "debug-menu-profile.catalog",
            };
            var profiles = new DebugMenuProfiles(storage);
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay").Root.Int("Count", 7);

            var failed = false;
            try
            {
                profiles.Save("Boss", menu);
            }
            catch (IOException)
            {
                failed = true;
            }

            Assert.IsTrue(failed, "カタログ保存失敗が呼び出し元へ返っていない");
            Assert.AreEqual(0, profiles.Count);
            Assert.AreEqual(0, storage.Count);
            Assert.AreEqual(0, new DebugMenuProfiles(storage).Count);
        }

        [Test]
        public void Profiles_DeleteRollsBackCatalogAndDataWhenDataDeleteFails()
        {
            var storage = new FaultingStorage();
            var profiles = new DebugMenuProfiles(storage);
            var menu = new DebugMenuRoot();
            var value = menu.AddPage("Gameplay").Root.Int("Count", 7);
            profiles.Save("Boss", menu);
            storage.FailAfterDeleteKey = storage.FindKeyContaining(".data.");

            var failed = false;
            try
            {
                profiles.Delete("Boss");
            }
            catch (IOException)
            {
                failed = true;
            }

            Assert.IsTrue(failed, "値の削除失敗が呼び出し元へ返っていない");
            Assert.IsTrue(profiles.Contains("Boss"));
            var restored = new DebugMenuProfiles(storage);
            Assert.IsTrue(restored.Contains("Boss"));
            value.Value = 0;
            Assert.IsTrue(restored.TryApply("Boss", menu, out var applied));
            Assert.AreEqual(1, applied);
            Assert.AreEqual(7, value.Value);
        }

        [Test]
        public void Profiles_ReloadClearsNamesAndNotifiesWhenCatalogLoadFails()
        {
            var storage = new FaultingStorage();
            var profiles = new DebugMenuProfiles(storage);
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay").Root.Int("Count", 7);
            profiles.Save("Boss", menu);
            var changedCount = 0;
            profiles.Changed += () => changedCount++;
            storage.FailOnLoadKey = "debug-menu-profile.catalog";

            var loaded = profiles.Reload();

            Assert.AreEqual(0, loaded);
            Assert.AreEqual(0, profiles.Count);
            Assert.AreEqual(1, changedCount);
        }
    }
}
