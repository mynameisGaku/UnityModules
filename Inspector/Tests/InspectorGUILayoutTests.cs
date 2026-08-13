using Inspector.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Inspector.Tests
{
    /// <summary>class の入れ子所有者。変更通知の呼び先と保存値を持つ。</summary>
    [System.Serializable]
    internal sealed class NestedCallbackSubject
    {
        [SerializeField] private int _value;
        [SerializeField] private int _calls;

        public int Value => _value;
        public int Calls => _calls;

        private void Notify() => _calls++;
    }

    /// <summary>boxed 値の書き戻しまで必要な struct の入れ子所有者。</summary>
    [System.Serializable]
    internal struct NestedStructCallbackSubject
    {
        [SerializeField] private int _calls;

        public int Calls => _calls;

        private void Notify() => _calls++;
    }

    /// <summary>値変更通知とボタン呼び出しの対象を確認するための保存対象。</summary>
    internal sealed class InspectorGUILayoutSubject : ScriptableObject
    {
        /// <summary>複数選択で編集する値。</summary>
        [SerializeField] private int _value;

        /// <summary>instance メソッドが呼ばれた回数。</summary>
        [SerializeField] private int _instanceCalls;

        /// <summary>class の入れ子変更通知を検査する値。</summary>
        [SerializeField] private NestedCallbackSubject _nested = new NestedCallbackSubject();

        /// <summary>struct の入れ子変更通知を検査する値。</summary>
        [SerializeField] private NestedStructCallbackSubject _nestedStruct;

        /// <summary>static メソッドが呼ばれた回数。</summary>
        private static int _staticCalls;

        /// <summary>テストから編集値を読み書きする。</summary>
        public int Value
        {
            get => _value;
            set => _value = value;
        }

        /// <summary>対象別の呼び出し回数を返す。</summary>
        public int InstanceCalls => _instanceCalls;

        /// <summary>class の入れ子値を返す。</summary>
        public NestedCallbackSubject Nested => _nested;

        /// <summary>struct の入れ子値を返す。</summary>
        public NestedStructCallbackSubject NestedStruct => _nestedStruct;

        /// <summary>全対象で共有する呼び出し回数を返す。</summary>
        public static int StaticCalls => _staticCalls;

        /// <summary>共有回数をテスト開始時の値へ戻す。</summary>
        public static void ResetStaticCalls() => _staticCalls = 0;

        /// <summary>選択対象ごとに副作用を残す。</summary>
        private void InvokeInstance() => _instanceCalls++;

        /// <summary>選択数に依存しない副作用を残す。</summary>
        private static void InvokeStatic() => _staticCalls++;
    }

    internal sealed class ReadOnlyValueSubject : ScriptableObject
    {
        public int Value;
        public int ReadOnlyValue => Value;
    }

    /// <summary>
    /// Inspector の補助操作と実値変更を分離し、複数選択時の呼び出し回数と Undo を確かめる。
    /// </summary>
    public sealed class InspectorGUILayoutTests
    {
        /// <summary>1 件目の対象。</summary>
        private InspectorGUILayoutSubject _first;

        /// <summary>2 件目の対象。</summary>
        private InspectorGUILayoutSubject _second;

        /// <summary>各テスト用の対象を作る。</summary>
        [SetUp]
        public void SetUp()
        {
            _first = ScriptableObject.CreateInstance<InspectorGUILayoutSubject>();
            _second = ScriptableObject.CreateInstance<InspectorGUILayoutSubject>();
            InspectorGUILayoutSubject.ResetStaticCalls();
        }

        /// <summary>Undo 履歴と一時対象を残さない。</summary>
        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            if (_first != null) Object.DestroyImmediate(_first);
            if (_second != null) Object.DestroyImmediate(_second);
        }

        [Test]
        public void ReadOnlyValuesDiffer_UsesEverySelectedOwner()
        {
            var first = ScriptableObject.CreateInstance<ReadOnlyValueSubject>();
            var second = ScriptableObject.CreateInstance<ReadOnlyValueSubject>();

            try
            {
                var property = typeof(ReadOnlyValueSubject).GetProperty(nameof(ReadOnlyValueSubject.ReadOnlyValue));
                var member = new InspectorMember(
                    InspectorMemberKind.NativeProperty,
                    nameof(ReadOnlyValueSubject.ReadOnlyValue),
                    property,
                    new InspectorAttribute[0],
                    0);

                first.Value = 3;
                second.Value = 3;
                Assert.IsFalse(InspectorGUILayout.ReadOnlyValuesDiffer(member, new object[] { first, second }, 3));

                second.Value = 4;
                Assert.IsTrue(InspectorGUILayout.ReadOnlyValuesDiffer(member, new object[] { first, second }, 3));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        /// <summary>同じ値のままなら、開閉や補助ボタンに相当する再判定でも変更対象を返さない。</summary>
        [Test]
        public void FindChangedTargets_IgnoresOperationsThatDoNotChangeTheFieldValue()
        {
            var targets = Targets();
            var before = InspectorGUILayout.CapturePropertyValues(targets, "_value");

            var changed = InspectorGUILayout.FindChangedTargets(before);

            Assert.IsEmpty(changed, "保存フィールドが同じなら GUI 側の操作だけで変更通知を出さない");
        }

        /// <summary>mixed 値を揃えたとき、元から同じだった対象を変更通知から外す。</summary>
        [Test]
        public void FindChangedTargets_ReturnsOnlyObjectsWhoseFieldValueActuallyChanged()
        {
            _first.Value = 1;
            _second.Value = 2;
            var targets = Targets();
            var before = InspectorGUILayout.CapturePropertyValues(targets, "_value");

            using (var serialized = new SerializedObject(targets))
            {
                serialized.Update();
                serialized.FindProperty("_value").intValue = 2;
                Assert.IsTrue(serialized.ApplyModifiedProperties());
            }

            var changed = InspectorGUILayout.FindChangedTargets(before);

            CollectionAssert.AreEqual(new Object[] { _first }, changed,
                "元から 2 だった対象には変更通知を重ねない");
        }

        /// <summary>instance ボタンは各対象へ 1 回ずつ呼び、全副作用を 1 回の Undo で戻す。</summary>
        [Test]
        public void InvokeOnTargets_InvokesInstanceMethodPerTargetAndRecordsUndo()
        {
            var targets = Targets();
            var undoGroup = Undo.GetCurrentGroup();

            InspectorGUILayout.InvokeOnTargets(
                targets,
                typeof(InspectorGUILayoutSubject),
                "InvokeInstance",
                "instance ボタン",
                record: true);
            Undo.FlushUndoRecordObjects();

            Assert.AreEqual(1, _first.InstanceCalls);
            Assert.AreEqual(1, _second.InstanceCalls);

            Undo.CollapseUndoOperations(undoGroup);
            Undo.PerformUndo();

            Assert.AreEqual(0, _first.InstanceCalls, "1 件目の副作用を戻す");
            Assert.AreEqual(0, _second.InstanceCalls, "2 件目の副作用を戻す");
        }

        /// <summary>static ボタンは複数選択でも全体で 1 回だけ呼ぶ。</summary>
        [Test]
        public void InvokeOnTargets_InvokesStaticMethodOnlyOnceForMultipleSelection()
        {
            InspectorGUILayout.InvokeOnTargets(
                Targets(),
                typeof(InspectorGUILayoutSubject),
                "InvokeStatic",
                "static ボタン",
                record: false);

            Assert.AreEqual(1, InspectorGUILayoutSubject.StaticCalls);
        }

        /// <summary>入れ子 class の通知は選択対象ごとの所有者へ届き、根の Undo に含まれる。</summary>
        [Test]
        public void InvokeOnOwners_InvokesNestedClassPerTargetAndRecordsRootUndo()
        {
            var undoGroup = Undo.GetCurrentGroup();

            InspectorGUILayout.InvokeOnOwners(
                Targets(),
                typeof(NestedCallbackSubject),
                "_nested",
                "Notify",
                "入れ子 class",
                record: true);
            Undo.FlushUndoRecordObjects();

            Assert.AreEqual(1, _first.Nested.Calls);
            Assert.AreEqual(1, _second.Nested.Calls);

            Undo.CollapseUndoOperations(undoGroup);
            Undo.PerformUndo();

            Assert.AreEqual(0, _first.Nested.Calls);
            Assert.AreEqual(0, _second.Nested.Calls);
        }

        /// <summary>入れ子 struct の通知結果は boxed copy に残さず、根フィールドへ書き戻す。</summary>
        [Test]
        public void InvokeOnOwners_WritesNestedStructChangesBackToTheRoot()
        {
            InspectorGUILayout.InvokeOnOwners(
                Targets(),
                typeof(NestedStructCallbackSubject),
                "_nestedStruct",
                "Notify",
                "入れ子 struct",
                record: true);

            Assert.AreEqual(1, _first.NestedStruct.Calls);
            Assert.AreEqual(1, _second.NestedStruct.Calls);
        }

        /// <summary>完全な propertyPath の差分から、実際に変わった根だけへ通知する。</summary>
        [Test]
        public void NestedPropertyChange_NotifiesOnlyChangedRootTargets()
        {
            var targets = Targets();
            var before = InspectorGUILayout.CapturePropertyValues(targets, "_nested._value");

            using (var serialized = new SerializedObject(_first))
            {
                serialized.Update();
                serialized.FindProperty("_nested._value").intValue = 7;
                Assert.IsTrue(serialized.ApplyModifiedProperties());
            }

            var changed = InspectorGUILayout.FindChangedTargets(before);
            InspectorGUILayout.InvokeOnOwners(
                changed,
                typeof(NestedCallbackSubject),
                "_nested",
                "Notify",
                "入れ子変更通知",
                record: true);

            CollectionAssert.AreEqual(new Object[] { _first }, changed);
            Assert.AreEqual(7, _first.Nested.Value);
            Assert.AreEqual(1, _first.Nested.Calls);
            Assert.AreEqual(0, _second.Nested.Calls);
        }

        /// <summary>テスト対象を Unity の複数選択と同じ配列にする。</summary>
        private Object[] Targets() => new Object[] { _first, _second };
    }
}
