using NUnit.Framework;

namespace DebugMenu.Tests
{
    public sealed class DebugMenuIntegrationTests
    {
        [Test]
        public void SearchPage_FindsNestedElementAndNavigatesToIt()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Gameplay");
            var child = new DebugPage("Physics");
            var gravity = child.Root.Add(new DebugFloat("Gravity Scale", 1f));
            root.AddChildPage(child);

            var search = new DebugMenuSearchPage(menu);
            menu.AddPage(search.Page);

            search.Open();
            search.SetQuery("grav");

            Assert.AreEqual(1, search.ResultCount);
            Assert.AreSame(search.Page, menu.CurrentPage);

            search.Page.CursorIndex = 1;
            menu.Decide();

            Assert.AreSame(child, menu.CurrentPage);
            Assert.AreSame(gravity, child.CurrentElement);
            Assert.AreEqual(1, menu.Depth);
        }

        [Test]
        public void SearchPage_EmptyQueryShowsOnlyQueryAndMessage()
        {
            var menu = new DebugMenuRoot();
            menu.AddPage("Gameplay").Root.Add(new DebugInt("Health", 100));
            var search = new DebugMenuSearchPage(menu);
            menu.AddPage(search.Page);

            search.SetQuery(string.Empty);

            Assert.AreEqual(0, search.ResultCount);
            Assert.AreEqual(2, search.Page.VisibleRows.Count);
            Assert.AreSame(search.QueryElement, search.Page.VisibleRows[0].Element);
            Assert.IsFalse(search.QueryElement.IsSaveable);
            Assert.IsFalse(search.QueryElement.IsSearchable);
        }

        [Test]
        public void CommandDispatcher_UndoAndRedoUseAttachedHistory()
        {
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Gameplay");
            var health = page.Root.Add(new DebugInt("Health", 100));
            using var history = new DebugMenuHistory();
            history.Attach(menu);

            health.Value = 75;
            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.Undo, history);
            Assert.AreEqual(100, health.Value);

            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.Redo, history);
            Assert.AreEqual(75, health.Value);
        }

        [Test]
        public void InputRepeater_PrioritizesSearchAndHistoryCommands()
        {
            var repeater = new DebugMenuInputRepeater();

            Assert.AreEqual(
                DebugMenuCommand.Search,
                repeater.Poll(new DebugMenuInputState { Search = true, ToggleFavorite = true }, 0f));

            repeater.Reset();
            Assert.AreEqual(
                DebugMenuCommand.Undo,
                repeater.Poll(new DebugMenuInputState { Undo = true }, 0f));

            repeater.Reset();
            Assert.AreEqual(
                DebugMenuCommand.Redo,
                repeater.Poll(new DebugMenuInputState { Redo = true }, 0f));
        }
    }
}
