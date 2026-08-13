using System;
using System.Collections;
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
    internal static class InspectorValidators
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

        /// <summary>
        /// 複数選択した全対象を検査する。
        /// 保存値が混在している場合は対象ごとの <see cref="SerializedProperty"/> を読み、先頭だけで判定を終えない。
        /// </summary>
        internal static void DrawAll(
            InspectorMember member,
            IReadOnlyList<object> owners,
            UnityEngine.Object[] rootTargets,
            SerializedProperty property,
            List<string> errors,
            bool allowMutation = true)
        {
            var attributes = member.Attributes;

            for (var i = 0; i < attributes.Length; i++)
            {
                switch (attributes[i])
                {
                    case MinValueAttribute min:
                        if (allowMutation) Clamp(property, min.Value, float.PositiveInfinity);
                        break;

                    case MaxValueAttribute max:
                        if (allowMutation) Clamp(property, float.NegativeInfinity, max.Value);
                        break;

                    case RequiredAttribute required:
                        DrawRequiredAll(required, member, owners, rootTargets, property);
                        break;

                    case AssetOnlyAttribute _:
                        DrawReferenceScopeAll(rootTargets, property, member, assetExpected: true);
                        break;

                    case SceneObjectOnlyAttribute _:
                        DrawReferenceScopeAll(rootTargets, property, member, assetExpected: false);
                        break;

                    case ValidateInputAttribute validate:
                        DrawValidateInputAll(validate, member, owners, rootTargets, property, errors);
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

        /// <summary>保存プロパティを持たない読み取り専用値が未設定か。</summary>
        internal static bool IsUnsetValue(object value)
        {
            if (value == null) return true;
            if (value is UnityEngine.Object unityObject) return unityObject == null;
            if (value is string text) return string.IsNullOrWhiteSpace(text);
            if (value is ICollection collection) return collection.Count == 0;
            return false;
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

        private static void DrawRequiredAll(
            RequiredAttribute attribute,
            InspectorMember member,
            IReadOnlyList<object> owners,
            UnityEngine.Object[] rootTargets,
            SerializedProperty property)
        {
            if (property == null)
            {
                var readOnlyCheckedCount = 0;
                var readOnlyUnsetCount = 0;

                for (var i = 0; owners != null && i < owners.Count; i++)
                {
                    if (!TryReadValue(member, owners[i], null, out var value)) continue;
                    readOnlyCheckedCount++;
                    if (IsUnsetValue(value)) readOnlyUnsetCount++;
                }

                if (readOnlyUnsetCount == 0) return;

                var readOnlyMessage = string.IsNullOrEmpty(attribute.Message)
                    ? $"{ObjectNames.NicifyVariableName(member.Name)} が {readOnlyUnsetCount}/{readOnlyCheckedCount} 件で設定されていない。"
                    : readOnlyCheckedCount <= 1 ? attribute.Message : $"{attribute.Message}（{readOnlyUnsetCount}/{readOnlyCheckedCount} 件）";
                EditorGUILayout.HelpBox(readOnlyMessage, MessageType.Error);
                return;
            }

            if (property == null || !property.hasMultipleDifferentValues)
            {
                DrawRequired(attribute, member, property);
                return;
            }

            var checkedCount = 0;
            var unsetCount = 0;

            for (var i = 0; rootTargets != null && i < rootTargets.Length; i++)
            {
                var root = rootTargets[i];
                if (root == null) continue;

                using (var serialized = new SerializedObject(root))
                {
                    serialized.Update();
                    var targetProperty = serialized.FindProperty(property.propertyPath);
                    if (targetProperty == null) continue;

                    checkedCount++;
                    if (IsUnset(targetProperty)) unsetCount++;
                }
            }

            if (unsetCount == 0) return;

            var message = string.IsNullOrEmpty(attribute.Message)
                ? $"{ObjectNames.NicifyVariableName(member.Name)} が {unsetCount}/{checkedCount} 件で設定されていない。"
                : $"{attribute.Message}（{unsetCount}/{checkedCount} 件）";

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

        private static void DrawReferenceScopeAll(
            UnityEngine.Object[] rootTargets,
            SerializedProperty property,
            InspectorMember member,
            bool assetExpected)
        {
            if (property == null || !property.hasMultipleDifferentValues)
            {
                DrawReferenceScope(property, member, assetExpected);
                return;
            }

            var checkedCount = 0;
            var invalidCount = 0;

            for (var i = 0; rootTargets != null && i < rootTargets.Length; i++)
            {
                var root = rootTargets[i];
                if (root == null) continue;

                using (var serialized = new SerializedObject(root))
                {
                    serialized.Update();
                    var targetProperty = serialized.FindProperty(property.propertyPath);
                    if (targetProperty == null || targetProperty.propertyType != SerializedPropertyType.ObjectReference) continue;

                    checkedCount++;
                    var value = targetProperty.objectReferenceValue;
                    if (value != null && EditorUtility.IsPersistent(value) != assetExpected) invalidCount++;
                }
            }

            if (invalidCount == 0) return;

            var label = ObjectNames.NicifyVariableName(member.Name);
            var message = assetExpected
                ? $"{label} にシーン上のオブジェクトが {invalidCount}/{checkedCount} 件入っている。プロジェクト内のアセットを入れること。"
                : $"{label} にプロジェクト内のアセットが {invalidCount}/{checkedCount} 件入っている。シーン上のオブジェクトを入れること。";

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
            if (!TryValidateInput(attribute, member, target, property, errors, out var failureMessage)) return;
            if (failureMessage == null) return;

            EditorGUILayout.HelpBox(failureMessage, InspectorStyles.ToMessageType(attribute.Kind));
        }

        private static void DrawValidateInputAll(
            ValidateInputAttribute attribute,
            InspectorMember member,
            IReadOnlyList<object> owners,
            UnityEngine.Object[] rootTargets,
            SerializedProperty property,
            List<string> errors)
        {
            if (owners == null || owners.Count == 0) return;

            var evaluatedCount = 0;
            var failureCount = 0;
            string firstMessage = null;
            var differentMessages = false;

            for (var i = 0; i < owners.Count; i++)
            {
                var owner = owners[i];
                if (owner == null) continue;

                if (property == null || !property.hasMultipleDifferentValues)
                {
                    AccumulateValidation(
                        attribute,
                        member,
                        owner,
                        property,
                        errors,
                        ref evaluatedCount,
                        ref failureCount,
                        ref firstMessage,
                        ref differentMessages);
                    continue;
                }

                if (rootTargets == null || i >= rootTargets.Length || rootTargets[i] == null) continue;

                using (var serialized = new SerializedObject(rootTargets[i]))
                {
                    serialized.Update();
                    var targetProperty = serialized.FindProperty(property.propertyPath);
                    if (targetProperty == null) continue;

                    AccumulateValidation(
                        attribute,
                        member,
                        owner,
                        targetProperty,
                        errors,
                        ref evaluatedCount,
                        ref failureCount,
                        ref firstMessage,
                        ref differentMessages);
                }
            }

            if (failureCount == 0) return;

            var message = evaluatedCount <= 1
                ? firstMessage
                : differentMessages
                    ? $"{failureCount}/{evaluatedCount} 件が検査を通らない。最初の理由: {firstMessage}"
                    : $"{firstMessage}（{failureCount}/{evaluatedCount} 件）";

            EditorGUILayout.HelpBox(message, InspectorStyles.ToMessageType(attribute.Kind));
        }

        private static void AccumulateValidation(
            ValidateInputAttribute attribute,
            InspectorMember member,
            object owner,
            SerializedProperty property,
            List<string> errors,
            ref int evaluatedCount,
            ref int failureCount,
            ref string firstMessage,
            ref bool differentMessages)
        {
            if (!TryValidateInput(attribute, member, owner, property, errors, out var failureMessage)) return;

            evaluatedCount++;
            if (failureMessage == null) return;

            failureCount++;
            if (firstMessage == null) firstMessage = failureMessage;
            else if (!string.Equals(firstMessage, failureMessage, StringComparison.Ordinal)) differentMessages = true;
        }

        private static bool TryValidateInput(
            ValidateInputAttribute attribute,
            InspectorMember member,
            object target,
            SerializedProperty property,
            List<string> errors,
            out string failureMessage)
        {
            failureMessage = null;
            if (target == null) return false;

            if (!TryReadValue(member, target, property, out var currentValue))
            {
                Report(errors, member.Name, "検査する現在値を取得できない。");
                return false;
            }

            var candidates = MemberResolver.FindMethods(target.GetType(), attribute.Method);
            MethodInfo chosen = null;
            var shape = ValidatorShape.None;
            var bestScore = int.MinValue;

            for (var i = 0; i < candidates.Count; i++)
            {
                var found = Classify(candidates[i]);
                if (found == ValidatorShape.None) continue;

                var score = CompatibilityScore(candidates[i], found, currentValue);
                if (score < 0 || score <= bestScore) continue;

                chosen = candidates[i];
                shape = found;
                bestScore = score;
            }

            if (chosen == null)
            {
                Report(errors, member.Name,
                    $"検査メソッド '{attribute.Method}' が見つからない。" +
                    " bool Method(値) / bool Method(値, out string) / bool Method() のいずれかの形にする。");
                return false;
            }

            object[] arguments;
            switch (shape)
            {
                case ValidatorShape.NoArguments:
                    arguments = null;
                    break;

                case ValidatorShape.Value:
                    arguments = new[] { currentValue };
                    break;

                default:
                    arguments = new[] { currentValue, null };
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
                return false;
            }

            if (passed) return true;

            if (string.IsNullOrEmpty(message)) message = attribute.Message;
            if (string.IsNullOrEmpty(message))
            {
                message = $"{ObjectNames.NicifyVariableName(member.Name)} の値が {attribute.Method} を通らない。";
            }

            failureMessage = message;
            return true;
        }

        /// <summary>
        /// 検査に渡す現在値。
        /// <para>
        /// <c>SerializedProperty</c> 側から読むのは、まだ対象へ書き戻していない編集中の値を見るため。
        /// フィールドから直接読むと 1 フレーム古い値を検査してしまう。
        /// </para>
        /// </summary>
        private static bool TryReadValue(
            InspectorMember member,
            object target,
            SerializedProperty property,
            out object value)
        {
            value = null;
            if (property != null)
            {
                try
                {
                    value = property.boxedValue;
                    return true;
                }
                catch (Exception)
                {
                    // boxedValue が扱えない形（一部のマネージド参照）は、フィールドから読む。
                }
            }

            try
            {
                switch (member.Member)
                {
                    case FieldInfo field:
                        value = field.GetValue(field.IsStatic ? null : target);
                        return true;
                    case PropertyInfo propertyInfo when propertyInfo.CanRead && propertyInfo.GetIndexParameters().Length == 0:
                        var getter = propertyInfo.GetGetMethod(true);
                        value = propertyInfo.GetValue(getter != null && getter.IsStatic ? null : target);
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static int CompatibilityScore(MethodInfo method, ValidatorShape shape, object value)
        {
            if (shape == ValidatorShape.NoArguments) return 0;

            var parameterType = method.GetParameters()[0].ParameterType;
            if (value == null)
            {
                return !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null ? 1 : -1;
            }

            var valueType = value.GetType();
            if (parameterType == valueType) return 3;
            return parameterType.IsAssignableFrom(valueType) ? 2 : -1;
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
            if (method.ReturnType != typeof(bool) || method.ContainsGenericParameters) return ValidatorShape.None;

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
