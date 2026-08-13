using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Inspector.Editor
{
    /// <summary>
    /// 値の妥当性を描画の直後に確かめる。
    /// <para>
    /// 範囲外の数値はその場で丸め、参照の間違いや未設定は文言で知らせる。
    /// <b>数値だけ書き換えて他は知らせるだけ</b>にしているのは、
    /// 数値は正しい値が一意に決まるのに対し、参照は何を入れるべきか機械には決められないため。
    /// </para>
    /// </summary>
    public static class InspectorValidators
    {
        /// <summary>このメンバーに付いている検査属性を全て適用する。</summary>
        public static void Draw(InspectorMember member, object target, SerializedProperty property, List<string> errors)
        {
            var attributes = member.Attributes;

            for (var i = 0; i < attributes.Length; i++)
            {
                switch (attributes[i])
                {
                    case MinValueAttribute min:
                        Clamp(property, min.Value, float.PositiveInfinity);
                        break;

                    case MaxValueAttribute max:
                        Clamp(property, float.NegativeInfinity, max.Value);
                        break;

                    case RequiredAttribute required:
                        DrawRequired(required, member, property);
                        break;

                    case AssetOnlyAttribute _:
                        DrawReferenceScope(property, member, assetExpected: true);
                        break;

                    case SceneObjectOnlyAttribute _:
                        DrawReferenceScope(property, member, assetExpected: false);
                        break;

                    case ValidateInputAttribute validate:
                        DrawValidateInput(validate, member, target, property, errors);
                        break;
                }
            }
        }

        /// <summary>数値を範囲に収める。対応していない型のフィールドには何もしない。</summary>
        public static void Clamp(SerializedProperty property, float min, float max)
        {
            if (property == null || property.hasMultipleDifferentValues) return;

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = ClampInt(property.intValue, min, max);
                    break;

                case SerializedPropertyType.Float:
                    property.floatValue = Mathf.Clamp(property.floatValue, min, max);
                    break;

                case SerializedPropertyType.Vector2:
                {
                    var value = property.vector2Value;
                    property.vector2Value = new Vector2(Mathf.Clamp(value.x, min, max), Mathf.Clamp(value.y, min, max));
                    break;
                }

                case SerializedPropertyType.Vector3:
                {
                    var value = property.vector3Value;
                    property.vector3Value = new Vector3(
                        Mathf.Clamp(value.x, min, max),
                        Mathf.Clamp(value.y, min, max),
                        Mathf.Clamp(value.z, min, max));
                    break;
                }

                case SerializedPropertyType.Vector4:
                {
                    var value = property.vector4Value;
                    property.vector4Value = new Vector4(
                        Mathf.Clamp(value.x, min, max),
                        Mathf.Clamp(value.y, min, max),
                        Mathf.Clamp(value.z, min, max),
                        Mathf.Clamp(value.w, min, max));
                    break;
                }

                case SerializedPropertyType.Vector2Int:
                {
                    var value = property.vector2IntValue;
                    property.vector2IntValue = new Vector2Int(ClampInt(value.x, min, max), ClampInt(value.y, min, max));
                    break;
                }

                case SerializedPropertyType.Vector3Int:
                {
                    var value = property.vector3IntValue;
                    property.vector3IntValue = new Vector3Int(
                        ClampInt(value.x, min, max),
                        ClampInt(value.y, min, max),
                        ClampInt(value.z, min, max));
                    break;
                }
            }
        }

        /// <summary>
        /// 未設定かどうか。参照は <c>null</c> と破棄済み、文字列は空白のみ、配列は要素 0 を未設定とみなす。
        /// </summary>
        public static bool IsUnset(SerializedProperty property)
        {
            if (property == null) return false;

            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue == null;

                case SerializedPropertyType.String:
                    return string.IsNullOrWhiteSpace(property.stringValue);

                case SerializedPropertyType.ExposedReference:
                    return property.exposedReferenceValue == null;

                case SerializedPropertyType.ManagedReference:
                    return property.managedReferenceValue == null;

                default:
                    // 文字列も isArray が true になるので、判定はこの順でなければならない。
                    return property.isArray && property.arraySize == 0;
            }
        }

        private static int ClampInt(int value, float min, float max)
        {
            if (!float.IsNegativeInfinity(min)) value = Mathf.Max(value, Mathf.CeilToInt(min));
            if (!float.IsPositiveInfinity(max)) value = Mathf.Min(value, Mathf.FloorToInt(max));

            return value;
        }

        private static void DrawRequired(RequiredAttribute attribute, InspectorMember member, SerializedProperty property)
        {
            if (property == null || property.hasMultipleDifferentValues) return;
            if (!IsUnset(property)) return;

            var message = string.IsNullOrEmpty(attribute.Message)
                ? $"{ObjectNames.NicifyVariableName(member.Name)} が設定されていない。"
                : attribute.Message;

            EditorGUILayout.HelpBox(message, MessageType.Error);
        }

        private static void DrawReferenceScope(SerializedProperty property, InspectorMember member, bool assetExpected)
        {
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) return;
            if (property.hasMultipleDifferentValues) return;

            var value = property.objectReferenceValue;
            if (value == null) return;

            var persistent = EditorUtility.IsPersistent(value);
            if (persistent == assetExpected) return;

            var label = ObjectNames.NicifyVariableName(member.Name);
            var message = assetExpected
                ? $"{label} にはプロジェクト内のアセットを入れること。シーン上のオブジェクトはプレハブに保存できず、参照が切れる。"
                : $"{label} にはシーン上のオブジェクトを入れること。";

            EditorGUILayout.HelpBox(message, MessageType.Error);
        }

        private static void DrawValidateInput(
            ValidateInputAttribute attribute,
            InspectorMember member,
            object target,
            SerializedProperty property,
            List<string> errors)
        {
            if (property != null && property.hasMultipleDifferentValues) return;
            if (target == null) return;

            var candidates = MemberResolver.FindMethods(target.GetType(), attribute.Method);
            MethodInfo chosen = null;
            var shape = ValidatorShape.None;

            for (var i = 0; i < candidates.Count; i++)
            {
                var found = Classify(candidates[i]);
                if (found == ValidatorShape.None) continue;

                chosen = candidates[i];
                shape = found;
                break;
            }

            if (chosen == null)
            {
                Report(errors, member.Name,
                    $"検査メソッド '{attribute.Method}' が見つからない。" +
                    " bool Method(値) / bool Method(値, out string) / bool Method() のいずれかの形にする。");
                return;
            }

            object[] arguments;
            switch (shape)
            {
                case ValidatorShape.NoArguments:
                    arguments = null;
                    break;

                case ValidatorShape.Value:
                    arguments = new[] { ReadValue(member, target, property) };
                    break;

                default:
                    arguments = new[] { ReadValue(member, target, property), null };
                    break;
            }

            bool passed;
            string message = null;

            try
            {
                passed = (bool)chosen.Invoke(chosen.IsStatic ? null : target, arguments);

                if (shape == ValidatorShape.ValueAndMessage && arguments != null) message = arguments[1] as string;
            }
            catch (Exception exception)
            {
                var inner = exception is TargetInvocationException invocation && invocation.InnerException != null
                    ? invocation.InnerException
                    : exception;

                Report(errors, member.Name, $"検査メソッド '{attribute.Method}' で例外が出た: {inner.Message}");
                return;
            }

            if (passed) return;

            if (string.IsNullOrEmpty(message)) message = attribute.Message;
            if (string.IsNullOrEmpty(message))
            {
                message = $"{ObjectNames.NicifyVariableName(member.Name)} の値が {attribute.Method} を通らない。";
            }

            EditorGUILayout.HelpBox(message, InspectorStyles.ToMessageType(attribute.Kind));
        }

        /// <summary>
        /// 検査に渡す現在値。
        /// <para>
        /// <c>SerializedProperty</c> 側から読むのは、まだ対象へ書き戻していない編集中の値を見るため。
        /// フィールドから直接読むと 1 フレーム古い値を検査してしまう。
        /// </para>
        /// </summary>
        private static object ReadValue(InspectorMember member, object target, SerializedProperty property)
        {
            if (property != null)
            {
                try
                {
                    return property.boxedValue;
                }
                catch (Exception)
                {
                    // boxedValue が扱えない形（一部のマネージド参照）は、フィールドから読む。
                }
            }

            return member.Member is FieldInfo field ? field.GetValue(field.IsStatic ? null : target) : null;
        }

        private enum ValidatorShape
        {
            None,
            NoArguments,
            Value,
            ValueAndMessage,
        }

        private static ValidatorShape Classify(MethodInfo method)
        {
            if (method.ReturnType != typeof(bool)) return ValidatorShape.None;

            var parameters = method.GetParameters();

            switch (parameters.Length)
            {
                case 0:
                    return ValidatorShape.NoArguments;

                case 1:
                    return parameters[0].IsOut ? ValidatorShape.None : ValidatorShape.Value;

                case 2:
                    return !parameters[0].IsOut
                        && parameters[1].IsOut
                        && parameters[1].ParameterType == typeof(string).MakeByRefType()
                        ? ValidatorShape.ValueAndMessage
                        : ValidatorShape.None;

                default:
                    return ValidatorShape.None;
            }
        }

        private static void Report(List<string> errors, string ownerName, string message)
        {
            if (errors == null) return;

            var text = $"{ownerName}: {message}";
            if (errors.Contains(text)) return;

            errors.Add(text);
        }
    }
}
