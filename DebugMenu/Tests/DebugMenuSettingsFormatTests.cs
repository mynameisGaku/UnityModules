using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace DebugMenu.Tests
{
    public sealed class DebugMenuSettingsFormatTests
    {
        [TestCase("1", true)]
        [TestCase("0", false)]
        [TestCase("true", true)]
        [TestCase("TRUE", true)]
        [TestCase("false", false)]
        [TestCase("FALSE", false)]
        public void BoolSnapshot_ParsesOnlyDocumentedTokens(string text, bool expected)
        {
            Assert.IsTrue(DebugValueSnapshot.TryParse(DebugValueKind.Bool, text, out var snapshot));
            var target = new DebugBool("flag", !expected);
            Assert.IsTrue(snapshot.Apply(target));
            Assert.AreEqual(expected, target.Value);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("yes")]
        [TestCase("2")]
        [TestCase(" false ")]
        public void BoolSnapshot_RejectsUnknownOrPaddedTokens(string text)
        {
            Assert.IsFalse(DebugValueSnapshot.TryParse(DebugValueKind.Bool, text, out _));
        }

        [Test]
        public void TextFormat_RoundTripsEscapedValues()
        {
            var data = new DebugMenuSettingsData();
            data.Keys.Add("Page/Path\tName");
            data.Values.Add("line1\nline2\\tail");
            data.Kinds.Add((int)DebugValueKind.Text);

            var serialized = DebugMenuSettingsSerializer.Serialize(data, DebugMenuSettingsFormat.Text);

            Assert.IsTrue(serialized.StartsWith("DEBUGMENU-TEXT", StringComparison.Ordinal));
            Assert.IsTrue(DebugMenuSettingsSerializer.TryDeserialize(serialized, out var restored, out var format));
            Assert.AreEqual(DebugMenuSettingsFormat.Text, format);
            Assert.AreEqual(data.Keys[0], restored.Keys[0]);
            Assert.AreEqual(data.Values[0], restored.Values[0]);
            Assert.AreEqual(data.Kinds[0], restored.Kinds[0]);
        }

        [Test]
        public void BinaryFormat_RoundTripsTypedRecords()
        {
            var data = new DebugMenuSettingsData();
            data.Keys.Add("Gameplay/Count");
            data.Values.Add("42");
            data.Kinds.Add((int)DebugValueKind.Int);

            var bytes = DebugMenuSettingsSerializer.SerializeFile(data, DebugMenuSettingsFormat.Binary);

            Assert.AreEqual((byte)'D', bytes[0]);
            Assert.AreEqual((byte)'M', bytes[1]);
            Assert.IsTrue(DebugMenuSettingsSerializer.TryDeserializeFile(bytes, out var restored, out var format));
            Assert.AreEqual(DebugMenuSettingsFormat.Binary, format);
            Assert.AreEqual("Gameplay/Count", restored.Keys[0]);
            Assert.AreEqual("42", restored.Values[0]);
            Assert.AreEqual((int)DebugValueKind.Int, restored.Kinds[0]);
        }

        [Test]
        public void FileDeserializer_InvalidUtf8ReturnsFalse()
        {
            Assert.IsFalse(DebugMenuSettingsSerializer.TryDeserializeFile(
                new byte[] { 0xFF, 0xFE, 0xFA },
                out var data,
                out _));
            Assert.IsNull(data);
        }

        [Test]
        public void FileDeserializer_StripsUtf8BomBeforeFormatDetection()
        {
            var source = new DebugMenuSettingsData();
            source.Keys.Add("Gameplay/Count");
            source.Values.Add("12");
            source.Kinds.Add((int)DebugValueKind.Int);
            var content = new UTF8Encoding(false).GetBytes(
                DebugMenuSettingsSerializer.Serialize(source, DebugMenuSettingsFormat.Text));
            var bytes = new byte[content.Length + 3];
            bytes[0] = 0xEF;
            bytes[1] = 0xBB;
            bytes[2] = 0xBF;
            Buffer.BlockCopy(content, 0, bytes, 3, content.Length);

            Assert.IsTrue(DebugMenuSettingsSerializer.TryDeserializeFile(bytes, out var data, out var format));
            Assert.AreEqual(DebugMenuSettingsFormat.Text, format);
            Assert.AreEqual("Gameplay/Count", data.Keys[0]);
            Assert.AreEqual("12", data.Values[0]);
        }

        [Test]
        public void Storage_LoadAutoDetectsTextAndBinaryEnvelopes()
        {
            var storage = new MemoryStorage();
            var menu = CreateMenu(out var value);
            var settings = new DebugMenuSettings(storage, "settings", DebugMenuSettingsFormat.Text);

            value.Value = 12;
            settings.Save(menu);
            value.Value = 0;
            Assert.AreEqual(1, settings.Load(menu));
            Assert.AreEqual(12, value.Value);
            Assert.AreEqual(DebugMenuSettingsFormat.Text, settings.LastLoadedFormat);

            settings.Format = DebugMenuSettingsFormat.Binary;
            value.Value = 24;
            settings.Save(menu);
            value.Value = 0;
            Assert.AreEqual(1, settings.Load(menu));
            Assert.AreEqual(24, value.Value);
            Assert.AreEqual(DebugMenuSettingsFormat.Binary, settings.LastLoadedFormat);
        }

        [Test]
        public void SaveAsAndLoadFrom_RoundTripBinaryFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "debug-menu-" + Guid.NewGuid().ToString("N") + ".bin");
            try
            {
                var menu = CreateMenu(out var value);
                var settings = new DebugMenuSettings(new MemoryStorage());
                value.Value = 77;

                Assert.AreEqual(1, settings.SaveAs(menu, path, DebugMenuSettingsFormat.Binary));
                value.Value = 0;
                Assert.AreEqual(1, settings.LoadFrom(menu, path));
                Assert.AreEqual(77, value.Value);
                Assert.AreEqual(DebugMenuSettingsFormat.Binary, settings.LastLoadedFormat);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
        }

        [Test]
        public void SettingsPage_SavesAndLoadsNamedProfile()
        {
            var storage = new MemoryStorage();
            var menu = CreateMenu(out var value);
            var settings = new DebugMenuSettings(storage);
            var profiles = new DebugMenuProfiles(storage, "profiles");
            using var page = new DebugMenuSettingsPage(menu, settings, profiles);
            menu.AddPage(page.Page);

            page.ProfileName = "Fast";
            value.Value = 90;
            Assert.AreEqual(1, page.SaveProfile());

            value.Value = 0;
            Assert.AreEqual(1, page.LoadProfile());
            Assert.AreEqual(90, value.Value);
            Assert.AreEqual(1, profiles.Count);
            Assert.IsTrue(page.Page.VisibleRows.Count >= 10);
        }

        private static DebugMenuRoot CreateMenu(out DebugInt value)
        {
            var menu = new DebugMenuRoot();
            value = menu.AddPage("Gameplay").Root.Add(new DebugInt("Count", 0));
            return menu;
        }

        private sealed class MemoryStorage : IDebugMenuStorage
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>();

            public string Load(string key) => _values.TryGetValue(key, out var value) ? value : null;
            public void Save(string key, string value) => _values[key] = value;
            public void Delete(string key) => _values.Remove(key);
        }
    }
}
