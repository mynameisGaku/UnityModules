using NUnit.Framework;

namespace DebugMenu.Tests
{
    public sealed class DebugMenuToastSnapshotTests
    {
        [Test]
        public void ToastService_QueuesAndExpiresMessagesInOrder()
        {
            var service = new DebugMenuToastService();
            service.Show("First", DebugMenuToastKind.Info, 0.1f);
            service.Show("Second", DebugMenuToastKind.Success, 0.2f);

            Assert.AreEqual("First", service.Current.Value.Message);
            Assert.AreEqual(1, service.PendingCount);

            service.Tick(0.11f);
            Assert.AreEqual("Second", service.Current.Value.Message);
            Assert.AreEqual(DebugMenuToastKind.Success, service.Current.Value.Kind);

            service.Tick(0.21f);
            Assert.IsFalse(service.Current.HasValue);
        }

        [Test]
        public void TextSnapshot_TraversesChildPagesAndDeduplicatesBorrowedElements()
        {
            var menu = new DebugMenuRoot();
            var root = menu.AddPage("Root");
            var value = root.Root.Add(new DebugInt("Count", 7));
            var child = new DebugPage("Child");
            child.Root.Add(new DebugText("Message", "hello\nworld"));
            root.AddChildPage(child, DebugAttachMode.Page);

            var borrowed = menu.AddPage("Borrowed");
            borrowed.Root.AddBorrowed(value);

            var text = DebugMenuTextSnapshot.Capture(menu);

            StringAssert.Contains("Root / Count = 7", text);
            StringAssert.Contains("Root / Child / Message = hello\\nworld", text);
            Assert.AreEqual(1, CountOccurrences(text, "Count = 7"));
        }

        private static int CountOccurrences(string value, string token)
        {
            var count = 0;
            var index = 0;
            while ((index = value.IndexOf(token, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }
    }
}
