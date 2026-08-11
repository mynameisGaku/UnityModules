using System.Collections.Generic;
using NUnit.Framework;

namespace DebugMenu.Tests
{
    /// <summary>整数・小数配列行の展開、書き込み、長さ追従、保存境界を検証する。</summary>
    public sealed class DebugArrayTests
    {
        [Test]
        public void IntArray_ExpandsIntoWritableIntRows()
        {
            var values = new List<int> { 2, 4, 6 };
            var array = new DebugIntArray("Values", values).WithRange(0, 10).WithStep(3);

            Assert.AreEqual(3, array.Children.Count);
            var second = (DebugInt)array.Children[1];
            second.OnAdjust(1);

            Assert.AreEqual(7, values[1]);
            Assert.AreEqual("[1]", second.Label);
            Assert.AreEqual(0, second.Min);
            Assert.AreEqual(10, second.Max);
            Assert.AreEqual(3, second.Step);
        }

        [Test]
        public void FloatArray_AppliesRangeStepAndDigits()
        {
            var values = new List<float> { 0.25f };
            var array = new DebugFloatArray("Weights", values)
                .WithRange(0f, 1f)
                .WithStep(0.2f)
                .WithDigits(3);

            var child = (DebugFloat)array.Children[0];
            child.OnAdjust(1);

            Assert.AreEqual(0.45f, values[0], 0.0001f);
            Assert.AreEqual(0f, child.Min);
            Assert.AreEqual(1f, child.Max);
            Assert.AreEqual(0.2f, child.Step);
            Assert.AreEqual(3, child.Digits);
        }

        [Test]
        public void Array_RefreshAddsRowsForNewItems()
        {
            var values = new List<int> { 1 };
            var array = new DebugIntArray("Values", values);
            values.Add(2);
            values.Add(3);

            Assert.IsTrue(array.Refresh());
            Assert.AreEqual(3, array.Children.Count);
            Assert.AreEqual(3, ((DebugInt)array.Children[2]).Value);
        }

        [Test]
        public void Array_PageExtensionInvalidatesVisibleRowsAfterLengthChange()
        {
            var values = new List<int> { 1 };
            var page = new DebugPage("Gameplay");
            var array = page.IntArray("Values", values);
            array.IsExpanded = true;
            page.Invalidate();
            Assert.AreEqual(2, page.VisibleRows.Count);

            values.Add(2);
            array.Tick(0f);

            Assert.AreEqual(3, page.VisibleRows.Count);
        }

        [Test]
        public void Array_ShrinkLeavesCachedChildSafe()
        {
            var values = new List<int> { 1, 2, 3 };
            var array = new DebugIntArray("Values", values);
            var removed = (DebugInt)array.Children[2];
            values.RemoveAt(2);

            Assert.IsTrue(array.Refresh());
            Assert.AreEqual(2, array.Children.Count);
            Assert.DoesNotThrow(() => removed.OnAdjust(1));
            CollectionAssert.AreEqual(new[] { 1, 2 }, values);
        }

        [Test]
        public void Array_ParentAvoidsDuplicateSaveEntry()
        {
            var page = new DebugPage("Gameplay");
            var array = page.IntArray("Values", new List<int> { 10, 20 });

            Assert.IsFalse(array.IsSaveable);
            Assert.IsTrue(array.Children[0].IsSaveable);
            Assert.IsTrue(array.Children[1].IsSaveable);
            Assert.AreEqual("Gameplay/Values/[0]", array.Children[0].ResolveSaveKey());
            Assert.AreEqual("Gameplay/Values/[1]", array.Children[1].ResolveSaveKey());
        }

        [Test]
        public void Array_ResetRestoresEachExistingIndexDefault()
        {
            var values = new List<float> { 1f, 2f };
            var array = new DebugFloatArray("Values", values);
            ((DebugFloat)array.Children[0]).Value = 8f;
            ((DebugFloat)array.Children[1]).Value = 9f;

            array.ResetToDefault();

            CollectionAssert.AreEqual(new[] { 1f, 2f }, values);
            Assert.IsFalse(array.IsModified);
        }

        [Test]
        public void Array_ExternalShrinkPreservesRemainingDefaults()
        {
            var values = new List<int> { 1, 2, 3 };
            var array = new DebugIntArray("Values", values);
            ((DebugInt)array.Children[0]).Value = 7;
            values.RemoveAt(2);
            array.Refresh();

            array.ResetToDefault();

            CollectionAssert.AreEqual(new[] { 1, 2 }, values);
        }
    }
}
