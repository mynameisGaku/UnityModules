using System;
using System.Collections.Generic;
using System.Reflection;

namespace Inspector.Editor
{
    /// <summary>Inspector に出るメンバーの出自。</summary>
    internal enum InspectorMemberKind
    {
        /// <summary>保存されるフィールド。<c>SerializedProperty</c> を通して描く。</summary>
        SerializedField,

        /// <summary><see cref="ShowNonSerializedAttribute"/> の付いた、保存されないフィールド。</summary>
        NonSerializedField,

        /// <summary><see cref="ShowNativePropertyAttribute"/> の付いたプロパティ。</summary>
        NativeProperty,

        /// <summary><see cref="ButtonAttribute"/> の付いたメソッド。</summary>
        Method,
    }

    /// <summary>
    /// Inspector に 1 行（あるいは 1 ブロック）として出るもの 1 件。
    /// <para>
    /// 型ごとに 1 回だけ作られて使い回される。ここに入るのは
    /// <b>選択中のオブジェクトに依存しない情報だけ</b>で、値や条件の判定結果は持たない。
    /// </para>
    /// </summary>
    internal sealed class InspectorMember
    {
        private static readonly InspectorAttribute[] NoAttributes = new InspectorAttribute[0];

        public InspectorMember(
            InspectorMemberKind kind,
            string name,
            MemberInfo member,
            InspectorAttribute[] attributes,
            int declarationIndex)
            : this(kind, name, member, attributes, declarationIndex, null, null, null)
        {
        }

        /// <summary>保存パスと入れ子構造を含む、走査処理用のメンバーを作る。</summary>
        internal InspectorMember(
            InspectorMemberKind kind,
            string name,
            MemberInfo member,
            InspectorAttribute[] attributes,
            int declarationIndex,
            string propertyPath,
            string ownerPath,
            IReadOnlyList<InspectorMember> children)
        {
            Kind = kind;
            Name = name;
            Member = member;
            Attributes = attributes ?? NoAttributes;
            DeclarationIndex = declarationIndex;
            PropertyPath = propertyPath ?? (kind == InspectorMemberKind.SerializedField ? name : null);
            OwnerPath = ownerPath;
            Children = children ?? Array.Empty<InspectorMember>();
            ChildLayout = Children.Count > 0 ? InspectorLayoutBuilder.Build(Children) : null;

            Order = GetAttribute<OrderAttribute>()?.Order ?? 0;
            GroupPath = ResolveGroupPath(Attributes);
        }

        public InspectorMemberKind Kind { get; }

        /// <summary>フィールド名・プロパティ名・メソッド名。<see cref="InspectorMemberKind.SerializedField"/> では property path でもある。</summary>
        public string Name { get; }

        /// <summary>
        /// <see cref="SerializedProperty"/> を根から探す完全なパス。
        /// 保存されないメンバーでは <c>null</c>。
        /// </summary>
        internal string PropertyPath { get; }

        /// <summary>
        /// 条件や変更通知を解決する所有者までの保存パス。
        /// 最上位のメンバーでは <c>null</c>。
        /// </summary>
        internal string OwnerPath { get; }

        /// <summary>
        /// この保存フィールドの中で独自に描くメンバー。
        /// 入れ子型が Inspector 属性を使わない場合は空。
        /// </summary>
        internal IReadOnlyList<InspectorMember> Children { get; }

        /// <summary>入れ子の Inspector 属性を独自に描く必要があるか。</summary>
        internal bool HasChildren => Children.Count > 0;

        /// <summary>入れ子メンバーの並びとグループを組み立てた結果。</summary>
        internal InspectorLayout ChildLayout { get; }

        /// <summary>元になったリフレクション情報。見つからなかった場合のみ <c>null</c>。</summary>
        public MemberInfo Member { get; }

        /// <summary>このメンバーに付いているこのモジュールの属性。</summary>
        public InspectorAttribute[] Attributes { get; }

        /// <summary>宣言順の通し番号。<see cref="Order"/> が同じもの同士の並びを決める。</summary>
        public int DeclarationIndex { get; }

        /// <summary><see cref="OrderAttribute"/> の値。無ければ 0。</summary>
        public int Order { get; }

        /// <summary>所属するグループのパス。どのグループにも入らないなら <c>null</c>。</summary>
        public string GroupPath { get; }

        public T GetAttribute<T>() where T : class
        {
            for (var i = 0; i < Attributes.Length; i++)
            {
                if (Attributes[i] is T match) return match;
            }

            return null;
        }

        /// <summary>指定した型の属性を宣言順で全て返す。1 つも無ければ長さ 0 の配列。</summary>
        public T[] GetAttributes<T>() where T : class
        {
            var count = 0;
            for (var i = 0; i < Attributes.Length; i++)
            {
                if (Attributes[i] is T) count++;
            }

            if (count == 0) return Array.Empty<T>();

            var result = new T[count];
            var next = 0;
            for (var i = 0; i < Attributes.Length; i++)
            {
                if (Attributes[i] is T match) result[next++] = match;
            }

            return result;
        }

        public bool HasAttribute<T>() where T : class => GetAttribute<T>() != null;

        /// <summary>このメンバーが実際に返す（あるいは受け取る）値の型。ボタンなら <c>null</c>。</summary>
        public Type ValueType
        {
            get
            {
                switch (Member)
                {
                    case FieldInfo field: return field.FieldType;
                    case PropertyInfo property: return property.PropertyType;
                    default: return null;
                }
            }
        }

        /// <summary>
        /// 所属先は「一番深いパスを持つグループ属性」。
        /// 浅いほうは途中の階層の見た目を宣言するためだけに書かれている。
        /// </summary>
        private static string ResolveGroupPath(InspectorAttribute[] attributes)
        {
            string deepest = null;
            var deepestDepth = -1;

            for (var i = 0; i < attributes.Length; i++)
            {
                if (!(attributes[i] is GroupAttribute group)) continue;

                var path = GroupPathUtility.Normalize(group.Path);
                if (path == null) continue;

                var depth = GroupPathUtility.Depth(path);
                if (depth <= deepestDepth) continue;

                deepest = path;
                deepestDepth = depth;
            }

            return deepest;
        }
    }
}
