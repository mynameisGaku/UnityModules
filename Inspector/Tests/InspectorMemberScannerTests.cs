using System;
using System.Collections.Generic;
using System.Linq;
using Inspector.Editor;
using NUnit.Framework;
using UnityEngine;

// 検査対象は「属性がどう付いているか」だけで、フィールドの値そのものは使わない。
#pragma warning disable 0649

namespace Inspector.Tests
{
    /// <summary>型を舐めて表示対象を拾う部分。順序と、属性の取り違えが無いことを見る。</summary>
    public sealed class InspectorMemberScannerTests
    {
        private class BaseSubject
        {
            [ShowNonSerialized] protected int _baseRuntimeValue;

            [Button] protected void BaseAction() { }
        }

        private sealed class Subject : BaseSubject
        {
            [SerializeField] private int _first;
            [SerializeField] [Order(-5)] private int _second;
            [SerializeField] [Foldout("表示")] [BoxGroup("表示/戦闘")] private int _grouped;
            [SerializeField] [LabelText("三番目")] [Required] private GameObject _third;

            [ShowNonSerialized] private float _runtimeValue;

            [ShowNativeProperty] public int Computed => 1;

            public int NotShown => 2;

            [Button("押す")] private void Action() { }

            private void NotAButton() { }
        }

        private sealed class Plain
        {
            public int Value;
        }

        private sealed class PlainScriptable : ScriptableObject
        {
            [ShowIf("Value")] public int Shown;
        }

        [Serializable]
        private sealed class NestedSettings
        {
            public bool Enabled;

            [HideInInspector]
            public int HiddenValue;

            [ShowIf(nameof(Enabled))]
            [OnValueChanged(nameof(Changed))]
            public int Conditional;

            [ShowNonSerialized] private int _runtimeValue;

            [HideInInspector] [ShowNonSerialized] private int _explicitHiddenRuntimeValue;

            [Button] private void Changed() { }
        }

        private sealed class NestedRoot
        {
            public NestedSettings Settings;
        }

        private sealed class ManagedReferenceRoot
        {
            [SerializeReference]
            public NestedSettings Settings;
        }

        [Serializable]
        private sealed class RecursiveSettings
        {
            public bool Enabled;

            [ShowIf(nameof(Enabled))] public int Conditional;

#pragma warning disable UAC1005 // 循環を打ち切る検査用に、意図して自己参照を作る。
            public RecursiveSettings Next;
#pragma warning restore UAC1005
        }

        private sealed class RecursiveRoot
        {
            public RecursiveSettings Settings;
        }

        private static InspectorMember Find(System.Collections.Generic.List<InspectorMember> members, string name)
        {
            var found = members.FirstOrDefault(member => member.Name == name);
            Assert.IsNotNull(found, $"{name} が拾われていない");
            return found;
        }

        [Test]
        public void Scan_KeepsTheGivenOrderForSerializedFields()
        {
            var names = new[] { "_first", "_second", "_grouped", "_third" };
            var members = InspectorMemberScanner.Scan(typeof(Subject), names);

            CollectionAssert.AreEqual(
                names,
                members.Where(member => member.Kind == InspectorMemberKind.SerializedField).Select(member => member.Name),
                "保存されるフィールドは Unity が並べた順のまま");
        }

        [Test]
        public void Scan_AppendsNonSerializedMembersWithBaseClassesFirst()
        {
            var members = InspectorMemberScanner.Scan(typeof(Subject), new[] { "_first" });

            var extras = members
                .Where(member => member.Kind != InspectorMemberKind.SerializedField)
                .Select(member => member.Name)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { "_baseRuntimeValue", "BaseAction", "_runtimeValue", "Computed", "Action" },
                extras,
                "基底クラスのぶんが先、同じ型の中では フィールド → プロパティ → メソッド の順");
        }

        [Test]
        public void Scan_PicksUpOnlyTheMarkedNonSerializedMembers()
        {
            var members = InspectorMemberScanner.Scan(typeof(Subject), new string[0]);

            Assert.IsFalse(members.Any(member => member.Name == "NotShown"), "印の無いプロパティは出さない");
            Assert.IsFalse(members.Any(member => member.Name == "NotAButton"), "印の無いメソッドは出さない");
        }

        [Test]
        public void Scan_AssignsKindsAndDeclarationIndexes()
        {
            var members = InspectorMemberScanner.Scan(typeof(Subject), new[] { "_first" });

            Assert.AreEqual(InspectorMemberKind.SerializedField, Find(members, "_first").Kind);
            Assert.AreEqual(InspectorMemberKind.NonSerializedField, Find(members, "_runtimeValue").Kind);
            Assert.AreEqual(InspectorMemberKind.NativeProperty, Find(members, "Computed").Kind);
            Assert.AreEqual(InspectorMemberKind.Method, Find(members, "Action").Kind);

            CollectionAssert.AreEqual(
                Enumerable.Range(0, members.Count),
                members.Select(member => member.DeclarationIndex),
                "宣言順の通し番号は詰まっている");
        }

        [Test]
        public void Scan_ReadsAttributesAttachedToTheMember()
        {
            var members = InspectorMemberScanner.Scan(typeof(Subject), new[] { "_third", "_second" });

            var third = Find(members, "_third");
            Assert.AreEqual("三番目", third.GetAttribute<LabelTextAttribute>()?.Text);
            Assert.IsTrue(third.HasAttribute<RequiredAttribute>());
            Assert.AreEqual(typeof(GameObject), third.ValueType);

            Assert.AreEqual(-5, Find(members, "_second").Order);
            Assert.AreEqual(0, third.Order, "[Order] が無ければ 0");
        }

        [Test]
        public void GroupPath_IsTheDeepestDeclaredGroup()
        {
            // 浅いほうは途中の階層の見た目を決めるための宣言で、所属先ではない。
            var member = Find(InspectorMemberScanner.Scan(typeof(Subject), new[] { "_grouped" }), "_grouped");

            Assert.AreEqual("表示/戦闘", member.GroupPath);
        }

        [Test]
        public void GroupPath_IsNullWithoutAnyGroupAttribute()
        {
            var member = Find(InspectorMemberScanner.Scan(typeof(Subject), new[] { "_first" }), "_first");

            Assert.IsNull(member.GroupPath);
        }

        [Test]
        public void UsesInspectorAttributes_SeparatesUntouchedTypes()
        {
            Assert.IsTrue(InspectorMemberScanner.UsesInspectorAttributes(typeof(Subject)));
            Assert.IsTrue(InspectorMemberScanner.UsesInspectorAttributes(typeof(PlainScriptable)));
            Assert.IsFalse(InspectorMemberScanner.UsesInspectorAttributes(typeof(Plain)),
                "属性を使っていない型は既定のインスペクタに任せる");
        }

        [Test]
        public void Scan_RecursivelyBuildsSerializableMembersWithFullPropertyPaths()
        {
            var root = Find(InspectorMemberScanner.Scan(typeof(NestedRoot), new[] { nameof(NestedRoot.Settings) }), nameof(NestedRoot.Settings));

            Assert.IsTrue(root.HasChildren);
            Assert.AreEqual(nameof(NestedRoot.Settings), root.PropertyPath);

            var conditional = root.Children.Single(member => member.Name == nameof(NestedSettings.Conditional));
            Assert.AreEqual("Settings.Conditional", conditional.PropertyPath);
            Assert.AreEqual("Settings", conditional.OwnerPath);
            Assert.IsTrue(conditional.HasAttribute<ShowIfAttribute>());
            Assert.IsTrue(conditional.HasAttribute<OnValueChangedAttribute>());

            Assert.IsFalse(root.Children.Any(member => member.Name == nameof(NestedSettings.HiddenValue)),
                "[HideInInspector] の保存フィールドは入れ子の独自描画にも出さない");
            Assert.IsTrue(root.Children.Any(member => member.Name == "_explicitHiddenRuntimeValue"),
                "[ShowNonSerialized] で明示した非保存値は HideInInspector と独立した経路で維持する");

            CollectionAssert.IsSubsetOf(
                new[] { "Enabled", "Conditional", "_runtimeValue", "_explicitHiddenRuntimeValue", "Changed" },
                root.Children.Select(member => member.Name).ToArray());
        }

        [Test]
        public void UsesInspectorAttributes_FindsAttributesInsideSerializableFields()
        {
            Assert.IsTrue(InspectorMemberScanner.UsesInspectorAttributes(typeof(NestedRoot)),
                "根に属性が無くても入れ子の属性を使う型は既定 Inspector に戻さない");
        }

        [Test]
        public void Scan_LeavesSerializeReferenceFieldsToUnitysDefaultDrawer()
        {
            var root = Find(
                InspectorMemberScanner.Scan(typeof(ManagedReferenceRoot), new[] { nameof(ManagedReferenceRoot.Settings) }),
                nameof(ManagedReferenceRoot.Settings));

            Assert.IsFalse(root.HasChildren, "実行時の派生型を持てる SerializeReference は既定 PropertyField に任せる");
            Assert.IsFalse(InspectorMemberScanner.UsesInspectorAttributes(typeof(ManagedReferenceRoot)),
                "宣言型の内部属性だけで managed reference の独自描画を有効にしない");
        }

        [Test]
        public void Scan_StopsRecursiveSerializableTypesAndReportsTheBoundary()
        {
            var errors = new List<string>();
            var members = InspectorMemberScanner.Scan(typeof(RecursiveRoot), new[] { nameof(RecursiveRoot.Settings) }, errors);
            var settings = Find(members, nameof(RecursiveRoot.Settings));
            var next = settings.Children.Single(member => member.Name == nameof(RecursiveSettings.Next));

            Assert.IsFalse(next.HasChildren, "循環先を再び展開しない");
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("循環", errors[0]);
            StringAssert.Contains("Settings.Next", errors[0]);
        }

        [Test]
        public void Hierarchy_StopsAtUnityTypes()
        {
            CollectionAssert.AreEqual(new[] { typeof(PlainScriptable) }, InspectorMemberScanner.Hierarchy(typeof(PlainScriptable)),
                "ScriptableObject より上は舐めても意味が無い");

            CollectionAssert.AreEqual(new[] { typeof(BaseSubject), typeof(Subject) }, InspectorMemberScanner.Hierarchy(typeof(Subject)),
                "基底クラスが先");
        }

        [Test]
        public void Scan_ToleratesNamesWithoutAMatchingField()
        {
            // SerializedObject 側にしか無い項目（利用側が名前を変えた直後など）でも落ちない。
            var members = InspectorMemberScanner.Scan(typeof(Subject), new[] { "_gone" });

            Assert.AreEqual(1, members.Count(member => member.Kind == InspectorMemberKind.SerializedField));
            Assert.IsNull(Find(members, "_gone").Member);
            Assert.IsEmpty(Find(members, "_gone").Attributes);
        }
    }
}
