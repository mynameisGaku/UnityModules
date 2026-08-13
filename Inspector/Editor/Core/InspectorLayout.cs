using System.Collections.Generic;

namespace Inspector.Editor
{
    /// <summary>グループの中に並ぶもの 1 件。メンバーか、入れ子のグループのどちらか。</summary>
    public readonly struct InspectorLayoutItem
    {
        private InspectorLayoutItem(InspectorMember member, InspectorGroup group)
        {
            Member = member;
            Group = group;
        }

        public InspectorMember Member { get; }

        public InspectorGroup Group { get; }

        public bool IsGroup => Group != null;

        public static InspectorLayoutItem Of(InspectorMember member) => new InspectorLayoutItem(member, null);

        public static InspectorLayoutItem Of(InspectorGroup group) => new InspectorLayoutItem(null, group);
    }

    /// <summary>まとまり 1 つ。<see cref="Items"/> には所属メンバーと子グループが表示順で並ぶ。</summary>
    public sealed class InspectorGroup
    {
        public InspectorGroup(string path, string name, GroupKind kind, GroupAttribute source)
        {
            Path = path;
            Name = name;
            Kind = kind;
            Source = source;
        }

        /// <summary><c>/</c> 区切りの完全なパス。最上位（根）は空文字。</summary>
        public string Path { get; }

        /// <summary>見出しに使う末尾の名前。</summary>
        public string Name { get; }

        public GroupKind Kind { get; }

        /// <summary>この階層の種類を決めた属性。<see cref="HorizontalGroupAttribute.ShowLabel"/> などを読むのに使う。</summary>
        public GroupAttribute Source { get; }

        public List<InspectorLayoutItem> Items { get; } = new List<InspectorLayoutItem>();

        /// <summary>この階層が <see cref="GroupKind.Tabs"/> のとき、タブとして描かれる子グループ。</summary>
        public bool HasTabPages
        {
            get
            {
                for (var i = 0; i < Items.Count; i++)
                {
                    if (Items[i].IsGroup && Items[i].Group.Kind == GroupKind.TabPage) return true;
                }

                return false;
            }
        }
    }

    /// <summary>型 1 つぶんの表示構造。<see cref="InspectorLayoutBuilder"/> が作る。</summary>
    public sealed class InspectorLayout
    {
        public InspectorLayout(InspectorGroup root, IReadOnlyList<InspectorMember> members, IReadOnlyList<string> errors)
        {
            Root = root;
            Members = members;
            Errors = errors;
        }

        /// <summary>最上位のまとまり。<see cref="InspectorGroup.Items"/> を上から描けばよい。</summary>
        public InspectorGroup Root { get; }

        /// <summary>表示順に並べ替えた全メンバー。</summary>
        public IReadOnlyList<InspectorMember> Members { get; }

        /// <summary>グループ指定の食い違いなど、組み立て時に見つかった設定ミス。</summary>
        public IReadOnlyList<string> Errors { get; }
    }
}
