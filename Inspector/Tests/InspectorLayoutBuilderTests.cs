using System.Collections.Generic;
using System.Linq;
using Inspector.Editor;
using NUnit.Framework;

// 検査対象は「属性がどう付いているか」だけで、フィールドの値そのものは使わない。
#pragma warning disable 0649

namespace Inspector.Tests
{
    /// <summary>並べ替えとグループの入れ子。GUI に触らない部分なので、ここで見た目の骨格を確定できる。</summary>
    public sealed class InspectorLayoutBuilderTests
    {
        private sealed class Ordered
        {
            [Order(10)] public int Last;
            public int MiddleA;
            public int MiddleB;
            [Order(-10)] public int First;
        }

        private sealed class Grouped
        {
            [BoxGroup("体力")] public int Hp;
            public int Loose;
            [BoxGroup("体力")] public int Regen;
            [Foldout("上級")] [BoxGroup("上級/物理")] public int Drag;

            // 途中の「孤立」を誰も宣言していない場合の受け皿。
            [BoxGroup("孤立/深い")] public int Orphan;
        }

        private sealed class Tabbed
        {
            [TabGroup("設定", "見た目")] public int Tint;
            [TabGroup("設定", "挙動")] public int Speed;
            [TabGroup("設定", "見た目")] public int Opacity;
        }

        private sealed class Conflicting
        {
            [BoxGroup("両方")] public int A;
            [Foldout("両方")] public int B;
        }

        private sealed class TabsAndFoldout
        {
            [Foldout("設定")] public int Direct;
            [TabGroup("設定", "音")] public int Volume;
        }

        private sealed class Spaced
        {
            [BoxGroup("組 / 中")] public int A;
            [BoxGroup("組/中")] public int B;
        }

        private static InspectorLayout Build<T>(params string[] serializedFieldNames)
        {
            return InspectorLayoutBuilder.Build(InspectorMemberScanner.Scan(typeof(T), serializedFieldNames));
        }

        private static IEnumerable<string> Names(InspectorGroup group)
        {
            return group.Items.Select(item => item.IsGroup ? "[" + item.Group.Name + "]" : item.Member.Name);
        }

        [Test]
        public void Order_SortsAscendingAndKeepsDeclarationOrderForTies()
        {
            var layout = Build<Ordered>("Last", "MiddleA", "MiddleB", "First");

            CollectionAssert.AreEqual(
                new[] { "First", "MiddleA", "MiddleB", "Last" },
                layout.Members.Select(member => member.Name),
                "同じ Order のものは宣言順のまま");
        }

        [Test]
        public void Groups_AppearWhereTheirFirstMemberWas()
        {
            var layout = Build<Grouped>("Hp", "Loose", "Regen", "Drag");

            CollectionAssert.AreEqual(new[] { "[体力]", "Loose", "[上級]" }, Names(layout.Root));

            var health = layout.Root.Items[0].Group;
            Assert.AreEqual(GroupKind.Box, health.Kind);
            CollectionAssert.AreEqual(new[] { "Hp", "Regen" }, Names(health),
                "離れた位置に書かれていても 1 つにまとまる");
        }

        [Test]
        public void NestedPaths_CreateIntermediateGroups()
        {
            var layout = Build<Grouped>("Hp", "Loose", "Regen", "Drag");
            var advanced = layout.Root.Items[2].Group;

            Assert.AreEqual("上級", advanced.Name);
            Assert.AreEqual(GroupKind.Foldout, advanced.Kind, "[Foldout] で宣言した種類が効く");

            var physics = advanced.Items[0].Group;
            Assert.AreEqual("上級/物理", physics.Path);
            Assert.AreEqual(GroupKind.Box, physics.Kind);
            CollectionAssert.AreEqual(new[] { "Drag" }, Names(physics));
        }

        [Test]
        public void UndeclaredIntermediateGroups_FallBackToFoldout()
        {
            var layout = Build<Grouped>("Orphan");
            var orphan = layout.Root.Items[0].Group;

            Assert.AreEqual("孤立", orphan.Name);
            Assert.AreEqual(GroupKind.Foldout, orphan.Kind, "誰も種類を宣言していない階層は折りたたみにする");
            Assert.AreEqual(GroupKind.Box, orphan.Items[0].Group.Kind, "宣言されている末尾の階層はそのまま");
            Assert.IsEmpty(layout.Errors, "宣言が無いだけなら間違いではない");
        }

        [Test]
        public void TabGroup_TurnsTheParentIntoATabStrip()
        {
            var layout = Build<Tabbed>("Tint", "Speed", "Opacity");

            var tabs = layout.Root.Items[0].Group;
            Assert.AreEqual("設定", tabs.Name);
            Assert.AreEqual(GroupKind.Tabs, tabs.Kind);
            Assert.IsTrue(tabs.HasTabPages);

            CollectionAssert.AreEqual(new[] { "[見た目]", "[挙動]" }, Names(tabs), "タブの並びは最初に現れた順");

            var look = tabs.Items[0].Group;
            Assert.AreEqual(GroupKind.TabPage, look.Kind);
            CollectionAssert.AreEqual(new[] { "Tint", "Opacity" }, Names(look));
        }

        [Test]
        public void ConflictingGroupKinds_AreReportedAndTheFirstOneWins()
        {
            var layout = Build<Conflicting>("A", "B");

            Assert.AreEqual(1, layout.Errors.Count);
            StringAssert.Contains("両方", layout.Errors[0]);
            Assert.AreEqual(GroupKind.Box, layout.Root.Items[0].Group.Kind);
        }

        [Test]
        public void TabStrip_WinsOverAnExplicitFoldoutOnTheSamePath()
        {
            // タブ列を折りたたみとして描くと、ぶら下がるタブが素通しになって見た目が壊れる。
            var layout = Build<TabsAndFoldout>("Direct", "Volume");

            Assert.AreEqual(1, layout.Errors.Count);

            var group = layout.Root.Items[0].Group;
            Assert.AreEqual(GroupKind.Tabs, group.Kind);
            CollectionAssert.AreEqual(new[] { "Direct", "[音]" }, Names(group),
                "タブに乗らないメンバーはタブ列の手前に残る");
        }

        [Test]
        public void GroupPaths_AreMatchedAfterNormalization()
        {
            var layout = Build<Spaced>("A", "B");

            Assert.AreEqual(1, layout.Root.Items.Count, "空白の有無で別グループに割れない");
            CollectionAssert.AreEqual(new[] { "A", "B" }, Names(layout.Root.Items[0].Group.Items[0].Group));
        }

        [Test]
        public void MembersWithoutGroups_StayAtTheRoot()
        {
            var layout = Build<Ordered>("Last", "MiddleA", "MiddleB", "First");

            Assert.IsFalse(layout.Root.Items.Any(item => item.IsGroup));
            Assert.AreEqual(4, layout.Root.Items.Count);
        }
    }
}
