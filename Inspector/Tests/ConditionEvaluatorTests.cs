using System.Collections.Generic;
using Inspector.Editor;
using NUnit.Framework;

// 検査対象は「属性がどう付いているか」だけで、フィールドの値そのものは使わない。
#pragma warning disable 0649

namespace Inspector.Tests
{
    /// <summary>
    /// 条件表示の判定。
    /// 成立・不成立だけでなく、<b>設定を間違えたときにどう振る舞うか</b>まで確かめる。
    /// </summary>
    public sealed class ConditionEvaluatorTests
    {
        private enum Mode
        {
            Simple,
            Advanced,
            Expert,
        }

        private sealed class Subject
        {
            public bool Enabled = true;
            public bool Locked;
            public int Count = 3;
            public float Ratio = 0.5f;
            public Mode Selected = Mode.Simple;
            public int ConditionReads;

            public bool CountedEnabled
            {
                get
                {
                    ConditionReads++;
                    return Enabled;
                }
            }

            [ShowIf(nameof(Enabled))] public int ShownWhenEnabled;
            [HideIf(nameof(Enabled))] public int HiddenWhenEnabled;
            [ShowIf(nameof(Selected), Mode.Advanced, Mode.Expert)] public int ShownForAdvancedModes;
            [ShowIf("!" + nameof(Locked))] public int ShownWhenUnlocked;
            [ShowIf(ConditionOperator.And, nameof(Enabled), "!" + nameof(Locked))] public int ShownWhenBoth;
            [ShowIf(ConditionOperator.Or, nameof(Locked), nameof(Enabled))] public int ShownWhenEither;
            [ShowIf(nameof(Count), 3)] public int ShownForCountThree;
            [ShowIf(nameof(Ratio), 0.5f)] public int ShownForHalfRatio;
            [EnableIf(nameof(Enabled))] public int EditableWhenEnabled;
            [DisableIf(nameof(Locked))] public int EditableWhenUnlocked;
            [ReadOnly] public int NeverEditable;
            [ShowInPlayMode] public int OnlyWhilePlaying;
            [HideInPlayMode] public int OnlyWhileEditing;
            [ShowIf(nameof(Enabled))] [HideIf(nameof(Locked))] public int ShownWhenEnabledAndUnlocked;
            [ShowIf("_showTypo")] public int BrokenShowCondition;
            [HideIf("_hideTypo")] public int BrokenHideCondition;
            [EnableIf("_enableTypo")] public int BrokenEnableCondition;
            [DisableIf("_disableTypo")] public int BrokenDisableCondition;
            [ShowIf(nameof(Enabled), nameof(Locked))] public int MistakenMultiCondition;
            [ShowIf(nameof(Count))] public int NonBooleanCondition;
            [ShowIf(nameof(CountedEnabled))] public int CountedCondition;
        }

        private static readonly List<string> Errors = new List<string>();

        private static InspectorMember Member(string name)
        {
            var members = InspectorMemberScanner.Scan(typeof(Subject), new[] { name });
            Assert.AreEqual(name, members[0].Name);
            return members[0];
        }

        private static MemberState Resolve(Subject subject, string name, bool isPlaying = false)
        {
            Errors.Clear();
            return ConditionEvaluator.Resolve(subject, Member(name), isPlaying, Errors);
        }

        [Test]
        public void ShowIf_FollowsABooleanMember()
        {
            var subject = new Subject { Enabled = true };
            Assert.IsTrue(Resolve(subject, nameof(Subject.ShownWhenEnabled)).Visible);

            subject.Enabled = false;
            Assert.IsFalse(Resolve(subject, nameof(Subject.ShownWhenEnabled)).Visible);
        }

        [Test]
        public void HideIf_IsTheOppositeOfShowIf()
        {
            var subject = new Subject { Enabled = true };
            Assert.IsFalse(Resolve(subject, nameof(Subject.HiddenWhenEnabled)).Visible);

            subject.Enabled = false;
            Assert.IsTrue(Resolve(subject, nameof(Subject.HiddenWhenEnabled)).Visible);
        }

        [Test]
        public void ShowIf_MatchesAnyOfTheGivenValues()
        {
            var subject = new Subject();

            subject.Selected = Mode.Simple;
            Assert.IsFalse(Resolve(subject, nameof(Subject.ShownForAdvancedModes)).Visible);

            subject.Selected = Mode.Advanced;
            Assert.IsTrue(Resolve(subject, nameof(Subject.ShownForAdvancedModes)).Visible);

            subject.Selected = Mode.Expert;
            Assert.IsTrue(Resolve(subject, nameof(Subject.ShownForAdvancedModes)).Visible);
        }

        [Test]
        public void LeadingExclamationMark_InvertsTheMember()
        {
            var subject = new Subject { Locked = false };
            Assert.IsTrue(Resolve(subject, nameof(Subject.ShownWhenUnlocked)).Visible);

            subject.Locked = true;
            Assert.IsFalse(Resolve(subject, nameof(Subject.ShownWhenUnlocked)).Visible);
        }

        [Test]
        public void ConditionOperator_And_NeedsEveryMember()
        {
            var subject = new Subject { Enabled = true, Locked = false };
            Assert.IsTrue(Resolve(subject, nameof(Subject.ShownWhenBoth)).Visible);

            subject.Locked = true;
            Assert.IsFalse(Resolve(subject, nameof(Subject.ShownWhenBoth)).Visible);

            subject.Locked = false;
            subject.Enabled = false;
            Assert.IsFalse(Resolve(subject, nameof(Subject.ShownWhenBoth)).Visible);
        }

        [Test]
        public void ConditionOperator_Or_NeedsOnlyOneMember()
        {
            var subject = new Subject { Enabled = false, Locked = false };
            Assert.IsFalse(Resolve(subject, nameof(Subject.ShownWhenEither)).Visible);

            subject.Locked = true;
            Assert.IsTrue(Resolve(subject, nameof(Subject.ShownWhenEither)).Visible);
        }

        [Test]
        public void SeveralConditionAttributes_AllHaveToAgree()
        {
            var subject = new Subject { Enabled = true, Locked = false };
            Assert.IsTrue(Resolve(subject, nameof(Subject.ShownWhenEnabledAndUnlocked)).Visible);

            subject.Locked = true;
            Assert.IsFalse(Resolve(subject, nameof(Subject.ShownWhenEnabledAndUnlocked)).Visible,
                "ShowIf が通っていても HideIf が成立すれば隠れる");
        }

        [Test]
        public void NumericComparison_IgnoresTheExactNumericType()
        {
            var subject = new Subject { Count = 3, Ratio = 0.5f };

            Assert.IsTrue(Resolve(subject, nameof(Subject.ShownForCountThree)).Visible);
            Assert.IsTrue(Resolve(subject, nameof(Subject.ShownForHalfRatio)).Visible);

            subject.Count = 4;
            Assert.IsFalse(Resolve(subject, nameof(Subject.ShownForCountThree)).Visible);
        }

        [Test]
        public void EnableIf_And_DisableIf_ChangeEditabilityNotVisibility()
        {
            var subject = new Subject { Enabled = false, Locked = true };

            var enableIf = Resolve(subject, nameof(Subject.EditableWhenEnabled));
            Assert.IsTrue(enableIf.Visible, "灰色にするだけで、消しはしない");
            Assert.IsFalse(enableIf.Enabled);

            var disableIf = Resolve(subject, nameof(Subject.EditableWhenUnlocked));
            Assert.IsTrue(disableIf.Visible);
            Assert.IsFalse(disableIf.Enabled);
        }

        [Test]
        public void ReadOnly_AlwaysDisables()
        {
            var state = Resolve(new Subject(), nameof(Subject.NeverEditable));

            Assert.IsTrue(state.Visible);
            Assert.IsFalse(state.Enabled);
        }

        [Test]
        public void PlayModeAttributes_FollowTheEditorState()
        {
            var subject = new Subject();

            Assert.IsFalse(Resolve(subject, nameof(Subject.OnlyWhilePlaying), isPlaying: false).Visible);
            Assert.IsTrue(Resolve(subject, nameof(Subject.OnlyWhilePlaying), isPlaying: true).Visible);

            Assert.IsTrue(Resolve(subject, nameof(Subject.OnlyWhileEditing), isPlaying: false).Visible);
            Assert.IsFalse(Resolve(subject, nameof(Subject.OnlyWhileEditing), isPlaying: true).Visible);
        }

        [TestCase(nameof(Subject.BrokenShowCondition), "_showTypo")]
        [TestCase(nameof(Subject.BrokenHideCondition), "_hideTypo")]
        [TestCase(nameof(Subject.BrokenEnableCondition), "_enableTypo")]
        [TestCase(nameof(Subject.BrokenDisableCondition), "_disableTypo")]
        public void MisspelledMember_KeepsTheFieldVisibleAndEditable(string memberName, string missingMember)
        {
            // 設定ミスで対象が消えたり編集不能になったりすると修正できないため、属性の効果を適用しない。
            var state = Resolve(new Subject(), memberName);

            Assert.IsTrue(state.Visible);
            Assert.IsTrue(state.Enabled);
            Assert.AreEqual(1, Errors.Count);
            StringAssert.Contains(missingMember, Errors[0]);
        }

        [Test]
        public void ComparingABooleanAgainstStrings_IsReportedAsAMistake()
        {
            // [ShowIf(nameof(a), nameof(b))] は「a の値が文字列 "b" と等しいか」になってしまう。
            // 複数条件のつもりで書きがちなので、黙って不成立にせず指摘する。
            var state = Resolve(new Subject(), nameof(Subject.MistakenMultiCondition));

            Assert.IsTrue(state.Visible);
            Assert.AreEqual(1, Errors.Count);
            StringAssert.Contains("ConditionOperator", Errors[0]);
        }

        [Test]
        public void NonBooleanMemberWithoutComparisonValue_IsReported()
        {
            var state = Resolve(new Subject(), nameof(Subject.NonBooleanCondition));

            Assert.IsTrue(state.Visible);
            Assert.AreEqual(1, Errors.Count);
            StringAssert.Contains("bool", Errors[0]);
        }

        [Test]
        public void AreEqual_TreatsEnumsAndNumbersOfDifferentWidthsAsComparable()
        {
            Assert.IsTrue(ConditionEvaluator.AreEqual(3, 3.0));
            Assert.IsTrue(ConditionEvaluator.AreEqual(0.5f, 0.5d));
            Assert.IsTrue(ConditionEvaluator.AreEqual(Mode.Advanced, 1));
            Assert.IsTrue(ConditionEvaluator.AreEqual(Mode.Expert, Mode.Expert));

            Assert.IsFalse(ConditionEvaluator.AreEqual(Mode.Advanced, 2));
            Assert.IsFalse(ConditionEvaluator.AreEqual(true, 1), "bool と数値は別物として扱う");
            Assert.IsFalse(ConditionEvaluator.AreEqual("1", 1));
            Assert.IsFalse(ConditionEvaluator.AreEqual(null, 0));
            Assert.IsTrue(ConditionEvaluator.AreEqual(null, null));
        }

        [Test]
        public void ResolveAll_KeepsMixedVisibilityVisibleButDisablesEditing()
        {
            Errors.Clear();
            var first = new Subject { Enabled = false };
            var second = new Subject { Enabled = true };

            var state = ConditionEvaluator.ResolveAll(
                new object[] { first, second },
                Member(nameof(Subject.ShownWhenEnabled)),
                false,
                Errors);

            Assert.IsTrue(state.Visible, "1 件でも条件に合うなら先頭対象の値だけで隠さない");
            Assert.IsFalse(state.Enabled, "条件外の対象まで一括変更しない");
            Assert.IsTrue(state.Mixed);
            Assert.IsEmpty(Errors, "条件の混在は設定ミスではない");
        }

        [Test]
        public void ResolveAll_HidesOnlyWhenEverySelectedTargetIsHidden()
        {
            var state = ConditionEvaluator.ResolveAll(
                new object[] { new Subject { Enabled = false }, new Subject { Enabled = false } },
                Member(nameof(Subject.ShownWhenEnabled)),
                false,
                Errors);

            Assert.IsFalse(state.Visible);
            Assert.IsFalse(state.Mixed);
        }

        [Test]
        public void ResolveAll_EvaluatesEverySelectedTarget()
        {
            Errors.Clear();
            var first = new Subject { Enabled = false };
            var second = new Subject { Enabled = true };

            ConditionEvaluator.ResolveAll(
                new object[] { first, second },
                Member(nameof(Subject.CountedCondition)),
                false,
                Errors);

            Assert.AreEqual(1, first.ConditionReads);
            Assert.AreEqual(1, second.ConditionReads, "先頭の不成立だけで後続対象の検証を打ち切らない");
        }
    }
}
