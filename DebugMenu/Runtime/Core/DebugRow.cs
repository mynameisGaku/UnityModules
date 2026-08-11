namespace DebugMenu
{
    /// <summary>
    /// 平坦化された可視行 1 つ分。
    /// <para>
    /// 木のままでは描画側が再帰と字下げを両方抱えることになるので、
    /// ページ側で「見えている行の並び」に均してから渡す。描画側は配列を上から描くだけでよく、
    /// 仮想化リストにもそのまま乗る。
    /// </para>
    /// </summary>
    public readonly struct DebugRow
    {
        /// <summary>この行が指す要素。</summary>
        public readonly DebugElement Element;

        /// <summary>字下げの深さ。0 がページ直下。</summary>
        public readonly int Depth;

        /// <summary>要素と深さを指定して作る。</summary>
        /// <param name="element">行が指す要素。</param>
        /// <param name="depth">字下げの深さ。</param>
        public DebugRow(DebugElement element, int depth)
        {
            Element = element;
            Depth = depth;
        }
    }
}
