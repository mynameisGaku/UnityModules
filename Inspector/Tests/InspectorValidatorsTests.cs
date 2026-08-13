using Inspector.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;

namespace Inspector.Tests
{
    /// <summary>検査に使う値の判定。実際の <see cref="SerializedObject"/> を通して確かめる。</summary>
    internal sealed class ValidationSubject : ScriptableObject
    {
        public int Count;
        public float Ratio;
        public Vector3 Offset;
        public Vector2Int Cell;
        public GameObject Reference;
        public string Text;
        public int[] Items;

        [ValidateInput(nameof(ValidateCount))]
        public int ValidatedCount;

        public int ValidationCalls;

        private bool ValidateCount(int value)
        {
            ValidationCalls++;
            return value >= 0;
        }

        public string TextValue => Text;

        public int OverloadValidationCalls;

        private bool ValidateOverload(string value)
        {
            OverloadValidationCalls++;
            return !string.IsNullOrWhiteSpace(value);
        }

        private bool ValidateOverload(int value)
        {
            Assert.Fail("string property に int overload を使ってはならない");
            return false;
        }
    }

    public sealed class InspectorValidatorsTests
    {
        private ValidationSubject _subject;
        private SerializedObject _serialized;

        [SetUp]
        public void SetUp()
        {
            _subject = ScriptableObject.CreateInstance<ValidationSubject>();
            _serialized = new SerializedObject(_subject);
        }

        [TearDown]
        public void TearDown()
        {
            _serialized?.Dispose();
            if (_subject != null) Object.DestroyImmediate(_subject);
        }

        private SerializedProperty Property(string name)
        {
            var property = _serialized.FindProperty(name);
            Assert.IsNotNull(property, $"{name} が見つからない");
            return property;
        }

        [Test]
        public void Clamp_PullsIntegersAndFloatsIntoRange()
        {
            var count = Property(nameof(ValidationSubject.Count));
            count.intValue = -5;
            InspectorValidators.Clamp(count, 0f, float.PositiveInfinity);
            Assert.AreEqual(0, count.intValue);

            count.intValue = 50;
            InspectorValidators.Clamp(count, float.NegativeInfinity, 10f);
            Assert.AreEqual(10, count.intValue);

            var ratio = Property(nameof(ValidationSubject.Ratio));
            ratio.floatValue = 2.5f;
            InspectorValidators.Clamp(ratio, 0f, 1f);
            Assert.AreEqual(1f, ratio.floatValue);
        }

        [Test]
        public void Clamp_WorksComponentWiseOnVectors()
        {
            var offset = Property(nameof(ValidationSubject.Offset));
            offset.vector3Value = new Vector3(-1f, 0.5f, 9f);
            InspectorValidators.Clamp(offset, 0f, 1f);
            Assert.AreEqual(new Vector3(0f, 0.5f, 1f), offset.vector3Value);

            var cell = Property(nameof(ValidationSubject.Cell));
            cell.vector2IntValue = new Vector2Int(-3, 7);
            InspectorValidators.Clamp(cell, 0f, 5f);
            Assert.AreEqual(new Vector2Int(0, 5), cell.vector2IntValue);
        }

        [Test]
        public void Clamp_LeavesUnsupportedTypesAlone()
        {
            var text = Property(nameof(ValidationSubject.Text));
            text.stringValue = "そのまま";

            InspectorValidators.Clamp(text, 0f, 1f);

            Assert.AreEqual("そのまま", text.stringValue);
        }

        [Test]
        public void IsUnset_CoversReferencesStringsAndArrays()
        {
            Assert.IsTrue(InspectorValidators.IsUnset(Property(nameof(ValidationSubject.Reference))));

            var text = Property(nameof(ValidationSubject.Text));
            Assert.IsTrue(InspectorValidators.IsUnset(text), "null の文字列は未設定");

            text.stringValue = "   ";
            Assert.IsTrue(InspectorValidators.IsUnset(text), "空白だけの文字列も未設定として扱う");

            text.stringValue = "値";
            Assert.IsFalse(InspectorValidators.IsUnset(text));

            var items = Property(nameof(ValidationSubject.Items));
            Assert.IsTrue(InspectorValidators.IsUnset(items), "要素 0 の配列は未設定");

            items.arraySize = 1;
            Assert.IsFalse(InspectorValidators.IsUnset(items));
        }

        [Test]
        public void IsUnset_DoesNotTreatNumbersAsUnset()
        {
            // 文字列も配列も SerializedProperty から見ると isArray が真になる。
            // 判定の順番を間違えると、0 の int まで「未設定」になってしまう。
            var count = Property(nameof(ValidationSubject.Count));
            count.intValue = 0;

            Assert.IsFalse(InspectorValidators.IsUnset(count));
        }

        [Test]
        public void Relativize_TrimsTheProjectFolderAndNormalizesSeparators()
        {
            var inside = Application.dataPath.Replace('/', '\\') + "\\Settings\\config.json";

            Assert.AreEqual("Assets/Settings/config.json", InspectorFieldDrawers.Relativize(inside, true));
            StringAssert.Contains("Assets/Settings/config.json", InspectorFieldDrawers.Relativize(inside, false));
        }

        [Test]
        public void Relativize_KeepsPathsOutsideTheProjectAbsolute()
        {
            const string outside = "D:/shared/data/table.csv";

            Assert.AreEqual(outside, InspectorFieldDrawers.Relativize(outside, true));
        }

        [Test]
        public void DrawAll_RunsValidateInputForEveryMixedTarget()
        {
            var second = ScriptableObject.CreateInstance<ValidationSubject>();

            try
            {
                _subject.ValidatedCount = 1;
                second.ValidatedCount = 2;

                using (var serialized = new SerializedObject(new Object[] { _subject, second }))
                {
                    serialized.Update();
                    var property = serialized.FindProperty(nameof(ValidationSubject.ValidatedCount));
                    Assert.IsTrue(property.hasMultipleDifferentValues);

                    var member = InspectorMemberScanner.Scan(
                        typeof(ValidationSubject),
                        new[] { nameof(ValidationSubject.ValidatedCount) })[0];

                    InspectorValidators.DrawAll(
                        member,
                        new object[] { _subject, second },
                        new Object[] { _subject, second },
                        property,
                        new System.Collections.Generic.List<string>());
                }

                Assert.AreEqual(1, _subject.ValidationCalls);
                Assert.AreEqual(1, second.ValidationCalls, "mixed 値でも先頭だけで検査を終えない");
            }
            finally
            {
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void DrawAll_MixedConditionKeepsValidationButDoesNotClampValues()
        {
            var second = ScriptableObject.CreateInstance<ValidationSubject>();

            try
            {
                _subject.Count = -4;
                second.Count = 3;

                using (var serialized = new SerializedObject(new Object[] { _subject, second }))
                {
                    serialized.Update();
                    var property = serialized.FindProperty(nameof(ValidationSubject.Count));
                    var member = new InspectorMember(
                        InspectorMemberKind.SerializedField,
                        nameof(ValidationSubject.Count),
                        typeof(ValidationSubject).GetField(nameof(ValidationSubject.Count)),
                        new InspectorAttribute[]
                        {
                            new MinValueAttribute(0f),
                        },
                        0);

                    InspectorValidators.DrawAll(
                        member,
                        new object[] { _subject, second },
                        new Object[] { _subject, second },
                        property,
                        new System.Collections.Generic.List<string>(),
                        allowMutation: false);

                    Assert.IsFalse(serialized.ApplyModifiedProperties(), "表示だけの検証で保存値を書き換えない");
                }

                Assert.AreEqual(-4, _subject.Count, "条件混在時は MinValue でも clamp しない");
                Assert.AreEqual(3, second.Count);

                var before = InspectorGUILayout.CapturePropertyValues(
                    new Object[] { _subject, second },
                    nameof(ValidationSubject.Count));
                Assert.IsEmpty(InspectorGUILayout.FindChangedTargets(before),
                    "条件混在の描画で値が変わらなければ OnValueChanged の対象を作らない");
            }
            finally
            {
                Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void DrawAll_ReadOnlyPropertyUsesRequiredAndMatchingValidatorOverload()
        {
            _subject.Text = "有効";
            var propertyInfo = typeof(ValidationSubject).GetProperty(nameof(ValidationSubject.TextValue));
            Assert.IsNotNull(propertyInfo);
            var member = new InspectorMember(
                InspectorMemberKind.NativeProperty,
                nameof(ValidationSubject.TextValue),
                propertyInfo,
                new InspectorAttribute[]
                {
                    new RequiredAttribute(),
                    new ValidateInputAttribute("ValidateOverload"),
                },
                0);
            var errors = new List<string>();

            InspectorValidators.DrawAll(
                member,
                new object[] { _subject },
                new Object[] { _subject },
                null,
                errors);

            Assert.AreEqual(1, _subject.OverloadValidationCalls);
            Assert.IsEmpty(errors);
        }
    }
}
