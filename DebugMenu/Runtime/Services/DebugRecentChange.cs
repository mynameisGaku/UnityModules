namespace DebugMenu
{
    /// <summary>最近変更された行と、その行を所有するページ。</summary>
    public readonly struct DebugRecentChange
    {
        /// <summary>行を所有するページ。</summary>
        public readonly DebugPage Page;

        /// <summary>変更された行の実体。</summary>
        public readonly DebugElement Element;

        /// <summary>変更された行を指定して作る。</summary>
        /// <param name="page">行を所有するページ。</param>
        /// <param name="element">変更された行。</param>
        public DebugRecentChange(DebugPage page, DebugElement element)
        {
            Page = page;
            Element = element;
        }
    }
}
