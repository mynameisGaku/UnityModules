using System.Collections.Generic;
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
    }
}
