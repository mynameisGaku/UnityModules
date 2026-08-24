// SPDX-License-Identifier: MIT

using System;
using NUnit.Framework;
using UnityEngine;

namespace PlayerOptions.Runtime.Tests
{
    /// <summary>実PlayerPrefs上の一意keyでwrite/read/save/delete境界を確認する。</summary>
    [Parallelizable(ParallelScope.None)]
    public sealed class PlayerPrefsPlayerOptionsStorageRuntimeTests
    {
        private string _key;

        [SetUp]
        public void SetUp()
        {
            _key = $"com.studiogaku.player-options.tests.storage.{Guid.NewGuid():N}";
            PlayerPrefs.DeleteKey(_key);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            if (!string.IsNullOrEmpty(_key))
            {
                PlayerPrefs.DeleteKey(_key);
                PlayerPrefs.Save();
                _key = null;
            }
        }

        [Test]
        public void MissingUniqueKey_ReturnsFalseAndNullContents()
        {
            var storage = new PlayerPrefsPlayerOptionsStorage(_key);

            var exists = storage.TryRead(out var contents);

            Assert.That(exists, Is.False);
            Assert.That(contents, Is.Null);
            Assert.That(PlayerPrefs.HasKey(_key), Is.False);
        }

        [Test]
        public void Write_PersistsOneStringReadableByNewBackendInstance()
        {
            const string contents = "{\"SchemaVersion\":1,\"Probe\":\"PlayerPrefs\"}";
            var storage = new PlayerPrefsPlayerOptionsStorage(_key);

            storage.Write(contents);

            Assert.That(PlayerPrefs.HasKey(_key), Is.True);
            Assert.That(PlayerPrefs.GetString(_key), Is.EqualTo(contents));
            var second = new PlayerPrefsPlayerOptionsStorage(_key);
            Assert.That(second.TryRead(out var reloaded), Is.True);
            Assert.That(reloaded, Is.EqualTo(contents));
            Assert.That(second.Key, Is.EqualTo(_key));
        }

        [Test]
        public void Write_NullThrowsWithoutCreatingUniqueKey()
        {
            var storage = new PlayerPrefsPlayerOptionsStorage(_key);

            Assert.That(() => storage.Write(null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(PlayerPrefs.HasKey(_key), Is.False);
        }
    }
}
