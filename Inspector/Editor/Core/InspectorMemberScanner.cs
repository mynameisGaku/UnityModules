using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Inspector.Editor
{
    /// <summary>
    /// 型を舐めて、Inspector に出すメンバーを拾い出す。
    /// <para>
    /// 保存されるフィールドの並びは Unity が持っている順（<c>SerializedObject</c> を辿った順）に従う。
    /// リフレクションの返す順は宣言順とは限らず、継承がある型では特に当てにならないため、
    /// 「Unity が表示するはずだった順」を外から渡してもらう形にしている。
    /// </para>
    /// </summary>
    internal static class InspectorMemberScanner
    {
        /// <summary>循環参照や極端な型で Inspector の組み立てが終わらなくなるのを防ぐ深さ。</summary>
        internal const int MaxNestedDepth = 8;

        private const BindingFlags DeclaredMembers =
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        private static readonly InspectorAttribute[] NoAttributes = new InspectorAttribute[0];
        private static readonly Dictionary<Type, bool> UsesAttributesCache = new Dictionary<Type, bool>();

        /// <summary>
        /// このモジュールの属性が 1 つでも使われているか。
        /// <para>
        /// 使われていない型では既定のインスペクタにそのまま任せる。
        /// 何も書いていないクラスの見た目が、このモジュールを入れただけで変わるのは避けたい。
        /// </para>
        /// </summary>
        public static bool UsesInspectorAttributes(Type type)
        {
            if (type == null) return false;
            if (UsesAttributesCache.TryGetValue(type, out var cached)) return cached;

            var found = UsesInspectorAttributes(type, 0, new HashSet<Type>());

            UsesAttributesCache[type] = found;
            return found;
        }

        /// <summary>
        /// 表示対象のメンバーを集める。
        /// </summary>
        /// <param name="type">調べる型。</param>
        /// <param name="serializedFieldNames">
        /// <c>SerializedObject</c> を辿って得た、保存されるフィールド名の並び（<c>m_Script</c> は除く）。
        /// </param>
        public static List<InspectorMember> Scan(Type type, IReadOnlyList<string> serializedFieldNames)
        {
            return Scan(type, serializedFieldNames, null);
        }

        /// <summary>
        /// 表示対象を集め、入れ子の打ち切り理由を <paramref name="errors"/> に積む。
        /// </summary>
        internal static List<InspectorMember> Scan(
            Type type,
            IReadOnlyList<string> serializedFieldNames,
            List<string> errors)
        {
            var members = new List<InspectorMember>();
            if (type == null) return members;

            var hierarchy = Hierarchy(type);
            var ancestry = new HashSet<Type> { type };
            var index = 0;

            if (serializedFieldNames != null)
            {
                for (var i = 0; i < serializedFieldNames.Count; i++)
                {
                    var name = serializedFieldNames[i];
                    var field = FindField(hierarchy, name);
                    var children = CreateNestedMembers(field, name, 1, ancestry, errors);

                    members.Add(new InspectorMember(
                        InspectorMemberKind.SerializedField,
                        name,
                        field,
                        GetInspectorAttributes(field),
                        index++,
                        name,
                        null,
                        children));
                }
            }

            AppendNonSerializedMembers(members, hierarchy, null, ref index);

            return members;
        }

        /// <summary>基底クラスが先に来る継承の並び。Unity 側の型に入ったところで打ち切る。</summary>
        public static List<Type> Hierarchy(Type type)
        {
            var chain = new List<Type>();

            for (var current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                if (IsEngineType(current)) break;

                chain.Add(current);
            }

            chain.Reverse();
            return chain;
        }

        /// <summary>入れ子型の保存メンバーと、明示された非保存メンバーを同じ所有者の下へ集める。</summary>
        private static IReadOnlyList<InspectorMember> CreateNestedMembers(
            FieldInfo field,
            string ownerPath,
            int depth,
            HashSet<Type> ancestry,
            List<string> errors)
        {
            if (!IsNestedSerializableField(field)) return Array.Empty<InspectorMember>();

            var nestedType = field.FieldType;
            if (!UsesInspectorAttributes(nestedType)) return Array.Empty<InspectorMember>();

            if (depth > MaxNestedDepth)
            {
                Report(errors, ownerPath, $"入れ子が最大深さ {MaxNestedDepth} を超えたため、これより内側は既定表示に戻した。");
                return Array.Empty<InspectorMember>();
            }

            if (!ancestry.Add(nestedType))
            {
                Report(errors, ownerPath, $"型 {nestedType.Name} が循環しているため、これより内側は既定表示に戻した。");
                return Array.Empty<InspectorMember>();
            }

            try
            {
                var hierarchy = Hierarchy(nestedType);
                var members = new List<InspectorMember>();
                var index = 0;

                for (var h = 0; h < hierarchy.Count; h++)
                {
                    var fields = hierarchy[h].GetFields(DeclaredMembers);

                    for (var i = 0; i < fields.Length; i++)
                    {
                        var childField = fields[i];
                        if (!IsSerializedField(childField)) continue;

                        var propertyPath = ownerPath + "." + childField.Name;
                        var children = CreateNestedMembers(childField, propertyPath, depth + 1, ancestry, errors);

                        members.Add(new InspectorMember(
                            InspectorMemberKind.SerializedField,
                            childField.Name,
                            childField,
                            GetInspectorAttributes(childField),
                            index++,
                            propertyPath,
                            ownerPath,
                            children));
                    }
                }

                AppendNonSerializedMembers(members, hierarchy, ownerPath, ref index);
                return members;
            }
            finally
            {
                ancestry.Remove(nestedType);
            }
        }

        /// <summary>保存されない明示メンバーを、宣言順で保存フィールドの後ろへ足す。</summary>
        private static void AppendNonSerializedMembers(
            List<InspectorMember> members,
            IReadOnlyList<Type> hierarchy,
            string ownerPath,
            ref int index)
        {
            // 保存されないものは Unity の並びに現れないので、宣言順（基底クラスが先）で後ろに足す。
            for (var h = 0; h < hierarchy.Count; h++)
            {
                var declaring = hierarchy[h];

                var fields = declaring.GetFields(DeclaredMembers);
                for (var i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    if (field.GetCustomAttribute<ShowNonSerializedAttribute>() == null) continue;
                    if (Contains(members, field.Name)) continue;

                    members.Add(new InspectorMember(
                        InspectorMemberKind.NonSerializedField,
                        field.Name,
                        field,
                        GetInspectorAttributes(field),
                        index++,
                        null,
                        ownerPath,
                        null));
                }

                var properties = declaring.GetProperties(DeclaredMembers);
                for (var i = 0; i < properties.Length; i++)
                {
                    var property = properties[i];
                    if (property.GetCustomAttribute<ShowNativePropertyAttribute>() == null) continue;
                    if (!property.CanRead) continue;
                    if (property.GetIndexParameters().Length != 0) continue;
                    if (Contains(members, property.Name)) continue;

                    members.Add(new InspectorMember(
                        InspectorMemberKind.NativeProperty,
                        property.Name,
                        property,
                        GetInspectorAttributes(property),
                        index++,
                        null,
                        ownerPath,
                        null));
                }

                var methods = declaring.GetMethods(DeclaredMembers);
                for (var i = 0; i < methods.Length; i++)
                {
                    var method = methods[i];
                    if (method.GetCustomAttribute<ButtonAttribute>() == null) continue;
                    if (Contains(members, method.Name)) continue;

                    members.Add(new InspectorMember(
                        InspectorMemberKind.Method,
                        method.Name,
                        method,
                        GetInspectorAttributes(method),
                        index++,
                        null,
                        ownerPath,
                        null));
                }
            }
        }

        /// <summary>型自身か、保存される入れ子型の内側に Inspector 属性があるか調べる。</summary>
        private static bool UsesInspectorAttributes(Type type, int depth, HashSet<Type> ancestry)
        {
            if (type == null || depth > MaxNestedDepth || !ancestry.Add(type)) return false;

            try
            {
                var hierarchy = Hierarchy(type);

                for (var h = 0; h < hierarchy.Count; h++)
                {
                    var declaring = hierarchy[h];
                    if (HasAttributeOnAnyMember(declaring)) return true;

                    var fields = declaring.GetFields(DeclaredMembers);
                    for (var i = 0; i < fields.Length; i++)
                    {
                        if (!IsNestedSerializableField(fields[i])) continue;
                        if (UsesInspectorAttributes(fields[i].FieldType, depth + 1, ancestry)) return true;
                    }
                }

                return false;
            }
            finally
            {
                ancestry.Remove(type);
            }
        }

        /// <summary>Unity が保存するフィールドか。入れ子を独自表示するときの欠落防止に使う。</summary>
        private static bool IsSerializedField(FieldInfo field)
        {
            if (field == null || field.IsStatic || field.IsLiteral || field.IsInitOnly) return false;
            if (field.IsNotSerialized) return false;
            if (field.IsDefined(typeof(HideInInspector), true)) return false;

            var hasStorageAttribute = field.IsPublic
                || field.IsDefined(typeof(SerializeField), true)
                || field.IsDefined(typeof(SerializeReference), true);

            return hasStorageAttribute && IsSupportedSerializedType(
                field.FieldType,
                field.IsDefined(typeof(SerializeReference), true),
                allowCollection: true);
        }

        /// <summary>Unity の保存対象になる型か。System の複雑な値や辞書を誤って欄へ足さない。</summary>
        private static bool IsSupportedSerializedType(Type type, bool serializeReference, bool allowCollection)
        {
            if (type == null || type.IsPointer || type.IsByRef || typeof(Delegate).IsAssignableFrom(type)) return false;
            if (serializeReference) return !type.IsValueType && !typeof(UnityEngine.Object).IsAssignableFrom(type);
            if (type.IsPrimitive || type.IsEnum || type == typeof(string)) return true;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return true;

            if (type.IsArray)
            {
                return allowCollection
                    && type.GetArrayRank() == 1
                    && IsSupportedSerializedType(type.GetElementType(), false, allowCollection: false);
            }

            if (type.IsGenericType)
            {
                return allowCollection
                    && type.GetGenericTypeDefinition() == typeof(List<>)
                    && IsSupportedSerializedType(type.GetGenericArguments()[0], false, allowCollection: false);
            }

            if (!type.IsDefined(typeof(SerializableAttribute), false)) return false;

            // DateTime など System 側の複雑な値は [Serializable] でも Unity の保存形式ではない。
            var space = type.Namespace;
            return space == null || !space.Equals("System", StringComparison.Ordinal)
                && !space.StartsWith("System.", StringComparison.Ordinal);
        }

        /// <summary>配列や Unity 組み込み型ではない、直接たどれる Serializable フィールドか。</summary>
        private static bool IsNestedSerializableField(FieldInfo field)
        {
            if (!IsSerializedField(field)) return false;
            if (field.IsDefined(typeof(SerializeReference), true)) return false;

            var type = field.FieldType;
            if (type == null || type.IsArray || type.IsEnum || type.IsPrimitive || type == typeof(string)) return false;
            if (type.IsGenericType) return false;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return false;
            if (IsEngineType(type)) return false;

            return type.IsDefined(typeof(SerializableAttribute), false);
        }

        private static bool IsEngineType(Type type)
        {
            var space = type.Namespace;
            if (space == null) return false;

            return space == "UnityEngine" || space.StartsWith("UnityEngine.", StringComparison.Ordinal)
                || space == "UnityEditor" || space.StartsWith("UnityEditor.", StringComparison.Ordinal);
        }

        private static bool HasAttributeOnAnyMember(Type type)
        {
            var fields = type.GetFields(DeclaredMembers);
            for (var i = 0; i < fields.Length; i++)
            {
                if (fields[i].IsDefined(typeof(InspectorAttribute), true)) return true;
            }

            var properties = type.GetProperties(DeclaredMembers);
            for (var i = 0; i < properties.Length; i++)
            {
                if (properties[i].IsDefined(typeof(InspectorAttribute), true)) return true;
            }

            var methods = type.GetMethods(DeclaredMembers);
            for (var i = 0; i < methods.Length; i++)
            {
                if (methods[i].IsDefined(typeof(InspectorAttribute), true)) return true;
            }

            return false;
        }

        private static FieldInfo FindField(List<Type> hierarchy, string name)
        {
            // 同名のフィールドが基底と派生の両方にある場合、Unity が描くのは派生側なので後ろから探す。
            for (var i = hierarchy.Count - 1; i >= 0; i--)
            {
                var field = hierarchy[i].GetField(name, DeclaredMembers);
                if (field != null) return field;
            }

            return null;
        }

        private static bool Contains(List<InspectorMember> members, string name)
        {
            for (var i = 0; i < members.Count; i++)
            {
                if (string.Equals(members[i].Name, name, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        private static InspectorAttribute[] GetInspectorAttributes(MemberInfo member)
        {
            if (member == null) return NoAttributes;

            var raw = member.GetCustomAttributes(typeof(InspectorAttribute), true);
            if (raw.Length == 0) return NoAttributes;

            var result = new InspectorAttribute[raw.Length];
            for (var i = 0; i < raw.Length; i++) result[i] = (InspectorAttribute)raw[i];

            return result;
        }

        /// <summary>同じ入れ子の問題を描画のたびに重ねない形で記録する。</summary>
        private static void Report(List<string> errors, string propertyPath, string message)
        {
            if (errors == null || string.IsNullOrEmpty(message)) return;

            var text = string.IsNullOrEmpty(propertyPath) ? message : propertyPath + ": " + message;
            if (!errors.Contains(text)) errors.Add(text);
        }
    }
}
