using System.Collections.Generic;
using System.Reflection;
using Inspector.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Inspector.Tests
{
    /// <summary>専用フィールド描画が保存する値を検査する。</summary>
    public sealed class InspectorFieldDrawersTests
    {
        private sealed class MixedValueSubject : ScriptableObject
        {
            public int Value;
            public string[] Options = { "A", "B" };
            public ScriptableObject Reference;
        }

        [Test]
        public void SceneBuildIndex_SkipsDisabledScenesBeforeTheSelection()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene("Assets/Disabled.unity", false),
                new EditorBuildSettingsScene("Assets/FirstEnabled.unity", true),
                new EditorBuildSettingsScene("Assets/SecondEnabled.unity", true),
            };
            var method = typeof(InspectorFieldDrawers).GetMethod("ResolveEnabledSceneBuildIndex", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method);
            Assert.AreEqual(0, method.Invoke(null, new object[] { scenes, 1 }));
            Assert.AreEqual(1, method.Invoke(null, new object[] { scenes, 2 }));
        }

        [Test]
        public void PopupDisplayIndex_MixedValuesDoNotUseTheFirstTargetsSelection()
        {
            var method = GetPrivateMethod("ResolveDisplayedPopupIndex");

            Assert.AreEqual(-1, method.Invoke(null, new object[] { true, 2 }));
            Assert.AreEqual(2, method.Invoke(null, new object[] { false, 2 }));
        }

        [Test]
        public void StoredValueSelection_UnknownValuesAreNotShownAsTheFirstCandidate()
        {
            var stringMethod = GetPrivateMethod("ResolveStringSelection");
            var intMethod = GetPrivateMethod("ResolveIntSelection");

            Assert.AreEqual(-1, stringMethod.Invoke(null, new object[] { new[] { "Default", "Effects" }, "Missing" }));
            Assert.AreEqual(0, stringMethod.Invoke(null, new object[] { new[] { "Default", "Effects" }, "Default" }));
            Assert.AreEqual(-1, intMethod.Invoke(null, new object[] { new[] { 0, 8 }, 31 }));
            Assert.AreEqual(0, intMethod.Invoke(null, new object[] { new[] { 0, 8 }, 0 }));
        }

        [Test]
        public void MissingOption_PreservesTheStoredValueAndMarksItAsMissing()
        {
            var labels = new List<string> { "Default" };
            var values = new List<int> { 0 };
            var method = GetPrivateMethod("InsertMissingOption").MakeGenericMethod(typeof(int));

            Assert.AreEqual(0, method.Invoke(null, new object[] { labels, values, 31 }));
            Assert.AreEqual("(候補に無い) 31", labels[0]);
            Assert.AreEqual(31, values[0]);
            Assert.AreEqual("Default", labels[1]);
            Assert.AreEqual(0, values[1]);
        }

        [Test]
        public void TryDraw_RestoresMixedValueDisplayWhenDrawingStopsEarly()
        {
            var first = ScriptableObject.CreateInstance<MixedValueSubject>();
            var second = ScriptableObject.CreateInstance<MixedValueSubject>();

            try
            {
                first.Value = 1;
                second.Value = 2;

                using (var serialized = new SerializedObject(new Object[] { first, second }))
                {
                    serialized.Update();
                    var property = serialized.FindProperty(nameof(MixedValueSubject.Value));
                    var member = new InspectorMember(
                        InspectorMemberKind.SerializedField,
                        nameof(MixedValueSubject.Value),
                        typeof(MixedValueSubject).GetField(nameof(MixedValueSubject.Value)),
                        new InspectorAttribute[] { new DropdownAttribute("MissingValues") },
                        0);

                    Assert.IsTrue(property.hasMultipleDifferentValues);
                    EditorGUI.showMixedValue = false;

                    Assert.IsFalse(InspectorFieldDrawers.TryDraw(member, first, property, GUIContent.none, new List<string>()));
                    Assert.IsFalse(EditorGUI.showMixedValue);
                }
            }
            finally
            {
                EditorGUI.showMixedValue = false;
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void DropdownOptionsMatch_RequiresEverySelectedOwnersCandidatesToMatch()
        {
            var first = ScriptableObject.CreateInstance<MixedValueSubject>();
            var second = ScriptableObject.CreateInstance<MixedValueSubject>();

            try
            {
                Assert.IsTrue(InspectorFieldDrawers.DropdownOptionsMatch(
                    new object[] { first, second },
                    nameof(MixedValueSubject.Options),
                    out var sameError));
                Assert.IsNull(sameError);

                second.Options = new[] { "A", "C" };
                Assert.IsFalse(InspectorFieldDrawers.DropdownOptionsMatch(
                    new object[] { first, second },
                    nameof(MixedValueSubject.Options),
                    out var differentError));
                Assert.IsNull(differentError, "候補不一致は取得エラーではない");
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void Expandable_MixedReferencesDoNotOpenTheFirstTarget()
        {
            Assert.IsFalse(InspectorFieldDrawers.CanExpandReference(hasMultipleDifferentValues: true));
            Assert.IsTrue(InspectorFieldDrawers.CanExpandReference(hasMultipleDifferentValues: false));
        }

        [Test]
        public void Expandable_AppliesConfiguredInitialStateOnlyOnce()
        {
            var closed = ScriptableObject.CreateInstance<MixedValueSubject>();
            var opened = ScriptableObject.CreateInstance<MixedValueSubject>();

            try
            {
                using (var serialized = new SerializedObject(closed))
                {
                    var property = serialized.FindProperty(nameof(MixedValueSubject.Reference));
                    property.isExpanded = true;
                    InspectorFieldDrawers.ApplyExpandableInitialState(new ExpandableAttribute(), property);
                    Assert.IsFalse(property.isExpanded, "既定値では最初に閉じる");
                }

                using (var serialized = new SerializedObject(opened))
                {
                    var property = serialized.FindProperty(nameof(MixedValueSubject.Reference));
                    property.isExpanded = false;
                    var attribute = new ExpandableAttribute { Expanded = true };

                    InspectorFieldDrawers.ApplyExpandableInitialState(attribute, property);
                    Assert.IsTrue(property.isExpanded, "Expanded=true は最初に開く");

                    property.isExpanded = false;
                    InspectorFieldDrawers.ApplyExpandableInitialState(attribute, property);
                    Assert.IsFalse(property.isExpanded, "利用者が閉じた後は初期値で開き直さない");
                }
            }
            finally
            {
                Object.DestroyImmediate(closed);
                Object.DestroyImmediate(opened);
            }
        }

        /// <summary>対象名の非公開静的メソッドを取得する。</summary>
        private static MethodInfo GetPrivateMethod(string name)
        {
            var method = typeof(InspectorFieldDrawers).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return method;
        }
    }
}
