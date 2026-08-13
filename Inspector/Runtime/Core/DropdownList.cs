using System.Collections;
using System.Collections.Generic;

namespace Inspector
{
    /// <summary>
    /// <see cref="DropdownAttribute"/> に「表示名と値が違う選択肢」を渡すための入れ物。
    /// <para>
    /// 単に候補を並べるだけなら <c>IEnumerable&lt;T&gt;</c> を返せばよい。
    /// こちらは <c>Vector3</c> や ScriptableObject のように
    /// <c>ToString()</c> が読めない値を選ばせたいときに使う。
    /// </para>
    /// <code>
    /// [Dropdown(nameof(SpawnPoints))]
    /// [SerializeField] private Vector3 _spawn;
    ///
    /// private DropdownList&lt;Vector3&gt; SpawnPoints =&gt; new DropdownList&lt;Vector3&gt;
    /// {
    ///     { "入口", new Vector3(0f, 0f, 0f) },
    ///     { "中庭", new Vector3(12f, 0f, 4f) },
    /// };
    /// </code>
    /// </summary>
    public sealed class DropdownList<T> : IDropdownList
    {
        private readonly List<KeyValuePair<string, object>> _entries = new List<KeyValuePair<string, object>>();

        /// <summary>選択肢を 1 つ足す。コレクション初期化子から <c>{ "名前", 値 }</c> の形で呼べる。</summary>
        public void Add(string label, T value) => _entries.Add(new KeyValuePair<string, object>(label, value));

        public int Count => _entries.Count;

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => _entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// 表示名と値の組を並べたもの。
    /// <para>
    /// 値を <c>object</c> で持つのは、Editor 側が型を知らないまま
    /// <c>SerializedProperty.boxedValue</c> に流し込めるようにするため。
    /// 利用側は型付きの <see cref="DropdownList{T}"/> を使えばよく、この形を直接扱う必要はない。
    /// </para>
    /// </summary>
    public interface IDropdownList : IEnumerable<KeyValuePair<string, object>>
    {
    }
}
