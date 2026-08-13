using System;
using System.Collections.Generic;
using System.Reflection;

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
    public static class InspectorMemberScanner
    {
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

            var found = false;

            foreach (var declaring in Hierarchy(type))
            {
                if (HasAttributeOnAnyMember(declaring))
                {
                    found = true;
                    break;
                }
            }

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
            var members = new List<InspectorMember>();
            if (type == null) return members;

            var hierarchy = Hierarchy(type);
            var index = 0;

            if (serializedFieldNames != null)
            {
                for (var i = 0; i < serializedFieldNames.Count; i++)
                {
                    var name = serializedFieldNames[i];
                    var field = FindField(hierarchy, name);

                    members.Add(new InspectorMember(
                        InspectorMemberKind.SerializedField,
                        name,
                        field,
                        GetInspectorAttributes(field),
                        index++));
                }
            }

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
                        index++));
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
                        index++));
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
                        index++));
                }
            }

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
    }
}
