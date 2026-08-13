using System;
using System.Collections.Generic;
using UnityEditor;

namespace Inspector.Editor
{
    /// <summary>
    /// 型ごとの表示構造を作り置きしておく。
    /// <para>
    /// 走査と並べ替えは型が決まれば結果も決まるので、選択のたびにやり直す必要が無い。
    /// Inspector は 1 秒に何十回も描き直されるため、ここを毎回計算すると
    /// フィールドの多いコンポーネントで目に見えて重くなる。
    /// </para>
    /// <para>
    /// スクリプトを書き換えるとドメインが読み直され、この辞書ごと消える。
    /// 属性を足したのに反映されない、という状態にはならない。
    /// </para>
    /// </summary>
    public static class InspectorLayoutCache
    {
        private static readonly Dictionary<Type, InspectorLayout> Cache = new Dictionary<Type, InspectorLayout>();

        /// <summary>型に対応する表示構造を返す。初回だけ組み立てる。</summary>
        public static InspectorLayout Get(Type type, SerializedObject serializedObject)
        {
            if (type == null) return null;
            if (Cache.TryGetValue(type, out var cached)) return cached;

            var layout = InspectorLayoutBuilder.Build(
                InspectorMemberScanner.Scan(type, CollectSerializedFieldNames(serializedObject)));

            Cache[type] = layout;
            return layout;
        }

        /// <summary>作り置きを捨てる。属性の付け外しを試すツールから使う。</summary>
        public static void Clear() => Cache.Clear();

        /// <summary>
        /// Unity が描くはずだった順に、保存されるフィールド名を並べる。
        /// <c>m_Script</c> は別枠で描くので除く。
        /// </summary>
        public static List<string> CollectSerializedFieldNames(SerializedObject serializedObject)
        {
            var names = new List<string>();
            if (serializedObject == null) return names;

            var iterator = serializedObject.GetIterator();

            // 最初の一歩だけ子へ降り、あとは同じ階層を横に進む。
            // 入れ子の中まで並べてしまうと、親子が同列に並んで二重に描かれる。
            if (!iterator.NextVisible(true)) return names;

            do
            {
                if (string.Equals(iterator.propertyPath, "m_Script", StringComparison.Ordinal)) continue;

                names.Add(iterator.propertyPath);
            }
            while (iterator.NextVisible(false));

            return names;
        }
    }
}
