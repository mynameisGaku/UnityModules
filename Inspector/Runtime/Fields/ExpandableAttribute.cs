using System;

namespace Inspector
{
    /// <summary>
    /// ScriptableObject 参照の中身を、その場で開いて編集できるようにする。
    /// <code>
    /// [Expandable]
    /// [SerializeField] private WeaponStats _stats;
    /// </code>
    /// <para>
    /// 既定では参照欄しか出ないため、中身を直すたびに
    /// 「Project ウィンドウで対象を探して選び直し、また戻る」ことになる。
    /// 開いて編集できれば往復が消える。
    /// </para>
    /// <para>
    /// 中身の編集は<b>そのアセット自体</b>を書き換える。同じアセットを参照している
    /// 他のオブジェクトにも影響する点は、通常のアセット編集と同じ。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ExpandableAttribute : FieldDrawerAttribute
    {
        /// <summary>最初から開いた状態にするか。</summary>
        public bool Expanded { get; set; }
    }
}
