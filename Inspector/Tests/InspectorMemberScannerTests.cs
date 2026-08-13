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
