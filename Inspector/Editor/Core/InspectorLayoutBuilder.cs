using System.Collections.Generic;

namespace Inspector.Editor
{
    /// <summary>
    /// メンバーの一覧から、並び順とグループの入れ子を決める。
    /// <para>
    /// GUI に触らないので、表示のされ方はここだけを見て確かめられる。
    /// </para>
    /// </summary>
    internal static class InspectorLayoutBuilder
    {
        private sealed class KindDeclaration
        {
            public GroupKind Kind;
            public GroupAttribute Source;
            public bool Implied;
        }

        /// <summary>
        /// 表示構造を組み立てる。
        /// <para>
        /// グループは<b>そこに入る最初のメンバーが現れた位置</b>に置かれる。
        /// 並べ替えは <see cref="OrderAttribute"/> の昇順で、同じ値なら宣言順を保つ。
        /// </para>
        /// </summary>
        public static InspectorLayout Build(IReadOnlyList<InspectorMember> members)
        {
            var errors = new List<string>();
            var sorted = Sort(members);
            var kinds = CollectKinds(sorted, errors);

            var root = new InspectorGroup(string.Empty, string.Empty, GroupKind.Foldout, null);
            var groups = new Dictionary<string, InspectorGroup>();

            for (var i = 0; i < sorted.Count; i++)
            {
                var member = sorted[i];
                var owner = member.GroupPath == null ? root : EnsureGroup(root, groups, kinds, member.GroupPath);

                owner.Items.Add(InspectorLayoutItem.Of(member));
            }

            return new InspectorLayout(root, sorted, errors);
        }

        private static List<InspectorMember> Sort(IReadOnlyList<InspectorMember> members)
        {
            var sorted = new List<InspectorMember>(members);

            // 同じ Order のときは宣言順のままにしたい。DeclarationIndex は重複しないので、
            // これを第 2 キーにすれば安定な並びになる（List.Sort 自体は安定ではない）。
            sorted.Sort((left, right) =>
            {
                var byOrder = left.Order.CompareTo(right.Order);
                return byOrder != 0 ? byOrder : left.DeclarationIndex.CompareTo(right.DeclarationIndex);
            });

            return sorted;
        }

        /// <summary>
        /// どの階層をどう描くかを集める。
        /// <para>
        /// タブは <c>[TabGroup("設定", "音")]</c> の 1 つの属性で
        /// 「設定 はタブ列」「設定/音 はその 1 枚」の 2 つを同時に宣言している。
        /// 親側は明示されないので、ここで補う。
        /// </para>
        /// </summary>
        private static Dictionary<string, KindDeclaration> CollectKinds(
            IReadOnlyList<InspectorMember> members,
            List<string> errors)
        {
            var kinds = new Dictionary<string, KindDeclaration>();

            for (var i = 0; i < members.Count; i++)
            {
                var attributes = members[i].Attributes;

                for (var j = 0; j < attributes.Length; j++)
                {
                    if (!(attributes[j] is GroupAttribute group)) continue;

                    var path = GroupPathUtility.Normalize(group.Path);
                    if (path == null)
                    {
                        errors.Add($"{members[i].Name}: グループ名が空。");
                        continue;
                    }

                    Declare(kinds, errors, path, group.Kind, group, implied: false);

                    if (group.Kind != GroupKind.TabPage) continue;

                    var parent = GroupPathUtility.Parent(path);
                    if (parent == null)
                    {
                        errors.Add($"{members[i].Name}: [TabGroup] にはタブ列の名前とタブ名の両方が要る。");
                        continue;
                    }

                    Declare(kinds, errors, parent, GroupKind.Tabs, group, implied: true);
                }
            }

            return kinds;
        }

        private static void Declare(
            Dictionary<string, KindDeclaration> kinds,
            List<string> errors,
            string path,
            GroupKind kind,
            GroupAttribute source,
            bool implied)
        {
            if (!kinds.TryGetValue(path, out var existing))
            {
                kinds[path] = new KindDeclaration { Kind = kind, Source = source, Implied = implied };
                return;
            }

            if (existing.Kind == kind) return;

            errors.Add($"グループ '{path}' に種類の違う指定が混ざっている（{existing.Kind} と {kind}）。");

            // タブ列は子のタブがぶら下がる前提の構造なので、ここだけは押し通す。
            // 折りたたみとして描いてしまうとタブが素通しになり、見た目が壊れる。
            if (kind == GroupKind.Tabs)
            {
                existing.Kind = kind;
                existing.Source = source;
                existing.Implied = implied;
            }
        }

        private static InspectorGroup EnsureGroup(
            InspectorGroup root,
            Dictionary<string, InspectorGroup> groups,
            Dictionary<string, KindDeclaration> kinds,
            string path)
        {
            var segments = GroupPathUtility.Split(path);
            var current = root;
            var accumulated = string.Empty;

            for (var i = 0; i < segments.Length; i++)
            {
                accumulated = i == 0 ? segments[i] : accumulated + "/" + segments[i];

                if (!groups.TryGetValue(accumulated, out var child))
                {
                    kinds.TryGetValue(accumulated, out var declaration);

                    child = new InspectorGroup(
                        accumulated,
                        segments[i],
                        declaration?.Kind ?? GroupKind.Foldout,
                        declaration?.Source);

                    groups[accumulated] = child;
                    current.Items.Add(InspectorLayoutItem.Of(child));
                }

                current = child;
            }

            return current;
        }
    }
}
