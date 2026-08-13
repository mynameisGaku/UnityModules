using System;

namespace Inspector
{
    /// <summary>
    /// 決められた候補からしか選べない欄にする。
    /// <code>
    /// [Dropdown(nameof(Sizes))]
    /// [SerializeField] private int _textureSize = 256;
    ///
    /// private static readonly int[] Sizes = { 128, 256, 512, 1024 };
    /// </code>
    /// <para>
    /// 候補を返すメンバーはフィールド・プロパティ・引数なしメソッドのいずれでもよく、
    /// <c>IEnumerable&lt;T&gt;</c> か <see cref="IDropdownList"/> を返すこと。
    /// 表示名と値を分けたいときは <see cref="DropdownList{T}"/> を返す。
    /// </para>
    /// <para>
    /// enum は Unity が既に選択式にするので不要。これは
    /// 「候補が実行時のデータで決まる」場合（アニメーション名、定義済み ID、設定済みのプリセット）のためにある。
    /// 現在値が候補に無いときは先頭に <c>(範囲外)</c> として出し、勝手に別の値へ差し替えたりはしない。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class DropdownAttribute : FieldDrawerAttribute
    {
        /// <summary>指定したメンバーが返す値を選択肢として使う。</summary>
        /// <param name="valuesMember">候補を返すメンバーの名前。</param>
        public DropdownAttribute(string valuesMember) => ValuesMember = valuesMember;

        /// <summary>候補を返すフィールド、プロパティ、または引数なしメソッドの名前。</summary>
        public string ValuesMember { get; }
    }
}
