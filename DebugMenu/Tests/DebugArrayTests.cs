using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DebugMenu.Tests
{
    /// <summary>整数・小数配列行の展開、書き込み、長さ追従、保存境界を検証する。</summary>
    public sealed class DebugArrayTests
    {
        private sealed class TrackingList<T> : IList<T>
        {
            private readonly List<T> _values;

            public TrackingList(params T[] values) => _values = new List<T>(values);
            public int ThrowIndex { get; set; } = -1;
            public int ThrowGetIndex { get; set; } = -1;
            public int SetCount { get; private set; }
            public T this[int index]
            {
                get => index == ThrowGetIndex
                    ? throw new System.InvalidOperationException("array getter failed")
                    : _values[index];
                set
                {
                    if (index == ThrowIndex) throw new System.InvalidOperationException("array setter failed");
                    SetCount++;
                    _values[index] = value;
                }
            }
            public int Count => _values.Count;
            public bool IsReadOnly => false;
            public void Add(T item) => _values.Add(item);
            public void Clear() => _values.Clear();
            public bool Contains(T item) => _values.Contains(item);
            public void CopyTo(T[] array, int arrayIndex) => _values.CopyTo(array, arrayIndex);
            public IEnumerator<T> GetEnumerator() => _values.GetEnumerator();
            public int IndexOf(T item) => _values.IndexOf(item);
            public void Insert(int index, T item) => _values.Insert(index, item);
            public bool Remove(T item) => _values.Remove(item);
            public void RemoveAt(int index) => _values.RemoveAt(index);
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

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
        public void Array_NestedLengthChangeRefreshesPageWithoutPageEvent()
        {
            var values = new List<int> { 1 };
            var page = new DebugPage("Gameplay");
            var group = page.Root.Add(new DebugGroup("Nested", true));
            var array = group.Add(new DebugIntArray("Values", values));
            array.IsExpanded = true;
            page.Invalidate();
            Assert.AreEqual(3, page.VisibleRows.Count);

            values.Add(2);
            array.Tick(0f);

            Assert.AreEqual(4, page.VisibleRows.Count);
        }

        [Test]
        public void Page_ExpansionRefreshesVisibleRowsWithoutInvalidate()
        {
            var page = new DebugPage("Gameplay");
            var group = page.Root.Add(new DebugGroup("Nested", false));
            group.Add(new DebugElement("Child"));
            page.Invalidate();
            Assert.AreEqual(1, page.VisibleRows.Count);

            group.IsExpanded = true;

            Assert.AreEqual(2, page.VisibleRows.Count);
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
        public void Array_StaleChildrenRejectWritesWithoutChangeNotificationAndCanRecoverAfterGrow()
        {
            var intValues = new List<int> { 1, 2 };
            var floatValues = new List<float> { 1f, 2f };
            var intArray = new DebugIntArray("Ints", intValues);
            var floatArray = new DebugFloatArray("Floats", floatValues);
            var staleInt = (DebugInt)intArray.Children[1];
            var staleFloat = (DebugFloat)floatArray.Children[1];
            var changed = 0;
            DebugElement.SetChangeListener(_ => changed++);
            var valueVersion = DebugElement.ValueVersion;
            intValues.RemoveAt(1);
            floatValues.RemoveAt(1);
            intArray.Refresh();
            floatArray.Refresh();

            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
                Assert.IsFalse(staleInt.TrySetInt(7));
                Assert.IsFalse(staleFloat.TrySetFloat(7f));
                Assert.AreEqual(0, changed);
                Assert.AreEqual(valueVersion, DebugElement.ValueVersion);

                intValues.Add(2);
                floatValues.Add(2f);
                Assert.IsTrue(staleInt.TrySetInt(7));
                Assert.IsTrue(staleFloat.TrySetFloat(7f));
                Assert.IsFalse(staleInt.HasError);
                Assert.IsFalse(staleFloat.HasError);
                CollectionAssert.AreEqual(new[] { 1, 7 }, intValues);
                CollectionAssert.AreEqual(new[] { 1f, 7f }, floatValues);
            }
            finally
            {
                DebugElement.SetChangeListener(null);
            }
        }

        [Test]
        public void Array_ConfigureGetterFailuresAreIsolatedAndRecoverWithClamp()
        {
            var ints = new TrackingList<int>(20);
            var floats = new TrackingList<float>(2f);
            var intArray = new DebugIntArray("Ints", ints);
            var floatArray = new DebugFloatArray("Floats", floats);
            ints.ThrowGetIndex = 0;
            floats.ThrowGetIndex = 0;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値取得"));
            Assert.DoesNotThrow(() => intArray.WithRange(0, 10));
            Assert.DoesNotThrow(() => floatArray.WithRange(0f, 1f));
            Assert.IsTrue(intArray.Children[0].HasError);
            Assert.IsTrue(floatArray.Children[0].HasError);

            ints.ThrowGetIndex = -1;
            floats.ThrowGetIndex = -1;
            intArray.WithRange(0, 10);
            floatArray.WithRange(0f, 1f);

            Assert.AreEqual(10, ints[0]);
            Assert.AreEqual(1f, floats[0]);
            Assert.IsFalse(intArray.Children[0].HasError);
            Assert.IsFalse(floatArray.Children[0].HasError);
        }

        [Test]
        public void Array_ParentResetContinuesAfterChildFailureAndReportsFailure()
        {
            var values = new TrackingList<int>(1, 2);
            var menu = new DebugMenuRoot();
            var page = menu.AddPage("Arrays");
            var array = page.Root.Add(new DebugIntArray("Values", values));
            ((DebugInt)array.Children[0]).Value = 8;
            ((DebugInt)array.Children[1]).Value = 9;
            values.ThrowIndex = 0;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(@"\[DebugMenu\].*値設定"));
            Assert.DoesNotThrow(() => DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.ResetValue));
            Assert.AreEqual(8, values[0]);
            Assert.AreEqual(2, values[1], "失敗した要素より後ろの復元が止まっている");
            Assert.IsTrue(array.HasError);

            values.ThrowIndex = -1;
            DebugMenuCommandDispatcher.Dispatch(menu, DebugMenuCommand.ResetValue);
            Assert.AreEqual(1, values[0]);
            Assert.IsFalse(array.HasError);
        }

        [Test]
        public void SettingsResetAll_ResetsEachArrayIndexOnceAndCountsOnlyValues()
        {
            var values = new TrackingList<int>(1, 2);
            var menu = new DebugMenuRoot();
            var array = menu.AddPage("Arrays").Root.Add(new DebugIntArray("Values", values));
            ((DebugInt)array.Children[0]).Value = 8;
            ((DebugInt)array.Children[1]).Value = 9;
            var beforeReset = values.SetCount;

            var result = DebugMenuSettings.ResetAll(menu);

            Assert.AreEqual(2, values.SetCount - beforeReset, "配列親と子の両方から二重に復元した");
            Assert.AreEqual(2, result.TotalCount);
            Assert.AreEqual(2, result.SucceededCount);
            Assert.AreEqual(0, result.FailedCount);
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

        [Test]
        public void ArrayChildren_ExposeLiveReadOnlyView()
        {
            var values = new List<int> { 1, 2 };
            var array = new DebugIntArray("Values", values);
            var children = array.Children;

            Assert.Throws<NotSupportedException>(() => ((IList<DebugElement>)children).RemoveAt(0));

            values.Add(3);
            Assert.AreSame(children, array.Children, "同期のたびに別の子行一覧が作られている");
            Assert.AreEqual(3, children.Count);
        }
    }
}
