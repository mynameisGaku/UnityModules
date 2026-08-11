using System.Reflection;
using NUnit.Framework;

namespace DebugMenu.Tests
{
    /// <summary>Enum の候補一覧を既存のマウス経路から操作できることを検証する。</summary>
    public sealed class DebugEnumComboVisualTests
    {
        [Test]
        public void MouseDoubleClick_UsesMenuViewToOpenAndChooseOption()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Page");
            var element = page.Root.Add(new DebugEnum("Mode", new[] { "A", "B", "C" }));
            var view = new DebugMenuView(menu);

            DoubleClickRow(view, 0);
            Assert.IsTrue(element.IsExpanded);

            DoubleClickRow(view, 3);

            Assert.AreEqual(2, element.Index);
            Assert.IsFalse(element.IsExpanded);
        }

        /// <summary>既存のマウス決定経路へ同じ行の 2 クリックを渡す。</summary>
        private static void DoubleClickRow(DebugMenuView view, int index)
        {
            var method = typeof(DebugMenuView).GetMethod("ClickRow", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(view, new object[] { index });
            method.Invoke(view, new object[] { index });
        }
    }
}
