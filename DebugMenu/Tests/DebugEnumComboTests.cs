using NUnit.Framework;

namespace DebugMenu.Tests
{
    /// <summary>Enum の候補一覧と、既存入力経路からの操作を検証する。</summary>
    public sealed class DebugEnumComboTests
    {
        [Test]
        public void Decide_ExpandsOptionsWithoutChangingValue()
        {
            var element = new DebugEnum("Mode", new[] { "A", "B", "C" }, 1);

            element.OnDecide();

            Assert.IsTrue(element.IsExpanded);
            Assert.AreEqual(1, element.Index);
            Assert.AreEqual(3, element.Children.Count);
            Assert.AreEqual("Selected", element.Children[1].GetValueText());
        }

        [Test]
        public void Keyboard_DecideOnOptionSetsValueAndCollapsesList()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Page");
            var element = page.Root.Add(new DebugEnum("Mode", new[] { "A", "B", "C" }));

            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.Decide);
            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.Down);
            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.Down);
            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.Decide);

            Assert.AreEqual(1, element.Index);
            Assert.IsFalse(element.IsExpanded);
            Assert.AreEqual(1, page.VisibleRows.Count);
            Assert.AreSame(element, page.CurrentElement);
        }

        [Test]
        public void LeftAndRight_KeepCyclingParentValue()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Page");
            var element = page.Root.Add(new DebugEnum("Mode", new[] { "A", "B", "C" }));

            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.Left);
            Assert.AreEqual(2, element.Index);

            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.Right);
            Assert.AreEqual(0, element.Index);
        }

        [Test]
        public void Save_IncludesParentButExcludesOptionRows()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Page");
            var element = page.Root.Add(new DebugEnum("Mode", new[] { "A", "B", "C" }, 2));
            var storage = new MemoryStorage();
            var settings = new DebugMenuSettings(storage);

            var savedCount = settings.Save(menu);

            Assert.AreEqual(1, savedCount);
            Assert.IsFalse(string.IsNullOrEmpty(storage.Value));
            Assert.IsTrue(element.IsSaveable);
            for (var i = 0; i < element.Children.Count; i++) Assert.IsFalse(element.Children[i].IsSaveable);
        }

        /// <summary>保存内容だけを保持するテスト用の保存先。</summary>
        private sealed class MemoryStorage : IDebugMenuStorage
        {
            /// <summary>最後に保存された文字列。</summary>
            public string Value { get; private set; }

            /// <inheritdoc/>
            public string Load(string key) => Value;

            /// <inheritdoc/>
            public void Save(string key, string value) => Value = value;

            /// <inheritdoc/>
            public void Delete(string key) => Value = null;
        }
    }
}
