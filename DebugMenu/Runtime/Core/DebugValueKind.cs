namespace DebugMenu
{
    /// <summary>
    /// 行が持つ値の種類。表示側が「どう描くか」を、保存側が「どう書き出すか」を決めるのに使う。
    /// <para>
    /// 型そのものではなく種別で分岐させているのは、描画層と保存層が具体的な行クラスを
    /// 知らずに済むようにするため。新しい行を足しても、この列挙に収まる限り両者は変わらない。
    /// </para>
    /// </summary>
    public enum DebugValueKind
    {
        /// <summary>値を持たない（見出し、区切り、実行するだけの行）。</summary>
        None,

        /// <summary>真偽値。</summary>
        Bool,

        /// <summary>整数。</summary>
        Int,

        /// <summary>小数。</summary>
        Float,

        /// <summary>選択肢の中から 1 つ。</summary>
        Enum,

        /// <summary>文字列。</summary>
        Text,

        /// <summary>色。</summary>
        Color,

        /// <summary>成分が複数ある数値（Vector2 / 3 / 4）。</summary>
        Vector,
    }

    /// <summary>展開マーカーを出すかどうかの方針。</summary>
    public enum DebugMarkerVisibility
    {
        /// <summary>子行を持つときだけ出す。</summary>
        Auto,

        /// <summary>子行の有無にかかわらず出す。見出しとして使う行向け。</summary>
        Always,

        /// <summary>出さない。</summary>
        Never,
    }

    /// <summary>子ページを親ページへ組み込む方法。</summary>
    public enum DebugAttachMode
    {
        /// <summary>遷移行を 1 行置き、決定で画面ごと切り替える。項目が多いページ向け。</summary>
        Page,

        /// <summary>子ページの行を親の中へその場で展開する。画面は切り替わらない。</summary>
        Inline,
    }
}
